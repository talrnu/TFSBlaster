using System;
using System.Collections.Generic;

namespace TFSBlasterLib.Model
{
    public class Blast
    {
        public Uri TfsUri;
        public int? RequestTimeout;
        public TfsCredentials Credentials;
        public WorkItemProfile WorkItemDefaults;
        public List<TfsAction> Actions;
    }
}
