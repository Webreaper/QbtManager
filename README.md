# QbtManager
 QBitTorrent Manager for clearing up jobs and processing RSS. It has two aims:
 * Pause any completed torrents unless their Magnet URL/Tracker info contains specific keywords specified in the trackersToKeep settings section. This allows you to stop torrents on most trackers when they complete, but keep torrents seeding on private trackers with certain ratios etc.
 * Do RSS for server-based (linux) QBitTorrent. QBT handles RSS on Windows, but not on Linux. So this will take a list of RSS URLs, and download all new torrents that are listed in them. Note that it will create a downloadhistory.json file to track snatched torrents so they're not repeatedly downloaded and added to QBT.
 
Note - you will need to ensure auth is disabled for localhost etc. for QBitorrrent.
 
## To use:
1. Copy onto your linux NAS.
2. Create 'Settings.json' file in the same folder
3. Run, in a script or cron job. 

## Settings.json format/sample:
```
  {
    "logLocation": "./dscleanup.log",
    "qbt": {
      "username": "admin",
      "url": "http://192.168.1.30:8090/api/v2"
    },
    "trackers": [
      {
        "tracker":"tvchaos",
        "maxDaysToKeep" : 365,
        "up_limit" : -1
      },   
      {
        "tracker":"*",
        "maxDaysToKeep" : 1,
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
