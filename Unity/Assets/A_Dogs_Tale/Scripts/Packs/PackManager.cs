using System;
using System.Collections.Generic;
using UnityEngine;

public class PackManager : MonoBehaviour
{
    public Dir dir;
    public Pack playerPack;
    public GameObject FreeAgentsParent;
    public GameObject PackParentObject;
    public List<Pack> packs;

    public bool debug_RandomJoin = false;    // Makes join pack commands => join object to a random pack including a new pack.

    // initialize the packs array, and put playerPack in the first spot if we have one.
    void Start()
    {
        InitializeRuntimeReferences();
    }

    void Awake()
    {
        if (dir == null)
            dir = Dir.Instance;

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

        EnsureFreeAgentsParent();
    }

    public void InitializeRuntimeReferences()
    {
        if (dir == null)
            dir = Dir.Instance;

        if (packs == null)
            packs = new();

        if (playerPack == null)
            playerPack = FindPackByName("Player Pack");

        EnsureFreeAgentsParent();

        if (playerPack != null && !packs.Contains(playerPack))
            packs.Insert(0, playerPack);
    }

    private void EnsureFreeAgentsParent()
    {
        if (FreeAgentsParent != null)
            return;

        FreeAgentsParent = GameObject.Find("FreeAgents");
        if (FreeAgentsParent == null)
            FreeAgentsParent = new GameObject("FreeAgents");
    }

    public int GetPackNumber(Pack searchFor)
    {
        if (searchFor==null) return -1;
        for (int num = 0; num < packs.Count; num++)
        {
            if (packs[num]==searchFor) return num;
        }
        return -1;
    }

    public Pack FindPackByName(String targetPackName)
    {
        if (packs == null)
            packs = new();

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
            Pack[] scenePacks = FindObjectsByType<Pack>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Pack p in scenePacks)
            {
                if (p != null && p.packName == targetPackName)
                {
                    found = p;
                    if (!packs.Contains(p))
                        packs.Add(p);
                    break;
                }
            }
        }

        if (found == null)
        {
            Debug.LogWarning(
                $"[PackManager] No pack found with packName '{targetPackName}'. " +
                "Make sure the Pack GameObject exists in the scene.", this);
            return found;
        }

        Debug.Log($"[PackManager] Found Player Pack on object '{found.gameObject.name}'.", this);
        return found;
    }

    public Pack CreateNewPack(string newPackName, WorldObject leader=null)
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
        return pack;
    }

    public Herd CreateNewHerd(string newHerdName = "Herd", WorldObject firstSheep = null)
    {
        GameObject new_go = new();
        new_go.name = newHerdName;
        Herd herd = new_go.AddComponent<Herd>();
        herd.packName = newHerdName;
        herd.dir = dir;
        if (firstSheep != null)
        {
            herd.AddMember(firstSheep, true);
        }
        packs.Add(herd);
        return herd;
    }
}
