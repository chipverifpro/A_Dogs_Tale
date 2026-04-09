using DogGame.Modules;

namespace DogGame.Lua
{
    public class TaskState
    {
        public string current            = "";
        public string target             = "";
        public string destination        = "";
        public bool hasTask              = false;
        public bool isIdle               = true;

        public bool targetVisible        = false;
        public float targetDistance      = 0.0f;
        public bool destinationReached   = false;
        public bool pathBlocked          = false;

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            var taskController = worldObject != null ? worldObject.GetComponent<DogGame.LLM.TaskController>() : null;
            if (taskController == null || taskController.taskExecutor == null)
            {
                current = "";
                hasTask = false;
                isIdle = true;
                return;
            }

            current = taskController.taskExecutor.CurrentTaskName ?? "";
            hasTask = taskController.IsDriving;
            isIdle = !hasTask;
        }

        public void Tick(float interval)
        {
            targetDistance = UnityEngine.Mathf.Max(0f, targetDistance);
        }
    }
}
