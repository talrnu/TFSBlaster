using System;
using System.Collections.Generic;
using System.Linq;
using TFSBlasterLib.Model;
using TFSBlasterLib.Model.Connection;
using TFSBlasterLib.Service.Helpers;

namespace TFSBlasterLib.Service
{
    internal class WorkItemRequestService
    {
        public ConnectionService ConnectionService;
        public LogService LogService;

        private Dictionary<WorkItemLink.LinkType, string> linkRelations = new Dictionary<WorkItemLink.LinkType, string>
        {
            {WorkItemLink.LinkType.Child, "System.LinkTypes.Hierarchy-Forward"},
            {WorkItemLink.LinkType.Parent, "System.LinkTypes.Hierarchy-Reverse"},
            {WorkItemLink.LinkType.Predecessor, "System.LinkTypes.Dependency-Reverse"},
            {WorkItemLink.LinkType.Successor, "System.LinkTypes.Dependency-Forward"},
            {WorkItemLink.LinkType.Related, "System.LinkTypes.Related"},
            {WorkItemLink.LinkType.TestedBy, "Microsoft.VSTS.Common.TestedBy-Forward"},
            {WorkItemLink.LinkType.Tests, "Microsoft.VSTS.Common.TestedBy-Reverse"},
        };

        private Dictionary<WorkItemProfile.WorkItemType, string> webSafeWorkItemTypes = new Dictionary<WorkItemProfile.WorkItemType, string>
        {
            { WorkItemProfile.WorkItemType.Epic, "Epic" },
            { WorkItemProfile.WorkItemType.Feature, "Feature" },
            { WorkItemProfile.WorkItemType.UserStory, "User%20Story" },
            { WorkItemProfile.WorkItemType.Task, "Task" },
            { WorkItemProfile.WorkItemType.Bug, "Bug" }
        };

        public virtual void QueueCreation(WorkItemProfile workItem, Connection connection)
        {
            ValidateForCreate(workItem);

            var context = CreateRequestContext(workItem, connection, $"_apis/wit/workitems/${webSafeWorkItemTypes[workItem.Type.Value]}");
            ConnectionService.QueuePost(connection, context);
        }

        public void QueueUpdate(WorkItemProfile workItem, Connection connection)
        {
            ValidateForUpdate(workItem);

            var context = CreateRequestContext(workItem, connection, $"_apis/wit/workitems/{workItem.WorkItemID}");
            ConnectionService.QueuePatch(connection, context);
        }

        private void ValidateForCreate(WorkItemProfile workItem)
        {
            Log($"Validating profile {workItem.ProfileID} for creation");
            InvalidIf(!workItem.Type.HasValue, "Type is required", workItem.ProfileID);
            InvalidIf(string.IsNullOrEmpty(workItem.Title), "Title is required", workItem.ProfileID);
            InvalidIf(string.IsNullOrEmpty(workItem.AreaPath), "AreaPath is required", workItem.ProfileID);
            InvalidIf(string.IsNullOrEmpty(workItem.IterationPath), "IterationPath is required", workItem.ProfileID);

            Validate(workItem);
            Log($"Profile {workItem.ProfileID} is valid for creation");
        }

        private void ValidateForUpdate(WorkItemProfile workItem)
        {
            Log($"Validating profile {workItem.ProfileID} for update");
            InvalidIf(!workItem.WorkItemID.HasValue, "WorkItemID is required", workItem.ProfileID);

            Validate(workItem);
            Log($"Profile {workItem.ProfileID} is valid for update");
        }

        private void Validate(WorkItemProfile workItem)
        {
            InvalidIf(workItem.Tags != null && workItem.Tags.Any(tag => tag.Contains(";")), "Tags cannot contain semicolons", workItem.ProfileID);
            var offendingLinks = workItem.Links
                ?.Where(link => link.LinkedItemID == null && link.LinkedProfile?.WorkItemID == null).ToList();
            var offendingLinksDescriptions = string.Join(", ", offendingLinks?.Select(L => $"{L.Type} {L.LinkedItemID}/{L.LinkedProfileID}") ?? new List<string>());
            InvalidIf(offendingLinks != null && offendingLinks.Count > 0, $"Links must specify a LinkedItemID, or else a LinkedProfileID for a profile with a WorkItemID." +
                                                                                   $"\n\tOffending links: {offendingLinksDescriptions}", workItem.ProfileID);
        }

        private void InvalidIf(bool isInvalid, string failureNote, string profileID)
        {
            if (isInvalid)
            {
                var message = $"Validation error: {failureNote}.";
                if (!string.IsNullOrEmpty(profileID)) 
                    message += $" Work item profile {profileID}";
                LogErr(message);
                throw new System.Exception(message);
            }
        }

        private TfsRequestContext CreateRequestContext(WorkItemProfile workItem, Connection connection, string endpointPath)
        {
            var body = new TfsRequestBody();
            AddRequiredFields(workItem, body);
            AddOptionalFields(workItem, body, connection.BaseUri);

            var endpoint = new Uri(endpointPath, UriKind.Relative);
            var context = new TfsRequestContext
            {
                Endpoint = endpoint,
                Body = body,
                WorkItem = workItem
            };
            return context;
        }

        private void AddRequiredFields(WorkItemProfile workItem, TfsRequestBody body)
        {
            TryAddString(workItem.Title, "/fields/System.Title", body);
            TryAddNullable(workItem.StoryPoints, "/fields/Microsoft.VSTS.Scheduling.StoryPoints", body);

            //Inherit area path from parent profile if not specified
            var areaPath = workItem.AreaPath;
            if (string.IsNullOrEmpty(areaPath))
            {
                areaPath = workItem.Links
                    ?.FirstOrDefault(link => link.Type == WorkItemLink.LinkType.Parent)
                    ?.LinkedProfile
                    ?.AreaPath;
            }
            TryAddString(areaPath, "/fields/System.AreaPath", body);

            //Inherit iteration path from parent profile if not specified
            var iterationPath = workItem.IterationPath;
            if (string.IsNullOrEmpty(iterationPath))
            {
                iterationPath = workItem.Links
                    ?.FirstOrDefault(link => link.Type == WorkItemLink.LinkType.Parent)
                    ?.LinkedProfile
                    ?.IterationPath;
            }
            TryAddString(iterationPath, "/fields/System.IterationPath", body);

            if (workItem.Type == WorkItemProfile.WorkItemType.UserStory)
            {
                TryAddString(workItem.AcceptanceCriteria, "/fields/MMSI.UserStory.AcceptanceCriteria", body);
                TryAddString(workItem.RiskAnalysis, "/fields/MMSI.UserStory.RiskAnalysis", body);
            }
        }

        private void AddOptionalFields(WorkItemProfile workItem, TfsRequestBody body, Uri baseUri)
        {
            TryAddString(workItem.Description, "/fields/System.Description", body);
            TryAddString(workItem.FirstComment, "/fields/System.History", body);
            TryAddString(workItem.State?.ToString(), "/fields/System.State", body);
            TryAddString(workItem.Activity?.ToString(), "/fields/Microsoft.VSTS.Common.Activity", body);
            TryAddNullable(workItem.Assignee, "/fields/System.AssignedTo", body);
            TryAddNullable(workItem.HoursRemaining, "/fields/Microsoft.VSTS.Scheduling.RemainingWork", body);
            TryAddNullable(workItem.HoursCompleted, "/fields/Microsoft.VSTS.Scheduling.CompletedWork", body);

            if (workItem.Type != WorkItemProfile.WorkItemType.Task && workItem.Tags != null && workItem.Tags.Count > 0)
            {
                var tags = string.Join("; ", workItem.Tags);
                TryAddString(tags, "/fields/System.Tags", body);
            }

            if (workItem.Links != null)
            {
                foreach (var link in workItem.Links)
                {
                    TryAddNullable(new
                    {
                        rel = linkRelations[link.Type],
                        url = $"{baseUri}_apis/wit/workitems/{link.LinkedItemID ?? link.LinkedProfile.WorkItemID}"
                    }, "/relations/-", body);
                }
            }
        }

        private bool TryAddString(string value, string tfsPath, TfsRequestBody body)
        {
            if (string.IsNullOrEmpty(value)) return false;
            Add(value, tfsPath, body);
            return true;
        }

        private bool TryAddNullable(object value, string tfsPath, TfsRequestBody body)
        {
            if (value == null) return false;
            Add(value, tfsPath, body);
            return true;
        }

        private void Add(object value, string tfsPath, TfsRequestBody body)
        {
            body.Items.Add(
                new TfsRequestItem
                {
                    op = "add",
                    path = tfsPath,
                    value = value
                }
            );
        }

        private void Log(string message)
        {
            LogService.LogToFile(nameof(WorkItemRequestService), message);
        }

        private void LogErr(string message)
        {
            LogService.LogDebug(nameof(WorkItemRequestService), message);
        }
    }
}