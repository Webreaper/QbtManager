# QbtManager
 QBitTorrent Manager for clearing up jobs and processing RSS. It has several aims:
 * Pause any completed torrents unless their tracker is listed in the settings, and the tracker is listed in the settings. This allows you to stop torrents on most trackers when they complete, but keep torrents seeding on private trackers with certain ratios etc.
 * Adjust other torrent properties on a per-tracker basis, including upload limit and other parameters.
 * Do RSS for server-based (linux) QBitTorrent. QBT handles RSS on Windows, but not on Linux. So this will take a list of RSS URLs, and download all new torrents that are listed in them. Note that it will create a downloadhistory.json file to track snatched torrents so they're not repeatedly downloaded and added to QBT.
  
## To use:
1. Copy the binaries from the latest [release] (https://github.com/Webreaper/QbtManager/releases/tag) to your linux NAS. 
There's Linux and Windows binaries each in a zip file. 
2. Create 'Settings.json' file in the same folder
3. Run, in a script or cron job. 

## Settings.json format/sample:

This example uses a wildcard to stop all torrents once they complete, and set their upload
limite to 30KB/s, except for those from APrivateTracker, which will be kept for 1 year
and seeded with an unlimied upload speed. Trackers are processed in-order, so for global
settings, specify the wildcard tracker last.

Note that if `deleteTasks=true` then torrents will be deleted from QBT when they expire.
If `deleteTasks=false`, torrents will be paused, but not deleted. 

If `deleteFiles=true` then torrent content/files will be deleted when torrents are deleted.
This has no effect if `deleteTasks=false`.

The `deleteMessages` setting is a special filter for particular messages on a tracker. Any
strings listed here will result in the torrent being deleted if the tracker message in QBT
matches them. This can be particularly useful when a tracker drops a torrent - e.g., if it's
been repacked, and the individual torrent is no longer valid. 

The QBT password field is optional. If you leave it out, you will need to change the settings
in QBT to include the subnet where this tool is running to be excluded from authentication.

```
  {
    "logLocation": "./dscleanup.log",
    "deleteTasks": true,
    "deleteFiles": false,
    "qbt": {
      "username": "admin",
      "password": "sekritpasswd",
      "url": "http://192.168.1.30:8090/api/v2"
    },
    "trackers": [
      {
        "tracker":"APrivateTracker",
        "maxDaysToKeep" : 365,
        "up_limit" : -1,
        "deleteMessages" : [
          "unregistered torrent"
        ] 
      },   
      {
        "tracker":"*",
        "maxDaysToKeep" : 0,
        "up_limit" : 30
      }      
    ],
    "rssfeeds": [
      {
        "url" : "https://mytracker.com/rssfeed"
      }
    ],  "email": {
      "smtpserver": "mail.somehost.co.uk",
      "smtpport": 25,
      "username": "joe@example.com",
      "password": "sekritpassword",
      "toaddress": "joe@example.com",
      "fromaddress": "joe@example.com",
      "toname": "Joe Smith"
    }
  }
```
