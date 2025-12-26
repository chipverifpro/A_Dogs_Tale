#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public interface IMotionModuleBridge
    {
        void SetDeltaTime(float deltaTime);
        void Move(Vector3 desiredVelocity);
    }
}