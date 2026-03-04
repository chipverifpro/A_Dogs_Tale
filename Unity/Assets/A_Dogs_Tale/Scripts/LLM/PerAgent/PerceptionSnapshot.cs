using System.Collections.Generic;
using UnityEngine;

public readonly struct PerceptionSnapshot
{
    public readonly IReadOnlyList<PerceivedAgent> agents;
    public readonly IReadOnlyList<PerceivedSound> sounds;
    public readonly IReadOnlyList<PerceivedScent> scents;

    public PerceptionSnapshot(
        IReadOnlyList<PerceivedAgent> agents,
        IReadOnlyList<PerceivedSound> sounds,
        IReadOnlyList<PerceivedScent> scents)
    {
        this.agents = agents;
        this.sounds = sounds;
        this.scents = scents;
    }
}

public readonly struct PerceivedAgent
{
    public readonly int agentId;
    public readonly float distance;
    public readonly bool isInLineOfSight;
    public readonly bool isMoving;
    public readonly bool isKnown;
    public readonly bool isInPack;

    public PerceivedAgent(int agentId, float distance, bool los, bool moving, bool known, bool inPack)
    {
        this.agentId = agentId;
        this.distance = distance;
        isInLineOfSight = los;
        isMoving = moving;
        isKnown = known;
        isInPack = inPack;
    }
}

public enum SoundCategory
{
    None = 0,
    Bark,
    Voice,
    Mechanical,
    // ...
}

public readonly struct PerceivedSound
{
    public readonly Vector3 worldPosition;
    public readonly float loudness;
    public readonly SoundCategory category;
}

public readonly struct PerceivedScent
{
    public readonly ScentSource source;
    public readonly float combinedStrength;
    public readonly Vector2Int cellPos;
}


namespace DogGame.Perception
{
    /// <summary>One tick’s “smell” view, already filtered + sorted for this agent.</summary>
    public readonly struct ScentSnapshot
    {
        public readonly Vector2Int agentCellPos;
        public readonly float timeSeconds;

        /// <summary>Strongest scents at the agent’s current cell (sorted descending).</summary>
        public readonly IReadOnlyList<ScentAtCell> strongestHere;

        /// <summary>
        /// Optional 3×3 samples around the agent for a single “tracked scent”.
        /// Index mapping: [0..2,0..2] where [1,1] is agent cell.
        /// Null means that neighbor cell is not accessible / not present.
        /// </summary>
        public readonly ScentSample?[,] tracked3x3;

        public ScentSnapshot(
            Vector2Int agentCellPos,
            float timeSeconds,
            IReadOnlyList<ScentAtCell> strongestHere,
            ScentSample?[,] tracked3x3)
        {
            this.agentCellPos = agentCellPos;
            this.timeSeconds = timeSeconds;
            this.strongestHere = strongestHere;
            this.tracked3x3 = tracked3x3;
        }
    }

    public readonly struct ScentAtCell
    {
        public readonly ScentSource source;
        public readonly float airStrength;
        public readonly float groundStrength;
        public readonly float combinedStrength;

        public ScentAtCell(ScentSource source, float airStrength, float groundStrength, float combinedStrength)
        {
            this.source = source;
            this.airStrength = airStrength;
            this.groundStrength = groundStrength;
            this.combinedStrength = combinedStrength;
        }
    }

    /// <summary>One sample of one scent in one cell.</summary>
    public readonly struct ScentSample
    {
        public readonly Vector2Int cellPos;
        public readonly float combinedStrength;

        public ScentSample(Vector2Int cellPos, float combinedStrength)
        {
            this.cellPos = cellPos;
            this.combinedStrength = combinedStrength;
        }
    }
}