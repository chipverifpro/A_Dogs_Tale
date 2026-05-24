using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public partial class TopPulldown
{
    private AgentDecisionType GetCurrentDecisionType()
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        AgentModule agentModule = controlledObject != null ? controlledObject.agentModule : null;
        return agentModule != null && agentModule.currentDecisionModule != null
            ? agentModule.currentDecisionModule.DecisionType
            : AgentDecisionType.Undefined;
    }

    private WalkMode GetCurrentWalkMode()
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject != null && controlledObject.motionModule != null)
            return controlledObject.motionModule.currentWalkMode;

        return WalkMode.Walk;
    }

    private WorldObject GetCurrentControlledWorldObject()
    {
        GameInputRouter router = GameInputRouter.Instance != null
            ? GameInputRouter.Instance
            : (EnsureDir() ? dir.gameInputRouter : null);

        if (router != null && router.currentControlledWorldObject != null)
            return router.currentControlledWorldObject;

        return EnsureDir() && dir.playerPack != null ? dir.playerPack.packLeader : null;
    }
}
