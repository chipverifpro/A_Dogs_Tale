using DogGame.LLM;

public interface ILLMPlanner
{
    /// <summary>
    /// Submit an async plan request for an agent. Returns a request id for correlation/logging.
    /// The plan will be delivered later through your existing driver/scheduler callback path.
    /// </summary>
    string SubmitPlanRequest(LLMPlanRequestOnDemand request);
}