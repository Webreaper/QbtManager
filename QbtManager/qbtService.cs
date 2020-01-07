using System;
using System.Collections.Generic;
using RestSharp;
using System.Net;
using System.IO;

namespace QbtManager
{
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
            public string magnet_uri { get; set; }
            public string state { get; set; }
            public string tracker { get; set; }
            public DateTime added_on { get; set; }
            public DateTime completed_on { get; set; }

            public override string ToString()
			{
				return string.Format("{0:dd-MMM-yyyy}: {1} [{2}]", added_on, name,tracker);
			}
		}

        public qbtService(QBittorrentSettings qbtSettings)
        {
            settings = qbtSettings;
            client = new RestClient(settings.url);
        }

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

        public IList<Torrent> GetTasks()
        {
            var parms = new Dictionary<string, string>();

            // Dont use ?filter=completed here - we'll filter ourselves.
            var data = MakeRestRequest<List<Torrent>>("/torrents/info", parms);

            return data;
        }

        public bool DeleteTask( string[] taskIds )
        {
            var parms = new Dictionary<string, string>();

            parms["hashes"] = string.Join("|", taskIds);
            parms["deleteFiles"] = "false";
            return ExecuteRequest("/torrents/delete", parms);
        }

        public bool DownloadTorrent(string torrentUrl, string category)
        {
            var parms = new Dictionary<string, string>();

            parms["urls"] = torrentUrl;
            parms["category"] = category;
            return ExecuteCommand("/torrents/add", parms );
        }

        public bool PauseTask(string[] taskIds)
        {
            var parms = new Dictionary<string, string>();

            parms["hashes"] = string.Join("|", taskIds);
            return ExecuteRequest("/torrents/pause", parms);
        }

        public bool ExecuteRequest( string requestMethod, IDictionary<string, string> parms )
        {
            var request = new RestRequest(requestMethod, Method.GET);

            foreach (var kvp in parms)
                request.AddParameter(kvp.Key, kvp.Value);

            request.AddCookie("SID", token);

            var queryResult = client.Execute(request);

            return queryResult.StatusCode == HttpStatusCode.OK;
        }

        public bool ExecuteCommand(string requestMethod, IDictionary<string, string> parms)
        {
            var request = new RestRequest(requestMethod, Method.POST);
            
            foreach (var kvp in parms)
                request.AddParameter(kvp.Key, kvp.Value, ParameterType.GetOrPost);

            request.AddCookie("SID", token);

            var queryResult = client.Execute(request);

            return queryResult.StatusCode == HttpStatusCode.OK;
        }

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
