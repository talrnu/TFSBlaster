using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TFSBlasterLib.Model;
using TFSBlasterLib.Model.Connection;
using TFSBlasterLib.Service.Helpers;

namespace TFSBlasterLib.Service
{
    internal class ConnectionService
    {
        public LogService LogService;
        public JsonService JsonService;

        private HttpMessageHandler httpMessageHandler;

        public ConnectionService() : this(null) { }

        internal ConnectionService(HttpMessageHandler httpMessageHandler)
        {
            this.httpMessageHandler = httpMessageHandler;
        }

        public virtual Connection StartConnectionTo(Uri remoteUri, TfsCredentials credentials, int? timeoutMillis)
        {
            var connection = new Connection()
            {
                ConnectionService = this,
                LogService = LogService,
                JsonService = JsonService,
                Client = CreateHttpClient(remoteUri, credentials, timeoutMillis ?? Connection.DEFAULT_REQUEST_TIMEOUT)
            };
            return connection;
        }

        public virtual void QueuePost(Connection connection, TfsRequestContext context)
        {
            Log($"Queueing POST request context:\n{context}");
            var request = CreateRequest("POST", connection, context);
            connection.PendingRequests.Enqueue(request);
        }

        public virtual void QueuePatch(Connection connection, TfsRequestContext context)
        {
            var request = CreateRequest("PATCH", connection, context);
            connection.PendingRequests.Enqueue(request);
        }

        public virtual async Task ProcessQueue(Connection connection)
        {
            var pendingCount = connection.PendingRequests.Count;
            var activeCount = connection.ActiveRequests.Count;
            var lastPendingCount = 0;
            var lastActiveCount = 0;

            while (pendingCount > 0 || activeCount > 0)
            {
                if (pendingCount != lastPendingCount || activeCount != lastActiveCount)
                {
                    Log($"Process queue: {connection.PendingRequests.Count} pending, {connection.ActiveRequests.Count} active");
                }
                int removed = connection.ActiveRequests.RemoveAll(r => r.RequestTask.IsCompleted);
                if (removed > 0)
                {
                    Log($"Removed {removed} completed requests from active list.");
                }

                while (connection.PendingRequests.Any() && connection.ActiveRequests.Count < Connection.MAX_CONCURRENT_REQUESTS)
                {
                    var dequeueSuccess = connection.PendingRequests.TryDequeue(out var nextRequest);
                    if (dequeueSuccess)
                    {
                        Log($"\tSending HTTP request for profile {nextRequest.Context.WorkItem.ProfileID}...");
                        nextRequest.RequestTask = Task.Run(async () => await PerformRequest(nextRequest, connection));
                        connection.ActiveRequests.Add(nextRequest);
                        Log($"\t");
                    }
                }

                lastPendingCount = pendingCount;
                lastActiveCount = activeCount;
                await Task.Delay(500);
                pendingCount = connection.PendingRequests.Count;
                activeCount = connection.ActiveRequests.Count;
            }

            Log($"All queued requests have been processed.");
        }

        private HttpClient CreateHttpClient(Uri remoteUri, TfsCredentials credentials, int timeoutMillis)
        {
            var client = (httpMessageHandler != null) ? new HttpClient(httpMessageHandler) : new HttpClient();
            client.BaseAddress = remoteUri;
            client.Timeout = TimeSpan.FromMilliseconds(timeoutMillis);

            var oauthTokenDecoded = $"{credentials.UserName}:{credentials.Password}";
            var oauthTokenBytes = Encoding.UTF8.GetBytes(oauthTokenDecoded);
            var oauthToken = Convert.ToBase64String(oauthTokenBytes);
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {oauthToken}");

            return client;
        }

        private TfsHttpRequest CreateRequest(string method, Connection connection, TfsRequestContext context)
        {
            var httpRequest = new HttpRequestMessage();
            httpRequest.Method = new HttpMethod(method);

            httpRequest.RequestUri = new Uri($"{context.Endpoint}?{connection.QueryText}", UriKind.Relative);

            var bodyText = JsonService.Serialize(context.Body.Items);
            Log($"Profile {context.WorkItem.ProfileID} request body text:\n{bodyText}");
            httpRequest.Content = new StringContent(bodyText, Encoding.UTF8, "application/json-patch+json");

            var tfsRequest = new TfsHttpRequest
            {
                HttpRequest = httpRequest,
                Context = context
            };

            return tfsRequest;
        }

        private async Task PerformRequest(TfsHttpRequest request, Connection connection)
        {
            Log($"Profile {request.Context.WorkItem.ProfileID} request: {request.HttpRequest.Method} {request.HttpRequest.RequestUri}");

            try
            {
                request.HttpResponse = await connection.Client.SendAsync(request.HttpRequest);
            }
            catch (System.Exception ex)
            {
                Log($"HTTP client failed to send request for Profile {request.Context.WorkItem.ProfileID}: {ex}");
                return;
            }

            var responseBody = await request.HttpResponse.Content.ReadAsStringAsync();
            Log($"Profile {request.Context.WorkItem.ProfileID} response:\n{request.HttpResponse.StatusCode} {request.HttpResponse.ReasonPhrase}");

            var responseData = JsonService.DeserializeAnonymous(responseBody);
            if (!request.HttpResponse.IsSuccessStatusCode)
            {
                var failMessage = (string) responseData["message"];
                Log($"Profile {request.Context.WorkItem.ProfileID} HTTP request FAIL: {failMessage}\n{request.HttpResponse}\n{responseBody}");
                request.Context.WorkItem.WorkItemID = null;
            }
            else
            {
                var dataText = string.Join("\n\t", responseData.Select(d => $"{d.Key} : {d.Value}"));
                Log($"Profile {request.Context.WorkItem.ProfileID} HTTP request SUCCESS:\nResponse dictionary deserialized:\n\t{dataText}");
                request.Context.WorkItem.WorkItemID = (long)responseData["id"];

                //TODO: abstract out this result handling/reporting
                var verb = (request.HttpRequest.Method.Method == "POST") ? "Create" :
                    (request.HttpRequest.Method.Method == "PATCH") ? "Update" : $"HTTP {request.HttpRequest.Method.Method}";
                Print($"{verb} {request.Context.WorkItem.Type} {request.Context.WorkItem.Title}: Success (Work Item ID {request.Context.WorkItem.WorkItemID})");
            }
        }

        private void Print(string message)
        {
            LogService.LogToConsole(nameof(ConnectionService), message);
        }

        private void Log(string message)
        {
            LogService.LogToFile(nameof(ConnectionService), message);
        }
    }
}
