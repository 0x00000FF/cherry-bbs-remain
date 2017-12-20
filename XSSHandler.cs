using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace CherryBBS
{
    public class XSSHandler : IDisposable
    {
        private HtmlDocument m_doc { get; set; }

        public XSSHandler(string target)
        {
            if (target == null)
                Dispose();

            var decodeString = HttpUtility.HtmlDecode(target).Replace("\n", string.Empty);

            m_doc = new HtmlDocument();
            m_doc.LoadHtml(decodeString);
        }

        private readonly string[] illegalTags =
        {
            "html", "head", "link", "meta", "title", "style", "object", "script",
            "frameset", "frame", "body", "form", "input", "xml"
        };

        private readonly string[] legalAttributes =
        {
            "class", "id", "style", "src", "href", "controls", "autoplay"
        };

        private readonly string[] legalEmbedOrigins =
        {
            "https://www.youtube.com/"
        };

        private readonly string[] illegalExtensions =
        {
            ".js", ".php", ".php3", ".jsp", ".asp", ".aspx", ".htm", ".html", ".css",
            ".swf", ".exe", ".vb", "cs", ".vbs", ".jar", ".jse", ".ws", ".wsc"
        };

        public string Purify()
        {
            if (m_doc == null)
                return null;

            var nodes = m_doc.DocumentNode.SelectNodes("/node()");
            if (nodes == null)
                return m_doc.DocumentNode.InnerHtml;

            foreach (var node in nodes)
            {
                var nodeInNode = new HtmlDocument();
                nodeInNode.LoadHtml(node.Name);
                if (nodeInNode.DocumentNode.ChildNodes.Count > 1)
                    node.Remove();

                var continueFlag = false;

                foreach (var illegalTag in illegalTags)
                {
                    if (node.Name.ToLower() == illegalTag ||
                        !MatchScript(node.Name))
                    {
                        node.Remove();
                        continueFlag = true;
                        break;
                    }
                }

                if (continueFlag)
                    continue;

                foreach (var attribute in node.Attributes.ToList())
                {
                    var illegalFlag = 0;

                    foreach (var legalAttribute in legalAttributes)
                    {
                        if (attribute.Name != legalAttribute)
                            illegalFlag++;
                        else if (attribute.Name == legalAttribute)
                        {
                            if (node.Name == "embed" && attribute.Name == "src")
                            {
                                var illegalEmbedFlag = 0;
                                foreach (var legalEmbedOrigin in legalEmbedOrigins)
                                {
                                    if (!attribute.Value.StartsWith(legalEmbedOrigin))
                                    {
                                        illegalEmbedFlag++;
                                    }
                                    else break;
                                }
                                if (illegalEmbedFlag == legalEmbedOrigins.Length)
                                {
                                    attribute.Remove();
                                    break;
                                }
                            }

                            foreach (var illegalExtension in illegalExtensions)
                            {
                                if (attribute.Value.Trim().EndsWith(illegalExtension))
                                {
                                    attribute.Remove();
                                    break;
                                }
                            }
                        }
                    }

                    if (illegalFlag == legalAttributes.Length)
                    {
                        attribute.Remove();
                        continue;
                    }

                    if (!MatchScript(attribute.Value))
                        attribute.Remove();
                }

                if (node.HasChildNodes)
                {
                    Purify(node.ChildNodes);
                }
            }

            return m_doc.DocumentNode.InnerHtml;
        }

        public void Purify(HtmlNodeCollection childNodes)
        {
            foreach (var node in childNodes.ToList())
            {
                var nodeInNode = new HtmlDocument();
                nodeInNode.LoadHtml(node.Name);
                if (nodeInNode.DocumentNode.ChildNodes.Count > 1)
                    node.Remove();

                var continueFlag = false;

                foreach (var illegalTag in illegalTags)
                {
                    if (node.Name.ToLower() == illegalTag ||
                        !MatchScript(node.Name))
                    {
                        node.Remove();
                        continueFlag = true;
                        break;
                    }
                }

                if (continueFlag)
                    continue;

                foreach (var attribute in node.Attributes.ToList())
                {
                    var illegalFlag = 0;

                    foreach (var legalAttribute in legalAttributes)
                    {
                        if (attribute.Name != legalAttribute)
                            illegalFlag++;
                        else if (attribute.Name == legalAttribute)
                        {
                            if (node.Name == "embed" && attribute.Name == "src")
                            {
                                var illegalEmbedFlag = 0;
                                foreach (var legalEmbedOrigin in legalEmbedOrigins)
                                {
                                    if (!attribute.Value.StartsWith(legalEmbedOrigin))
                                    {
                                        illegalEmbedFlag++;
                                    }
                                    else break;
                                }
                                if (illegalEmbedFlag == legalEmbedOrigins.Length)
                                {
                                    attribute.Remove();
                                    break;
                                }
                            }
                            
                            foreach(var illegalExtension in illegalExtensions)
                            {
                                if(attribute.Value.Trim().EndsWith(illegalExtension))
                                {
                                    attribute.Remove();
                                    break;
                                } 
                            }
                        }
                    }

                    if (illegalFlag == legalAttributes.Length)
                    {
                        attribute.Remove();
                        continue;
                    }

                    if (!MatchScript(attribute.Value))
                        attribute.Remove();
                }

                if (node.HasChildNodes)
                {
                    Purify(node.ChildNodes);
                }
            }
        }

        public bool MatchScript(string test)
        {
            var regex = new Regex(@"&#\d*?", RegexOptions.IgnoreCase);
            if (regex.Match(test).Success)
                return false;
            else
            {
                regex = new Regex(@"j.*?a.*?v.*?a.*?s.*?c.*?r.*?i.*?p.*?t.*?:", RegexOptions.IgnoreCase);
                if (regex.Match(test).Success)
                    return false;
                else
                {
                    regex = new Regex(@"u.*?r.*?l.*?\(.*?\)");
                    if (regex.Match(test).Success)
                        return false;
                    else
                        return true;
                }
            }
        }

        public void Dispose()
        {
            if (m_doc != null)
                m_doc = null;
        }
    }
}