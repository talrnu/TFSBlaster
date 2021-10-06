using TFSBlasterLib.Service;

namespace TFSBlasterLib
{
    internal class DependencyManager
    {
        public readonly BlastService BlastService;
        public readonly ConnectionService ConnectionService;
        public readonly FileService FileService;
        public readonly JsonService JsonService;
        public readonly LogService LogService;
        public readonly WorkItemRequestService WorkItemRequestService;

        public DependencyManager()
        {
            //Instantiate
            BlastService = new BlastService();
            ConnectionService = new ConnectionService();
            FileService = new FileService();
            JsonService = new JsonService();
            LogService = new LogService();
            WorkItemRequestService = new WorkItemRequestService();

            //Inject
            BlastService.ConnectionService = ConnectionService;
            BlastService.LogService = LogService;
            BlastService.WorkItemRequestService = WorkItemRequestService;

            ConnectionService.LogService = LogService;
            ConnectionService.JsonService = JsonService;

            JsonService.FileService = FileService;
            JsonService.LogService = LogService;

            WorkItemRequestService.ConnectionService = ConnectionService;
            WorkItemRequestService.LogService = LogService;
        }
    }
}
