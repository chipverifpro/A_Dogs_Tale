using System;
using UnityEngine;

//   PlayerAgent is a member of the player's party.
public class PlayerAgent : Agent
{
    public bool isPlayer = false;   // controlled by player
    public bool selected = false;   // highlights and accepts comands
    public float tendencyToWander = 0.1f;   // ignores commands, investigate surroundings
    public bool isAlpha = false;    // has special command abilities

    //public String RoleInPack;     // Alpha, Beta, Tank, Scout, etc.
    //public Command CurrentCommmand = Sit, Stay, Follow, Search, Escape, Sneak, Attack, Defend etc.

    protected override void Update()
    {
        base.Update();
    }
}