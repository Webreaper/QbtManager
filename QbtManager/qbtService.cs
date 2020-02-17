using System;
using System.Collections.Generic;
using RestSharp;
using System.Net;
using System.IO;

namespace QbtManager
{
    /// <summary>
    /// Wrapper for the QBitorrent service.
    /// </summary>
    public class qbtService
    {
        private readonly RestClient client;
        private readonly QBittorrentSettings settings;
        private string token;

        public class Torrent 
        {
            public string hash { get; set; }  
            public string category { get; set; }  
            public string name { get; set; }
            public string tracker { get; set; }
            public string magnet_uri { get; set; }
            public string state { get; set; }
            public int up_limit { get; set; }
            public DateTime added_on { get; set; }
            public DateTime completed_on { get; set; }

            public override string ToString()
			{
                var age = DateTime.Now - added_on;
                string span = "(" + state + ", " + age.ToHumanReadableString() + ")";
                return $" * {name} {span}";
			}
		}

        public qbtService(QBittorrentSettings qbtSettings)
        {
            settings = qbtSettings;
            client = new RestClient(settings.url);
        }

        /// <summary>
        /// Authenticate and get an SID token
        /// </summary>
        /// <returns></returns>
        public bool SignIn()
        {
            token = null;

            try
            {
                CookieContainer cookies = new CookieContainer();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(settings.url + "/auth/login");
                req.Method = "GET";
                req.CookieContainer = cookies;
                req.Accept = "application/json";
                req.UserAgent = "QBTCleanup";

                HttpWebResponse response = (HttpWebResponse)req.GetResponse();

                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string result = reader.ReadToEnd();

                    var tokenCookie = response.Cookies["SID"];
                    if( tokenCookie != null )
                    {
                        token = tokenCookie.Value;
                        if (!string.IsNullOrEmpty(token))
                            return true;
                    }
                }
            }
            catch( Exception ex )
            {
                Utils.Log("Exception! " + ex.Message);
            }

            return false;
        }

        /// <summary>
        /// Get the list of torrents
        /// </summary>
        /// <returns></returns>
        public IList<Torrent> GetTasks()
        {
            var parms = new Dictionary<string, string>();

            // Dont use ?filter=completed here - we'll filter ourselves.
            var data = MakeRestRequest<List<Torrent>>("/torrents/info", parms);

            return data;
        }

        /// <summary>
        /// Delete a list of torrents via hash
        /// </summary>
        /// <param name="taskIds"></param>
        /// <returns></returns>
        public bool DeleteTask( string[] taskIds )
        {
            var parms = new Dictionary<string, string>();

            parms["hashes"] = string.Join("|", taskIds);
            parms["deleteFiles"] = "false";
            return ExecuteRequest("/torrents/delete", parms);
        }

        /// <summary>
        /// Download a torrent, given a URL, adding an optional category
        /// </summary>
        /// <param name="torrentUrl"></param>
        /// <param name="category"></param>
        /// <returns></returns>
        public bool DownloadTorrent(string torrentUrl, string category)
        {
            var parms = new Dictionary<string, string>();

            parms["urls"] = torrentUrl;
            parms["category"] = category;
            return ExecuteCommand("/torrents/add", parms );
        }

        /// <summary>
        /// Pause a list of torrents, via hashes
        /// </summary>
        /// <param name="taskIds"></param>
        /// <returns></returns>
        public bool PauseTask(string[] taskIds)
        {
            var parms = new Dictionary<string, string>();

            parms["hashes"] = string.Join("|", taskIds);
            return ExecuteRequest("/torrents/pause", parms);
        }

        /// <summary>
        /// Sets the upload limit for a list of hashes
        /// </summary>
        /// <param name="taskIds"></param>
        /// <param name="limitKiloBytesPerSec">Upload limit in KB/s</param>
        /// <returns></returns>
        public bool SetUploadLimit(string[] taskIds, int limitKiloBytesPerSec)
        {
            var parms = new Dictionary<string, string>();

            // Convert from KB to bytes, for the service
            int limitBytesPerSec = limitKiloBytesPerSec * 1024;

            parms["hashes"] = string.Join("|", taskIds);
            parms["limit"] = limitBytesPerSec.ToString();

            return ExecuteCommand("/torrents/setUploadLimit", parms);
        }

        /// <summary>
        /// Execute a GET request, passing the Auth token
        /// </summary>
        /// <param name="requestMethod"></param>
        /// <param name="parms"></param>
        /// <returns></returns>
        public bool ExecuteRequest( string requestMethod, IDictionary<string, string> parms )
        {
            var request = new RestRequest(requestMethod, Method.GET);

            foreach (var kvp in parms)
                request.AddParameter(kvp.Key, kvp.Value);

            request.AddCookie("SID", token);

            var queryResult = client.Execute(request);

            return queryResult.StatusCode == HttpStatusCode.OK;
        }

        /// <summary>
        /// Execute a POST request, passing the Auth token
        /// </summary>
        /// <param name="requestMethod"></param>
        /// <param name="parms"></param>
        /// <returns></returns>
        public bool ExecuteCommand(string requestMethod, IDictionary<string, string> parms)
        {
            var request = new RestRequest(requestMethod, Method.POST);
            
            foreach (var kvp in parms)
                request.AddParameter(kvp.Key, kvp.Value, ParameterType.GetOrPost);

            request.AddCookie("SID", token);

            var queryResult = client.Execute(request);

            return queryResult.StatusCode == HttpStatusCode.OK;
        }

        /// <summary>
        /// Generic REST method handler.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="requestMethod"></param>
        /// <param name="parms"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public T MakeRestRequest<T>(string requestMethod, IDictionary<string, string> parms, Method method = Method.GET) where T : new()
        {
            var request = new RestRequest(requestMethod, method );

            foreach (var kvp in parms)
                request.AddParameter(kvp.Key, kvp.Value, ParameterType.GetOrPost);

            request.AddCookie("SID", token);

            try
            {
                var queryResult = client.Execute<T>(request);

                if (queryResult != null)
                {
                    if (queryResult.StatusCode != HttpStatusCode.OK)
                    {
                        Utils.Log("Error: {0} - {1}", queryResult.StatusCode, queryResult.Content);
                    }
                    else
                    {
                        var response = queryResult.Data;

                        if (response != null)
                        {
                            return response;
                        }
                        else
                            Utils.Log("No response Data.");
                    }
                }
                else
                    Utils.Log("No valid queryResult.");
            }
            catch (Exception ex)
            {
                Utils.Log("Exception: {0}: {1}", ex.Message, ex);
            }

            return default(T);
        }
    }
}
