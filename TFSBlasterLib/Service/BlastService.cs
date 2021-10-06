using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TFSBlasterLib.Exception;
using TFSBlasterLib.Model;
using TFSBlasterLib.Service.Helpers;

namespace TFSBlasterLib.Service
{
    internal class BlastService
    {
        public ConnectionService ConnectionService;
        public LogService LogService;
        public WorkItemRequestService WorkItemRequestService;
        public JsonService JsonService;

        private Dictionary<TfsAction.ActionMode, Action<WorkItemProfile, Connection>> workItemActions;

        public void ProcessProfiles(Blast blast)
        {
            var stateTransitionUpdate = new TfsAction
            {
                Name = "STATE TRANSITION UPDATES",
                Mode = TfsAction.ActionMode.Update,
                WorkItems = new List<WorkItemProfile>()
            };

            foreach (var action in blast.Actions)
            { 
                foreach (var profile in action.WorkItems)
                {
                    if (string.IsNullOrEmpty(profile.ProfileID))
                        profile.ProfileID = Guid.NewGuid().ToString();

                    ApplyDefaults(action, profile, blast);

                    LinkProfiles(profile, action, blast);
                    
                    if (action.Mode == TfsAction.ActionMode.Create && profile.State.HasValue)
                    {
                        DeferStateTransition(profile, stateTransitionUpdate);
                    }

                    ApplyTextReplacements(profile);
                }
            }

            if (stateTransitionUpdate.WorkItems.Count > 0)
            {
                Log("Adding a new final update batch to apply non-default state transitions.");
                blast.Actions.Add(stateTransitionUpdate);
            }
        }

        public async Task Send(Blast blast, bool terminateOnFailure)
        {
            //Building the dictionary here so it's guaranteed to use injected services
            InitWorkItemActions();

            var connection = ConnectionService.StartConnectionTo(blast.TfsUri, blast.Credentials, blast.RequestTimeout);

            Log($"Connection ready, processing batches");
            foreach (var workItemAction in blast.Actions)
            {
                if (!workItemAction.WorkItems.Any())
                {
                    //For convenience, all work items in an action might be commented out in the json, so just skip that action
                    continue;
                }

                var actionSuccess = true;
                var actionToInvoke = workItemActions[workItemAction.Mode];

                Debug($"Begin sending {workItemAction.Mode} batch \"{workItemAction.Name}\"...");

                foreach (var workItem in workItemAction.WorkItems)
                {
                    try
                    {
                        actionToInvoke.Invoke(workItem, connection);
                    }
                    catch (System.Exception ex)
                    {
                        Debug($"Failed to {workItemAction.Mode} {workItem.Type} {workItem.WorkItemID}: {ex}");
                        actionSuccess = false;
                        if (terminateOnFailure)
                        {
                            Debug("Skipping remaining work items in this batch.");
                            break;
                        }
                    }
                    Log($"{workItemAction.Mode} {workItem.Type} {workItem.WorkItemID}: request is ready.");
                }

                await ConnectionService.ProcessQueue(connection);

                if (workItemAction.Mode == TfsAction.ActionMode.Create)
                {
                    actionSuccess &= TryUpdateWorkItemIds(workItemAction, blast, terminateOnFailure);
                }

                Debug($"...End {workItemAction.Mode} batch \"{workItemAction.Name}\".");

                if (!actionSuccess && terminateOnFailure)
                {
                    Debug($"Terminating remaining actions.");
                    break;
                }
            }
        }

        private void ApplyDefaults(TfsAction action, WorkItemProfile profile, Blast blast)
        {
            var blastDefaultProfile = blast.WorkItemDefaults;
            //The blast defaults may specify a profile ID or work item ID because profiles in different actions may share these.

            var actionDefaultProfile = action.WorkItemDefaults;

            if (actionDefaultProfile == null && blastDefaultProfile == null) return;
            
            if (actionDefaultProfile != null)
            {
                if (!string.IsNullOrEmpty(actionDefaultProfile.ProfileID)) throw new System.Exception($"Action {action.Name} specifies a default profile ID, not allowed: this would make multiple profiles in the same action have the same profile ID");
                if (actionDefaultProfile.WorkItemID != null) throw new System.Exception($"Action {action.Name} specifies a default work item ID, not allowed: this would make multiple profiles in the same action have the same work item ID");
            }

            var fieldInfo = typeof(WorkItemProfile).GetFields();
            foreach (var field in fieldInfo)
            {
                object profileValue = field.GetValue(profile);
                if (profileValue != null) continue;

                object defaultValue = null;
                if (actionDefaultProfile != null) defaultValue = field.GetValue(actionDefaultProfile);
                if (defaultValue == null && blastDefaultProfile != null) defaultValue = field.GetValue(blastDefaultProfile);
                if (defaultValue != profileValue)
                {
                    field.SetValue(profile, defaultValue);
                }
            }
        }

        private object ChooseDefault(object profileValue, object actionDefault, object blastDefault)
        {
            return profileValue ?? actionDefault ?? blastDefault;
        }

        private void LinkProfiles(WorkItemProfile profile, TfsAction action, Blast blast)
        {
            //Use profile IDs to add refs to actual profiles for linking later
            //Necessary to allow one blast to create multiple work items that link to each other
            if (profile.Links == null)
            {
                Log($"Profile {profile.ProfileID} has no links; OK");
                return;
            }

            foreach (var link in profile.Links)
            {
                //Only search for a profile if a work item ID isn't already indicated
                if (link.LinkedItemID.HasValue)
                {
                    Log($"Profile {profile.ProfileID} will be linked to work item {link.LinkedItemID}; OK");
                    continue;
                }

                AddLinkRef(profile, link, action, blast);
            }
        }

        private void DeferStateTransition(WorkItemProfile profile, TfsAction stateTransitionUpdate)
        {
            //Work items can't be created in a non-default state, so a separate update must be queued after creation to transition into the target state
            Log($"Profile {profile.ProfileID} will attempt to create an item in a non-default state ({profile.State}). Adding a separate action to apply the state transition as an update after creation.");
            stateTransitionUpdate.WorkItems.Add(new WorkItemProfile
            {
                Type = profile.Type,
                ProfileID = profile.ProfileID,
                State = profile.State
            });
            profile.State = null;
        }

        private void ApplyTextReplacements(WorkItemProfile profile)
        {
            if (profile.TextReplacements == null) return;

            foreach(var pattern in profile.TextReplacements.Keys)
            {
                var newText = profile.TextReplacements[pattern];
                if (!string.IsNullOrEmpty(profile.Title)) profile.Title = profile.Title.Replace(pattern, newText);
                if (!string.IsNullOrEmpty(profile.Description)) profile.Description = profile.Description.Replace(pattern, newText);
                if (!string.IsNullOrEmpty(profile.AcceptanceCriteria)) profile.AcceptanceCriteria = profile.AcceptanceCriteria.Replace(pattern, newText);
                if (!string.IsNullOrEmpty(profile.RiskAnalysis)) profile.RiskAnalysis = profile.RiskAnalysis.Replace(pattern, newText);
            }
        }

        private void InitWorkItemActions()
        {
            if (workItemActions == null) 
                workItemActions = new Dictionary<TfsAction.ActionMode, Action<WorkItemProfile, Connection>>
                {
                    {
                        TfsAction.ActionMode.NoAction,
                        (profile, connection) => Debug($"Profile {profile.ProfileID} is included for reference only, no action will be performed on it.")
                    },
                    {
                        TfsAction.ActionMode.Create,
                        WorkItemRequestService.QueueCreation
                    },
                    {
                        TfsAction.ActionMode.Update,
                        WorkItemRequestService.QueueUpdate
                    }
                };
            Log($"Blast service work item actions ready");
        }

        private bool TryUpdateWorkItemIds(TfsAction workItemAction, Blast blast, bool terminateOnFailure)
        {
            foreach (var workItem in workItemAction.WorkItems)
            {
                if (!workItem.WorkItemID.HasValue)
                {
                    Log($"Profile {workItem.ProfileID} never received a work item ID.");
                    if (terminateOnFailure)
                        return false;
                    else
                        continue;
                }

                //Copy newly assigned work item IDs to all other update profiles intended for the same work item
                foreach (var otherAction in blast.Actions)
                {
                    foreach (var otherItem in otherAction.WorkItems)
                    {
                        if (otherItem.ProfileID == workItem.ProfileID)
                        {
                            if (otherItem.WorkItemID.HasValue &&
                                otherItem.WorkItemID != workItem.WorkItemID)
                            {
                                Log($"Another profile has the same profile ID, {otherItem.ProfileID}, but a different work item ID, {otherItem.WorkItemID}, from the one just created: {workItem.WorkItemID}");
                                if (terminateOnFailure)
                                    return false;
                            }
                            otherItem.WorkItemID = workItem.WorkItemID;
                        }
                    }
                }
            }
            return true;
        }

        private void AddLinkRef(WorkItemProfile profile, WorkItemLink link, TfsAction action, Blast blast)
        {
            bool stillCheckingPrecedingActions = true;
            foreach (var otherAction in blast.Actions)
            {
                //Only profiles in preceding actions will be updated with a valid work item ID in time
                if (otherAction == action) stillCheckingPrecedingActions = false;

                foreach (var otherProfile in otherAction.WorkItems)
                {
                    if (otherProfile.ProfileID != link.LinkedProfileID) continue;

                    if (otherProfile.WorkItemID.HasValue)
                    {
                        Log($"Profile {profile.ProfileID} is linked to profile {link.LinkedProfileID} so its work item will be linked to work item {otherProfile.WorkItemID}; OK");
                    }
                    else if (stillCheckingPrecedingActions)
                    {
                        Log($"Profile {profile.ProfileID} will be linked to the work item that will be created for profile {link.LinkedProfileID}; OK");
                    }
                    else
                    {
                        var message =
                            $"Profile {profile.ProfileID} links to another profile ({otherProfile.ProfileID}) that will not have a valid work item ID in time to create the link";
                        Debug(message);
                        throw new ProfileLinkException(message);
                    }
                    link.LinkedProfile = otherProfile;
                    link.LinkedItemID = otherProfile.WorkItemID;
                    break;
                }

                if (link.LinkedProfile != null) break;
            }
        }

        private void Log(string message)
        {
            LogService.LogToFile(nameof(BlastService), message);
        }

        private void Debug(string message)
        {
            LogService.LogDebug(nameof(BlastService), message);
        }
    }
}
