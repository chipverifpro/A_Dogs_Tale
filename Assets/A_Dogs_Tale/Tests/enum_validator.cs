
// This file was created over an argument I had with ChatGPT about
// how to best validate an enumerated type with flags.

// ModuleFlags below is a hacked up version for our testing because
// testing the real thing is pretty boring.  It just silently passes.

// The MonoBehavior class at the bottom just runs the validator at Awake().
// Attach it somewhere and watch with amazement.

// The final validotor features...
// -- compares every enumerated value.
// -- reports duplicate SINGLE-BIT values (leaf values).
// -- reports when a COMPOSITE value (more than one bit)
//    includes bits that don't have a single bit definition.
// -- Displays names for all copies of duplicated values.
// -- Displays values in name, hex, and bit shift form (1 << 10)

// WHAT IT DOESN'T FIND...
// -- Duplicated composite values (see buggy and also_buggy below)
// -- The compiler already reports if the same name is used twice,
//    so that's not included.

using System.Collections.Generic;
using UnityEngine;
using System;
/*
namespace ModuleFlagsValidator
{
[Flags]
public enum ModuleFlags : ulong
{
    none          = 0UL,
    // --- Sensory ---
    hearingModule = 1UL << 1,
    smellModule   = 1UL << 2,
    visionModule  = 1UL << 3,
    eatModule     = 1UL << 4,

    // --- Agent Decision Modules
    playerDecisionModule   = 1UL << 5,
    followerDecisionModule = 1UL << 6,
    wanderDecisionModule   = 1UL << 7,

    // --- Agent Interface Modules ---
    agentMovementModule   = 1UL << 8,
    packMemberModule = 1UL << 9,
    // agentSensesModule     = 1UL << 10, //OBSOLETE
    agentModule           = 1UL << 11,

    // --- Motivation ---
    motivationModule   = 1UL << 12,
        
    // --- Ability ---
    activatorModule   = 1UL << 13,
    containerModule   = 1UL << 14,
    interactionModule = 1UL << 15,
    locationModule    = 1UL << 16,
    motionModule      = 1UL << 17,

    // --- Output ---
    appearanceModule  = 1UL << 18,
    noiseMakerModule  = 1UL << 19,
    scentEmitterModule= 1UL << 20,


    // --- Data ---
    blackboardModule  = 1UL << 21,
    placementModule   = 1UL << 22,
    statusModule      = 1UL << 23,

    // --- Quest ---
    questModuleBase   = 1UL << 24,

    // --- Test Cases ---
    bogus             = 1UL << 10,
    buggy             = 1UL << 22 | 1 << 24,
    google            = 1UL << 30 | 1 << 10,
    also_buggy        = 1UL << 24 | 1 << 22
}

// The following templates can be used for configuring new WorldModule instantiations...
public class enum_validator : MonoBehaviour  // extension functions for the ModuleFlags enum
{
    private void Awake()
    {
        Debug.Log($"Validate Module Flags: {Validate()}");
    }

    // I wish I'd kept my original version, but this here was probably effort #3 or #4.  One of the early ones had used linq that was pretty cool.
    public static bool Validate_OLD()
    {
        bool valid=true;
        Array flags = Enum.GetValues(typeof(ModuleFlags));
        ulong prev = (ulong)flags.GetValue(0) -1UL; // suppress duplicate identical match messages, guaranteed to not match first item
        for (int a=0; a<flags.GetLength(0); a++)
            for (int b=a+1; b<flags.GetLength(0); b++)
                if ((ulong)flags.GetValue(a) == (ulong)flags.GetValue(b) && (prev != (ulong)flags.GetValue(a)))
                {
                    var names = GetAllDeclaredNamesForValue<ModuleFlags>((ModuleFlags)flags.GetValue(a));
                    Debug.LogError($"ModuleFlags Validate found {names.Count} elements with identical bits: {string.Join(", ", names)} = 0x{(ulong)flags.GetValue(a):X}");
                    valid=false;
                    prev=(ulong)flags.GetValue(a);
                }
                else if (((ulong)flags.GetValue(a) & (ulong)flags.GetValue(b)) != 0UL)
                {
                    Debug.LogError($"ModuleFlags Validate found two elements with overlapping bits: {flags.GetValue(a)} = {(ulong)flags.GetValue(a):X} and {flags.GetValue(b)} = {(ulong)flags.GetValue(b):X}");
                    valid=false;
                }
        return valid;
    }

    public static List<string> GetAllDeclaredNamesForValue<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        Array values = Enum.GetValues(enumType);
        string[] names = Enum.GetNames(enumType);

        ulong target = Convert.ToUInt64(value);

        var results = new List<string>();
        for (int i = 0; i < values.Length; i++)
        {
            ulong v = Convert.ToUInt64(values.GetValue(i));
            if (v == target)
                results.Add(names[i]); // <-- the declared name at this slot
        }

        return results;
    }

    private static bool IsSingleBit(ulong v) => v != 0UL && (v & (v - 1UL)) == 0UL;

    // Yet another version...
    public static bool Validate_X1()
    {
        bool valid = true;

        Array values = Enum.GetValues(typeof(ModuleFlags));
        string[] names = Enum.GetNames(typeof(ModuleFlags));

        var reportedDuplicateValues = new HashSet<ulong>();

        for (int a = 0; a < values.Length; a++)
        {
            ulong aVal = Convert.ToUInt64(values.GetValue(a));

            for (int b = a + 1; b < values.Length; b++)
            {
                ulong bVal = Convert.ToUInt64(values.GetValue(b));

                // 1) Identical numeric value (duplicate constant)
                if (aVal == bVal)
                {
                    if (!reportedDuplicateValues.Contains(aVal))
                    {
                        var declaredNames = GetAllDeclaredNamesForValue((ModuleFlags)aVal);
                        Debug.LogError(
                            $"ModuleFlags Validate found {declaredNames.Count} elements with identical value: " +
                            $"{string.Join(", ", declaredNames)} = 0x{aVal:X}"
                        );
                        reportedDuplicateValues.Add(aVal);
                    }

                    valid = false;
                    continue;
                }
            }
        }

        return valid;
    }

    // The final? version.  I'm going to bed, ain't gonna do no mow.
    public static bool Validate()
    {
        bool valid = true;

        Array values = Enum.GetValues(typeof(ModuleFlags));
        string[] names = Enum.GetNames(typeof(ModuleFlags));

        var seenLeafBits = new HashSet<ulong>();
        ulong allLeafBits = 0;

        for (int i = 0; i < values.Length; i++)
        {
            ulong v = Convert.ToUInt64(values.GetValue(i));
            if (v == 0UL) continue;

            bool isSingleBit = (v & (v - 1UL)) == 0UL;

            if (isSingleBit)
            {
                if (!seenLeafBits.Add(v))
                {
                    var dupNames = GetAllDeclaredNamesForValue((ModuleFlags)v);
                    Debug.LogError($"Duplicate leaf bit: {string.Join(", ", dupNames)} = 0x{v:X} = {ToShiftExpression(v)}");
                    valid = false;
                }
                allLeafBits |= v;
            }
        }

        // Now validate composites (if any exist)
        for (int i = 0; i < values.Length; i++)
        {
            ulong v = Convert.ToUInt64(values.GetValue(i));
            if (v == 0UL) continue;

            bool isSingleBit = (v & (v - 1UL)) == 0UL;
            if (isSingleBit) continue;

            // Composite must be made only of known leaf bits
            ulong unknownBits = v & ~allLeafBits;
            if (unknownBits != 0UL)
            {
                Debug.LogError($"Composite '{names[i]}' contains unknown bits: 0x{unknownBits:X} (value=0x{v:X} = {ToShiftExpression(v)})");
                valid = false;
            }
        }

        return valid;
    }

    public static List<string> GetAllDeclaredNamesForValue(ModuleFlags value)
    {
        Array values = Enum.GetValues(typeof(ModuleFlags));
        string[] names = Enum.GetNames(typeof(ModuleFlags));

        ulong target = (ulong)value;

        var results = new List<string>();
        for (int i = 0; i < values.Length; i++)
        {
            ulong v = Convert.ToUInt64(values.GetValue(i));
            if (v == target)
                results.Add(names[i]);
        }

        return results;
    }

    // a couple versions of pretty printing...
    public static string ToShiftExpression_MIXED<TEnum>(ulong value)
        where TEnum : struct, Enum
    {
        if (value == 0UL)
            return "0UL";

        var enumValues = Enum.GetValues(typeof(TEnum));
        var enumNames  = Enum.GetNames(typeof(TEnum));

        // Map single-bit enum values to names
        var bitToName = new Dictionary<ulong, string>();
        for (int i = 0; i < enumValues.Length; i++)
        {
            ulong v = Convert.ToUInt64(enumValues.GetValue(i));
            if (v != 0UL && (v & (v - 1UL)) == 0UL) // single-bit
                bitToName[v] = enumNames[i];
        }

        var parts = new List<string>();

        for (int bit = 0; bit < 64; bit++)
        {
            ulong mask = 1UL << bit;
            if ((value & mask) == 0) continue;

            if (bitToName.TryGetValue(mask, out var name))
                parts.Add(name);
            else
                parts.Add($"1UL << {bit}");
        }

        return string.Join(" | ", parts);
    }

    public static string ToShiftExpression(ulong value)
    {
        if (value == 0UL)
            return "0UL";

        var parts = new List<string>();

        for (int bit = 0; bit < 64; bit++)
        {
            ulong mask = 1UL << bit;
            if ((value & mask) != 0)
                parts.Add($"1UL << {bit}");
        }

        return string.Join(" | ", parts);
    }

    // Automatic generation of All the used bits so we can do subtractive configurations.
    // After this, the argument with ChatGPT really heated up.
    public static ModuleFlags All { get {
            ModuleFlags all = 0;
            foreach (ModuleFlags flag in Enum.GetValues(typeof(ModuleFlags)))
                all |= flag;
            return all;
        }
    }
    // Some examples of handy configurations...
    private static readonly ModuleFlags FullAgent =  All
                                                     & ~ModuleFlags.questModuleBase
                                                     & ~ModuleFlags.placementModule;
    private static readonly ModuleFlags ScatterTerrain = ModuleFlags.placementModule
                                                       | ModuleFlags.scentEmitterModule
                                                       | ModuleFlags.appearanceModule;
    private static readonly ModuleFlags TreasureChest = ScatterTerrain
                                                      | ModuleFlags.containerModule;
}
}

/*
Just to make sure ChatGPT gets the last word in about this topic, here's a final question for it...

Is there any way to get the same effect as the Flags enum, without manually specifying what bit is which value?



Short answer: not with a C# enum alone.
If you want Flags-like behavior without manually assigning bit positions, you have to move one level up from enums.

Below are the real options, with tradeoffs, and which ones fit your project best.

⸻

Why enums fundamentally can’t do this

C# enums are compile-time constants. That means:
	•	Every value must be known at compile time
	•	The compiler does not auto-assign unique powers-of-two
	•	There is no “next available bit” concept

So this is impossible:

enum Modules
{
    Hearing,   // ← cannot auto-be 1 << 0
    Smell,     // ← cannot auto-be 1 << 1
}

Enums only auto-increment integers, not bit positions.

⸻

Option 1 (Best overall): Symbol → bit registry (recommended)

This gives you:
	•	Flags semantics
	•	Auto-assigned bits
	•	Zero manual bit math
	•	Strong validation
	•	Debuggable output

Core idea

Instead of an enum, use symbols + a registry that assigns bits once.

public sealed class ModuleKey
{
    internal readonly int bitIndex;
    internal ModuleKey(int bitIndex) => this.bitIndex = bitIndex;
}

Registry (assigns bits automatically)

public static class ModuleRegistry
{
    private static int nextBit = 0;
    private static readonly Dictionary<string, ModuleKey> keys = new();

    public static ModuleKey Register(string name)
    {
        if (keys.ContainsKey(name))
            throw new InvalidOperationException($"Duplicate module key '{name}'");

        var key = new ModuleKey(nextBit++);
        keys[name] = key;
        return key;
    }
}

Define modules (no bit math)

public static class Modules
{
    public static readonly ModuleKey Hearing  = ModuleRegistry.Register("Hearing");
    public static readonly ModuleKey Smell    = ModuleRegistry.Register("Smell");
    public static readonly ModuleKey Vision   = ModuleRegistry.Register("Vision");
    public static readonly ModuleKey Motion   = ModuleRegistry.Register("Motion");
}

The “flags value”

public struct ModuleMask
{
    private ulong bits;

    public void Add(ModuleKey key) =>
        bits |= 1UL << key.bitIndex;

    public bool Has(ModuleKey key) =>
        (bits & (1UL << key.bitIndex)) != 0;

    public override string ToString() =>
        $"0x{bits:X16}";
}

Usage

ModuleMask mask = new();
mask.Add(Modules.Hearing);
mask.Add(Modules.Motion);

if (mask.Has(Modules.Hearing))
{
    // ...
}

Why this is excellent for Dog’s Tale
	•	Modules are data-driven
	•	You already treat modules as dynamic
	•	Debug tooling becomes trivial
	•	You can still serialize the ulong

⸻

Option 2: BitArray (flexible but meh)

BitArray bits = new BitArray(128);
bits[hearingIndex] = true;

Pros
	•	Unlimited size
	•	No bit math

Cons
	•	Heap allocations
	•	Hard to debug
	•	Poor inspector support
	•	No compile-time safety

❌ Not recommended for gameplay flags.

⸻

Option 3: Source generator (advanced, heavy)

You can use a Roslyn source generator to:
	•	Read an enum
	•	Rewrite it with 1 << n automatically

Pros
	•	Keeps enum syntax
	•	Zero runtime cost

Cons
	•	Complex tooling
	•	Harder to debug
	•	Overkill unless you’re writing a framework

This is how some engine-level libraries do it — but probably not worth it here.

⸻

Option 4: Stick with enums + validation (what you already have)

Honestly?
What you’ve built is already the best enum-based solution:
	•	ulong backing
	•	Validation
	•	Auto-generated AllDefined
	•	Clear diagnostics

Enums are still valuable when:
	•	You want switch statements
	•	You want inspector dropdowns
	•	You want compile-time constants

⸻

Clear recommendation for your project

Given:
	•	You want dynamic module composition
	•	You already validate aggressively
	•	You want debug-friendly tooling
	•	You’re not trying to expose this to designers as an enum dropdown

👉 Use ModuleKey + ModuleMask for runtime composition

👉 Keep ModuleFlags only for static templates / presets if you want

You can even bridge them:

ModuleMask FromFlags(ModuleFlags flags) { ... }


⸻

Hacker’s Summary
	•	❌ Enums cannot auto-assign flag bits
	•	✅ You can get the same effect with a registry + bitmask
	•	🥇 Best solution: Symbol registry + ulong mask
	•	🥈 Second-best: validated ulong enum (what you have)
	•	🧠 Source generators exist, but are heavy

If you want, next I can:
	•	Convert your existing ModuleFlags → ModuleKey system
	•	Add inspector/debug visualization for the mask
	•	Add save/load serialization
	•	Show how to keep enum and dynamic flags in sync
*/