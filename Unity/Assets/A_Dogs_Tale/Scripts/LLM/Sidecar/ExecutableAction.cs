using UnityEngine;

namespace DogGame.LLM
{
    public abstract class ExecutableAction
    {
        public abstract string ActionType { get; }

        public virtual void Begin(GameObject agentObject)
        {
        }

        public virtual void Tick(GameObject agentObject, float deltaTime)
        {
        }

        public virtual bool IsComplete(GameObject agentObject)
        {
            return true;
        }

        public virtual void Cancel(GameObject agentObject)
        {
        }

        public virtual bool CanBeInterruptedNow(GameObject agentObject)
        {
            return true;
        }
    }
}