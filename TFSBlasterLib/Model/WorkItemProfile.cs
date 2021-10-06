using System.Collections.Generic;

namespace TFSBlasterLib.Model
{
    public class WorkItemProfile
    {
        public string ProfileID;
        public long? WorkItemID;
        public WorkItemType? Type;
        public string Title;
        public string AreaPath;
        public string IterationPath;
        public string Assignee;
        public WorkItemState? State;
        public string Description;
        public string AcceptanceCriteria;
        public string RiskAnalysis;
        public string FirstComment;
        public WorkItemActivity? Activity;
        public float? StoryPoints;
        public float? HoursRemaining;
        public float? HoursCompleted;
        public List<string> Tags;
        public List<WorkItemLink> Links;
        public Dictionary<string, string> TextReplacements;

        public enum WorkItemType
        {
            None,
            Epic,
            Feature,
            UserStory,
            Task,
            Bug
        }

        public enum WorkItemState
        {
            None,
            New,
            Blocked
        }

        public enum WorkItemActivity
        {
            None,
            Development,
            Testing,
            Documentation,
            Review,
            Infrastructure
        }
    }
}
