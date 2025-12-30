using System;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;




[Serializable]
public class ScentSource
{
    // Unique key that ties this scent to your ScentSystem / emitters.
    public int agentId = -1;

    // Broad category: Dog, Human, Food, Machine, etc.
    public ScentCategory category = ScentCategory.Unknown;

    // Display name:
    // - Category-level initially (e.g., "Food")
    // - Specific after identification (e.g., "Hot Dog")
    public string scentName;

    // Base category color (e.g., all Food ≈ shades of red)
    public Color categoryColor;

    // Individualized color for this particular source (e.g., slightly different red hue).
    public Color sourceAirColor;
    public Color sourceGroundColor;

    // Default deposit / emission rates for this scent source
    // (can be fed directly into AddScentToCell or used as multipliers).
    public float airDepositRate = 1.0f;
    public float groundDepositRate = 0.1f;

    // How familiar the pack is with this scent.
    public ScentFamiliarity familiarity = ScentFamiliarity.New;

    // Sensitivity multiplier: >1.0 when trained, applied when dogs sniff for this scent.
    public float sensitivityBoost = 1.0f;

 //   public bool scentStabilized = false;
 //   public bool scentNextStabilized = false;
    
    // Optional: persistent ID for saving/loading (if you want something beyond agentId).
    public string persistentId;

    // Pointer to the scent physics system where we can deposit scent.
    private ScentAirGround scentAirGround;

    public void Emit(Cell cell, float dt, float decayed = 1.0f)
    {
        if (cell==null) return; // need location
        if (scentAirGround == null) // need physics controller
            scentAirGround = UnityEngine.Object.FindFirstObjectByType<ScentAirGround>();
        if (scentAirGround == null)
        {
            Debug.LogWarning("ScentAirGround instance not found in scene.");
            return;
        }

        // deposit the scent. dt is the time interval, decayed is fraction of full scent to deposit.
        scentAirGround.DepositScentToCell(cell, this, dt, decayed, visualizeImmediately: true);    
    }
}

