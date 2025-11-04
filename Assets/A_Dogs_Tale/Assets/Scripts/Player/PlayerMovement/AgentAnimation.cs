using UnityEngine;

public enum DogAnimCodes
{
    Breathing = 0,
    WigglingTail = 1,
    Walking01 = 2,
    Walking02 = 3,
    Running = 4,
    Eating = 5,
    Angry = 6,
    Sitting = 7
}

public enum ActivityTasks
{
    travelling,
    waiting,
    exploring
}

public class AgentAnimation : MonoBehaviour
{
    public ActivityTasks activity;

}