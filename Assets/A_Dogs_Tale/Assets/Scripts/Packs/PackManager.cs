using System;
using System.Collections.Generic;
using UnityEngine;

public class PackManager : MonoBehaviour
{
    public Directory dir;
    public Pack playerPack;
    public GameObject PackParentObject;
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

    void Awake()
    {
        // --- Parent object for packs ---
        if (!PackParentObject)
        {
            string parentName = "Packs";
            PackParentObject = GameObject.Find(parentName);
            if (PackParentObject)
            {
                Debug.Log($"[Pack] Found PackParentObject: {PackParentObject.name}");
            }
            else
            {
                // Create one if missing
                PackParentObject = new GameObject(parentName);
                Debug.Log($"[Pack] Created PackParentObject: {PackParentObject.name}");
            }
        }
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
        GameObject new_go = new();
        new_go.name = newPackName;
        Pack pack = new_go.AddComponent<Pack>();
        pack.packName = newPackName;
        pack.dir = dir;
        if (leader!=null)
        {
            pack.AddMember(leader,true);
        }
        packs.Add(pack);    // register new pack
    }
}
