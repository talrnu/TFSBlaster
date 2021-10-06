using System.Net.Http;
using System.Threading.Tasks;
using TFSBlasterLib.Service.Helpers;

namespace TFSBlasterLib.Model.Connection
{
    internal class TfsHttpRequest
    {
        public HttpRequestMessage HttpRequest;
        public HttpResponseMessage HttpResponse;
        public TfsRequestContext Context;
        public Task RequestTask;
    }
}
