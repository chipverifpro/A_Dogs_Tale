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
