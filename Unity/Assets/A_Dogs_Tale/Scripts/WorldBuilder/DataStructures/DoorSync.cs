using System.Collections.Generic;

public static class DoorSync
{
    // Use a global registry for quick lookup; populate as you create doors.
    public static readonly Dictionary<int, Door> ById = new Dictionary<int, Door>();

    public static void Register(params Door[] doors)
    {
        foreach (var d in doors) ById[d.id] = d;
    }

    public static void SetOpen(int doorId, bool open)
    {
        if (!ById.TryGetValue(doorId, out var d)) return;
        d.SetOpen(open);

        if (ById.TryGetValue(d.partnerDoorId, out var p))
            p.SetOpen(open);
    }

    public static void SetLocked(int doorId, bool locked)
    {
        if (!ById.TryGetValue(doorId, out var d)) return;
        if (locked) d.flags |= DoorFlags.Locked; else d.flags &= ~DoorFlags.Locked;

        if (ById.TryGetValue(d.partnerDoorId, out var p))
        {
            if (locked) p.flags |= DoorFlags.Locked; else p.flags &= ~DoorFlags.Locked;
        }
    }
}
