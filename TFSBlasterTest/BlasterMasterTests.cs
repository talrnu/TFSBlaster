using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TFSBlasterLib;

namespace TFSBlasterTest
{
    [TestClass]
    public class BlasterMasterTests
    {
        private BlasterMaster blasterMaster;

        [TestMethod]
        public void TestMethod1()
        {
            blasterMaster = new BlasterMaster();
            blasterMaster.SendBlastFromJSON(
                new Uri("Data\\feature.json", UriKind.Relative)
                , new Uri("Data\\credentials.json", UriKind.Relative)
                , true
                //, 30000
                );
        }
    }
}
