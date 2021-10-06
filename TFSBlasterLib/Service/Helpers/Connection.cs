using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Formatting;
using TFSBlasterLib.Model.Connection;

namespace TFSBlasterLib.Service.Helpers
{
    internal class Connection : IDisposable
    {
        public const int MAX_CONCURRENT_REQUESTS = 10;
        public const int DEFAULT_REQUEST_TIMEOUT = 30000;

        public JsonService JsonService;
        public LogService LogService;
        public ConnectionService ConnectionService;

        public List<TfsHttpRequest> ActiveRequests;
        public ConcurrentQueue<TfsHttpRequest> PendingRequests;
        public HttpClient Client;
        public string QueryText;

        public virtual Uri BaseUri => Client.BaseAddress;

        public Connection()
        {
            ActiveRequests = new List<TfsHttpRequest>();
            PendingRequests = new ConcurrentQueue<TfsHttpRequest>();

            QueryText = new FormDataCollection(
                new Dictionary<string, string>
                {
                    {"api-version", "5.1"},
                    //{"bypassRules", "true"}
                }
            ).ReadAsNameValueCollection().ToString();
        }

        public void Dispose()
        {
            Client?.Dispose();
            foreach (var activeRequest in ActiveRequests)
            {
                activeRequest.RequestTask.Dispose();
            }
        }
    }
}
