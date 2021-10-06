using System;
using TFSBlasterLib.Model;

namespace TFSBlasterLib
{
    public class BlasterMaster
    {
        private DependencyManager dependencies;

        public BlasterMaster() : this(new DependencyManager()) { }

        internal BlasterMaster(DependencyManager dependencies)
        {
            this.dependencies = dependencies;
        }

        public void SendBlastFromJSON(Uri blastLocalPath, Uri credentialsLocalPath, bool terminateOnFailure) //, int? timeoutMillis)
        {
            Log("Reading raw blast from .json file");
            var blast = dependencies.JsonService.ReadJsonFile<Blast>(blastLocalPath);
            
            Log("Processing raw profiles");
            dependencies.BlastService.ProcessProfiles(blast);
            
            Log("Reading credentials from .json file");
            blast.Credentials = dependencies.JsonService.ReadJsonFile<TfsCredentials>(credentialsLocalPath);
            
            Log("Sending blast");
            var sendTask = dependencies.BlastService.Send(blast, terminateOnFailure);
            sendTask.Wait();
        }

        public string GetCredentialsJsonSchema()
        {
            return dependencies.JsonService.GetSchema<TfsCredentials>();
        }

        public string GetBlastJsonSchema()
        {
            return dependencies.JsonService.GetSchema<Blast>();
        }

        private void Log(string message)
        {
            dependencies.LogService.LogToFile(nameof(BlasterMaster), message);
        }
    }
}
