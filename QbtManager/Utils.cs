using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;
using QBTManager.Logging;
using static QbtManager.qbtService;

namespace QbtManager
{
    public static class Utils
    {
        public static string ToHumanReadableString(this TimeSpan t)
        {
            if (t.TotalSeconds <= 1)
            {
                return $@"{t:s\.ff} seconds";
            }
            if (t.TotalMinutes <= 1)
            {
                return $@"{t:%s} seconds";
            }
            if (t.TotalHours <= 1)
            {
                return $@"{t:%m} minutes";
            }
            if (t.TotalDays <= 1)
            {
                return $@"{t:%h} hours";
            }

            return $@"{t:%d} days";
        }

        public static DateTime getFromEpoch(long epoch)
        {
            var stamp = new DateTime(1970, 1, 1, 0, 0, 0).ToUniversalTime().AddMilliseconds(Convert.ToDouble(epoch));
            return stamp;
        }

        public static long getEpochTime(DateTime time)
        {
            TimeSpan t = time.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0);
            return (long)t.TotalMilliseconds;
        }

        public static void SendAlertEmail( EmailSettings settings, IEnumerable<Torrent> tasks )
        {
            string body = "Cleaned up the following downloads:\n";
            foreach (var task in tasks.OrderBy(x => x.name))
            {
                var msg = $" - {task.name}: {task.state} (Tracker: {task.tracker})";
                body += msg + "\n";
            }

            try
            {
                var mimeMsg = new MimeMessage();
                mimeMsg.From.Add(new MailboxAddress("QBT Manager", settings.fromaddress));
                mimeMsg.To.Add(new MailboxAddress(settings.toname, settings.toaddress));
                mimeMsg.Subject = "[Download Station] Download Cleanup";
                mimeMsg.Body = new TextPart("plain") { Text = body };

                using (var client = new SmtpClient())
                {
                    client.Timeout = 30 * 1000;
                    client.Connect(settings.smtpserver, settings.smtpport, false);

                    // Note: since we don't have an OAuth2 token, disable
                    // the XOAUTH2 authentication mechanism.
                    client.AuthenticationMechanisms.Remove("XOAUTH2");

                    // Note: only needed if the SMTP server requires authentication
                    client.Authenticate(settings.username, settings.password);

                    client.Send(mimeMsg);
                    client.Disconnect(true);
                }
            }
            catch (Exception ex)
            {
                Log("Exception sending emai: {0}", ex.Message);
            }
        }


        public static void Log(string format, params object[] args)
        {
            LogHandler.Log(format, args);
        }

        public static T deserializeJSON<T>(string json)
        {
            var instance = Activator.CreateInstance<T>();
            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(instance.GetType());
                return (T)serializer.ReadObject(ms);
            }
        }

        public static void serializeJSON<T>(T obj, string path)
        {
            var instance = Activator.CreateInstance<T>();
            using (var ms = new FileStream(path, FileMode.OpenOrCreate))
            {
                var serializer = new DataContractJsonSerializer(instance.GetType());
                serializer.WriteObject(ms, obj);
            }
        }
    }
}
