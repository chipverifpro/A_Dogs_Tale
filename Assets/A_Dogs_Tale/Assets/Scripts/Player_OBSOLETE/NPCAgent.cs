using System;
using UnityEngine;


//   NPCAgent is a non-player-character (friendly / neutral / hostile).
public class NPCAgent : Agent
{
    public int hostilityLevel;      // <0 freindly, 0= neutral, >0 hostile
    public bool knowsOfPlayer;      // is aware of player nearby
    public bool followingPlayer;    // actively tracking player
    public bool attacking;          // actively targeting player
    public bool fleeing;            // running away

    // conversation tree?

    protected override void Update()
    {
        base.Update();
    }
}