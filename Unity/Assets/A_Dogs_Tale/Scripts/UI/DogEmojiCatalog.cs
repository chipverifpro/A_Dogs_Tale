public readonly struct DogEmojiEntry
{
    public DogEmojiEntry(string name, char sheetId, int spriteIndex)
    {
        Name = name;
        SheetId = sheetId;
        SpriteIndex = spriteIndex;
    }

    public string Name { get; }
    public char SheetId { get; }
    public int SpriteIndex { get; }
    public string EntryId => $"{SheetId}_{SpriteIndex}";
}

public static class DogEmojiCatalog
{
    // Generated from /Users/markpontius/Documents/emoji_catalog/EmoteLookup-Table 1.csv
    public static readonly DogEmojiEntry[] Entries =
    {
        new("Afraid", 'B', 5),
        new("Alert", 'B', 2),
        new("Angry", 'A', 2),
        new("Annoyed", 'B', 10),
        new("Anticipating", 'B', 12),
        new("Begging", 'C', 25),
        new("Bored", 'C', 16),
        new("Calm", 'A', 25),
        new("Celebrating", 'C', 26),
        new("Confused", 'A', 8),
        new("Content", 'B', 13),
        new("Cool", 'A', 26),
        new("Cry", 'A', 11),
        //new("Crying", 'C', 23),
        new("Curious", 'C', 17),
        new("Determined", 'C', 15),
        new("Disappointed", 'C', 9),
        new("Disgusted", 'A', 19),
        new("Distracted", 'B', 19),
        new("Dreamy", 'B', 24),
        new("Embarrassed", 'C', 11),
        new("Excited", 'B', 27),
        new("Focused", 'C', 10),
        new("Found", 'B', 8),
        new("Greedy", 'C', 2),
        new("Groggy", 'B', 25),
        new("Grumpy", 'A', 24),
        new("Guild-ridden", 'B', 15),
        new("Happy", 'A', 0),
        new("Harvy", 'C', 6),
        new("Haughty", 'C', 12),
        new("Hiding", 'B', 22),
        new("Hungry", 'A', 23),
        new("Hypnotized", 'C', 24),
        new("Jealous", 'B', 17),
        new("Laugh", 'A', 12),
        new("Love", 'C', 1),
        new("Mischievous", 'A', 16),
        new("Need Help", 'B', 9),
        new("Nervous", 'C', 19),
        new("Party", 'C', 20),
        new("Paws-up", 'B', 23),
        new("Pensive", 'A', 27),
        new("Playful", 'B', 0),
        new("Playful Bow", 'C', 7),
        new("Proud", 'B', 18),
        new("Relieved", 'B', 21),
        new("Sad", 'A', 1),
        new("Scared", 'A', 6),
        new("Shocked", 'C', 3),
        new("Shy", 'A', 15),
        new("Sick", 'A', 17),
        new("Silly", 'A', 21),
        new("Skeptical", 'B', 11),
        new("Sleepy", 'C', 8),
        new("Smith", 'C', 13),
        new("Smug", 'C', 4),
        new("Sneaky", 'C', 22),
        new("Stern", 'A', 22),
        new("Stubborn", 'B', 16),
        new("Submissive", 'B', 6),
        new("Surprised", 'A', 10),
        new("Suspicious", 'B', 3),
        new("Worried", 'B', 4)
    };
}
