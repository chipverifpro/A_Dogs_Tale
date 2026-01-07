#nullable enable
using UnityEngine;

//2) IMotionModuleBridge — decouples tasks from WorldObject/MotionModule
//
//Who uses it
//	•	Implemented by: WorldObjectMotionBridge (or the newer “intent bridge” if you move to Option B)
//	•	Used by: your movement adapter (MotionAdapter) and possibly tasks like Task_MoveToCell
//
//Why it’s necessary
//
//Tasks should not depend on:
//	•	WorldObject class layout
//	•	MotionModule implementation details
//	•	whether movement is CharacterController vs Rigidbody vs custom transform
//
//If tasks directly call worldObject.motionModule.Move(...), you hard-lock your tasks to one movement backend.
//
//With the bridge:
//	•	tasks/adapters can say “move this way / stop / face this direction”
//	•	the bridge decides how that maps to your current locomotion system
//
//It’s your hardware abstraction layer for movement.
//
//Note: as you move to Option B (“only AgentMovementModule calls Move”), the bridge interface should evolve from “Move()” to “Set movement intent / Set target / Stop”.

namespace DogGame.LLM
{
    public interface IMotionModuleBridge
    {
        void SetDeltaTime(float deltaTime);
        void Move(Vector3 desiredVelocity);
    }
}