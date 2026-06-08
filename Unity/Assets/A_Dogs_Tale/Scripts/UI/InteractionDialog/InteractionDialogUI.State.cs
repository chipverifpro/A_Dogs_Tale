using DogGame;
using UnityEngine;

public sealed partial class InteractionDialogUI : MonoBehaviour
{
    #region Fields

    private readonly SharedInteractionTabState sharedState = new();

    private readonly ItemsInteractionTabState itemsState = new();

    private readonly PackInteractionTabState packState = new();

    private readonly SocialInteractionTabState socialState = new();

    private readonly QuestsInteractionTabState questsState = new();

    private readonly ScentInteractionTabState scentState = new();

    #endregion

    #region Nested Types

    private sealed class SharedInteractionTabState
    {
        public int SelectedPlayerAgentIndex;

        public WorldObject DisplayedPlayer;

        public WorldObject PendingLeftAgentSelection;

        public WorldObject PendingRightAgentSelection;

        public InteractionTab CurrentTab = InteractionTab.Items;
    }

    private sealed class ItemsInteractionTabState
    {
        public int SelectedPlayerItemIndex;

        public int SelectedTargetAgentIndex;

        public int SelectedTargetItemIndex;

        public WorldObject DisplayedPlayerItem;

        public WorldObject DisplayedTarget;

        public WorldObject DisplayedTargetItem;

        public bool PackHeldItemListDirty = true;

        public WorldObject DisplayedPackHeldItemSelectedAgent;

        public WorldObject DisplayedPackHeldItemSelectedItem;
    }

    private sealed class PackInteractionTabState
    {
        public int SelectedLeftIndex;

        public int SelectedRightIndex = 1;

        public WorldObject DisplayedLeft;

        public WorldObject DisplayedRight;
    }

    private sealed class SocialInteractionTabState
    {
        public int SelectedTargetIndex;

        public WorldObject DisplayedLeft;

        public WorldObject DisplayedRight;

        public bool DisplayedEmoteGridUsesHuman;

        public bool DisplayedEmoteGridInitialized;
    }

    private sealed class QuestsInteractionTabState
    {
        public int SelectedTargetIndex;

        public WorldObject DisplayedLeft;

        public WorldObject DisplayedRight;

        public bool InteractionQuestListDirty = true;
    }

    private sealed class ScentInteractionTabState
    {
        public int DisplayedSourceListSelectedIndex = -1;

        public int DisplayedSourceListOptionCount = -1;

        public WorldObject DisplayedSourceListFirst;

        public WorldObject DisplayedSourceListLast;

        public int SelectedTargetIndex;

        public WorldObject DisplayedLeft;

        public WorldObject DisplayedRight;
    }

    #endregion
}
