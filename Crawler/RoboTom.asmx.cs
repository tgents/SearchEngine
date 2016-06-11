using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using RobotomLibrary;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for RoboTom
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class RoboTom : System.Web.Services.WebService
    {
        public static Dictionary<string, Tuple<List<string>, DateTime>> searchcache;

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string start(string url)
        {
            if (!url.Equals("http://cnn.com"))
            {
                return "only works with http://cnn.com at the moment";
            }

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue commandQ = queueClient.GetQueueReference("commandq");

            CloudQueueMessage command = new CloudQueueMessage(Robotom.COMMAND_START + " " + url);
            commandQ.AddMessage(command);

            return "Starting crawl...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string stop()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue commandQ = queueClient.GetQueueReference("commandq");

            CloudQueueMessage command = new CloudQueueMessage(Robotom.COMMAND_STOP);
            commandQ.AddMessage(command);

            return "Stopping crawl...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string clearTables()
        {
            stop();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable resultsTable = tableClient.GetTableReference("crawltable");
            CloudTable statsTable = tableClient.GetTableReference("stattable");
            CloudTable errorTable = tableClient.GetTableReference("errortable");
            try
            {
                resultsTable.Delete();
                statsTable.Delete();
                errorTable.Delete();
            }
            catch (Exception e)
            {
                return "Tables already deleted: " + e.Message;
            }

            return "Crawl stopped, Clearing tables...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string createTables()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            try
            {
                CloudTable resultTable = tableClient.GetTableReference("crawltable");
                CloudTable errorTable = tableClient.GetTableReference("errortable");
                CloudTable statsTable = tableClient.GetTableReference("stattable");
                resultTable.CreateIfNotExists();
                statsTable.CreateIfNotExists();
                errorTable.CreateIfNotExists();
            }
            catch (Exception e)
            {
                return "Please wait before recreating tables: " + e.Message;
            }

            return "Creating tables...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<int> getStats()
        {
            List<int> stats = new List<int>();

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable statsTable = tableClient.GetTableReference("stattable");
            try
            {
                TableQuery<StatEntity> counterquery = new TableQuery<StatEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "TomBot"));
                List<StatEntity> bean = statsTable.ExecuteQuery(counterquery).ToList();

                if (bean.Count != 0)
                {
                    StatEntity getem = bean.ElementAt(0);
                    stats.Add(getem.memUsage);
                    stats.Add(getem.cpuUsage);
                    stats.Add(getem.queuesize);
                    stats.Add(getem.tablesize);
                    stats.Add(getem.visitcount);
                }
            }
            catch (Exception e)
            {
                stats.Add(-1);
                stats.Add(-1);
                stats.Add(-1);
                stats.Add(-1);
                stats.Add(-1);
                return stats;
            }


            return stats;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getLastTen()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable statsTable = tableClient.GetTableReference("stattable");

            try
            {
                TableQuery<StatEntity> counterquery = new TableQuery<StatEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "TomBot"));
                List<StatEntity> bean = statsTable.ExecuteQuery(counterquery).ToList();

                if (bean.Count != 0)
                {
                    StatEntity getem = bean.ElementAt(0);
                    return getem.lastTen;
                }
            }
            catch (Exception e)
            {
                return "Could not access stats: " + e.Message;
            }

            return "";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getStatus()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable statsTable = tableClient.GetTableReference("stattable");

            try
            {
                TableQuery<StatEntity> counterquery = new TableQuery<StatEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "TomBot"));
                List<StatEntity> bean = statsTable.ExecuteQuery(counterquery).ToList();

                if (bean.Count != 0)
                {
                    StatEntity getem = bean.ElementAt(0);
                    if (getem.status == Robotom.STATUS_STOP)
                    {
                        return "TomBot is stopped...";
                    }
                    else if (getem.status == Robotom.STATUS_IDLE)
                    {
                        return "TomBot is idle...";
                    }
                    else if (getem.status == Robotom.STATUS_LOADING)
                    {
                        return "TomBot is loading...";
                    }
                    else if (getem.status == Robotom.STATUS_CRAWLING)
                    {
                        return "TomBot is crawling...";
                    }
                }
            }
            catch (Exception e)
            {
                return "Could not access stats: " + e.Message;
            }


            return "Could not retrieve status...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> getErrors()
        {
            List<string> uris = new List<string>();

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable errorTable = tableClient.GetTableReference("errortable");
            try
            {
                TableQuery<StatEntity> counterquery = new TableQuery<StatEntity>()
                                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "TomBot"));
                List<UriEntity> bean = errorTable.ExecuteQuery(new TableQuery<UriEntity>()).ToList();

                if (bean.Count != 0)
                {
                    foreach (UriEntity getem in bean)
                    {
                        if (uris.Count < 300)
                        {
                            uris.Add(getem.Site);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                uris.Add("Could not access errors: " + e.Message);
                return uris;
            }

            if (uris.Count == 0)
            {
                uris.Add("No errors so far :)");
            }
            return uris;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getTitle(string url)
        {
            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (Exception e)
            {
                return e.Message;
            }
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable statsTable = tableClient.GetTableReference("crawltable");

            try
            {
                TableQuery<UriEntity> counterquery = new TableQuery<UriEntity>()
                                .Where(TableQuery.CombineFilters(
                                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, uri.Host),
                                    TableOperators.And,
                                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, uri.AbsolutePath.GetHashCode().ToString())));

                List<UriEntity> bean = statsTable.ExecuteQuery(counterquery).ToList();

                if (bean.Count != 0)
                {
                    return bean.ElementAt(0).Title;
                }
            }
            catch (Exception e)
            {
                return "Could not access table: " + e.Message;
            }


            return "Could not find URL...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> getUrl(string title)
        {
            title = Robotom.CleanWord(title);

            if (searchcache == null || searchcache.Count > 100)
            {
                searchcache = new Dictionary<string, Tuple<List<string>, DateTime>>();
            }

            if (searchcache.ContainsKey(title))
            {
                if(searchcache[title].Item2.AddMinutes(30) > DateTime.Now)
                {
                    return searchcache[title].Item1;
                }
                else
                {
                    searchcache.Remove(title);
                }
            }

            List<string> returnthis = new List<string>();

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable statsTable = tableClient.GetTableReference("crawltable");

            try
            {
                var results = new Dictionary<string, int>();
                foreach (string word in title.Split(' '))
                {
                    TableQuery<UriEntity> query = new TableQuery<UriEntity>()
                                .Where(
                                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, word));
                    var stuffs = statsTable.ExecuteQuery(query);
                    foreach (UriEntity stuff in stuffs)
                    {
                        string key = stuff.Title + " " + stuff.Site;
                        if (results.ContainsKey(key))
                        {
                            results[key]++;
                        }
                        else
                        {
                            results.Add(key, 1);
                        }
                    }
                }
                returnthis = results.OrderByDescending(x => x.Value).Select(x => x.Key).Take(20).ToList();
            }
            catch (Exception e)
            {
                returnthis.Add("Could not access table: " + e.Message);
            }

            if (returnthis.Count == 0)
            {
                returnthis.Add("Could not find any results...");
            }
            
            searchcache.Add(title, new Tuple<List<string>, DateTime>(returnthis, DateTime.Now));

            return returnthis;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getTimer()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable statsTable = tableClient.GetTableReference("stattable");
            try
            {
                TableQuery<StatEntity> counterquery = new TableQuery<StatEntity>()
                                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "TomBot"));
                List<StatEntity> bean = statsTable.ExecuteQuery(counterquery).ToList();

                if (bean.Count != 0)
                {
                    StatEntity getem = bean.ElementAt(0);
                    return "Finding links took " + getem.timer + " ms...";
                }
            }
            catch (Exception e)
            {
                return "Could not access stats: " + e.Message;
            }
            return "Could not retrieve timer...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getTrienhardt()
        {
            string stats = "Trienhardt is not here...";

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable statsTable = tableClient.GetTableReference("stattable");
            try
            {
                TableQuery<StatEntity> counterquery = new TableQuery<StatEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Trienhardt"));
                List<StatEntity> bean = statsTable.ExecuteQuery(counterquery).ToList();

                if (bean.Count != 0)
                {
                    StatEntity getem = bean.ElementAt(0);
                    stats = "Last Word Added: '" + getem.lastTen + "', Lines: " + getem.visitcount; 
                }
            }
            catch (Exception e)
            {
                return "Could not retrieve stats: " + e.Message;
            }

            return stats;
        }
    }
}
