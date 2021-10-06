using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TFSBlasterLib.Exception;
using TFSBlasterLib.Model;
using TFSBlasterLib.Service;
using TFSBlasterLib.Service.Helpers;

namespace TFSBlasterTest.Service
{
    [TestClass]
    public class BlastServiceTests
    {
        private BlastService service;
        private Mock<ConnectionService> connectionService;
        private Mock<LogService> logService;
        private Mock<WorkItemRequestService> workItemRequestService;

        private Blast blast;

        [TestInitialize]
        public void Init()
        {
            connectionService = new Mock<ConnectionService>();
            logService = new Mock<LogService>();
            workItemRequestService = new Mock<WorkItemRequestService>();
            service = new BlastService
            {
                ConnectionService = connectionService.Object,
                LogService = logService.Object,
                WorkItemRequestService = workItemRequestService.Object
            };

            blast = new Blast
            {
                TfsUri = new Uri("http://test.uri"),
                Credentials = new TfsCredentials
                {
                    UserName = "testUser",
                    Password = "testPass"
                },
                Actions = new List<TfsAction>
                {
                    new TfsAction
                    {
                        Mode = TfsAction.ActionMode.Create,
                        WorkItems = new List<WorkItemProfile>
                        {
                            new WorkItemProfile
                            {
                                ProfileID = "1",
                                AreaPath = "test/area",
                                IterationPath = "test/iteration"
                            }
                        }
                    },
                    new TfsAction
                    {
                        Mode = TfsAction.ActionMode.Create,
                        WorkItems = new List<WorkItemProfile>
                        {
                            new WorkItemProfile
                            {
                                ProfileID = "2",
                                AreaPath = "test/area",
                                IterationPath = "test/iteration"
                            },
                            new WorkItemProfile
                            {
                                ProfileID = "3",
                                AreaPath = "test/area",
                                IterationPath = "test/iteration"
                            }
                        }
                    }
                }
            };
        }

        [TestMethod]
        public void BlastService_ProcessProfiles_AddsProfileRefsForValidLinks()
        {
            var link = new WorkItemLink
            {
                LinkedProfileID = "1"
            };

            blast.Actions[1].WorkItems[0].Links = new List<WorkItemLink>
            {
                link
            };

            service.ProcessProfiles(blast);

            var expectedLinkedWorkItem = blast.Actions[0].WorkItems[0];
            var actualLinkedWorkItem = link.LinkedProfile;
            Assert.AreEqual(expectedLinkedWorkItem, actualLinkedWorkItem);
        }

        [TestMethod, ExpectedException(typeof(ProfileLinkException))]
        public void BlastService_ProcessProfiles_RejectsInvalidLinks()
        {
            blast.Actions[1].WorkItems[0].Links = new List<WorkItemLink>
            {
                new WorkItemLink
                {
                    LinkedProfileID = "3"
                }
            };

            service.ProcessProfiles(blast);
        }

        [TestMethod]
        public async Task BlastService_Send_ProcessesAllActionsAndWorkItems()
        {
            Uri actualUri = null;
            TfsCredentials actualCredentials = null;

            var connection = new Connection();
            connectionService.Setup(s => s.StartConnectionTo(It.IsAny<Uri>(), It.IsAny<TfsCredentials>(), It.IsAny<int?>()))
                .Returns((Uri usedUri, TfsCredentials usedCredentials) =>
                {
                    actualUri = usedUri;
                    actualCredentials = usedCredentials;
                    return connection;
                });

            int actualWorkItemQueuedCount = 0;
            workItemRequestService.Setup(s => s.QueueCreation(It.IsAny<WorkItemProfile>(), connection))
                .Callback((WorkItemProfile usedWorkItem, Connection usedConnection) =>
                {
                    Assert.IsTrue(blast.Actions.Any(a => a.WorkItems.Contains(usedWorkItem)), "Each work item queued must have been included in the original blast object");
                    actualWorkItemQueuedCount++;
                });

            int actualQueuesProcessed = 0;
            connectionService.Setup(s => s.ProcessQueue(connection)).Returns((Connection usedConnection) =>
            {
                actualQueuesProcessed++;
                return Task.CompletedTask;
            });

            await service.Send(blast, true);

            connection.Dispose();

            var expectedUri = blast.TfsUri;
            Assert.AreEqual(expectedUri, actualUri, "Must use the URI provided in the blast");

            var expectedCredentials = blast.Credentials;
            Assert.AreEqual(expectedCredentials, actualCredentials, "Must use the credentials provided in the blast");
            
            int expectedQueuesProcessed = 2;
            Assert.AreEqual(expectedQueuesProcessed, actualQueuesProcessed, "Must process the queue for each action");

            int expectedWorkItemQueuedCount = 3;
            Assert.AreEqual(expectedWorkItemQueuedCount, actualWorkItemQueuedCount, "Must queue all work items");
        }
    }
}
