using System;
using System.Collections.Generic;
using UnityEngine;

public static class SpriteServer
{
    struct SpriteReference
    {
        public SpriteReference(string spriteSheet, int index)
        {
            SpriteSheet = spriteSheet;
            Index = index;
        }

        public string SpriteSheet { get; }
        public int Index { get; }
    }

    static readonly Dictionary<string, string> spriteSheetResourcePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "AndroidButtonsAndQuests_B", "Sprites/AndroidButtonsAndQuests_B" },
        // Home, Quest, Camera, Scent, Blank
        { "ArrowsSpriteSheetA",        "Sprites/ArrowsSpriteSheetA" },
        // Arrow_Left, Arrow_Right, Arrow_Up, Arrow_Down, Arrow_Repeat,
        //  Red_X, Green_PawPrint
        { "BonesButtonsSprites_A",     "Sprites/BonesButtonsSprites_A" },
        // ...9 colors, 3 sizes each...
        { "DigHoleSpriteA",            "Sprites/DigHoleSpriteA" },
        // Dig_Hole
        { "DogActions_B",              "Sprites/DogActions_B" },
        // Throw_Item, Fetch_Item, Jump, Hide, Open_Door, Close_Door, 
        //  Open_Container, Close_Container
        { "DogEmojiSheetA",            "Sprites/Emotes/DogEmojiSheetA" },
        //
        { "DogEmojiSheetB",            "Sprites/Emotes/DogEmojiSheetB" },
        //
        { "DogEmojiSheetC",            "Sprites/Emotes/DogEmojiSheetC" },
        //
        { "GraphicsQualitySprites_A",  "Sprites/GraphicsQualitySprites_A" },
        // Graphics_Low, Graphics_Medium, Graphics_High
        //{ "InventoryActions_A",        "Sprites/InventoryActions_A" },
        //{ "InventoryActions_B",        "Sprites/InventoryActions_B" },
        { "InventoryActionsSheetA",    "Sprites/InventoryActionsSheetA" },
        // Use_Item, Eat_Item, Give_Item, Trade_Item, Drop_Item, Pick_Up_Item
        { "InventoryActionsSheetB",    "Sprites/InventoryActionsSheetB" },
        // Paw_Square, Play_Square, GreenCheck_Square,
        //  RaisePaw, Beg, RollOver, Give, Get, Trade
        { "LeashHanging_A",            "Sprites/LeashHanging_A" },
        // Leash_Shorter
        { "LeashHanging_B",            "Sprites/LeashHanging_B" },
        // Leash_Longer
        { "LLM_Icons_A",               "Sprites/LLM_Icons_A" },
        // LLM_Send_Letter, LLM_Receive_Package
        { "MapsSpriteSheet",           "Sprites/MapsSpriteSheet" },
        // Map, Blueprint, Dog_With_Map, Hardhat_With_Map, Blueprint_B, Map_With_Compass
        { "Modes",                     "Sprites/SpriteSheet_Modes_V3" },
        // PlayerControl, Follow, Explore, Stay, Wander, AIControl
        { "PackFormationsSprites_C",   "Sprites/PackFormationsSprites_C" },
        // JoinPack_Paw, JoinPack, LeavePack_Paw, LeavePack, PackLeader_Paw, PackLeader,
        //  Formation_Abreast_Shadow, Formation_Abreast, Formation_Inline_Shadow, Formation_Inline,
        //  Formation_2Columns_Shadow, Formation_2Columns, Formation_Wedge_Shadow, Formation_Wedge,
        //  Formation_Circle_Shadow, Formation_Circle, Formation_Snake_Shadow, Formation_Snake
        { "PlayAndPause_Dual",         "Sprites/PlayAndPause_Dual" },
        // Paw_Play, Paw_Pause
        { "Senses",                    "Sprites/SensesSymbolsColor_v4" },
        { "SensesSymbolsColor_v4",     "Sprites/SensesSymbolsColor_v4" },
        //
        { "SettingsIcons_A",           "Sprites/SettingsIcons_A" },
        // Gear, Shield_Question, Shield_Note, Alert_Paw, Hardhat, Crossed_Wrench_Hammer, Colar_Gear, Blueprint_Gear, Toolbox
        //{ "SettingsIcons_B",           "Sprites/SettingsIcons_B" },
        //{ "SettingsIcons_C",           "Sprites/SettingsIcons_C" },
        { "SettingsIcons_D",           "Sprites/SettingsIcons_D" },
        // ChatGPT, Gemini, Qwen, Ollama, OpenAI_API_KEY, Gemini_API_KEY,
        //  HappyDog, HeadphonesDog, JoystickDog_A, JoystickDog_B, JoystickDog_C,
        //  Documents, Gemma, Mistral, MetaAI, Mistral_API_KEY,
        //  Ollama_Qwen, Ollama_Gemma, Ollama_Mistral
        { "SettingsMapType",           "Sprites/SettingsMapType" },
        // House_Dis, Yard_Dis, DogPark_Dis, Forest_Dis, Castle_Dis,
        // House_En, Yard_En, DogPark_En, Forest_En, Castle_En
        { "Speeds",                    "Sprites/Speeds" },
        // Gait_Sneak, Gait_Walk, Gait_Run
        { "SpriteSheet_Modes_V3",      "Sprites/SpriteSheet_Modes_V3" },
        // Mode_PlayerControl, Mode_Follow, Mode_Explore, Mode_Pause, Mode_Wander, Mode_LLMControl
        { "TakeItemSpriteSheetA",      "Sprites/TakeItemSpriteSheetA" },
        // Trade_Item, Take_Item, Give_Item
        { "TargetIcon_C",              "Sprites/TargetIcon_C" },
        // Target_Scope_Paw
        { "TargetIcon_D",              "Sprites/TargetIcon_D" },
        // Target_Scope

        { "TradeArrows_B",             "Sprites/TradeArrows_B" },
        // Curved_Arrow_Give, Curved_Arrow_Swap, Curved_Arrow_Take
        //{ "TricksSpritesheet_A",       "Sprites/TricksSpritesheet_A" },
        { "TricksSpritesheet_B",       "Sprites/TricksSpritesheet_B" },
        // Fetch, Stay, Come, DropIt, LeaveIt, Heel, Quiet, Bark, GoodDog, BadDog,
        //  FindIt, RollOver, Sit, Down, Release

        // === Frames ===
        { "Behavior_Frame_A",          "Sprites/Behavior_Frame_A" },
        { "Emotes_Frame_A",            "Sprites/Emotes_Frame_A" },
        { "Gait_Frame_AB",             "Sprites/Gait_Frame_AB" },
        // Gait_Frame_1row, Gait_Frame_2row
        { "GenericBigBoneFrame",       "Sprites/GenericBigBoneFrame" },
        { "GenericFrame_A",            "Sprites/GenericFrame_A" },
        { "GenericNoTitleFrame_B",     "Sprites/GenericNoTitleFrame_B" },
        { "InventoryBackground_B",     "Sprites/InventoryBackground_B" },
        { "InventoryBackground_C",     "Sprites/InventoryBackground_C" },
        { "PulldownFrame",             "Sprites/PulldownFrame" },
        { "PulldownFrame_2row",        "Sprites/PulldownFrame_2x5" },
        { "PulldownFrame_2x5",         "Sprites/PulldownFrame_2x5" },
        { "PulldownFrame_2x7",         "Sprites/PulldownFrame_2x7" },
        { "PulldownTab",               "Sprites/PulldownTab" },
        { "PulldownTab_larger",        "Sprites/PulldownTab_larger" },
        { "Quest_Frame_A",             "Sprites/Quest_Frame_A" },
        { "Settings_Background",       "Sprites/Settings_Background" },
        { "Settings_Background_Vert",  "Sprites/Settings_Background_Vert" },
        { "Settings_Background_Vert_C","Sprites/Settings_Background_Vert_C" },

        // === Misc ===
        { "ColorRing1",                "Sprites/ColorRing1" },
        { "ColorRing2",                "Sprites/ColorRing2" },
        { "GameIcon_A",                "Sprites/GameIcon_A" },
        { "Floor_square",              "Sprites/Floor_square" },
        { "FloorGreen_square",         "Sprites/FloorGreen_square" },
        { "TitleTextOnBlack",          "Sprites/TitleTextOnBlack" },
    };

    static readonly Dictionary<string, SpriteReference> knownSprites = new Dictionary<string, SpriteReference>(StringComparer.OrdinalIgnoreCase)
    {
        { NormalizeLookupKey("Home"),       new SpriteReference("AndroidButtonsAndQuests_B", 0) },
        { NormalizeLookupKey("Quest"),      new SpriteReference("AndroidButtonsAndQuests_B", 1) },
        { NormalizeLookupKey("Camera"),     new SpriteReference("AndroidButtonsAndQuests_B", 2) },
        { NormalizeLookupKey("Blank"),      new SpriteReference("AndroidButtonsAndQuests_B", 4) },

        { NormalizeLookupKey("Arrow_Left"),      new SpriteReference("ArrowsSpriteSheetA", 0) },
        { NormalizeLookupKey("Arrow_Right"),     new SpriteReference("ArrowsSpriteSheetA", 1) },
        { NormalizeLookupKey("Arrow_Up"),        new SpriteReference("ArrowsSpriteSheetA", 2) },
        { NormalizeLookupKey("Arrow_Down"),      new SpriteReference("ArrowsSpriteSheetA", 3) },
        { NormalizeLookupKey("Arrow_Repeat"),    new SpriteReference("ArrowsSpriteSheetA", 4) },
        { NormalizeLookupKey("Red_X"),           new SpriteReference("ArrowsSpriteSheetA", 5) },
        { NormalizeLookupKey("Green_PawPrint"),  new SpriteReference("ArrowsSpriteSheetA", 6) },

        { NormalizeLookupKey("Throw_Item"),      new SpriteReference("DogActions_B", 0) },
        { NormalizeLookupKey("Fetch_Item"),      new SpriteReference("DogActions_B", 1) },
        { NormalizeLookupKey("Jump"),            new SpriteReference("DogActions_B", 2) },
        { NormalizeLookupKey("Hide"),            new SpriteReference("DogActions_B", 3) },
        { NormalizeLookupKey("Open_Door"),       new SpriteReference("DogActions_B", 4) },
        { NormalizeLookupKey("Close_Door"),      new SpriteReference("DogActions_B", 5) },
        { NormalizeLookupKey("Open_Container"),  new SpriteReference("DogActions_B", 6) },
        { NormalizeLookupKey("Close_Container"), new SpriteReference("DogActions_B", 7) },

        { NormalizeLookupKey("Graphics_Low"),    new SpriteReference("GraphicsQualitySprites_A", 0) },
        { NormalizeLookupKey("Graphics_Medium"), new SpriteReference("GraphicsQualitySprites_A", 1) },
        { NormalizeLookupKey("Graphics_High"),   new SpriteReference("GraphicsQualitySprites_A", 2) },

        { NormalizeLookupKey("Inventory"),                new SpriteReference("InventoryActionsSheetA", 2) },
        { NormalizeLookupKey("InventoryButton"),          new SpriteReference("InventoryActionsSheetA", 2) },
        { NormalizeLookupKey("InventoryActionsSheetA_2"), new SpriteReference("InventoryActionsSheetA", 2) },
        { NormalizeLookupKey("UseItem"),    new SpriteReference("InventoryActionsSheetA", 0) },
        { NormalizeLookupKey("EatItem"),    new SpriteReference("InventoryActionsSheetA", 1) },
        { NormalizeLookupKey("GiveItem"),   new SpriteReference("InventoryActionsSheetA", 2) },
        { NormalizeLookupKey("TradeItem"),  new SpriteReference("InventoryActionsSheetA", 3) },
        { NormalizeLookupKey("DropItem"),   new SpriteReference("InventoryActionsSheetA", 4) },
        { NormalizeLookupKey("PickUpItem"), new SpriteReference("InventoryActionsSheetA", 5) },

        { NormalizeLookupKey("Paw_Square"),        new SpriteReference("InventoryActionsSheetB", 0) },
        { NormalizeLookupKey("Play_Square"),       new SpriteReference("InventoryActionsSheetB", 1) },
        { NormalizeLookupKey("GreenCheck_Square"), new SpriteReference("InventoryActionsSheetB", 2) },
        { NormalizeLookupKey("RaisePaw"),          new SpriteReference("InventoryActionsSheetB", 3) },
        { NormalizeLookupKey("Beg"),               new SpriteReference("InventoryActionsSheetB", 4) },
        { NormalizeLookupKey("Give"),              new SpriteReference("InventoryActionsSheetB", 6) },
        { NormalizeLookupKey("Get"),               new SpriteReference("InventoryActionsSheetB", 7) },
        { NormalizeLookupKey("Trade"),             new SpriteReference("InventoryActionsSheetB", 8) },

        { NormalizeLookupKey("Leash_Shorter"), new SpriteReference("LeashHanging_A", 0) },
        { NormalizeLookupKey("Leash_Longer"),  new SpriteReference("LeashHanging_B", 0) },

        { NormalizeLookupKey("LLM_Send_Letter"),     new SpriteReference("LLM_Icons_A", 0) },
        { NormalizeLookupKey("LLM_Receive_Package"), new SpriteReference("LLM_Icons_A", 1) },

        { NormalizeLookupKey("BuildProgress"),      new SpriteReference("MapsSpriteSheet", 1) },
        { NormalizeLookupKey("MapBuildProgress"),   new SpriteReference("MapsSpriteSheet", 1) },
        { NormalizeLookupKey("Map"),                new SpriteReference("MapsSpriteSheet", 0) },
        { NormalizeLookupKey("Blueprint"),          new SpriteReference("MapsSpriteSheet", 1) },
        { NormalizeLookupKey("Dog_With_Map"),       new SpriteReference("MapsSpriteSheet", 2) },
        { NormalizeLookupKey("Hardhat_With_Map"),   new SpriteReference("MapsSpriteSheet", 3) },
        { NormalizeLookupKey("Blueprint_B"),        new SpriteReference("MapsSpriteSheet", 4) },
        { NormalizeLookupKey("Map_With_Compass"),   new SpriteReference("MapsSpriteSheet", 5) },
        { NormalizeLookupKey("TreasureMap"),        new SpriteReference("MapsSpriteSheet", 0) },
        { NormalizeLookupKey("BlueprintsA"),        new SpriteReference("MapsSpriteSheet", 1) },
        { NormalizeLookupKey("TreasureMapReader"),  new SpriteReference("MapsSpriteSheet", 2) },
        { NormalizeLookupKey("TreasureMapHardHat"), new SpriteReference("MapsSpriteSheet", 3) },
        { NormalizeLookupKey("BlueprintsB"),        new SpriteReference("MapsSpriteSheet", 4) },
        { NormalizeLookupKey("TreasureMapCompass"), new SpriteReference("MapsSpriteSheet", 5) },

        { NormalizeLookupKey("JoinPack_Paw"),              new SpriteReference("PackFormationsSprites_C", 0) },
        { NormalizeLookupKey("JoinPack"),                  new SpriteReference("PackFormationsSprites_C", 1) },
        { NormalizeLookupKey("LeavePack_Paw"),             new SpriteReference("PackFormationsSprites_C", 2) },
        { NormalizeLookupKey("LeavePack"),                 new SpriteReference("PackFormationsSprites_C", 3) },
        { NormalizeLookupKey("PackLeader_Paw"),            new SpriteReference("PackFormationsSprites_C", 4) },
        { NormalizeLookupKey("PackLeader"),                new SpriteReference("PackFormationsSprites_C", 5) },
        { NormalizeLookupKey("Formation_Abreast_Shadow"),  new SpriteReference("PackFormationsSprites_C", 6) },
        { NormalizeLookupKey("Formation_Abreast"),         new SpriteReference("PackFormationsSprites_C", 7) },
        { NormalizeLookupKey("Formation_Inline_Shadow"),   new SpriteReference("PackFormationsSprites_C", 8) },
        { NormalizeLookupKey("Formation_Inline"),          new SpriteReference("PackFormationsSprites_C", 9) },
        { NormalizeLookupKey("Formation_2Columns_Shadow"), new SpriteReference("PackFormationsSprites_C", 10) },
        { NormalizeLookupKey("Formation_2Columns"),        new SpriteReference("PackFormationsSprites_C", 11) },
        { NormalizeLookupKey("Formation_Wedge_Shadow"),    new SpriteReference("PackFormationsSprites_C", 12) },
        { NormalizeLookupKey("Formation_Wedge"),           new SpriteReference("PackFormationsSprites_C", 13) },
        { NormalizeLookupKey("Formation_Circle_Shadow"),   new SpriteReference("PackFormationsSprites_C", 14) },
        { NormalizeLookupKey("Formation_Circle"),          new SpriteReference("PackFormationsSprites_C", 15) },
        { NormalizeLookupKey("Formation_Snake_Shadow"),    new SpriteReference("PackFormationsSprites_C", 16) },
        { NormalizeLookupKey("Formation_Snake"),           new SpriteReference("PackFormationsSprites_C", 17) },

        { NormalizeLookupKey("Scent"),   new SpriteReference("Senses", -1) },
        { NormalizeLookupKey("Smell"),   new SpriteReference("Senses", -1) },
        { NormalizeLookupKey("Notice3"), new SpriteReference("Senses", 0) },
        { NormalizeLookupKey("Notice2"), new SpriteReference("Senses", 1) },
        { NormalizeLookupKey("Notice1"), new SpriteReference("Senses", 2) },
        { NormalizeLookupKey("Notice0"), new SpriteReference("Senses", 3) },
        { NormalizeLookupKey("Sound3"),  new SpriteReference("Senses", 4) },
        { NormalizeLookupKey("Sound2"),  new SpriteReference("Senses", 5) },
        { NormalizeLookupKey("Sound1"),  new SpriteReference("Senses", 6) },
        { NormalizeLookupKey("Sound0"),  new SpriteReference("Senses", 7) },
        { NormalizeLookupKey("Vision3"), new SpriteReference("Senses", 8) },
        { NormalizeLookupKey("Vision2"), new SpriteReference("Senses", 9) },
        { NormalizeLookupKey("Vision1"), new SpriteReference("Senses", 10) },
        { NormalizeLookupKey("Vision0"), new SpriteReference("Senses", 11) },
        { NormalizeLookupKey("Smell3"),  new SpriteReference("Senses", 12) },
        { NormalizeLookupKey("Smell2"),  new SpriteReference("Senses", 13) },
        { NormalizeLookupKey("Smell1"),  new SpriteReference("Senses", 14) },
        { NormalizeLookupKey("Smell0"),  new SpriteReference("Senses", 15) },
        
        { NormalizeLookupKey("Paw_Play"),  new SpriteReference("PlayAndPause_Dual", 0) },
        { NormalizeLookupKey("Paw_Pause"), new SpriteReference("PlayAndPause_Dual", 1) },
        { NormalizeLookupKey("Play"),   new SpriteReference("PlayAndPause_Dual", 0) },
        { NormalizeLookupKey("Pause"),  new SpriteReference("PlayAndPause_Dual", 1) },
        
        { NormalizeLookupKey("Gear"),                   new SpriteReference("SettingsIcons_A", 0) },
        { NormalizeLookupKey("Shield_Question"),        new SpriteReference("SettingsIcons_A", 1) },
        { NormalizeLookupKey("Shield_Note"),            new SpriteReference("SettingsIcons_A", 2) },
        { NormalizeLookupKey("Alert_Paw"),              new SpriteReference("SettingsIcons_A", 3) },
        { NormalizeLookupKey("Hardhat"),                new SpriteReference("SettingsIcons_A", 4) },
        { NormalizeLookupKey("Crossed_Wrench_Hammer"),  new SpriteReference("SettingsIcons_A", 5) },
        { NormalizeLookupKey("Colar_Gear"),             new SpriteReference("SettingsIcons_A", 6) },
        { NormalizeLookupKey("Blueprint_Gear"),         new SpriteReference("SettingsIcons_A", 7) },
        { NormalizeLookupKey("Toolbox"),                new SpriteReference("SettingsIcons_A", 8) },

        { NormalizeLookupKey("ChatGPT"),        new SpriteReference("SettingsIcons_D", 0) },
        { NormalizeLookupKey("Gemini"),         new SpriteReference("SettingsIcons_D", 1) },
        { NormalizeLookupKey("Qwen"),           new SpriteReference("SettingsIcons_D", 2) },
        { NormalizeLookupKey("Ollama"),         new SpriteReference("SettingsIcons_D", 3) },
        { NormalizeLookupKey("OpenAI_API_KEY"), new SpriteReference("SettingsIcons_D", 4) },
        { NormalizeLookupKey("Gemini_API_KEY"), new SpriteReference("SettingsIcons_D", 5) },
        { NormalizeLookupKey("HappyDog"),       new SpriteReference("SettingsIcons_D", 6) },
        { NormalizeLookupKey("HeadphonesDog"),  new SpriteReference("SettingsIcons_D", 7) },
        { NormalizeLookupKey("JoystickDog_A"),  new SpriteReference("SettingsIcons_D", 8) },
        { NormalizeLookupKey("JoystickDog_B"),  new SpriteReference("SettingsIcons_D", 9) },
        { NormalizeLookupKey("JoystickDog_C"),  new SpriteReference("SettingsIcons_D", 10) },
        { NormalizeLookupKey("Documents"),      new SpriteReference("SettingsIcons_D", 11) },
        { NormalizeLookupKey("Gemma"),          new SpriteReference("SettingsIcons_D", 12) },
        { NormalizeLookupKey("Mistral"),        new SpriteReference("SettingsIcons_D", 13) },
        { NormalizeLookupKey("MetaAI"),         new SpriteReference("SettingsIcons_D", 14) },
        { NormalizeLookupKey("Mistral_API_KEY"),new SpriteReference("SettingsIcons_D", 15) },
        { NormalizeLookupKey("Ollama_Qwen"),    new SpriteReference("SettingsIcons_D", 16) },
        { NormalizeLookupKey("Ollama_Gemma"),   new SpriteReference("SettingsIcons_D", 17) },
        { NormalizeLookupKey("Ollama_Mistral"), new SpriteReference("SettingsIcons_D", 18) },

        { NormalizeLookupKey("House_Dis"),   new SpriteReference("SettingsMapType", 0) },
        { NormalizeLookupKey("Yard_Dis"),    new SpriteReference("SettingsMapType", 1) },
        { NormalizeLookupKey("DogPark_Dis"), new SpriteReference("SettingsMapType", 2) },
        { NormalizeLookupKey("Forest_Dis"),  new SpriteReference("SettingsMapType", 3) },
        { NormalizeLookupKey("Castle_Dis"),  new SpriteReference("SettingsMapType", 4) },
        { NormalizeLookupKey("House_En"),    new SpriteReference("SettingsMapType", 5) },
        { NormalizeLookupKey("Yard_En"),     new SpriteReference("SettingsMapType", 6) },
        { NormalizeLookupKey("DogPark_En"),  new SpriteReference("SettingsMapType", 7) },
        { NormalizeLookupKey("Forest_En"),   new SpriteReference("SettingsMapType", 8) },
        { NormalizeLookupKey("Castle_En"),   new SpriteReference("SettingsMapType", 9) },

        { NormalizeLookupKey("Gait_Sneak"), new SpriteReference("Speeds", 0) },
        { NormalizeLookupKey("Gait_Walk"),  new SpriteReference("Speeds", 1) },
        { NormalizeLookupKey("Gait_Run"),   new SpriteReference("Speeds", 2) },
        { NormalizeLookupKey("Sneak"), new SpriteReference("Speeds", 0) },
        { NormalizeLookupKey("Walk"),  new SpriteReference("Speeds", 1) },
        { NormalizeLookupKey("Run"),   new SpriteReference("Speeds", 2) },

        { NormalizeLookupKey("PlayerControl"),     new SpriteReference("Modes", 0) },
        { NormalizeLookupKey("AIControl"),         new SpriteReference("Modes", 5) },
        { NormalizeLookupKey("Mode_PlayerControl"),new SpriteReference("SpriteSheet_Modes_V3", 0) },
        { NormalizeLookupKey("Mode_Follow"),       new SpriteReference("SpriteSheet_Modes_V3", 1) },
        { NormalizeLookupKey("Mode_Explore"),      new SpriteReference("SpriteSheet_Modes_V3", 2) },
        { NormalizeLookupKey("Mode_Pause"),        new SpriteReference("SpriteSheet_Modes_V3", 3) },
        { NormalizeLookupKey("Mode_Wander"),       new SpriteReference("SpriteSheet_Modes_V3", 4) },
        { NormalizeLookupKey("Mode_LLMControl"),   new SpriteReference("SpriteSheet_Modes_V3", 5) },
        { NormalizeLookupKey("Player"),        new SpriteReference("Modes", 0) },
        { NormalizeLookupKey("Follow"),        new SpriteReference("Modes", 1) },
        { NormalizeLookupKey("Explore"),       new SpriteReference("Modes", 2) },
        { NormalizeLookupKey("Hold"),          new SpriteReference("Modes", 3) },
        { NormalizeLookupKey("Wander"),        new SpriteReference("Modes", 4) },
        { NormalizeLookupKey("LLMControlled"), new SpriteReference("Modes", 5) },
        
        { NormalizeLookupKey("DigHole"),   new SpriteReference("DigHoleSpriteA", 0) },
        { NormalizeLookupKey("DigButton"), new SpriteReference("DigHoleSpriteA", 0) },

        { NormalizeLookupKey("Take_Item"), new SpriteReference("TakeItemSpriteSheetA", 1) },

        { NormalizeLookupKey("Target_Scope_Paw"), new SpriteReference("TargetIcon_C", 0) },
        { NormalizeLookupKey("Target_Scope"),     new SpriteReference("TargetIcon_D", 0) },

        { NormalizeLookupKey("Curved_Arrow_Give"), new SpriteReference("TradeArrows_B", 0) },
        { NormalizeLookupKey("Curved_Arrow_Swap"), new SpriteReference("TradeArrows_B", 1) },
        { NormalizeLookupKey("Curved_Arrow_Take"), new SpriteReference("TradeArrows_B", 2) },
        
        { NormalizeLookupKey("Fetch"),     new SpriteReference("TricksSpritesheet_B", 0) },
        { NormalizeLookupKey("Stay"),      new SpriteReference("TricksSpritesheet_B", 1) },
        { NormalizeLookupKey("Come"),      new SpriteReference("TricksSpritesheet_B", 2) },
        { NormalizeLookupKey("DropIt"),    new SpriteReference("TricksSpritesheet_B", 3) },
        { NormalizeLookupKey("LeaveIt"),   new SpriteReference("TricksSpritesheet_B", 4) },
        { NormalizeLookupKey("Heel"),      new SpriteReference("TricksSpritesheet_B", 5) },
        { NormalizeLookupKey("Quiet"),     new SpriteReference("TricksSpritesheet_B", 6) },
        { NormalizeLookupKey("Bark"),      new SpriteReference("TricksSpritesheet_B", 7) },
        { NormalizeLookupKey("GoodDog"),   new SpriteReference("TricksSpritesheet_B", 8) },
        { NormalizeLookupKey("BadDog"),    new SpriteReference("TricksSpritesheet_B", 9) },
        { NormalizeLookupKey("FindIt"),    new SpriteReference("TricksSpritesheet_B", 10) },
        { NormalizeLookupKey("RollOver"),  new SpriteReference("TricksSpritesheet_B", 11) },
        { NormalizeLookupKey("Sit"),       new SpriteReference("TricksSpritesheet_B", 12) },
        { NormalizeLookupKey("Down"),      new SpriteReference("TricksSpritesheet_B", 13) },
        { NormalizeLookupKey("Release"),   new SpriteReference("TricksSpritesheet_B", 14) },

        { NormalizeLookupKey("Gait_Frame_1row"), new SpriteReference("Gait_Frame_AB", 0) },
        { NormalizeLookupKey("Gait_Frame_2row"), new SpriteReference("Gait_Frame_AB", 1) }
    };

    static readonly Dictionary<string, Dictionary<int, Sprite>> spritesBySheet = new Dictionary<string, Dictionary<int, Sprite>>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Dictionary<string, Sprite>> spritesByNameBySheet = new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Sprite[]> spriteArraysBySheet = new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Sprite> spritesByResourcePath = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    public static Sprite SpriteLookup(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return null;

        string trimmedName = spriteName.Trim();
        string lookupKey = NormalizeLookupKey(trimmedName);

        if (knownSprites.TryGetValue(lookupKey, out SpriteReference knownReference))
        {
            if (knownReference.Index >= 0)
                return SpriteSheetLookup(knownReference.SpriteSheet, knownReference.Index);

            if (lookupKey == NormalizeLookupKey("Scent") || lookupKey == NormalizeLookupKey("Smell"))
                return SpriteLookup("Sense_Smell_None") ?? SpriteLookup("Sense_Smell_Low") ?? SpriteLookup("Sense_Alert_None");
        }

        if (TryGetEmojiSprite(trimmedName, out Sprite emojiSprite, out _))
            return emojiSprite;

        if (trimmedName.StartsWith("Sense_", StringComparison.OrdinalIgnoreCase))
            return SpriteSheetLookupByName("Senses", trimmedName);

        Sprite sheetSprite = SpriteSheetLookup(trimmedName);
        if (sheetSprite != null)
            return sheetSprite;

        string normalizedResourcePath = NormalizeResourcePath(trimmedName);
        int slashIndex = normalizedResourcePath.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < normalizedResourcePath.Length - 1)
        {
            string fileName = normalizedResourcePath.Substring(slashIndex + 1);
            return SpriteLookup(fileName) ?? SpriteResourceLookup(normalizedResourcePath);
        }

        return null;
    }

    public static Sprite SpriteSheetLookup(string spriteSheet, int index = -1)
    {
        if (string.IsNullOrWhiteSpace(spriteSheet))
            return null;

        string sheetName = spriteSheet.Trim();
        if (index < 0 && TrySplitSpriteSheetIndex(sheetName, out string parsedSheetName, out int parsedIndex))
        {
            sheetName = parsedSheetName;
            index = parsedIndex;
        }

        if (index < 0)
            return SpriteSheetLookupByName(sheetName, spriteSheet.Trim());

        Dictionary<int, Sprite> lookup = GetSpritesByIndex(sheetName);
        return lookup.TryGetValue(index, out Sprite sprite) ? sprite : null;
    }

    public static Sprite SpriteSheetLookupByName(string spriteSheet, string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteSheet) || string.IsNullOrWhiteSpace(spriteName))
            return null;

        Dictionary<string, Sprite> lookup = GetSpritesByName(spriteSheet);
        return lookup.TryGetValue(spriteName.Trim(), out Sprite sprite) ? sprite : null;
    }

    public static Sprite SpriteResourceLookup(string resourcePath, float pixelsPerUnit = 100f)
    {
        return SpriteResourceLookup(resourcePath, false, pixelsPerUnit);
    }

    public static Sprite SpriteResourceLookup(string resourcePath, bool useTopHalf, float pixelsPerUnit = 100f)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        string normalizedPath = NormalizeResourcePath(resourcePath);
        string cacheKey = $"{normalizedPath}|topHalf:{useTopHalf}|ppu:{pixelsPerUnit}";
        if (spritesByResourcePath.TryGetValue(cacheKey, out Sprite cachedSprite))
            return cachedSprite;

        Sprite sprite = null;
        if (!useTopHalf)
            sprite = Resources.Load<Sprite>(normalizedPath);

        if (sprite == null && useTopHalf)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(normalizedPath);
            if (sprites != null && sprites.Length > 0)
                sprite = sprites[0];
        }

        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(normalizedPath);
            if (texture != null)
            {
                Rect spriteRect = useTopHalf
                    ? new Rect(0f, texture.height * 0.5f, texture.width, texture.height * 0.5f)
                    : new Rect(0f, 0f, texture.width, texture.height);

                sprite = Sprite.Create(
                    texture,
                    spriteRect,
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                sprite.name = useTopHalf ? $"{texture.name}_0" : texture.name;
            }
        }

        if (sprite != null)
            spritesByResourcePath[cacheKey] = sprite;

        return sprite;
    }

    public static bool TryGetEmojiSprite(string emote, out Sprite sprite, out string displayName)
    {
        sprite = null;
        displayName = FormatSpriteDisplayName(emote);

        if (!TryResolveEmojiEntry(emote, out DogEmojiEntry entry))
            return false;

        displayName = entry.Name;
        sprite = SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
        return sprite != null;
    }

    public static Dictionary<int, Sprite> GetSpriteSheet(string spriteSheet)
    {
        return new Dictionary<int, Sprite>(GetSpritesByIndex(spriteSheet));
    }

    public static Sprite[] GetSpriteSheetSprites(string spriteSheet)
    {
        string resourcePath = ResolveSpriteSheetResourcePath(spriteSheet);
        if (!spriteArraysBySheet.ContainsKey(resourcePath))
            LoadSpriteSheet(resourcePath);

        return spriteArraysBySheet.TryGetValue(resourcePath, out Sprite[] sprites)
            ? (Sprite[])sprites.Clone()
            : Array.Empty<Sprite>();
    }

    public static int GetSpriteSheetIndex(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return -1;

        int separatorIndex = spriteName.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex >= spriteName.Length - 1)
            return -1;

        return int.TryParse(spriteName.Substring(separatorIndex + 1), out int index)
            ? index
            : -1;
    }

    public static string NormalizeResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return string.Empty;

        resourcePath = resourcePath.Trim().Replace("\\", "/");

        int extensionIndex = resourcePath.LastIndexOf(".", StringComparison.Ordinal);
        if (extensionIndex >= 0)
            resourcePath = resourcePath.Substring(0, extensionIndex);

        const string resourcesToken = "/Resources/";
        int resourcesIndex = resourcePath.IndexOf(resourcesToken, StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex >= 0)
            resourcePath = resourcePath.Substring(resourcesIndex + resourcesToken.Length);

        return resourcePath.Trim('/');
    }

    static Dictionary<int, Sprite> GetSpritesByIndex(string spriteSheet)
    {
        string resourcePath = ResolveSpriteSheetResourcePath(spriteSheet);
        if (spritesBySheet.TryGetValue(resourcePath, out Dictionary<int, Sprite> lookup))
            return lookup;

        LoadSpriteSheet(resourcePath);
        return spritesBySheet.TryGetValue(resourcePath, out lookup)
            ? lookup
            : new Dictionary<int, Sprite>();
    }

    static Dictionary<string, Sprite> GetSpritesByName(string spriteSheet)
    {
        string resourcePath = ResolveSpriteSheetResourcePath(spriteSheet);
        if (spritesByNameBySheet.TryGetValue(resourcePath, out Dictionary<string, Sprite> lookup))
            return lookup;

        LoadSpriteSheet(resourcePath);
        return spritesByNameBySheet.TryGetValue(resourcePath, out lookup)
            ? lookup
            : new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    }

    static void LoadSpriteSheet(string resourcePath)
    {
        string normalizedPath = NormalizeResourcePath(resourcePath);
        Dictionary<int, Sprite> spritesByIndex = new Dictionary<int, Sprite>();
        Dictionary<string, Sprite> spritesByName = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        Sprite[] sprites = Resources.LoadAll<Sprite>(normalizedPath);
        Sprite firstSprite = null;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            if (firstSprite == null)
                firstSprite = sprite;

            spritesByName[sprite.name] = sprite;

            int index = GetSpriteSheetIndex(sprite.name);
            if (index >= 0)
                spritesByIndex[index] = sprite;
        }

        if (!spritesByIndex.ContainsKey(0) && firstSprite != null)
            spritesByIndex[0] = firstSprite;

        spritesBySheet[normalizedPath] = spritesByIndex;
        spritesByNameBySheet[normalizedPath] = spritesByName;
        spriteArraysBySheet[normalizedPath] = sprites;
    }

    static string ResolveSpriteSheetResourcePath(string spriteSheet)
    {
        if (string.IsNullOrWhiteSpace(spriteSheet))
            return string.Empty;

        string normalized = NormalizeResourcePath(spriteSheet);
        if (spriteSheetResourcePaths.TryGetValue(normalized, out string resourcePath))
            return resourcePath;

        string fileName = normalized;
        int slashIndex = fileName.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < fileName.Length - 1)
            fileName = fileName.Substring(slashIndex + 1);

        if (spriteSheetResourcePaths.TryGetValue(fileName, out resourcePath))
            return resourcePath;

        return normalized.Contains("/")
            ? normalized
            : $"Sprites/{normalized}";
    }

    static bool TrySplitSpriteSheetIndex(string spriteName, out string spriteSheet, out int index)
    {
        spriteSheet = spriteName;
        index = -1;

        int separatorIndex = spriteName.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex >= spriteName.Length - 1)
            return false;

        if (!int.TryParse(spriteName.Substring(separatorIndex + 1), out index))
            return false;

        spriteSheet = spriteName.Substring(0, separatorIndex);
        return true;
    }

    static bool TryResolveEmojiEntry(string emote, out DogEmojiEntry entry)
    {
        string normalized = NormalizeLookupKey(emote);
        string aliased = ResolveEmojiAlias(normalized);

        for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
        {
            DogEmojiEntry candidate = DogEmojiCatalog.Entries[i];
            if (NormalizeLookupKey(candidate.EntryId) == normalized ||
                NormalizeLookupKey(candidate.Name) == aliased)
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    static string ResolveEmojiAlias(string normalized)
    {
        switch (normalized)
        {
            case "friendly":
                return "happy";
            case "fearful":
                return "afraid";
            case "tilthead":
                return "curious";
            case "dig":
                return "determined";
            case "stay":
                return "alert";
            case "setuptrap":
                return "sneaky";
            case "scratch":
                return "annoyed";
            default:
                return normalized;
        }
    }

    static string NormalizeLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
    }

    static string FormatSpriteDisplayName(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return "Sprite";

        string spaced = spriteName.Trim().Replace("_", " ").Replace("-", " ");
        return char.ToUpperInvariant(spaced[0]) + (spaced.Length > 1 ? spaced.Substring(1) : string.Empty);
    }
}
