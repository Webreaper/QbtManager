using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel.Syndication;
using QbtManager;

namespace QBTCleanup
{
    public class DownloadHistory
    {
        [DataContract]
        class History
        {
            [DataMember]
            public List<HistoryItem> items { get; set; } = new List<HistoryItem>();
        }

        [DataContract]
        class HistoryItem
        {
            [DataMember]
            public string title { get; set; }
            [DataMember]
            public string comment { get; set; }
            [DataMember]
            public string url { get; set; }
            [DataMember]
            public DateTime dateDownloaded { get; set; } = DateTime.UtcNow;
        }

        private const string historyFilePath = "downloadhistory.json";

        private History history = null;

        public bool Contains(string url )
        {
            return history.items.Any(x => x.url == url);
        }

        public void AddHistory( SyndicationItem item)
        {
            var newItem = new HistoryItem
            {
                title = item.Title.Text,
                url = item.Links.Select( x => x.Uri.ToString() ).FirstOrDefault()
            };

            history.items.Add(newItem);
        }

        public void ReadHistory()
        {
            if (File.Exists(historyFilePath))
            {
                string json = File.ReadAllText(historyFilePath);

                history = Utils.deserializeJSON<History>(json);
            }
            else
                history = new History();
        }

        public void WriteHistory()
        {
            Utils.serializeJSON(history, historyFilePath);
        }
    }
}
