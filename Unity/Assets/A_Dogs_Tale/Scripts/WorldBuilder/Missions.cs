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
    public Dir dir;

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
            missionName = "House",
            missionFilename = "Map1_House"
        };
        missions.Add(mission);

        mission = new()
        {
            missionName = "Yard",
            missionFilename = "Map2_Yard"
        };
        missions.Add(mission);

        mission = new()
        {
            missionName = "Dog Park",
            missionFilename = "Map3_DogPark"
        };
        missions.Add(mission);
        
        mission = new()
        {
            missionName = "Forest",
            missionFilename = "Map4_Forest"
        };
        missions.Add(mission);

        mission = new()
        {
            missionName = "Castle",
            missionFilename = "Map5_Castle"
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
            BottomBanner.LogBuildProgress($"Mission {mission_num}: {mission.missionName}");
            return true;
        }
        else
        {
            Debug.LogWarning($"StartMission({mission_num}) with only {missions.Count} missions defined.");
            return false;
        }
    }

}
