using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RobotomLibrary
{
    public class UriEntity : TableEntity
    {
        public UriEntity(Uri uri, string title, DateTime date, string key)
        {
            this.PartitionKey = key;
            this.RowKey = uri.AbsolutePath.GetHashCode().ToString();

            this.Site = uri.AbsoluteUri;
            this.Date = date.ToString();
            this.Title = title;
        }

        public UriEntity() { }

        public string Site { get; set; }
        public string Date { get; set; }
        public string Title { get; set; }
    }
}
