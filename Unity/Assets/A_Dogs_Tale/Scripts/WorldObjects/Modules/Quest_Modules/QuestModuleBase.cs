using System;
using System.Collections.Generic;
using System.Text;
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
        private const string QuestBannerIconSpriteName = "AndroidButtonsAndQuests_1";
        private const string QuestOverheadIconInstanceName = "QuestRequestIconVisual";
        private const float QuestOverheadIconSize = 0.45f;
        private const float QuestOverheadIconTopPadding = 0.12f;

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
        public virtual WorldObject QuestInteractionTarget => worldObject;
        public virtual bool CanStartFromQuestDialog => !IsRunning;
        protected virtual WorldObject QuestIconTarget => worldObject;
        protected virtual string QuestIconSpriteSheet => "";
        protected virtual int QuestIconSpriteIndex => -1;

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
            {
                QuestManager.RegisterActiveQuest(this);
                ShowQuestRequestIcon();
            }
        }

        protected virtual void OnDisable()
        {
            HideQuestRequestIcon();
            QuestManager.UnregisterQuest(this);
        }

        protected virtual void OnDestroy()
        {
            HideQuestRequestIcon();
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
            ShowQuestRequestIcon();
            onQuestStarted.Invoke();
            onStateChanged.Invoke(currentStateName);
            LogQuestStateToBottomBanner(currentStateName, message);
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
            ShowQuestRequestIcon();
            onStateChanged.Invoke(currentStateName);
            LogQuestStateToBottomBanner(currentStateName, message);
        }

        protected void CompleteQuest(string message = "")
        {
            status = QuestRunStatus.Succeeded;
            lastMessage = message;
            currentStateName = "Succeeded";
            stateElapsedSeconds = 0f;
            HideQuestRequestIcon();
            QuestManager.UnregisterQuest(this);
            onQuestCompleted.Invoke();
            onStateChanged.Invoke(currentStateName);
            LogQuestStateToBottomBanner(currentStateName, message);
        }

        protected void FailQuest(string reason)
        {
            status = QuestRunStatus.Failed;
            lastMessage = reason;
            currentStateName = "Failed";
            stateElapsedSeconds = 0f;
            HideQuestRequestIcon();
            QuestManager.UnregisterQuest(this);
            onQuestFailed.Invoke(reason);
            onStateChanged.Invoke(currentStateName);
            LogQuestStateToBottomBanner(currentStateName, reason);
        }

        public virtual void CancelQuest(string reason = "cancelled")
        {
            status = QuestRunStatus.Cancelled;
            lastMessage = reason;
            currentStateName = "Cancelled";
            stateElapsedSeconds = 0f;
            HideQuestRequestIcon();
            QuestManager.UnregisterQuest(this);
            onQuestCancelled.Invoke(reason);
            onStateChanged.Invoke(currentStateName);
            LogQuestStateToBottomBanner(currentStateName, reason);
        }

        protected void ResetQuestState()
        {
            status = QuestRunStatus.Inactive;
            currentStateName = "Inactive";
            elapsedSeconds = 0f;
            stateElapsedSeconds = 0f;
            lastSignal = "";
            lastMessage = "";
            HideQuestRequestIcon();
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

        private void LogQuestStateToBottomBanner(string stateName, string message)
        {
            string title = string.IsNullOrWhiteSpace(QuestTitle) ? ObjectDisplayName : QuestTitle;
            string displayState = FormatQuestStateName(stateName);
            string bannerMessage = string.IsNullOrWhiteSpace(message)
                ? $"Quest: {title} - {displayState}"
                : $"Quest: {title} - {displayState}: {message}";

            BottomBanner.LogMessageWithIcon(
                BannerSense.None,
                BannerLevel.None,
                bannerMessage,
                QuestBannerIconSpriteName);
        }

        private void ShowQuestRequestIcon()
        {
            WorldObject target = QuestIconTarget;
            if (target == null || QuestIconSpriteIndex < 0 || string.IsNullOrWhiteSpace(QuestIconSpriteSheet))
                return;

            Sprite iconSprite = SpriteServer.SpriteSheetLookup(QuestIconSpriteSheet, QuestIconSpriteIndex);
            if (iconSprite == null)
                return;

            Vector3 localOffset = GetQuestIconLocalOffset(target);
            EmoteIconVisualFactory.Show(
                target.transform,
                iconSprite,
                localOffset: localOffset,
                size: QuestOverheadIconSize,
                lifetimeSeconds: 0f,
                instanceName: QuestOverheadIconInstanceName);
        }

        private void HideQuestRequestIcon()
        {
            WorldObject target = QuestIconTarget;
            if (target == null)
                return;

            EmoteIconVisualFactory.Hide(target.transform, QuestOverheadIconInstanceName);
        }

        private static Vector3 GetQuestIconLocalOffset(WorldObject target)
        {
            if (target == null)
                return new Vector3(0f, 1.35f, 0f);

            if (!TryGetRenderableBounds(target, out Bounds bounds))
                return new Vector3(0f, 1.35f, 0f);

            float iconCenterY = bounds.max.y + (QuestOverheadIconSize * 0.5f) + QuestOverheadIconTopPadding;
            Vector3 localOffset = target.transform.InverseTransformPoint(new Vector3(
                target.transform.position.x,
                iconCenterY,
                target.transform.position.z));

            localOffset.x = 0f;
            localOffset.z = 0f;
            return localOffset;
        }

        private static bool TryGetRenderableBounds(WorldObject target, out Bounds bounds)
        {
            bounds = default;
            if (target == null)
                return false;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(includeInactive: false);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.GetComponentInParent<EmoteIconSpinner>() != null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static string FormatQuestStateName(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                return "Updated";

            StringBuilder builder = new();
            char previous = '\0';

            foreach (char current in stateName.Trim())
            {
                if (builder.Length > 0 &&
                    current != ' ' &&
                    char.IsUpper(current) &&
                    (char.IsLower(previous) || char.IsDigit(previous)))
                {
                    builder.Append(' ');
                }

                builder.Append(current == '_' ? ' ' : current);
                previous = current;
            }

            return builder.ToString();
        }
    }
}
