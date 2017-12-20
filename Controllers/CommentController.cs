using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace CherryBBS.Controllers
{
    using Parameter = System.Data.SqlClient.SqlParameter;

    public class CommentController : Controller
    {
        [HttpPost]
        public ActionResult Add(string boardid, int articleid, string userid, string password, string content)
        {
            try
            {
                AntiForgery.Validate();
            }
            catch (HttpAntiForgeryException)
            {
                Session.Abandon();
                return RedirectPermanent("/");
            }

            if (!DataHandler.ValidateInject(ref boardid))
                using (var handler = new DataHandler())
                {
                    handler.CreateCommand("INSERT INTO Cherrybbs_comment_{0} (article_seq, author, password, content) VALUES (@seq, @author, @password, @content)",
                        new Parameter[] {
                            new Parameter("@seq", articleid),
                            new Parameter("@author", Session["userIndex"] == null ?
                                string.Format("{0}.{1}.*.*",
                                    ((string)Session["ipAddr"]).Split('.')[0],
                                    ((string)Session["ipAddr"]).Split('.')[1]) :
                                    (string)Session["nickname"]),
                            new Parameter("@password", DataHandler.HashString(ref password)),
                            new Parameter("@content", new XSSHandler(content).Purify())
                            });

                    if (handler.Executable())
                    {
                        handler.Execute();
                        return Redirect(Request["redirUrl"]);
                    }
                    else
                    {
                        Session.Abandon();
                        return null;
                    }
                }

            Session.Abandon();
            return null;
        }
    }
}