using System.Collections.Generic;
using UnityEngine;

// PackFormations is a MonoBheavior class that sits at the top,
// and does formation to position translations.  You must pass in
// all relevant data from your pack, as this has no local storage
// for anything but arrangements of positions.

public enum FormationsEnum
{
    LineAbreast,
    SingleFile,
    TwoColums,
    Wedge,
    Circle,
    Snake
}

public class PackFormations : MonoBehaviour
{
    readonly List<Vector2> LineAbreastPos = new()
    {
        new Vector2(0,0),
        new Vector2(-1,0),
        new Vector2(1,0),
        new Vector2(-2,0),
        new Vector2(2,0)
    };
    readonly List<Vector2> SingleFilePos = new()
    {
        new Vector2(0,0),
        new Vector2(0,-1),
        new Vector2(0,-2),
        new Vector2(0,-3),
        new Vector2(0,-4)
    };
    readonly List<Vector2> TwoColumnsPos5 = new()
    {
        new Vector2(0,0),
        new Vector2(-0.5f,-1),
        new Vector2(0.5f,-1),
        new Vector2(-0.5f,-2),
        new Vector2(0.5f,-2)
    };
    readonly List<Vector2> TwoColumnsPos4 = new()
    {
        new Vector2(0,0),
        new Vector2(-0.5f,-1),
        new Vector2(0.5f,-1),
        new Vector2(0,-2)
    };
    readonly List<Vector2> WedgePos = new()
    {
        new Vector2(0f,0f),
        new Vector2(-.5f,-1f),
        new Vector2(.5f,-1f),
        new Vector2(-1f,-2f),
        new Vector2(1f,-2f)
    };
    readonly List<Vector2> CirclePos5 = new()   // normalized later
    {
        new Vector2(0,0),
        new Vector2(-1,1),
        new Vector2(1,1),
        new Vector2(1,-1),
        new Vector2(-1,-1)
    };
    readonly List<Vector2> CirclePos4 = new()   // normalized later
    {
        new Vector2(0,0),
        new Vector2(-1,1),
        new Vector2(1,1),
        new Vector2(0,-1)
    };
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
                if (position_in_pack < LineAbreastPos.Count)
                    return LineAbreastPos[position_in_pack];
                return new Vector2(position_in_pack % 2 == 0 ? position_in_pack / 2f : -((position_in_pack + 1) / 2f), 0f);
            case FormationsEnum.SingleFile:
                if (position_in_pack < SingleFilePos.Count)
                    return SingleFilePos[position_in_pack];
                return new Vector2(0f, -position_in_pack);
            case FormationsEnum.TwoColums:
                if (safeNumberInPack == 4 && position_in_pack < TwoColumnsPos4.Count)
                    return TwoColumnsPos4[position_in_pack];
                if (position_in_pack < TwoColumnsPos5.Count)
                    return TwoColumnsPos5[position_in_pack];
                return new Vector2(position_in_pack % 2 == 0 ? 0.5f : -0.5f, -((position_in_pack + 1) / 2f));
            case FormationsEnum.Wedge:
                if (position_in_pack < WedgePos.Count)
                    return WedgePos[position_in_pack];
                float wedgeRow = (position_in_pack + 1) / 2f;
                float wedgeSide = position_in_pack % 2 == 0 ? 1f : -1f;
                return new Vector2(wedgeSide * 0.5f * wedgeRow, -wedgeRow);
            case FormationsEnum.Circle:
                if (safeNumberInPack == 4 && position_in_pack < CirclePos4.Count)
                    return CirclePos4[position_in_pack].normalized;
                if (position_in_pack < CirclePos5.Count)
                    return CirclePos5[position_in_pack].normalized;
                float angleRad = ((position_in_pack - 1) / Mathf.Max(1f, safeNumberInPack - 1f)) * Mathf.PI * 2f;
                return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            default:
                return new Vector2(0, 0);
        }
    }

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
