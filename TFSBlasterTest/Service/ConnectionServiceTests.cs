using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using TFSBlasterLib.Model;
using TFSBlasterLib.Model.Connection;
using TFSBlasterLib.Service;
using TFSBlasterLib.Service.Helpers;

namespace TFSBlasterTest.Service
{
    [TestClass]
    public class ConnectionServiceTests
    {
        private ConnectionService service;
        private Mock<HttpMessageHandler> httpMessageHandler;
        private Mock<LogService> logService;
        private Mock<JsonService> jsonService;

        [TestInitialize]
        public void Init()
        {
            logService = new Mock<LogService>();
            jsonService = new Mock<JsonService>();
            httpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            service = new ConnectionService(httpMessageHandler.Object)
            {
                LogService = logService.Object,
                JsonService = jsonService.Object
            };
        }

        [TestMethod]
        public void ConnectionService_StartConnection_CreatesConnectionWithHttpClient()
        {
            var expectedBaseUri = "http://test.uri/";
            var givenBaseUri = new Uri(expectedBaseUri);
            var givenCredentials = new TfsCredentials
            {
                UserName = "testUser",
                Password = "testPass"
            };
            var givenTimeout = 1000;
            
            var connection = service.StartConnectionTo(givenBaseUri, givenCredentials, givenTimeout);

            var expectedAuthHeader = "Basic dGVzdFVzZXI6dGVzdFBhc3M=";
            bool authHeaderAdded =
                connection.Client.DefaultRequestHeaders.TryGetValues("Authorization", out var actualAuthHeaders);
            Assert.IsTrue(authHeaderAdded && actualAuthHeaders.Count() == 1, "Must add exactly 1 default Authorization header");
            Assert.AreEqual(expectedAuthHeader, actualAuthHeaders.First(), "Must add correct auth header");

            var actualBaseUri = connection.Client.BaseAddress.ToString();
            Assert.AreEqual(expectedBaseUri, actualBaseUri, "Must set the correct base URI");
        }

        [TestMethod]
        public void ConnectionService_QueuePost_AddsRequestToQueueAsPost()
        {
            var connection = new Connection();
            var bodyItems = new List<TfsRequestItem>();
            var givenEndpoint = "relative/path";
            var context = new TfsRequestContext
            {
                Endpoint = new Uri(givenEndpoint, UriKind.Relative),
                Body = new TfsRequestBody
                {
                    Items = bodyItems
                }
            };
            var givenBodyText = "{ \"field\": \"value\" }";
            jsonService.Setup(s => s.Serialize(bodyItems)).Returns(givenBodyText);

            service.QueuePost(connection, context);

            var requestWasQueued = connection.PendingRequests.TryDequeue(out var actualRequest);

            Assert.IsTrue(requestWasQueued, "A request must be queued");
            Assert.AreEqual(HttpMethod.Post, actualRequest.HttpRequest.Method, "Must queue a HTTP POST request");
            Assert.AreEqual($"{givenEndpoint}?api-version=5.1&bypassRules=true", actualRequest.HttpRequest.RequestUri.ToString(), "Request must use correct endpoint and query params");
            Assert.AreSame(context, actualRequest.Context, "Request must reference the given context");

            var actualBodyText = actualRequest.HttpRequest.Content.ReadAsStringAsync().Result;
            Assert.AreEqual(givenBodyText, actualBodyText, "Request must use correct body text");
        }

        [TestMethod]
        public async Task ConnectionService_ProcessQueue_ProcessesAllRequestsInQueue()
        {
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("response text")
                })
                .Verifiable();

            var expectedBaseUri = "http://test.uri/";
            var givenBaseUri = new Uri(expectedBaseUri);
            var givenCredentials = new TfsCredentials
            {
                UserName = "testUser",
                Password = "testPass"
            };
            var givenTimeout = 1000;
            var connection = service.StartConnectionTo(givenBaseUri, givenCredentials, givenTimeout);

            jsonService.Setup(s => s.DeserializeAnonymous(It.IsAny<string>())).Returns(new Dictionary<string, object> {{"ID", 5}});

            var request1 = new TfsHttpRequest
            {
                HttpRequest = new HttpRequestMessage(),
                Context = new TfsRequestContext
                {
                    WorkItem = new WorkItemProfile()
                }
            };
            var request2 = new TfsHttpRequest
            {
                HttpRequest = new HttpRequestMessage(),
                Context = new TfsRequestContext
                {
                    WorkItem = new WorkItemProfile()
                }
            };
            connection.PendingRequests.Enqueue(request1);
            connection.PendingRequests.Enqueue(request2);

            await service.ProcessQueue(connection);

            Assert.AreEqual(5, request1.Context.WorkItem.WorkItemID, "First request must update source work item with its new ID");
            Assert.AreEqual(5, request2.Context.WorkItem.WorkItemID, "Second request must update source work item with its new ID");
        }
    }
}
