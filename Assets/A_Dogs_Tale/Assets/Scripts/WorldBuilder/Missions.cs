using System;
using System.Collections.Generic;
using UnityEngine;

public class Mission
{
    public String missionFilename;
    public String missionName;
}

public class MissionManager : MonoBehaviour
{
    public Directory dir;

    public List<Mission> missions;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        missions = new();
        BuildMissionList();
    }

    public void BuildMissionList()
    {
        Mission mission;

        mission = new()
        {
            missionName = "Tutorial: Home",
            missionFilename = "01_House_Tutorial"
        };
        missions.Add(mission);

        mission = new()
        {
            missionName = "Home Escape",
            missionFilename = "2_Home_Escape"
        };
        missions.Add(mission);

        mission = new()
        {
            missionName = "Park",
            missionFilename = "3_Park"
        };
        missions.Add(mission);
        
        mission = new()
        {
            missionName = "Neighbor House",
            missionFilename = "4_Neighbor_House"
        };
        missions.Add(mission);
    }

    public bool StartMission(int mission_num)
    {
        Mission mission;
        if (missions.Count > mission_num)
        {
            mission = missions[mission_num];
            // todo: load and start mission
            BottomBanner.Show("Mission {mission_num}: {mission.missionName}");
            return true;
        }
        else
        {
            Debug.LogWarning($"StartMission({mission_num}) with only {missions.Count} missions defined.");
            return false;
        }
    }

}
