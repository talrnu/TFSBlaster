using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using TFSBlasterLib.Model;
using TFSBlasterLib.Service;

namespace TFSBlasterTest.Service
{
    [TestClass]
    public class JsonServiceTests
    {
        private JsonService jsonService;
        private Mock<FileService> fileService;

        [TestInitialize]
        public void Init()
        {
            fileService = new Mock<FileService>();
            jsonService = new JsonService()
            {
                FileService = fileService.Object
            };
        }

        [TestMethod]
        public void JsonService_ReadJsonFile_ParsesJsonText()
        {
            var oldBlast = new Blast
            {
                Actions = new List<TfsAction>
                {
                    new TfsAction
                    {
                        Mode = TfsAction.ActionMode.Create,
                        WorkItems = new List<WorkItemProfile>
                        {
                            new WorkItemProfile
                            {
                                WorkItemID = 1,
                                ProfileID = "2",
                                AcceptanceCriteria = "acceptance criteria",
                                AreaPath = "area/path",
                                IterationPath = "iteration/path",
                                Description = "description",
                                Title = "title",
                                Type = WorkItemProfile.WorkItemType.Feature
                            }
                        }
                    }
                }
            };
            var jsonText = jsonService.Serialize(oldBlast);
            fileService.Setup(s => s.ReadRelativeFileText(It.IsAny<Uri>())).Returns(jsonText);
            var blast = jsonService.ReadJsonFile<Blast>(new Uri("http://testurl"));
            var finalText = jsonService.Serialize(blast);
            Assert.AreEqual(jsonText,finalText);
        }

        [TestMethod]
        public void JsonService_ReadJsonFile_ParsesFile()
        {
            jsonService.FileService = new FileService();
            //var baseUri = new Uri(System.Reflection.Assembly.GetEntryAssembly().Location);
            var blast = jsonService.ReadJsonFile<Blast>(new Uri("Data/feature.json", UriKind.Relative));
        }
    }
}
