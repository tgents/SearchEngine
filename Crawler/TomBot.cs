using HtmlAgilityPack;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using RobotomLibrary;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace WorkerRole1
{
    public class TomBot
    {
        private HashSet<Uri> disallow;
        public HashSet<string> visited { get; private set; }
        public HashSet<string> parsed { get; private set; }
        public HashSet<string> hosts { get; private set; }

        private CloudQueue htmlQ;
        private CloudTable resultTable;
        private CloudTable errorTable;

        public int queueCount { get; set; }
        public int tableCount { get; set; }

        public Queue<string> lastTen { get; private set; }
        public long timer { get; private set; }

        public TomBot(CloudQueue htmlqueue, CloudTable results, CloudTable errors, int resultscount)
        {
            disallow = new HashSet<Uri>();
            visited = new HashSet<string>();
            parsed = new HashSet<string>();
            hosts = new HashSet<string>();
            lastTen = new Queue<string>();
            htmlQ = htmlqueue;
            resultTable = results;
            errorTable = errors;
            htmlQ.CreateIfNotExists();
            timer = 0;

            queueCount = 0;
            tableCount = resultscount;
        }

        //return 1 for success, 0 for fail
        public int ParseHtml(Uri uri)
        {
            if (isDisallowed(uri))
            {
                queueCount--;
                return 0;
            }

            long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            string sitedata = "";
            try
            {
                WebClient downloader = new WebClient();
                sitedata = downloader.DownloadString(uri);
            }
            catch (Exception e)
            {
                UriEntity error = new UriEntity(uri, e.Message, DateTime.Now, e.Message);
                errorTable.ExecuteAsync(TableOperation.Insert(error));
                parsed.Add(uri.AbsoluteUri);
                visited.Remove(uri.AbsoluteUri);
                queueCount--;
                return 0;
            }

            string hi = uri.AbsoluteUri;

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(sitedata);

            HtmlNodeCollection hrefs = doc.DocumentNode.SelectNodes("//a[@href]");

            if (hrefs == null)
            {
                queueCount--;
                return 0;
            }
            foreach (HtmlNode node in hrefs)
            {
                var href = node.Attributes["href"];
                string url = href.Value;

                //remove this if crawler break
                try
                {
                    Uri newsite = new Uri(uri, url);
                    string host = newsite.Host;
                    if (host.Equals("cnn.com") || host.Equals("www.cnn.com") || newsite.AbsoluteUri.StartsWith("http://bleacherreport.com/articles"))
                    {
                        if (!visited.Contains(newsite.AbsoluteUri) && !parsed.Contains(newsite.AbsoluteUri))
                        {
                            htmlQ.AddMessageAsync(new CloudQueueMessage(newsite.AbsoluteUri));
                            visited.Add(newsite.AbsoluteUri);
                            queueCount++;
                        }
                    }
                }
                catch (Exception e)
                {
                }
                //to here

                //if (url.StartsWith("/") && !url.StartsWith("//"))
                //{
                //    Uri test = new Uri("http://" + uri.Host + url);
                //    if (!visited.Contains(test.AbsoluteUri) && !parsed.Contains(test.AbsoluteUri))
                //    {
                //        htmlQ.AddMessageAsync(new CloudQueueMessage(test.AbsoluteUri));
                //        visited.Add(test.AbsoluteUri);
                //        queueCount++;
                //    }

                //}
                //else if (url.StartsWith("http://bleacherreport.com/articles"))
                //{
                //    Uri test = new Uri(url);
                //    if (!visited.Contains(test.AbsoluteUri) && !parsed.Contains(test.AbsoluteUri))
                //    {
                //        htmlQ.AddMessageAsync(new CloudQueueMessage(test.AbsoluteUri));
                //        visited.Add(test.AbsoluteUri);
                //        queueCount++;
                //    }
                //}
            }

            long stop = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            timer = stop - start;

            //get title
            HtmlNode titleNode = doc.DocumentNode.SelectSingleNode("//title");
            string title = "";
            if (titleNode != null)
            {
                title = titleNode.InnerText;
            }

            //get date
            HtmlNode lastmod = doc.DocumentNode.SelectSingleNode("//meta[@name='lastmod']");
            if (uri.Host.Equals("bleacherreport.com"))
            {
                lastmod = doc.DocumentNode.SelectSingleNode("//meta[@name='pubdate']");
            }
            string date = "";
            if (lastmod != null)
            {
                date = lastmod.GetAttributeValue("content", "");
            }

            DateTime converteddate = date.Equals("") ? new DateTime() : Convert.ToDateTime(date);

            HashSet<UriEntity> words = new HashSet<UriEntity>();
            foreach (string word in Robotom.CleanWord(title).Split(' '))
            {
                if (!word.Trim().Equals(""))
                {
                    words.Add(new UriEntity(uri, title, converteddate, word));
                }

            }

            try
            {
                if (!parsed.Contains(uri.AbsoluteUri))
                {
                    foreach (UriEntity add in words)
                    {
                        resultTable.ExecuteAsync(TableOperation.Insert(add));
                        tableCount++;
                    }
                    lastTen.Enqueue(uri + " - \"" + title + "\"");
                    if (lastTen.Count > 10)
                    {
                        lastTen.Dequeue();
                    }

                }
            }
            catch (Exception e)
            {

            }

            parsed.Add(uri.AbsoluteUri);
            visited.Remove(uri.AbsoluteUri);
            queueCount--;

            return 1;
        }

        //parses a robot.txt
        public List<Uri> ParseRobot(Uri uri)
        {
            StreamReader reader;
            try
            {
                var webRequest = WebRequest.Create(@uri.AbsoluteUri);
                var response = webRequest.GetResponse();
                var content = response.GetResponseStream();
                reader = new StreamReader(content);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return new List<Uri>();
            }

            List<Uri> sitemaps = new List<Uri>();

            bool foundUserAgent = false;
            while (!reader.EndOfStream)
            {
                string current = reader.ReadLine();
                string url = current.Length > 0 ? current.Substring(current.IndexOf(' ')) : current;
                if (current.StartsWith("user-agent", StringComparison.OrdinalIgnoreCase))
                {
                    foundUserAgent = url.Contains("*");
                }
                else if (current.StartsWith("sitemap", StringComparison.OrdinalIgnoreCase))
                {
                    sitemaps.Add(new Uri(url));
                }
                else if (current.StartsWith("disallow", StringComparison.OrdinalIgnoreCase))
                {
                    disallow.Add(new Uri(uri, url));
                }
            }

            hosts.Add(uri.Host);
            return sitemaps;
        }

        //parses a sitemap xml
        public List<Uri> ParseXml(Uri uri)
        {
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(uri.AbsoluteUri);
            DateTime compare = Convert.ToDateTime("2016-04-01");
            List<Uri> newSitemaps = new List<Uri>();
            foreach (XmlNode child in xmldoc.ChildNodes)
            {
                string childname = child.Name;
                if (childname.Equals("sitemapindex") || childname.Equals("urlset"))
                {
                    foreach (XmlNode sitemap in xmldoc.LastChild.ChildNodes)
                    {
                        string url = "";
                        string date = DateTime.Now.ToString();
                        foreach (XmlNode info in sitemap.ChildNodes)
                        {
                            if (info.Name.Equals("loc"))
                            {
                                url = info.InnerText;
                            }
                            else if (info.Name.Equals("lastmod"))
                            {
                                date = info.InnerText;
                            }
                        }

                        if (!date.Equals("") && Convert.ToDateTime(date).CompareTo(compare) >= 0)
                        {
                            if (url.EndsWith(".xml"))
                            {
                                newSitemaps.Add(new Uri(url));
                            }
                            else
                            {
                                htmlQ.AddMessageAsync(new CloudQueueMessage(url));
                                visited.Add(url);
                                queueCount++;
                            }
                        }
                    }
                }
            }

            return newSitemaps;
        }

        private bool isDisallowed(Uri uri)
        {
            foreach (Uri no in disallow)
            {
                if (uri.AbsoluteUri.Contains(no.AbsoluteUri))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
