using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RobotomLibrary
{
    public class StatEntity : TableEntity
    {
        public StatEntity() { }

        public string lastTen { get; set; }
        public int memUsage { get; set; }
        public int cpuUsage { get; set; }
        public int queuesize { get; set; }
        public int tablesize { get; set; }
        public int visitcount { get; set; }
        public int status { get; set; }
        public int timer { get; set; }
    }
}
