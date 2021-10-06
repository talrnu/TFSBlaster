namespace TFSBlasterLib.Model
{
    public class WorkItemLink
    {
        public LinkType Type;
        public long? LinkedItemID;
        public string LinkedProfileID;
        internal WorkItemProfile LinkedProfile;

        public enum LinkType
        {
            Parent,
            Child,
            TestedBy,
            Tests,
            Predecessor,
            Successor,
            Related
        }
    }
}
