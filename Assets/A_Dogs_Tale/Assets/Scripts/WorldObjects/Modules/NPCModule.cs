using UnityEngine;

public class NPCModule : WorldModule
{
    public enum State { Idle, Patrol, Attack }
    public State CurrentState = State.Idle;
}
