using System.Collections.Generic;
using UnityEngine;

namespace DogGame.AI
{
    public interface IAgentHandle
    {
        Transform Transform { get; }
        string AgentName { get; }
    }

    public interface IPackProvider
    {
        bool IsInPack(IAgentHandle agent);
        IAgentHandle GetLeader(IAgentHandle agent);
        IReadOnlyList<IAgentHandle> GetMembers(IAgentHandle agent);

        // First pass: keep simple, return cached values.
        Vector3 GetPackCentroid(IAgentHandle agent);
        float GetPackDistress01(IAgentHandle agent); // 0..1
    }
}