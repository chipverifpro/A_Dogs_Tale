using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DogGame.Modules
{
    public enum QuestRunStatus
    {
        Inactive = 0,
        Running,
        Succeeded,
        Failed,
        Cancelled
    }

    public readonly struct QuestObjectiveSnapshot
    {
        public QuestObjectiveSnapshot(string description, bool isCompleted, bool isCurrent = false)
        {
            Description = description;
            IsCompleted = isCompleted;
            IsCurrent = isCurrent;
        }

        public string Description { get; }
        public bool IsCompleted { get; }
        public bool IsCurrent { get; }
    }

    public abstract class QuestModuleBase : WorldModule
    {
        private static readonly QuestObjectiveSnapshot[] NoObjectives = Array.Empty<QuestObjectiveSnapshot>();
        private static readonly List<QuestModuleBase> KnownQuestModulesMutable = new();

        public static IReadOnlyList<QuestModuleBase> KnownQuestModules => KnownQuestModulesMutable;

        [Header("Quest State")]
        [SerializeField] private QuestRunStatus status = QuestRunStatus.Inactive;
        [SerializeField] private string currentStateName = "Inactive";
        [SerializeField] private float elapsedSeconds;
        [SerializeField] private float stateElapsedSeconds;
        [SerializeField] private string lastSignal = "";
        [SerializeField] private string lastMessage = "";

        [Header("Quest Events")]
        public UnityEvent onQuestStarted = new();
        public UnityEvent onQuestCompleted = new();
        public UnityEvent<string> onQuestFailed = new();
        public UnityEvent<string> onQuestCancelled = new();
        public UnityEvent<string> onStateChanged = new();
        public UnityEvent<string> onSignal = new();

        public QuestRunStatus Status => status;
        public bool IsRunning => status == QuestRunStatus.Running;
        public string CurrentStateName => currentStateName;
        public float ElapsedSeconds => elapsedSeconds;
        public float StateElapsedSeconds => stateElapsedSeconds;
        public string LastSignal => lastSignal;
        public string LastMessage => lastMessage;
        public virtual string QuestTitle => ObjectDisplayName;
        public virtual string QuestSummary => lastMessage;
        public virtual IReadOnlyList<QuestObjectiveSnapshot> ObjectiveSnapshots => NoObjectives;
        public virtual bool HasCountdown => false;
        public virtual string CountdownLabel => "";
        public virtual float CountdownRemainingSeconds => 0f;
        public virtual float CountdownDurationSeconds => 0f;

        protected string ObjectDisplayName => worldObject != null ? worldObject.DisplayName : name;

        internal static void ResetStaticStateForReload()
        {
            KnownQuestModulesMutable.Clear();
        }

        protected override void Awake()
        {
            base.Awake();
            RegisterKnownQuestModule(this);
        }

        protected virtual void OnEnable()
        {
            RegisterKnownQuestModule(this);

            if (IsRunning)
                QuestManager.RegisterActiveQuest(this);
        }

        protected virtual void OnDisable()
        {
            QuestManager.UnregisterQuest(this);
        }

        protected virtual void OnDestroy()
        {
            KnownQuestModulesMutable.Remove(this);
            QuestManager.UnregisterQuest(this);
        }

        public override void Tick(float deltaTime)
        {
            if (status != QuestRunStatus.Running || deltaTime <= 0f)
                return;

            elapsedSeconds += deltaTime;
            stateElapsedSeconds += deltaTime;
            TickQuest(deltaTime);
        }

        protected virtual void TickQuest(float deltaTime) { }

        protected void StartQuest(string initialStateName, string message = "")
        {
            status = QuestRunStatus.Running;
            elapsedSeconds = 0f;
            stateElapsedSeconds = 0f;
            currentStateName = string.IsNullOrWhiteSpace(initialStateName) ? "Running" : initialStateName;
            lastMessage = message;
            RegisterKnownQuestModule(this);
            QuestManager.RegisterActiveQuest(this);
            onQuestStarted.Invoke();
            onStateChanged.Invoke(currentStateName);
        }

        protected void ChangeState(string stateName, string message = "")
        {
            if (string.IsNullOrWhiteSpace(stateName))
                stateName = "Running";

            if (currentStateName == stateName)
                return;

            currentStateName = stateName;
            stateElapsedSeconds = 0f;
            lastMessage = message;
            onStateChanged.Invoke(currentStateName);
        }

        protected void CompleteQuest(string message = "")
        {
            status = QuestRunStatus.Succeeded;
            lastMessage = message;
            currentStateName = "Succeeded";
            stateElapsedSeconds = 0f;
            QuestManager.UnregisterQuest(this);
            onQuestCompleted.Invoke();
            onStateChanged.Invoke(currentStateName);
        }

        protected void FailQuest(string reason)
        {
            status = QuestRunStatus.Failed;
            lastMessage = reason;
            currentStateName = "Failed";
            stateElapsedSeconds = 0f;
            QuestManager.UnregisterQuest(this);
            onQuestFailed.Invoke(reason);
            onStateChanged.Invoke(currentStateName);
        }

        public virtual void CancelQuest(string reason = "cancelled")
        {
            status = QuestRunStatus.Cancelled;
            lastMessage = reason;
            currentStateName = "Cancelled";
            stateElapsedSeconds = 0f;
            QuestManager.UnregisterQuest(this);
            onQuestCancelled.Invoke(reason);
            onStateChanged.Invoke(currentStateName);
        }

        protected void ResetQuestState()
        {
            status = QuestRunStatus.Inactive;
            currentStateName = "Inactive";
            elapsedSeconds = 0f;
            stateElapsedSeconds = 0f;
            lastSignal = "";
            lastMessage = "";
            QuestManager.UnregisterQuest(this);
        }

        protected void EmitSignal(string signalName)
        {
            lastSignal = signalName;
            onSignal.Invoke(signalName);
        }

        protected bool TimedOut(float timeoutSeconds)
        {
            return timeoutSeconds > 0f && stateElapsedSeconds >= timeoutSeconds;
        }

        private static void RegisterKnownQuestModule(QuestModuleBase questModule)
        {
            if (questModule == null || KnownQuestModulesMutable.Contains(questModule))
                return;

            KnownQuestModulesMutable.Add(questModule);
        }
    }
}
