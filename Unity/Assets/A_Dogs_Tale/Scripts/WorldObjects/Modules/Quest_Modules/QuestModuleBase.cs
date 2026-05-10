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

    public abstract class QuestModuleBase : WorldModule
    {
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
            onQuestCompleted.Invoke();
            onStateChanged.Invoke(currentStateName);
        }

        protected void FailQuest(string reason)
        {
            status = QuestRunStatus.Failed;
            lastMessage = reason;
            currentStateName = "Failed";
            stateElapsedSeconds = 0f;
            onQuestFailed.Invoke(reason);
            onStateChanged.Invoke(currentStateName);
        }

        public virtual void CancelQuest(string reason = "cancelled")
        {
            status = QuestRunStatus.Cancelled;
            lastMessage = reason;
            currentStateName = "Cancelled";
            stateElapsedSeconds = 0f;
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
    }
}
