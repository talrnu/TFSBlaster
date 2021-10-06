using System;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TFSBlasterLib.Model;
using TFSBlasterLib.Model.Connection;
using TFSBlasterLib.Service;
using TFSBlasterLib.Service.Helpers;

namespace TFSBlasterTest.Service
{
    [TestClass]
    public class WorkItemRequestServiceTests
    {
        private WorkItemRequestService service;
        private Mock<ConnectionService> connectionService;

        [TestInitialize]
        public void Init()
        {
            connectionService = new Mock<ConnectionService>();
            service = new WorkItemRequestService
            {
                ConnectionService = connectionService.Object
            };
        }

        [TestMethod]
        public void WorkItemRequestService_QueueCreation_QueuesCorrectRequest()
        {
            var expectedWorkItem = new WorkItemProfile
            {
                WorkItemID = 1,
                ProfileID = "2",
                AcceptanceCriteria = "acceptance criteria",
                AreaPath = "area/path",
                IterationPath = "iteration/path",
                Description = "description",
                Title = "title",
                Type = WorkItemProfile.WorkItemType.Feature,
                State = WorkItemProfile.WorkItemState.Blocked,

            };

            var expectedConnection = new Mock<Connection>();
            expectedConnection.SetupGet(s => s.BaseUri).Returns(new Uri("http://test.uri"));

            Connection actualConnection = null;
            TfsRequestContext actualContext = null;

            connectionService.Setup(s => s.QueuePost(expectedConnection.Object, It.IsAny<TfsRequestContext>())).Callback(
                (Connection usedConnection, TfsRequestContext usedContext) =>
                {
                    actualConnection = usedConnection;
                    actualContext = usedContext;
                });

            service.QueueCreation(expectedWorkItem,expectedConnection.Object);
            Assert.AreEqual(expectedConnection.Object, actualConnection, "Provided connection must be used to queue request");
            Assert.AreEqual("/_apis/wit/workitems/Feature", actualContext.Endpoint.ToString(), "Request endpoint must match work item type");
            Assert.AreEqual(expectedWorkItem, actualContext.WorkItem, "Provided work item must be referenced in produced context");
        }
    }
}
