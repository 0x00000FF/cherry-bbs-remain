using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace CherryBBS.Controllers
{
    using DataRow = System.Data.DataRow;
    using Parameter = System.Data.SqlClient.SqlParameter;

    public class BoardController : Controller
    {
        List<Models.Board.Article> articleList = new List<Models.Board.Article>();
        List<Models.Board.Comment> commentList = new List<Models.Board.Comment>();


        // GET: Board
        public ActionResult List(string category, uint page = 0)
        {
            articleList.Clear();

            if (category == null || category.Trim().Length == 0 || !DataHandler.ValidateInject(ref category))
            {
                ViewBag.Message = "잘못된 접근입니다.";
                return View();
            }

            try
            {
                using (var handler = new DataHandler())
                {
                    var CountQuery = string.Format("SELECT COUNT(*) FROM cherrybbs_board_{0} WHERE is_removed=0",
                        category);

                    handler.CreateCommand(CountQuery);
                    ViewBag.TotalCount = handler.ExecuteScalar();
                    ViewBag.CurrentPage = page + 1;

                    var InquiryQuery = ViewBag.TotalCount > 30 ?
                        string.Format("SELECT * FROM (SELECT seq, title, author, author_ip, written_date, is_anonymous, viewcount FROM cherrybbs_board_{0} WHERE is_removed=0 ORDER BY seq DESC) lists WHERE lists.rownum BETWEEN {1} AND {2}",
                        category,
                        page <= 0 ? 0 : 30 * page,
                        page <= 0 ? 0 : 31 * page) :
                        string.Format("SELECT seq, title, author, author_ip, written_date, is_anonymous, viewcount FROM cherrybbs_board_{0} WHERE is_removed=0 ORDER BY seq DESC",
                        category);

                    handler.CreateCommand(InquiryQuery);
                    var dataTable = handler.Execute();

                    foreach (DataRow row in dataTable.Rows)
                    {
                        var fragment = ((string)row["author_ip"]).Split('.');

                        var article = new Models.Board.Article();
                        {
                            article.Sequence = (int)row["seq"];
                            article.Title = (string)row["title"];
                            article.Author = (byte)row["is_anonymous"] == 0 ?
                                        (string)row["author"] :
                                        string.Format("{0}.{1}.*.*", fragment[0], fragment[1]);
                            article.WrittenDate = (string)row["written_date"];
                            article.Viewcount = (int)row["viewcount"];
                        }
                        articleList.Add(article);
                    }

                    handler.CreateCommand("SELECT author, author_ip, is_anonymous, body, image FROM cherrybbs_comment_{0} WHERE");


                    ViewBag.ArticleList = articleList;
                    return View();
                }
            }
            catch
            {
                ViewBag.Message = "존재하지 않는 게시판입니다.";
                return View();
            }
        }

        public ActionResult Read(string category, uint seq)
        {
            if (category == null || category.Trim().Length == 0 || !DataHandler.ValidateInject(ref category))
            {
                ViewBag.Message = "잘못된 접근입니다.";
                return View();
            }

            using (var handler = new DataHandler())
            {
                var Query = string.Format("SELECT * FROM cherrybbs_board_{0} WHERE seq=@seq", category);
                handler.CreateCommand(Query);
                handler.ModifyParameter("@seq", seq);

                var dataTable = handler.Execute();
                if (dataTable.Rows.Count > 0)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var fragment = ((string)row["author_ip"]).Split('.');

                        if (row["file"].GetType() != typeof(DBNull))
                        {
                            var fileList = ((string)row["file"]).Split('/');
                            ViewBag.Files = fileList;
                        }

                        return View(new Models.Board.Article()
                        {
                            Sequence = (int)row["seq"],
                            Title = (string)row["title"],
                            WrittenDate = (string)row["written_date"],
                            Author = (int)row["is_anonymous"] == 0 ?
                            (string)row["author"] : string.Format("{0}.{1}.*.*", fragment[0], fragment[1]),
                            Content = (string)row["content"],
                            Viewcount = (int)row["viewcount"]
                        });
                    }

                    return null;
                }
                else
                {
                    ViewBag.Message = "존재하지 않는 글 요청입니다!";
                    return View();
                }
            }
        }

        public ActionResult GetFile(string guid)
        {          
            using (var handler = new DataHandler())
            {
                handler.CreateCommand("SELECT name FROM Cherrybbs_File WHERE guid=@guid",
                    new Parameter[]
                    {
                        new Parameter("@guid", guid)
                    });
                if(handler.Executable())
                {
                    var read = handler.Execute();
                    if(read.Rows.Count > 0)
                    {
                        foreach(DataRow row in read.Rows)
                        {
                            var filePath = string.Format("/files/{0}/{1}", guid,
                                (string)row["filepath"]);

                            if(System.IO.File.Exists(filePath))
                            {
                                ViewBag.Message = "파일이 존재하지 않습니다!";
                                return View();
                            }
                            else
                            {
                                handler.CreateCommand("UPDATE Cherrybbx_File SET download_count = download_count + 1 WHERE guid=@guid",
                                        new Parameter[]
                                        {
                                            new Parameter("@guid", guid)
                                        }
                                    );
                                var fs = new FileStream(filePath, FileMode.Open);
                                return File(fs, "application/octet-stream");
                            }
                        }
                    }
                }

                return null;
            }
        }

        public ActionResult Write(string category, string title, string author, string content, string files=null)
        {
            try
            {
                AntiForgery.Validate();
            }
            catch (HttpAntiForgeryException)
            {
                Session.Abandon();

                ViewBag.Message = "잘못된 접근입니다.";
                return View();
            }

            if (category == null || category.Trim().Length == 0 || !DataHandler.ValidateInject(ref category))
            {
                ViewBag.Message = "잘못된 접근입니다.";
                return View();
            }

            if (!DataHandler.ValidateInject(ref category))
            {
                using (var handler = new DataHandler())
                {
                    handler.CreateCommand(
                        string.Format("INSERT INTO cherrybbs_board_{0}(title, author, author_ip, written_date, is_anonymous, content, file, viewcount) VALUES (@title, @author, @ip, @date, @is_anon, @content, @file, @viewcount); select SCOPE_IDENTITY();", category)
                        , new Parameter[]
                        {
                            new Parameter("@title", new XSSHandler(content).Purify()),
                            new Parameter("@author", author),
                            new Parameter("@ip", Request.UserHostAddress),
                            new Parameter("@date", DateTime.Now.ToString()),
                            new Parameter("@is_anon", Session["userIndex"] == null ? 0 : 1),
                            new Parameter("@content", new XSSHandler(content).Purify()),
                            new Parameter("@file", files )
                        });

                    if (handler.Executable())
                    {
                        var result = handler.ExecuteScalar();

                        if (result.GetType() != typeof(DBNull))
                        {
                            return Redirect(
                                    string.Format("/Board/Read?category={0}&seq={1}", category, result)
                                );
                        }
                    }

                    ViewBag.Message = "글 쓰기에 오류가 발생했습니다. 관리자에게 문의하세요.";
                    return View();
                }
            }
            else
            {
                ViewBag.Message = "글 쓰기에 오류가 발생했습니다. 관리자에게 문의하세요.";
                return View();
            }
        }
        
        public ActionResult Delete(string category, uint seq)
        {
            if (category == null || category.Trim().Length == 0 || !DataHandler.ValidateInject(ref category))
            {
                ViewBag.Message = "잘못된 접근입니다.";
                return View();
            }

            using (var handler = new DataHandler())
            {
                handler.CreateCommand(
                        string.Format(
                            "SELECT seq FROM cherrybbs_board_{0} WHERE seq=@seq, is_removed=0"
                            , category),
                        new Parameter[]
                        {
                            new Parameter("@seq", seq)
                        }
                    );

                if(handler.Executable())
                {
                    var dataTable = handler.Execute();
                    if(dataTable.Rows.Count == 0)
                    {
                        ViewBag.Message = "해당 글이 존재하지 않거나 권한이 없습니다.";
                        return View();
                    }
                    else
                    {
                        if((string)Session["nickname"] == (string)dataTable.Rows[0]["author"])
                        {
                            if((byte)dataTable.Rows[0]["is_anonymous"] == 0)
                            {
                                handler.CreateCommand(
                                    string.Format("UPDATE cherrybbs_board_{0} SET is_removed=1 WHERE seq=@seq", category),
                                    new Parameter[]
                                    {
                                        new Parameter("@seq", seq)
                                    }
                                    );

                                if(handler.Executable())
                                {
                                    if(handler.ExecuteNonQuery() > 0)
                                    {
                                        return Redirect("/Board/List?category=" + category);
                                    }
                                    else
                                    {
                                        return View(new
                                        {
                                            code = 1
                                        });
                                    }
                                }
                            }
                            else
                            {
                                ViewBag.Message = "권한이 없습니다.";
                                return View();
                            }
                        }
                    }
                }
            }

            return Redirect("/Board/List?category=" + category);
        }
    }
}