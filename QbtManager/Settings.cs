using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace QbtManager
{
    [DataContract]
    public class QBittorrentSettings
    {
        [DataMember]
        public string username { get; set; }
        [DataMember]
        public string password { get; set; }
        [DataMember]
        public string url { get; set; }
    }

    [DataContract]
    public class TorrentCleanupSettings
    {
        [DataMember]
        public string[] trackersToKeep { get; set; }
        [DataMember]
        public int maxDaysToKeep { get; set; }
        [DataMember]
        public int diskFileAgeBeforeDeleteMins = 15;
        [DataMember]
        public bool deleteTasks = false;
    }

    [DataContract]
    public class RSSSettings
    {
        [DataMember]
        public string[] rssUrls { get; set; }
    }

    [DataContract]
    public class Settings
    {
        [DataMember]
        public QBittorrentSettings qbt { get; set; }
        [DataMember]
        public TorrentCleanupSettings cleanup { get; set; }
        [DataMember]
        public RSSSettings rss { get; set; }
        [DataMember]
        public string logLocation { get; set; }
    }
}
