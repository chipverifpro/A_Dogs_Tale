using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.Modules
{
    public sealed class QuestManager : MonoBehaviour
    {
        private static QuestManager instance;

        private readonly List<QuestModuleBase> activeQuestModules = new();

        public static QuestManager Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindFirstObjectByType<QuestManager>();
                if (instance != null)
                    return instance;

                GameObject managerObject = new("QuestManager");
                instance = managerObject.AddComponent<QuestManager>();
                return instance;
            }
        }

        public static IReadOnlyList<QuestModuleBase> ActiveQuestModules => Instance.activeQuestModules;
        public static QuestManager Current => instance;
        public event Action ActiveQuestsChanged;

        internal static void ResetStaticStateForReload()
        {
            instance = null;
        }

        public static void RegisterActiveQuest(QuestModuleBase questModule)
        {
            if (questModule == null)
                return;

            Instance.Register(questModule);
        }

        public static void RefreshActiveQuestModules()
        {
            Instance.RefreshFromScene(notifyIfChanged: false);
        }

        public static void UnregisterQuest(QuestModuleBase questModule)
        {
            if (questModule == null || instance == null)
                return;

            instance.Unregister(questModule);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void Register(QuestModuleBase questModule)
        {
            if (activeQuestModules.Contains(questModule))
                return;

            activeQuestModules.Add(questModule);
            ActiveQuestsChanged?.Invoke();
        }

        private void Unregister(QuestModuleBase questModule)
        {
            if (!activeQuestModules.Remove(questModule))
                return;

            ActiveQuestsChanged?.Invoke();
        }

        private void RefreshFromScene(bool notifyIfChanged)
        {
            bool changed = false;

            for (int index = activeQuestModules.Count - 1; index >= 0; index--)
            {
                QuestModuleBase questModule = activeQuestModules[index];
                if (questModule != null && questModule.IsRunning)
                    continue;

                activeQuestModules.RemoveAt(index);
                changed = true;
            }

            QuestModuleBase[] sceneQuestModules = FindObjectsByType<QuestModuleBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (QuestModuleBase questModule in sceneQuestModules)
            {
                if (questModule == null || !questModule.IsRunning || activeQuestModules.Contains(questModule))
                    continue;

                activeQuestModules.Add(questModule);
                changed = true;
            }

            if (changed && notifyIfChanged)
                ActiveQuestsChanged?.Invoke();
        }
    }
}

public static class QuestManagementBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureQuestManagementExists()
    {
        _ = DogGame.Modules.QuestManager.Instance;

        if (UnityEngine.Object.FindFirstObjectByType<QuestJournalUI>() != null)
            return;

        GameObject journalObject = new("QuestJournalUI");
        journalObject.AddComponent<QuestJournalUI>();
    }
}
