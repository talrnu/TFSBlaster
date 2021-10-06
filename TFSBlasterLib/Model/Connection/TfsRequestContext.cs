using System;
using System.Text;

namespace TFSBlasterLib.Model.Connection
{
    internal class TfsRequestContext
    {
        public WorkItemProfile WorkItem;
        public Uri Endpoint;
        public TfsRequestBody Body;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Request context for profile {WorkItem.ProfileID}:");
            sb.AppendLine($"\tEndpoint: {Endpoint}");
            sb.AppendLine($"\tBody: {Body.ToString().Replace("\n","\n\t\t")}");
            return sb.ToString();
        }
    }
}
