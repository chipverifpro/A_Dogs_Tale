using System;
using System.Collections.Generic;
using UnityEngine;

public class PackManager : MonoBehaviour
{
    public Directory dir;
    public Pack playerPack;
    public List<Pack> packs;

    // initialize the packs array, and put playerPack in the first spot if we have one.
    void Start()
    {
        packs = new();
        if (playerPack == null)
        {
            playerPack = FindPackByName("Player Pack");
            if (playerPack == null)
            {
                Debug.LogError($"PackManager Start() did not find Player Pack.");
                return;
            }
        }

        packs.Add(playerPack);
    }

    public Pack FindPackByName(String targetPackName)
    {
        // Search every Pack component in the scene (active or inactive).
        //Pack[] allPacks = FindObjectsByType<Pack>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Pack found = null;

        foreach (var p in packs)
        {
            if (p != null && p.packName == targetPackName)
            {
                if (found == null)
                {
                    found = p;
                }
                else
                {
                    Debug.LogWarning(
                        $"[PackManager] Multiple packs found with name '{targetPackName}'. " +
                        $"Using the first one: {found.name}", this);
                }
            }
        }

        if (found == null)
        {
            Debug.LogError(
                $"[PackManager] No pack found with packName '{targetPackName}'. " +
                "Make sure the Pack GameObject exists in the scene.", this);
            return found;
        }

        Debug.Log($"[PackManager] Found Player Pack on object '{found.gameObject.name}'.", this);
        return found;
    }

    public void CreateNewPack(string newPackName, WorldObject leader=null)
    {
        Pack pack = new();
        pack.packName = newPackName;
        
    }
}
