using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace QbtManager
{
    [DataContract]
    public class EmailSettings
    {
        [DataMember]
        public string smtpserver { get; set; }
        [DataMember]
        public int smtpport { get; set; }
        [DataMember]
        public string username { get; set; }
        [DataMember]
        public string password { get; set; }
        [DataMember]
        public string toaddress { get; set; }
        [DataMember]
        public string fromaddress { get; set; }
        [DataMember]
        public string toname { get; set; }
    }

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
    public class Tracker
    {
        [DataMember]
        public string tracker { get; set; }
        [DataMember]
        public int maxDaysToKeep { get; set; }
        [DataMember]
        public int diskFileAgeBeforeDeleteMins = 15;
        [DataMember]
        public int? up_limit { get; set; }
        [DataMember]
        public List<string> deleteMessages { get; set; }
    }

    [DataContract]
    public class RSSSettings
    {
        [DataMember]
        public string url { get; set; }
    }

    [DataContract]
    public class Settings
    {
        [DataMember]
        public QBittorrentSettings qbt { get; set; }
        [DataMember]
        public List<Tracker> trackers { get; set; }
        [DataMember]
        public List<RSSSettings> rssfeeds { get; set; }
        [DataMember]
        public string logLocation { get; set; }
        [DataMember]
        public EmailSettings email { get; set; }
        [DataMember]
        public bool deleteTasks = false;
        [DataMember]
        public bool deleteFiles = false;
    }
}
