using System.Collections.Generic;

namespace TFSBlasterLib.Model
{
    public class TfsAction
    {
        public string Name;
        public ActionMode Mode;
        public WorkItemProfile WorkItemDefaults;
        public List<WorkItemProfile> WorkItems;

        public enum ActionMode
        {
            NoAction,
            Create,
            Update
        }
    }
}
