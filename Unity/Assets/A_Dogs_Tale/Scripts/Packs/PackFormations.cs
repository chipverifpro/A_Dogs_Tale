using System.Collections.Generic;
using UnityEngine;

// PackFormations is a MonoBehaviour class that sits at the top,
// and does formation to position translations.  You must pass in
// all relevant data from your pack, as this has no local storage
// except cached random offsets for herd formation positions.

public enum FormationsEnum
{
    LineAbreast,
    SingleFile,
    TwoColums,
    Wedge,
    Circle,
    Snake,
    Herd
}

public class PackFormations : MonoBehaviour
{
    private const float HerdMinOffsetDistance = 1f;
    private const int HerdRandomPlacementAttempts = 512;
    private readonly List<Vector2> herdOffsetsByPosition = new() { Vector2.zero };

    public Vector2 GetOffsetForFormation(FormationsEnum formation, int position_in_pack, int number_in_pack)
    {
        // return the offset vector for the given formation and position in pack.
        // Assumes position_in_pack is 0 for leader, 1..n for followers.
        // Assumes leader facing north.  Rotation to be applied later.
        // some formations depend on number in pack.
        if (position_in_pack <= 0)
            return Vector2.zero;

        int safeNumberInPack = Mathf.Max(1, number_in_pack);
        switch (formation)
        {
            case FormationsEnum.LineAbreast:
                return GetLineAbreastOffset(position_in_pack);
            case FormationsEnum.SingleFile:
                return GetSingleFileOffset(position_in_pack);
            case FormationsEnum.TwoColums:
                return GetTwoColumnsOffset(position_in_pack, safeNumberInPack);
            case FormationsEnum.Wedge:
                return GetWedgeOffset(position_in_pack);
            case FormationsEnum.Circle:
                return GetCircleOffset(position_in_pack, safeNumberInPack);
            case FormationsEnum.Herd:
                return GetHerdOffset(position_in_pack, safeNumberInPack);
            default:
                return new Vector2(0, 0);
        }
    }

#region OffsetFunctions
    private static Vector2 GetLineAbreastOffset(int positionInPack)
    {
        int column = (positionInPack + 1) / 2;
        float side = positionInPack % 2 == 0 ? 1f : -1f;
        return new Vector2(side * column, 0f);
    }

    private static Vector2 GetSingleFileOffset(int positionInPack)
    {
        return new Vector2(0f, -positionInPack);
    }

    private static Vector2 GetTwoColumnsOffset(int positionInPack, int numberInPack)
    {
        if (numberInPack == 4 && positionInPack == 3)
            return new Vector2(0f, -2f);

        int row = (positionInPack + 1) / 2;
        float side = positionInPack % 2 == 0 ? 0.5f : -0.5f;
        return new Vector2(side, -row);
    }

    private static Vector2 GetWedgeOffset(int positionInPack)
    {
        int row = (positionInPack + 1) / 2;
        float side = positionInPack % 2 == 0 ? 1f : -1f;
        return new Vector2(side * 0.5f * row, -row);
    }

    private static Vector2 GetCircleOffset(int positionInPack, int numberInPack)
    {
        if (numberInPack == 4)
        {
            if (positionInPack == 1)
                return new Vector2(-1f, 1f).normalized;
            if (positionInPack == 2)
                return new Vector2(1f, 1f).normalized;
            if (positionInPack == 3)
                return Vector2.down;
        }

        float followerCount = Mathf.Max(1f, numberInPack - 1f);
        float angleRad = Mathf.PI * 0.75f - ((positionInPack - 1) / followerCount) * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
    }
#endregion

#region Herd
    private Vector2 GetHerdOffset(int positionInPack, int numberInPack)
    {
        EnsureHerdOffsetsThroughPosition(positionInPack, numberInPack);
        return herdOffsetsByPosition[positionInPack];
    }

    private void EnsureHerdOffsetsThroughPosition(int positionInPack, int numberInPack)
    {
        float radius = GetHerdRadius(numberInPack);
        while (herdOffsetsByPosition.Count <= positionInPack)
            herdOffsetsByPosition.Add(GenerateHerdOffset(radius));
    }

    private static float GetHerdRadius(int numberInPack)
    {
        return 1f + Mathf.Max(1, numberInPack) / 4f;
    }

    private Vector2 GenerateHerdOffset(float radius)
    {
        for (int attempt = 0; attempt < HerdRandomPlacementAttempts; attempt++)
        {
            Vector2 candidate = Random.insideUnitCircle * radius;
            if (IsAllowedHerdOffset(candidate))
            {
                Debug.Log($"Herd radius = {radius}, position = {candidate}");
                return candidate;
            }
        }

        const float radialStep = 0.25f;
        const int angleSteps = 72;
        for (float candidateRadius = HerdMinOffsetDistance; candidateRadius <= radius; candidateRadius += radialStep)
        {
            for (int angleIndex = 0; angleIndex < angleSteps; angleIndex++)
            {
                float angleRad = angleIndex * Mathf.PI * 2f / angleSteps;
                Vector2 candidate = new(Mathf.Cos(angleRad) * candidateRadius, Mathf.Sin(angleRad) * candidateRadius);
                if (IsAllowedHerdOffset(candidate))
                    return candidate;
            }
        }

        Debug.LogWarning($"PackFormations could not find a herd offset with radius {radius:0.00}; using edge fallback.", this);
        float fallbackAngle = herdOffsetsByPosition.Count * 137.508f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(fallbackAngle) * radius, Mathf.Sin(fallbackAngle) * radius);
    }

    private bool IsAllowedHerdOffset(Vector2 candidate)
    {
        if (candidate.sqrMagnitude < HerdMinOffsetDistance * HerdMinOffsetDistance)
            return false;

        float minDistanceSqr = HerdMinOffsetDistance * HerdMinOffsetDistance;
        for (int i = 0; i < herdOffsetsByPosition.Count; i++)
        {
            if ((candidate - herdOffsetsByPosition[i]).sqrMagnitude < minDistanceSqr)
                return false;
        }

        return true;
    }
#endregion

    public Vector2 RotateAndScaleOffset(Vector2 offset, float yawDeg, float scale)
    {
        // yawDeg is already 0 = +mapY/+worldZ and positive clockwise.
        float yawRad = -yawDeg * Mathf.Deg2Rad;
        float cosYaw = Mathf.Cos(yawRad);
        float sinYaw = Mathf.Sin(yawRad);
        float x = offset.x * cosYaw - offset.y * sinYaw;
        float y = offset.x * sinYaw + offset.y * cosYaw;
        // and muliply by scale
        return new Vector2(x * scale, y * scale);
    }

    // return the coordinates for the agent in pack formation.
    public Crumb GetFormationPosition(Pack pack, int agent_id, Crumb crumb)
    {
        Vector2 normalized = Vector2.zero;

        FormationsEnum formation = pack.formation;
        Vector2 crumbPos2 = crumb.pos2;
        float crumbYawDeg = crumb.yawDeg;
        int position_in_pack = pack.packAgentList.FindIndex(a => a.ObjectId == agent_id);
        int number_in_pack = pack.packAgentList.Count;
        float scale = pack.formationSpacing;
        WorldObject agent = pack.packAgentList[position_in_pack];

        if (position_in_pack == 0 || crumb.valid == false)
        {
            agent.agentMovementModule.next_formationCrumb.valid = false; // leader does not have a target.
            return agent.agentMovementModule.next_formationCrumb;
        }
        
        // get the offset for this formation and position in pack.
        Vector2 offset = GetOffsetForFormation(formation, position_in_pack, number_in_pack);
        Vector2 rotated_offset = RotateAndScaleOffset(offset, crumbYawDeg, scale);

        agent.agentMovementModule.next_formationCrumb.pos2 = crumbPos2 + rotated_offset;
        agent.agentMovementModule.next_formationCrumb.yawDeg = crumbYawDeg; // todo: for circle formation, face outwards.
        agent.agentMovementModule.next_formationCrumb.valid = true;
        return agent.agentMovementModule.next_formationCrumb;
    }
}
