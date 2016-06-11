using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using RobotomLibrary;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for TrieService
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class TrieService : System.Web.Services.WebService
    {
        public static Trie triehard;
        private PerformanceCounter memprocess = new PerformanceCounter("Memory", "Available MBytes");

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string CheckTrie()
        {
            return triehard == null? "Do or do not, there is no trie (could not find trie.)" : "We have a trie...";
        }

        [WebMethod]
        public float GetAvailableMBytes()
        {
            float memUsage = memprocess.NextValue();
            return memUsage;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string RemoveTrie()
        {
            triehard = null;
            return "Trie Deleted...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string CheckWiki()
        {
            string filepath = System.Web.HttpContext.Current.Server.MapPath(@"/wikititles.txt");
            return File.Exists(filepath) ? "We have a copy of the wiki..." : "Could not find the wiki...";
        }


        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string BuildTrie()
        {
            RemoveTrie();
            triehard = new Trie();
            string filepath = System.Web.HttpContext.Current.Server.MapPath(@"/wikititles.txt");
            if (!File.Exists(filepath))
            {
                return "Please download the wiki titles...";
            }
            StreamReader reader = new StreamReader(filepath);
            float startMem = GetAvailableMBytes();
            float memUsed = 0;
            int countLines = 0;
            string currentWord = "";
            while (!reader.EndOfStream)
            {
                if (countLines % 1000 == 0)
                {
                    float mem = GetAvailableMBytes();
                    memUsed = startMem - mem;
                    if(mem < 50)
                    {
                        break;
                    }
                }
                currentWord = reader.ReadLine();
                countLines++;
                triehard.Add(currentWord.Trim().ToLower());
                if(currentWord.Trim().ToLower().StartsWith("c"))
                {
                    break;
                }
            }

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
              ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable statsTable = tableClient.GetTableReference("stattable");
            statsTable.CreateIfNotExists();

            StatEntity stats = new StatEntity();
            stats.PartitionKey = "Trienhardt";
            stats.RowKey = "beepboopbastion";
            stats.lastTen = currentWord;
            stats.visitcount = countLines;
            statsTable.ExecuteAsync(TableOperation.InsertOrReplace(stats));

            return "Last insert: "+ currentWord + ", Lines: " + countLines + ", MemUsed: " + memUsed;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string DownloadWiki()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("something");
            CloudBlockBlob blobby = container.GetBlockBlobReference("wikititles2.txt");
            string filepath = System.Web.HttpContext.Current.Server.MapPath(@"/wikititles.txt");
            try{
                blobby.DownloadToFile(filepath, System.IO.FileMode.Create);
            }catch (Exception e)
            {
                return "File is busy: " + e.Message;
            }
            

            return "Download successful...";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string SearchTrie(string searchString)
        {
            if(triehard == null)
            {
                return new JavaScriptSerializer().Serialize("Service is down...");
            }
            return new JavaScriptSerializer().Serialize(triehard.Search(searchString.Trim().ToLower()));
        }
    }
}
