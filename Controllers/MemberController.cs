using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace CherryBBS.Controllers
{
    using Parameter = System.Data.SqlClient.SqlParameter;
    using DataRow = System.Data.DataRow;

    public class MemberController : Controller
    {
        public int LoginErrorCode { get; set; }

        public ActionResult Summary()
        {
            if (Session["userIndex"] == null)
            {
                ViewBag.Message = "로그인이 되어 있지 않습니다.";
                return View();
            }

            using (var handler = new DataHandler())
            {
                handler.CreateCommand("SELECT * FROM cherrybbs_users WHERE useridx=@code", new Parameter[] {
                        new Parameter("@code", Session["userIndex"])
                    });
                var dataTable = handler.Execute();

                if (dataTable.Rows.Count == 0)
                {
                    ViewBag.Message = "사용자가 존재하지 않거나 탈퇴한 사용자입니다.";
                }
            }

            return View();
        }

        public ActionResult Login(int error = 0)
        {
            if (Session["userIndex"] == null)
            {
                ViewBag.Error = error;

                return View();
            }
            else
            {
                ViewBag.Message = "이미 로그인 되었습니다.";
                return View();
            }
        }

        [HttpPost]
        public ActionResult Login(string id, string password, string redirect = null)
        {
            try
            {
                AntiForgery.Validate();
            }
            catch (HttpAntiForgeryException)
            {
                Session.Abandon();
                return RedirectToAction("Login");
            }

            using (var handler = new DataHandler())
            {
                handler.CreateCommand("SELECT * FROM cherrybbs_users WHERE email=@email AND password=@password",
                        new Parameter[]
                        {
                            new Parameter("@email", id),
                            new Parameter("@password", DataHandler.HashString(ref password))
                        });

                if (handler.Executable())
                {
                    var dataTable = handler.Execute();

                    handler.CreateCommand("INSERT INTO cherrybbs_user_signlog (email, logged_time, logged_ip, logged_country, log_result) VALUES (@email, @logtime, @logip, @logcountry, @logresult)",
                        new Parameter[]
                        {
                            new Parameter("@email", id),
                            new Parameter("@logtime", DateTime.Now.ToString()),
                            new Parameter("@logip", Request.UserHostAddress),
                            new Parameter("@logcountry", Session["Country"])
                        }
                    );

                    if (dataTable != null)
                    {
                        handler.ModifyParameter("@logresult", "SUCCESS");
                        handler.ExecuteNonQuery();

                        foreach (DataRow row in dataTable.Rows)
                        {
                            if (row["ban_start"].GetType() != typeof(DBNull))
                            {
                                var banStart = DateTime.Parse((string)row["ban_start"]);
                                var banDuration = long.Parse((string)row["ban_duration"]);

                                if ((banStart.Ticks + banDuration) - DateTime.Now.Ticks > 0)
                                {
                                    return View(new Models.User.BanInfo()
                                    {
                                        bannedId = string.Format("{0}({1})",
                                                        (string)row["nickname"], (string)row["email"]),
                                        bannedSince = banStart.ToString(),
                                        banReason = (string)row["ban_reason"],
                                        banTime = new DateTime(banStart.Ticks + banDuration).ToString()
                                    }); // user banned
                                }
                            }

                            handler.CreateCommand(
                                    "UPDATE cherrybbs_users SET last_logged=@logtime, last_logged_country=@logcountry WHERE useridx=@idx",
                                    new Parameter[]
                                    {
                                        new Parameter("@logtime", DateTime.Now.ToString()),
                                        new Parameter("@logcountry", Session["Country"]),
                                        new Parameter("@idx", row["useridx"])
                                    }
                                );

                            if (handler.ExecuteNonQuery() > 0)
                            {
                                Session["userIndex"] = row["useridx"];
                                Session["nickname"] = row["nickname"];
                                Session["isAdmin"] = row["is_admin"];

                                MvcApplication.ConnectedUsers[(string)Session["ipAddr"]] = (string)Session["nickname"];

                                return redirect == null ? Redirect("/") : Redirect(redirect);
                            }
                            else
                            {
                                return Redirect("/Member/Login?error=3");
                            }
                        }
                    }
                    else
                    {
                        handler.ModifyParameter("@logresult", "FAILED");
                        handler.ExecuteNonQuery();

                        return Redirect("/Member/Login?error=1");
                    }
                }
                else
                {
                    Session.Abandon();
                    return Redirect("/");
                }
            }

            return Redirect("/");
        }

        public ActionResult Logout()
        {
            if(Session["userIndex"] != null)
                Session.Abandon();

            return Redirect("/");
        }

        public ActionResult Join()
        {
            if(Session["userIndex"] == null)
            {
                using (var handler = new DataHandler())
                {
                    handler.CreateCommand("SELECT termsofservice FROM cherrybbs_misc");

                    try
                    {
                        ViewBag.Agreement = handler.Executable() ? (string)handler.ExecuteScalar() : "약관을 등록하여 주십시오.";
                    }
                    catch
                    {
                        ViewBag.Agreement = "약관을 등록하여 주십시오.";
                    }
                }

                return View();
            }
            else
            {
                return Redirect("/");
            }
        }

        [HttpPost]
        public ActionResult Join(string email, string password, string nickname, int recvMail=1)
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

            using (var handler = new DataHandler())
            {
                handler.CreateCommand("SELECT * FROM cherrybbs_users WHERE email=@email OR nickname=@nickname", 
                    new Parameter[] {
                        new Parameter("@email", email),
                        new Parameter("@nickname", nickname)
                    });
                if(handler.Executable())
                {
                    var table = handler.Execute();

                    if(table != null)
                    {
                        if (table.Rows.Count > 0)
                        {
                            ViewBag.Message = "이미 가입된 이메일 혹은 닉네임입니다.";
                            return View();
                        }
                    }
                }
                else
                {
                    return Redirect("/Member/Join");
                }

                if(password.Length < 8)
                {
                    ViewBag.Message = "비밀번호가 너무 짧습니다!";
                    return View();
                }

                handler.CreateCommand("INSERT INTO cherrybbs_users (email, password, nickname, recv_mail, reg_date, is_admin) VALUES (@email, @password, @nickname, @recvmail, @regdate, 0)",
                    new Parameter[] {
                            new Parameter("@email", email),
                            new Parameter("@password", DataHandler.HashString(ref password)),
                            new Parameter("@nickname", nickname),
                            new Parameter("@recvmail", recvMail),
                            new Parameter("@regdate", DateTime.Now.ToString())
                        });
                if(handler.Executable())
                {
                    if(handler.ExecuteNonQuery() > 0)
                    {
                        return Redirect("/Member/RegDone");
                    }
                    else
                    {
                        ViewBag.Message = "회원가입 중 문제가 발생했습니다.";
                        return View();
                    }
                }
            }

            return null;
        }
        
        public ActionResult RegDone()
        {
            return View();
        }

        public ActionResult Abandon()
        {
            if (Session["userIndex"] == null)
                return Redirect("/");

            return View();
        }

        [HttpPost]
        public ActionResult Abandon(string password=null)
        {
            try
            {
                if (Session["userIndex"] == null || password == null)
                    throw new HttpAntiForgeryException();
                AntiForgery.Validate();
            }
            catch (HttpAntiForgeryException)
            {
                Session.Abandon();
                ViewBag.Message = "잘못된 접근입니다.";
                return View();
            }

            using (var handler = new DataHandler())
            {
                handler.CreateCommand("DELETE FROM cherrybbs_users WHERE useridx=@code, password=@password",
                    new Parameter[] {
                            new Parameter("@code", Session["userIndex"]),
                            new Parameter("@password", DataHandler.HashString(ref password))
                        });
                
                if(handler.ExecuteNonQuery() == 0)
                {
                    ViewBag.Message = "비밀번호가 옳지 않습니다.";
                    return View();
                }
                else
                {
                    ViewBag.AbandonFlag = true;
                    return View();
                }
            }
        }

        public ActionResult Modify()
        {
            if (Session["userIndex"] == null)
                return Redirect("/");

            return View();
        }

        [HttpPost]
        public ActionResult Modify(string originalPassword, string password, string nickname, int recvMail)
        {
            try
            {
                AntiForgery.Validate();
                if (Session["userIndex"] == null) throw new HttpAntiForgeryException();
            }
            catch (HttpAntiForgeryException)
            {
                Session.Abandon();
                ViewBag.Message = "잘못된 접근입니다.";
                return View();
            }

            using (var handler = new DataHandler())
            {
                handler.CreateCommand("SELECT useridx FROM cherrybbs_users WHERE useridx=@idx AND password=@password",
                    new Parameter[]
                    {
                        new Parameter("@idx", Session["userIndex"]),
                        new Parameter("@password", DataHandler.HashString(ref originalPassword))
                    });

                if (handler.Execute().Rows.Count > 0)
                {
                    handler.CreateCommand("UPDATE cherrybbs_users SET password=@password, nickname=@nickname, recv_mail=@recvMail",
                        new Parameter[] {
                                    new Parameter("@password", originalPassword == password ?
                                                               DataHandler.HashString(ref originalPassword) :
                                                               DataHandler.HashString(ref password)),
                                    new Parameter("@nickname", nickname),
                                    new Parameter("@recvMail", recvMail.ToString())
                            });

                    if(handler.Executable())
                    {
                        if(handler.ExecuteNonQuery() > 0)
                        {
                            return View("/Memeber/Summary");
                        }
                        else
                        {
                            ViewBag.Message = "사용자 정보를 업데이트 하는 도중 문제가 발생했습니다.";
                            return View();
                        }
                    }
                    else
                    {
                        ViewBag.Message = "사용자 정보를 업데이트 하는 도중 문제가 발생했습니다.";
                        return View();
                    }
                }
                else
                {
                    ViewBag.Message = "원본 비밀번호가 틀렸습니다.";
                    return View();
                }
            }
        }
    }
}
