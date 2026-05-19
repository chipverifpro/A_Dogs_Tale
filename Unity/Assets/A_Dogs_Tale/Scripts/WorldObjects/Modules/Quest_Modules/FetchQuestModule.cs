using System.Collections.Generic;
using InspectorTools;
using UnityEngine;
using DogGame.LLM;
using DogGame.Tasks;

namespace DogGame.Modules
{
    public enum FetchQuestState
    {
        Inactive = 0,
        CommandGiven,
        WaitingForObjectToSettle,
        SignalRetrieve,
        WaitingForPickup,
        SignalReturn,
        WaitingForReturn,
        SignalRelease,
        WaitingForRelease,
        Succeeded,
        Failed,
        Cancelled
    }

    public enum FetchQuestSignalMode
    {
        None = 0,
        BlackboardOnly,
        EnqueueSuggestedTasks
    }

    [DisallowMultipleComponent]
    [InspectorNote("Quest_Modules/Fetch Quest Module", "Monitors fetch training as a state machine. It records the sequence and can signal agents, but does not own moment-to-moment behavior.")]
    public class FetchQuestModule : QuestModuleBase
    {
        [Header("Participants")]
        [SerializeField] private WorldObject dog;
        [SerializeField] private WorldObject fetchObject;
        [SerializeField] private WorldObject requester;

        [Header("State Machine")]
        [SerializeField] private FetchQuestState state = FetchQuestState.Inactive;
        [SerializeField] private bool waitForThrownObjectToSettle = true;
        [SerializeField, Min(0f)] private float pickupTimeoutSeconds = 30f;
        [SerializeField, Min(0f)] private float returnTimeoutSeconds = 30f;
        [SerializeField, Min(0f)] private float releaseTimeoutSeconds = 12f;
        [SerializeField, Min(0f)] private float completeDistance = 1.5f;
        [SerializeField, Min(0f)] private float returnDistance = 1.5f;
        [SerializeField] private bool failOnWrongItemPickup = true;

        [Header("Signals")]
        [SerializeField] private FetchQuestSignalMode signalMode = FetchQuestSignalMode.BlackboardOnly;
        [SerializeField, Range(0, 100)] private int suggestedTaskPriority = 70;
        [SerializeField] private bool debugLogStateChanges = true;

        [Header("Training")]
        [SerializeField, Range(0f, 1f)] private float fetchTrainingLevel;
        [SerializeField, Min(0f)] private float successTrainingGain = 0.08f;
        [SerializeField, Min(0f)] private float failureTrainingLoss = 0.03f;
        [SerializeField] private int successfulFetchCount;
        [SerializeField] private int failedFetchCount;
        [SerializeField] private string lastOutcome = "";

        public FetchQuestState State => state;
        public WorldObject Dog => dog;
        public WorldObject FetchObject => fetchObject;
        public WorldObject Requester => requester != null ? requester : worldObject;
        public float FetchTrainingLevel => fetchTrainingLevel;
        public int SuccessfulFetchCount => successfulFetchCount;
        public int FailedFetchCount => failedFetchCount;
        public string LastOutcome => lastOutcome;
        public override string QuestTitle => "Fetch Quest";
        public override string QuestSummary => BuildQuestSummary();
        public override IReadOnlyList<QuestObjectiveSnapshot> ObjectiveSnapshots => BuildObjectiveSnapshots();
        public override bool HasCountdown => IsRunning && CurrentTimeoutSeconds > 0f;
        public override string CountdownLabel => CurrentCountdownLabel;
        public override float CountdownRemainingSeconds => Mathf.Max(0f, CurrentTimeoutSeconds - StateElapsedSeconds);
        public override float CountdownDurationSeconds => CurrentTimeoutSeconds;
        public override WorldObject QuestInteractionTarget => Requester;
        protected override WorldObject QuestIconTarget => Requester;
        protected override string QuestIconSpriteSheet => "TricksSpritesheet_B";
        protected override int QuestIconSpriteIndex => 0;

        private int debugDoubleTick = -1;

        public override void Tick(float deltaTime)
        {
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: FetchQuestModule.Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            base.Tick(deltaTime);
        }

        protected override void TickQuest(float deltaTime)
        {
            if (!ValidateActiveQuest())
                return;

            switch (state)
            {
                case FetchQuestState.CommandGiven:
                    AdvanceAfterCommand();
                    break;
                case FetchQuestState.WaitingForObjectToSettle:
                    TickWaitingForObjectToSettle();
                    break;
                case FetchQuestState.SignalRetrieve:
                    SignalRetrieve();
                    break;
                case FetchQuestState.WaitingForPickup:
                    TickWaitingForPickup();
                    break;
                case FetchQuestState.SignalReturn:
                    SignalReturn();
                    break;
                case FetchQuestState.WaitingForReturn:
                    TickWaitingForReturn();
                    break;
                case FetchQuestState.SignalRelease:
                    SignalRelease();
                    break;
                case FetchQuestState.WaitingForRelease:
                    TickWaitingForRelease();
                    break;
            }
        }

        public void ObserveFetchCommand(WorldObject commandedDog, WorldObject targetObject, WorldObject commandRequester)
        {
            BeginFetchTraining(commandedDog, targetObject, commandRequester);
        }

        public void BeginFetchTraining(WorldObject commandedDog, WorldObject targetObject, WorldObject commandRequester)
        {
            dog = commandedDog;
            fetchObject = targetObject;
            requester = commandRequester != null ? commandRequester : worldObject;

            if (!ValidateParticipants(out string reason))
            {
                SetFailed(reason);
                return;
            }

            StartQuest(FetchQuestState.CommandGiven.ToString(), "Fetch command observed.");
            SetState(FetchQuestState.CommandGiven, "Fetch command observed.");
            WriteFetchBlackboard("commanded");
        }

        public void ObserveObjectThrown(WorldObject targetObject)
        {
            if (targetObject != null)
                fetchObject = targetObject;

            if (IsRunning && state == FetchQuestState.CommandGiven)
                SetState(FetchQuestState.WaitingForObjectToSettle, "Fetch object was thrown.");
        }

        public void ObserveDogPickedUp(WorldObject observedDog, WorldObject item)
        {
            if (!IsRunning || observedDog != dog)
                return;

            if (item == fetchObject)
                SetState(FetchQuestState.SignalReturn, "Dog picked up fetch object.");
            else if (failOnWrongItemPickup)
                SetFailed("Dog picked up the wrong item.");
        }

        public void ObserveDogReleased(WorldObject observedDog, WorldObject item)
        {
            if (!IsRunning || observedDog != dog || item != fetchObject)
                return;

            if (IsTargetNearRequester())
                SetSucceeded("Fetch object released near requester.");
            else
                SetFailed("Fetch object was released before returning.");
        }

        public override void CancelQuest(string reason = "cancelled")
        {
            base.CancelQuest(reason);
            state = FetchQuestState.Cancelled;
            ClearFetchBlackboard();
        }

        [ContextMenu("Begin Fetch Training With Assigned Objects")]
        private void BeginFetchTrainingWithAssignedObjects()
        {
            BeginFetchTrainingWithAssignedObjects(dog);
        }

        public bool BeginFetchTrainingWithAssignedObjects(WorldObject fallbackDog)
        {
            WorldObject commandedDog = dog != null ? dog : fallbackDog;
            BeginFetchTraining(commandedDog, fetchObject, requester != null ? requester : worldObject);
            return IsRunning;
        }

        [ContextMenu("Cancel Fetch Training")]
        private void CancelFetchTraining()
        {
            CancelQuest("cancelled from inspector");
        }

        private void AdvanceAfterCommand()
        {
            if (waitForThrownObjectToSettle && IsFetchObjectMoving())
            {
                SetState(FetchQuestState.WaitingForObjectToSettle, "Waiting for fetch object to settle.");
                return;
            }

            SetState(FetchQuestState.SignalRetrieve, "Ready for dog to retrieve.");
        }

        private void TickWaitingForObjectToSettle()
        {
            if (!IsFetchObjectMoving())
                SetState(FetchQuestState.SignalRetrieve, "Fetch object settled.");
        }

        private void SignalRetrieve()
        {
            EmitFetchSignal("retrieve");
            EnqueueSuggestedRetrieveIfEnabled();
            SetState(FetchQuestState.WaitingForPickup, "Waiting for dog to pick up fetch object.");
        }

        private void TickWaitingForPickup()
        {
            if (IsFetchObjectHeldByDog())
            {
                SetState(FetchQuestState.SignalReturn, "Dog picked up fetch object.");
                return;
            }

            if (failOnWrongItemPickup && IsDogHoldingWrongItem())
            {
                SetFailed("Dog picked up the wrong item.");
                return;
            }

            if (TimedOut(pickupTimeoutSeconds))
                SetFailed("Dog did not pick up the fetch object.");
        }

        private void SignalReturn()
        {
            EmitFetchSignal("return");
            EnqueueSuggestedReturnIfEnabled();
            SetState(FetchQuestState.WaitingForReturn, "Waiting for dog to return to requester.");
        }

        private void TickWaitingForReturn()
        {
            if (!IsFetchObjectHeldByDog())
            {
                SetFailed("Dog dropped the fetch object before returning.");
                return;
            }

            if (IsDogNearRequester())
            {
                SetState(FetchQuestState.SignalRelease, "Dog returned with fetch object.");
                return;
            }

            if (TimedOut(returnTimeoutSeconds))
                SetFailed("Dog did not return with the fetch object.");
        }

        private void SignalRelease()
        {
            EmitFetchSignal("release");
            EnqueueSuggestedReleaseIfEnabled();
            SetState(FetchQuestState.WaitingForRelease, "Waiting for dog to release fetch object.");
        }

        private void TickWaitingForRelease()
        {
            if (!IsFetchObjectHeldByDog() && IsTargetNearRequester())
            {
                SetSucceeded("Fetch completed.");
                return;
            }

            if (!IsFetchObjectHeldByDog() && !IsTargetNearRequester())
            {
                SetFailed("Fetch object was released away from requester.");
                return;
            }

            if (TimedOut(releaseTimeoutSeconds))
                SetFailed("Dog returned but did not release the fetch object.");
        }

        private bool ValidateActiveQuest()
        {
            if (ValidateParticipants(out string reason))
                return true;

            SetFailed(reason);
            return false;
        }

        private bool ValidateParticipants(out string reason)
        {
            if (dog == null)
            {
                reason = "Fetch quest has no dog.";
                return false;
            }

            if (fetchObject == null)
            {
                reason = "Fetch quest has no fetch object.";
                return false;
            }

            if (Requester == null)
            {
                reason = "Fetch quest has no requester.";
                return false;
            }

            reason = "";
            return true;
        }

        private void EmitFetchSignal(string signal)
        {
            WriteFetchBlackboard(signal);
            EmitSignal(signal);
        }

        private void WriteFetchBlackboard(string signal)
        {
            if (signalMode == FetchQuestSignalMode.None)
                return;

            BlackboardModule blackboardModule = EnsureDogBlackboard();
            if (blackboardModule == null || blackboardModule.Blackboard == null)
                return;

            blackboardModule.Blackboard.SetBool("fetch.active", true);
            blackboardModule.Blackboard.SetString("fetch.signal", signal);
            blackboardModule.Blackboard.SetString("fetch.state", state.ToString());
            blackboardModule.Blackboard.SetInt("fetch.questObjectId", worldObject != null ? worldObject.ObjectId : -1);
            blackboardModule.Blackboard.SetInt("fetch.targetId", fetchObject != null ? fetchObject.ObjectId : -1);
            blackboardModule.Blackboard.SetInt("fetch.requesterId", Requester != null ? Requester.ObjectId : -1);
            blackboardModule.Blackboard.SetInt("item.targetId", fetchObject != null ? fetchObject.ObjectId : -1);
        }

        private void ClearFetchBlackboard()
        {
            BlackboardModule blackboardModule = EnsureDogBlackboard();
            if (blackboardModule == null || blackboardModule.Blackboard == null)
                return;

            blackboardModule.Blackboard.SetBool("fetch.active", false);
            blackboardModule.Blackboard.SetString("fetch.signal", "");
            blackboardModule.Blackboard.SetString("fetch.state", state.ToString());
        }

        private BlackboardModule EnsureDogBlackboard()
        {
            if (dog == null)
                return null;

            if (dog.blackboardModule == null)
                dog.CreateModulesIfNeeded(ModuleFlags.blackboardModule);

            dog.blackboardModule?.ForceInitialize();
            return dog.blackboardModule;
        }

        private void EnqueueSuggestedRetrieveIfEnabled()
        {
            if (signalMode != FetchQuestSignalMode.EnqueueSuggestedTasks || dog == null || dog.taskController == null)
                return;

            TaskRequest request = dog.taskController.SubmitSequence(
                suggestedTaskPriority,
                TaskSource.AI,
                new IAgentTask[]
                {
                    new Task_SetInt("item.targetId", fetchObject.ObjectId),
                    new Task_MoveToObject(fetchObject),
                    new Task_TakeItem()
                },
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: "fetch.retrieve");
            dog.taskController.Submit(request);
        }

        private void EnqueueSuggestedReturnIfEnabled()
        {
            if (signalMode != FetchQuestSignalMode.EnqueueSuggestedTasks || dog == null || dog.taskController == null)
                return;

            TaskRequest request = dog.taskController.SubmitSequence(
                suggestedTaskPriority,
                TaskSource.AI,
                new IAgentTask[]
                {
                    new Task_MoveToObject(Requester)
                },
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: "fetch.return");
            dog.taskController.Submit(request);
        }

        private void EnqueueSuggestedReleaseIfEnabled()
        {
            if (signalMode != FetchQuestSignalMode.EnqueueSuggestedTasks || dog == null || dog.taskController == null)
                return;

            TaskRequest request = dog.taskController.SubmitSequence(
                suggestedTaskPriority,
                TaskSource.AI,
                new IAgentTask[]
                {
                    new Task_DropItem()
                },
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: "fetch.release");
            dog.taskController.Submit(request);
        }

        private bool IsFetchObjectMoving()
        {
            if (fetchObject == null)
                return false;

            KineticModule kinetic = fetchObject.kineticModule != null
                ? fetchObject.kineticModule
                : fetchObject.GetComponent<KineticModule>();
            if (kinetic != null)
                return kinetic.IsMoving;

            Rigidbody rigidbody = fetchObject.GetComponent<Rigidbody>();
            if (rigidbody == null)
                return false;

            return rigidbody.linearVelocity.sqrMagnitude > 0.01f || rigidbody.angularVelocity.sqrMagnitude > 0.01f;
        }

        private bool IsFetchObjectHeldByDog()
        {
            if (dog == null || fetchObject == null)
                return false;

            if (dog.containerModule != null && dog.containerModule.ContainsItem(fetchObject))
                return true;

            if (dog.blackboardModule != null &&
                dog.blackboardModule.Blackboard.TryGetInt("item.carriedId", out int carriedId) &&
                carriedId == fetchObject.ObjectId)
            {
                return true;
            }

            Transform itemParent = fetchObject.transform.parent;
            return itemParent != null && (itemParent == dog.transform || itemParent.IsChildOf(dog.transform));
        }

        private bool IsDogHoldingWrongItem()
        {
            if (dog == null || fetchObject == null)
                return false;

            if (dog.blackboardModule != null &&
                dog.blackboardModule.Blackboard.TryGetInt("item.carriedId", out int carriedId) &&
                carriedId > 0 &&
                carriedId != fetchObject.ObjectId)
            {
                return true;
            }

            if (dog.containerModule == null)
                return false;

            foreach (WorldObject held in dog.containerModule.HeldItems)
            {
                if (held != null && held != fetchObject)
                    return true;
            }

            return false;
        }

        private bool IsDogNearRequester()
        {
            return HorizontalDistance(dog, Requester) <= returnDistance;
        }

        private bool IsTargetNearRequester()
        {
            return HorizontalDistance(fetchObject, Requester) <= completeDistance;
        }

        private static float HorizontalDistance(WorldObject a, WorldObject b)
        {
            if (a == null || b == null)
                return float.PositiveInfinity;

            Vector3 delta = a.transform.position - b.transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private QuestObjectiveSnapshot[] BuildObjectiveSnapshots()
        {
            int completedCount = CompletedObjectiveCount;

            return new[]
            {
                new QuestObjectiveSnapshot("Give the fetch command", completedCount >= 1, completedCount == 0),
                new QuestObjectiveSnapshot("Throw the fetch object", completedCount >= 2, completedCount == 1),
                new QuestObjectiveSnapshot("Dog picks up the fetch object", completedCount >= 3, completedCount == 2),
                new QuestObjectiveSnapshot("Dog returns to the requester", completedCount >= 4, completedCount == 3),
                new QuestObjectiveSnapshot("Dog releases the fetch object", completedCount >= 5, completedCount == 4)
            };
        }

        private int CompletedObjectiveCount
        {
            get
            {
                return state switch
                {
                    FetchQuestState.CommandGiven => 1,
                    FetchQuestState.WaitingForObjectToSettle => 2,
                    FetchQuestState.SignalRetrieve => 2,
                    FetchQuestState.WaitingForPickup => 2,
                    FetchQuestState.SignalReturn => 3,
                    FetchQuestState.WaitingForReturn => 3,
                    FetchQuestState.SignalRelease => 4,
                    FetchQuestState.WaitingForRelease => 4,
                    FetchQuestState.Succeeded => 5,
                    _ => 0
                };
            }
        }

        private float CurrentTimeoutSeconds
        {
            get
            {
                return state switch
                {
                    FetchQuestState.WaitingForPickup => pickupTimeoutSeconds,
                    FetchQuestState.WaitingForReturn => returnTimeoutSeconds,
                    FetchQuestState.WaitingForRelease => releaseTimeoutSeconds,
                    _ => 0f
                };
            }
        }

        private string CurrentCountdownLabel
        {
            get
            {
                return state switch
                {
                    FetchQuestState.WaitingForPickup => "Pickup",
                    FetchQuestState.WaitingForReturn => "Return",
                    FetchQuestState.WaitingForRelease => "Release",
                    _ => ""
                };
            }
        }

        private string BuildQuestSummary()
        {
            string dogName = dog != null ? dog.DisplayName : "Dog";
            string objectName = fetchObject != null ? fetchObject.DisplayName : "object";
            string requesterName = Requester != null ? Requester.DisplayName : "requester";

            return $"{dogName} fetches {objectName} for {requesterName}.";
        }

        private void SetState(FetchQuestState nextState, string message)
        {
            state = nextState;
            ChangeState(nextState.ToString(), message);
            WriteFetchBlackboard("state_changed");

            if (debugLogStateChanges)
                Debug.Log($"[FetchQuest] {worldObject.DisplayName}: {nextState} - {message}", this);
        }

        private void SetSucceeded(string message)
        {
            state = FetchQuestState.Succeeded;
            successfulFetchCount++;
            fetchTrainingLevel = Mathf.Clamp01(fetchTrainingLevel + successTrainingGain);
            lastOutcome = message;
            ClearFetchBlackboard();
            CompleteQuest(message);
        }

        private void SetFailed(string reason)
        {
            state = FetchQuestState.Failed;
            failedFetchCount++;
            fetchTrainingLevel = Mathf.Clamp01(fetchTrainingLevel - failureTrainingLoss);
            lastOutcome = reason;
            ClearFetchBlackboard();
            FailQuest(reason);
        }
    }
}
