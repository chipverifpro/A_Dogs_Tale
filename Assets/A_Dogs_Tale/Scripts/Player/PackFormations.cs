using System.Collections.Generic;
using UnityEngine;


public enum FormationsEnum
{
    LineAbreast,
    SingleFile,
    TwoColums,
    Wedge,
    Circle
}

public partial class Agent
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
        switch (formation)
        {
            case FormationsEnum.LineAbreast:
                return LineAbreastPos[position_in_pack];
            case FormationsEnum.SingleFile:
                return SingleFilePos[position_in_pack];
            case FormationsEnum.TwoColums:
                if (number_in_pack == 4) return TwoColumnsPos4[position_in_pack];
                else return TwoColumnsPos5[position_in_pack];
            case FormationsEnum.Wedge:
                return WedgePos[position_in_pack];
            case FormationsEnum.Circle:
                if (number_in_pack == 4) return CirclePos4[position_in_pack].normalized;
                else return CirclePos5[position_in_pack].normalized;
            default:
                return new Vector2(0, 0);
        }
    }

    public Vector2 RotateAndScaleOffset(Vector2 offset, float yawDeg, float scale)
    {
        // rotate the offset vector by yawDeg degrees clockwise.
        float yawRad = -(yawDeg + yawCorrection) * Mathf.Deg2Rad;
        float cosYaw = Mathf.Cos(yawRad);
        float sinYaw = Mathf.Sin(yawRad);
        float x = offset.x * cosYaw - offset.y * sinYaw;
        float y = offset.x * sinYaw + offset.y * cosYaw;
        // and muliply by scale
        return new Vector2(x * scale, y * scale);
    }

    // return the coordinates for the agent in pack formation.
    Crumb GetFormationPosition(Pack pack, int agent_id, Crumb crumb)
    {
        Vector2 normalized = Vector2.zero;

        FormationsEnum formation = pack.formation;
        Vector2 crumbPos2 = crumb.pos2;
        float crumbYawDeg = crumb.yawDeg;
        int position_in_pack = pack.packList.FindIndex(a => a.id == agent_id);
        int number_in_pack = pack.packList.Count;
        float scale = pack.formationSpacing;
        Agent agent = pack.packList[position_in_pack];

        if (position_in_pack == 0 || crumb.valid == false)
        {
            agent.next_formationCrumb.valid = false; // leader does not have a target.
            return agent.next_formationCrumb;
        }
        
        // get the offset for this formation and position in pack.
        Vector2 offset = GetOffsetForFormation(formation, position_in_pack, number_in_pack);
        Vector2 rotated_offset = RotateAndScaleOffset(offset, crumbYawDeg, scale);

        agent.next_formationCrumb.pos2 = crumbPos2 + rotated_offset;
        agent.next_formationCrumb.yawDeg = crumbYawDeg; // todo: for circle formation, face outwards.
        agent.next_formationCrumb.valid = true;
        return agent.next_formationCrumb;
    }
}
