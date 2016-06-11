using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Queue;
using RobotomLibrary;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        private PerformanceCounter memprocess;
        private PerformanceCounter cpuprocess;

        private TomBot crawler;
        private int status;

        public override void Run()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
               ConfigurationManager.AppSettings["StorageConnectionString"]);

            // set up queues
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue commandQ = queueClient.GetQueueReference("commandq");
            CloudQueue htmlQ = queueClient.GetQueueReference("htmlq");
            htmlQ.CreateIfNotExists();
            commandQ.CreateIfNotExists();

            // set up tables
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable resultTable = tableClient.GetTableReference("crawltable");
            CloudTable errorTable = tableClient.GetTableReference("errortable");
            CloudTable statTable = tableClient.GetTableReference("stattable");
            resultTable.CreateIfNotExists();
            errorTable.CreateIfNotExists();
            statTable.CreateIfNotExists();

            this.memprocess = new PerformanceCounter("Memory", "Available MBytes");
            this.cpuprocess = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            memprocess.NextValue();
            cpuprocess.NextValue();

            int tablesize = 0;
            TableQuery<StatEntity> counterquery = new TableQuery<StatEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "TomBot"));
            List<StatEntity> bean = resultTable.ExecuteQuery(counterquery).ToList();
            if (bean.Count != 0)
            {
                tablesize = bean.ElementAt(0).tablesize;
            }

            crawler = new TomBot(htmlQ, resultTable, errorTable, tablesize);

            StatEntity stats = new StatEntity();
            stats.PartitionKey = "TomBot";
            stats.RowKey = "beepboopstats";
            status = Robotom.STATUS_IDLE;

            while (true)
            {
                updateStats(stats, statTable);
                //check commands
                CloudQueueMessage command = commandQ.GetMessage();
                if (command != null)
                {
                    string commandString = command.AsString;
                    if (commandString.Equals(Robotom.COMMAND_STOP))
                    {
                        status = Robotom.STATUS_STOP;
                        updateStats(stats, statTable);
                    }
                    else if (commandString.StartsWith(Robotom.COMMAND_START))
                    {
                        status = Robotom.STATUS_LOADING;
                        updateStats(stats, statTable);
                        string robotLink = commandString.Split(' ')[1] + "/robots.txt";
                        Uri thing = new Uri(robotLink);
                        List<Uri> sitemaps = crawler.ParseRobot(thing);
                        if (thing.Host.Equals("cnn.com"))
                        {
                            sitemaps.AddRange(crawler.ParseRobot(new Uri("http://bleacherreport.com/robots.txt")));
                        }
                        updateStats(stats, statTable);
                        while (sitemaps.Count > 0)
                        {
                            Uri next = sitemaps.ElementAt(0);
                            sitemaps.AddRange(crawler.ParseXml(next));
                            sitemaps.Remove(next);
                            updateStats(stats, statTable);
                        }
                    }
                    commandQ.DeleteMessage(command);
                }
                if (status != Robotom.STATUS_STOP)
                {
                    //check htmlq
                    CloudQueueMessage nextHtml = htmlQ.GetMessage();
                    if (nextHtml != null)
                    {
                        status = Robotom.STATUS_CRAWLING;
                        updateStats(stats, statTable);
                        crawler.ParseHtml(new Uri(nextHtml.AsString));
                        htmlQ.DeleteMessageAsync(nextHtml);
                    }
                    else
                    {
                        status = Robotom.STATUS_IDLE;
                        updateStats(stats, statTable);
                    }
                }
                else
                {
                    htmlQ.Clear();
                    crawler.queueCount = 0;
                }

                Thread.Sleep(10);
            }

        }

        private void updateStats(StatEntity stats, CloudTable statTable)
        {
            stats.memUsage = unchecked((int) memprocess.NextValue());
            stats.cpuUsage = unchecked((int) cpuprocess.NextValue());
            stats.queuesize = crawler.queueCount;
            stats.tablesize = crawler.tableCount;
            stats.visitcount = crawler.parsed.Count();
            System.Text.StringBuilder last = new System.Text.StringBuilder("");
            foreach (string u in crawler.lastTen)
            {
                last.Append(u + ";");
            }
            stats.lastTen = last.ToString();
            stats.timer = unchecked((int)crawler.timer);
            stats.status = status;
            try
            {
                statTable.ExecuteAsync(TableOperation.InsertOrReplace(stats));
            }
            catch (Exception e)
            {
                Trace.TraceInformation(e.Message);
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
