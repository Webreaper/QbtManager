using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using QBTManager.Logging;
using static QbtManager.qbtService;
using System.ServiceModel.Syndication;
using System.Xml;
using QBTCleanup;

namespace QbtManager
{
    public class MainClass
    {
        protected static bool KeepTracker(Torrent task, TorrentCleanupSettings settings)
        {
            bool keepTracker = false;

            if (settings.trackersToKeep.Any(t => task.tracker.ToLower().Contains(t)))
                keepTracker = true;

            if (settings.trackersToKeep.Any(t => task.magnet_uri.ToLower().Contains(t)))
                keepTracker = true;

            // No tracker? Give it the benefit of the doubt.
            if (! keepTracker && string.IsNullOrEmpty(task.tracker))
                keepTracker = true;

            return keepTracker;
        }

        private static readonly List<string> downloadedStates = new List<string> { "uploading", "pausedUP", "queuedUP", "stalledUP", "checkingUP", "forcedUP" };
        private ServerCertificateValidation certValidation = new ServerCertificateValidation();

        protected static bool isDeletable(Torrent task, TorrentCleanupSettings settings)
        {
            bool canDelete = false;

            if (downloadedStates.Contains(task.state))
            {
                canDelete = true;

                if (KeepTracker(task, settings))
                {
                    // If the torrent is > 90 days old, delete
                    var age = DateTime.Now - task.added_on;

                    if (age.TotalDays < settings.maxDaysToKeep)
                        canDelete = false;
                    else
                        Utils.Log("Task {0} deleted - too old", task.name);
                }
                else
                    Utils.Log("Task {0} deleted - wrong tracker", task.name);

            }

            return canDelete;
        }

        public static string ToHumanReadableString(TimeSpan t)
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

        public static void Main(string[] args)
        {
            var settingPath = args.Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (string.IsNullOrEmpty(settingPath))
                settingPath = "Settings.json";

            try
            {
                if (!File.Exists(settingPath))
                {
                    Utils.Log("Settings not found: {0}", settingPath);
                    return;
                }

                string json = File.ReadAllText(settingPath);

                var settings = Utils.deserializeJSON<Settings>(json);

                LogHandler.InitLogs();

                qbtService service = new qbtService(settings.qbt);

                Utils.Log("Tasks will be kept for trackers: {0}", string.Join(", ", settings.cleanup.trackersToKeep));
                Utils.Log("Tracker-held tasks will be deleted after {0} days.", settings.cleanup.maxDaysToKeep);
                Utils.Log("Signing in to QBittorrent.");

                if (service.SignIn())
                {
                    Utils.Log("Getting Seeding Task list...");
                    var tasks = service.GetTasks()
                                       .OrderBy(x => x.name)
                                       .ToList();

                    CleanUpTorrents(service, tasks, settings);

                    ReadRSSFeeds(service, settings.rss);
                }
                else
                    Utils.Log("Login failed.");
            }
            catch (Exception ex)
            {
                Utils.Log("Error initialising. {0}", ex);
            }
        }

        private static void CleanUpTorrents(qbtService service, IList<Torrent> tasks, Settings settings )
        {
            Utils.Log("Cleaning up torrent list...");

            var tasksToDelete = tasks.Where(x => isDeletable(x, settings.cleanup))
                                     .OrderBy(x => x.added_on)
                                     .ToList();

            var tasksToKeep = tasks.Except(tasksToDelete)
                                   .OrderBy(x => x.added_on)
                                   .ToList();

            if (tasksToKeep.Any())
            {
                Utils.Log("Tasks to Keep:");
                foreach (var task in tasksToKeep)
                {
                    string span = "(" + task.state + ", " + ToHumanReadableString(DateTime.Now - task.added_on) + ")";
                    Utils.Log($" * {task.name} {span}");
                }
            }

            if (tasksToDelete.Any())
            {
                var deleteTasks = new List<Torrent>();

                Utils.Log("Tasks to delete:");
                foreach (var task in tasksToDelete)
                {
                    Utils.Log($" - {task.name}");
                    deleteTasks.Add(task);
                }

                if (deleteTasks.Any())
                {
                    var deleteHashes = deleteTasks.Select(x => x.hash).ToArray();
                    if (settings.cleanup.deleteTasks)
                        service.DeleteTask(deleteHashes);
                    else
                        service.PauseTask(deleteHashes);

                    Utils.SendAlertEmail(settings.email, deleteTasks);
                }
            }
            else
                Utils.Log("No tasks to delete");
        }

        private static void ReadRSSFeeds(qbtService service, RSSSettings settings)
        {
            if (settings.rssUrls.Any())
            {
                Utils.Log("Processing RSS feed list...");

                foreach (string uri in settings.rssUrls)
                {
                    ReadRSSFeed(service, uri);
                }
            }
            else
                Utils.Log("No RSS feeds to process...");
        }

        private static void ReadRSSFeed(qbtService service, string feedUrl)
        {
            Utils.Log("Reading RSS feed for {0}", feedUrl);

            try
            {
                XmlReader reader = XmlReader.Create(feedUrl);
                SyndicationFeed feed = SyndicationFeed.Load(reader);
                reader.Close();

                if (feed.Items.Any())
                {
                    // Cache list and save dates. Filter by list entry and oldest date > 1 month
                    DownloadItems(service, feed.Items);
                }
                else
                    Utils.Log("No RSS items found.");
            }
            catch (Exception ex)
            {
                Utils.Log("Error reading feed: {0}", ex.Message);
            }
        }

        private static void DownloadItems(qbtService service, IEnumerable<SyndicationItem> items)
        {
            Utils.Log("Processing {0} RSS feed items.", items.Count() );

            DownloadHistory history = new DownloadHistory();

            history.ReadHistory();

            foreach (SyndicationItem item in items)
            {
                string subject = item.Title.Text;

                var link = item.Links.FirstOrDefault();

                if (link != null)
                {
                    string torrentUrl = link.Uri.ToString();

                    if( history.Contains( torrentUrl ) )
                    {
                        Utils.Log("Skipping item for {0} (downloaded already).", subject, torrentUrl);
                        continue;
                    }

                    Utils.Log("Sending link for {0} ({1}) to QBT...", subject, torrentUrl);

                    if (service.DownloadTorrent(torrentUrl, "freeleech"))
                    {
                        history.AddHistory(item);
                    }
                    else
                        Utils.Log("Error: torrent add failed.");
                }
            }

            history.WriteHistory();
        }
    }
}
