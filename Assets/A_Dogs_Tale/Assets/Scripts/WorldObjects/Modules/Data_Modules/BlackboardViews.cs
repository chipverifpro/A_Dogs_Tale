using UnityEngine;

public class AgentBlackboardView
{
    private readonly BlackboardModule bb;

    public AgentBlackboardView(BlackboardModule blackboard)
    {
        bb = blackboard;
    }

    const string TargetKey = "Agent.Target";
    const string FearKey   = "Agent.FearLevel";
    const string HungerKey = "Agent.HungerLevel";

    public Transform CurrentTarget
    {
        get => bb.TryGet<Transform>(TargetKey, out var t) ? t : null;
        set => bb.Set(TargetKey, value);
    }

    public float FearLevel
    {
        get => bb.TryGet<float>(FearKey, out var f) ? f : 0f;
        set => bb.Set(FearKey, value);
    }

    public float HungerLevel
    {
        get => bb.TryGet<float>(HungerKey, out var h) ? h : 0f;
        set => bb.Set(HungerKey, value);
    }
}

public class PuzzleBlackboardView
{
    private readonly BlackboardModule bb;

    public PuzzleBlackboardView(BlackboardModule blackboard)
    {
        bb = blackboard;
    }

    const string HasBallKey = "Puzzle.HasBall";
    const string KidHappyKey = "Puzzle.KidHappy";

    public bool HasBall
    {
        get => bb.TryGet<bool>(HasBallKey, out var v) && v;
        set => bb.Set(HasBallKey, value);
    }

    public bool KidHappy
    {
        get => bb.TryGet<bool>(KidHappyKey, out var v) && v;
        set => bb.Set(KidHappyKey, value);
    }
}