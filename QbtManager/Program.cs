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
        /// <summary>
        /// Given a task, see which tracker in the settings matches it.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        protected static Tracker FindTaskTracker(Torrent task, Settings settings)
        {
            Tracker tracker = null;

            if (settings.trackers == null || !settings.trackers.Any())
                return null;

            if (tracker == null)
                tracker = settings.trackers.FirstOrDefault(x => task.magnet_uri.ToLower().Contains(x.tracker));

            if (tracker == null)
                tracker = settings.trackers.FirstOrDefault(t => task.tracker.ToLower().Contains(t.tracker));

            // Allow wildcard ("keep all trackers")
            if( tracker == null )
                tracker = settings.trackers.FirstOrDefault(x => x.tracker == "*");

            return tracker;
        }

        private static readonly List<string> downloadedStates = new List<string> { "uploading", "pausedUP", "queuedUP", "stalledUP", "checkingUP", "forcedUP" };

        protected static bool IsDeletable( Torrent task, Tracker tracker )
        {
            bool canDelete = false;

            if (downloadedStates.Contains(task.state))
            {
                canDelete = true;

                if( tracker != null )
                {
                    // If the torrent is > 90 days old, delete
                    var age = DateTime.Now - task.added_on;

                    if (tracker.maxDaysToKeep == -1 || age.TotalDays < tracker.maxDaysToKeep)
                        canDelete = false;
                    else
                        Utils.Log("Task {0} deleted - too old", task.name);
                }
                else
                    Utils.Log("Task {0} deleted - wrong tracker", task.name);

            }

            return canDelete;
        }



        public static void Main(string[] args)
        {
            var settingPath = args.Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            LogHandler.InitLogs();

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

                qbtService service = new qbtService(settings.qbt);

                if (settings.deleteTasks)
                {
                    if (settings.deleteFiles)
                        Utils.Log("Filtered torrents will be deleted with their content.");
                    else
                        Utils.Log("Filtered torrents will be deleted (files will not be deleted).");
                }
                else
                    Utils.Log("Filtered torrents will be paused.");

                Utils.Log("Signing in to QBittorrent.");

                if (service.SignIn())
                {
                    Utils.Log("Getting Seeding Task list and mapping trackers...");
                    var tasks = service.GetTasks()
                                       .OrderBy(x => x.name)
                                       .ToList();

                    ProcessTorrents(service, tasks, settings);

                    if( settings.rssfeeds != null )
                        ReadRSSFeeds(service, settings.rssfeeds);
                }
                else
                    Utils.Log("Login failed.");
            }
            catch (Exception ex)
            {
                Utils.Log("Error initialising. {0}", ex);
            }
        }

        private static bool TrackerMsgIsDeletable( Torrent task, Tracker trackerSettings )
        {
            if (task.trackers == null || trackerSettings.deleteMessages == null )
                return false;

            var filterMsgs = trackerSettings.deleteMessages;

            var torrentMsgs = task.trackers.Where(x => !String.IsNullOrEmpty(x.msg)).Select(x => x.msg);

            if (torrentMsgs.Any(x => filterMsgs.Contains(x, StringComparer.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        private static void ProcessTorrents(qbtService service, IList<Torrent> tasks, Settings settings )
        {
            Utils.Log("Processing torrent list...");

            var toKeep = new List<Torrent>();
            var toDelete = new List<Torrent>();
            var limits = new Dictionary<Torrent, int>();
            var maxLimits = new Dictionary<Torrent, (float,int)>(); // max ratio, seeding time. API requires they both get set at once

            foreach (var task in tasks)
            {
                bool keepTask = true;
                var tracker = FindTaskTracker(task, settings);

                if (tracker != null)
                {
                    if (IsDeletable(task, tracker))
                        keepTask = false;

                    if( TrackerMsgIsDeletable( task, tracker ) )
                        keepTask = false;
                }

                if (keepTask)
                {
                    toKeep.Add(task);
                    Utils.Log($" - Keep: {task}");

                    if( tracker != null && tracker.up_limit.HasValue && task.up_limit != tracker.up_limit)
                    {
                        // Store the tracker limits.
                        limits[task] = tracker.up_limit.Value;
                    }

                    if (tracker != null && tracker.max_ratio.HasValue && task.max_ratio != tracker.max_ratio)
                    {
                        // Store the tracker limits.
                        maxLimits[task] = (tracker.max_ratio.Value, task.max_seeding_time);
                        // API call can't set just one have to set both
                    }

                    if (tracker != null && tracker.max_seeding_time.HasValue && task.max_seeding_time != tracker.max_seeding_time)
                    {
                        // Store the tracker limits.
                        // API call can't set just one have to set both
                        if (maxLimits.ContainsKey(task))
                        {
                            maxLimits[task] = (maxLimits[task].Item1, tracker.max_seeding_time.Value);
                        }
                        else
                        {
                            maxLimits[task] = (task.max_ratio, tracker.max_seeding_time.Value);
                        }

                    }
                }
                else
                {
                    if( ! settings.deleteTasks && task.state.StartsWith( "pause" ) )
                    {
                        Utils.Log($" - Already paused: {task}");

                        // Nothing to do, so skip.
                        continue;
                    }

                    toDelete.Add(task);
                    var action = settings.deleteTasks ? "Delete" : "Pause";
                    Utils.Log($" - {action}: {task}");
                }
            }

            if (limits.Any())
            {
                var limitGroups = limits.GroupBy(x => x.Value, y => y.Key);

                foreach (var x in limitGroups)
                {
                    int limit = x.Key;
                    var hashes = x.Select(t => t.hash).ToArray();

                    if (!service.SetUploadLimit(hashes, limit))
                        Utils.Log($"Failed to set upload limits.");
                }
            }

            if (maxLimits.Any())
            {
                var ratioGroups = maxLimits.GroupBy(x => x.Value, y => y.Key);

                foreach (var x in ratioGroups)
                {
                    float ratio_limit = x.Key.Item1;
                    int time_limit = x.Key.Item2;

                    var hashes = x.Select(t => t.hash).ToArray();

                    if (!service.SetMaxLimits(hashes, ratio_limit, time_limit))
                        Utils.Log($"Failed to set max ratio and time limit.");
                }
            }

            if (toDelete.Any())
            {
                var deleteHashes = toDelete.Select(x => x.hash)
                                           .Distinct()
                                           .ToArray();

                if (settings.deleteTasks)
                {
                    Utils.Log($"Deleting {deleteHashes.Count()} tasks...");
                    service.DeleteTask(deleteHashes, settings.deleteFiles);
                }
                else
                {
                    Utils.Log($"Pausing {deleteHashes.Count()} tasks...");
                    service.PauseTask(deleteHashes);
                }

                if (settings.email != null)
                {
                    Utils.Log("Sending alert email.");
                    Utils.SendAlertEmail(settings.email, toDelete);
                }
            }
            else
                Utils.Log("No tasks to delete/pause.");
        }

        private static void ReadRSSFeeds(qbtService service, List<RSSSettings> settings)
        {
            if( settings != null && settings.Any() )
            { 
                Utils.Log("Processing RSS feed list...");

                foreach (var rssFeed in settings)
                {
                    ReadRSSFeed(service, rssFeed);
                }
            }
            else
                Utils.Log("No RSS feeds to process...");
        }

        private static void ReadRSSFeed(qbtService service, RSSSettings rssFeed)
        {
            Utils.Log("Reading RSS feed for {0}", rssFeed.url);

            try
            {
                XmlReader reader = XmlReader.Create(rssFeed.url);
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
