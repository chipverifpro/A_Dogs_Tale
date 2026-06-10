using System.Collections.Generic;
using UnityEngine;
using InspectorTools;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    [InspectorNote("Thing_Modules/Container Module", "Stores and manages held WorldObjects.")]
    public class ContainerModule : WorldModule
    {
        [Header("Capacity")]
        [Min(0)] public int itemCapacity = 1;
        [Min(0f)] public float maxWeight = 10f;

        [Header("Held Item Presentation")]
        public bool heldItemsVisible = true;
        public float heldHeight = 0f;

        [Header("Access State")]
        public bool isLocked = false;
        public bool isClosed = false;

        [Header("Held Items")]
        [SerializeField] private List<WorldObject> heldItems = new();

        [Header("Agent Auto Pickup")]
        public bool autoPickupNearbyItems = true;
        [Min(0f)] public float pickupRadiusTiles = 1f;
        public bool autoConfigureAgentCapacity = true;
        [Min(0)] public int dogItemCapacity = 1;
        [Min(0)] public int humanItemCapacity = 2;

        private readonly HashSet<WorldObject> autoPickupSuppressedItems = new();
        private readonly List<WorldObject> autoPickupSuppressionPruneBuffer = new();

        public IReadOnlyList<WorldObject> HeldItems => heldItems;
        public int HeldItemCount => heldItems.Count;
        public bool IsFull => heldItems.Count >= itemCapacity;
        public bool IsLocked => isLocked;
        public bool IsClosed => isClosed;

        public float CurrentWeight
        {
            get
            {
                float totalWeight = 0f;
                for (int i = 0; i < heldItems.Count; i++)
                {
                    WorldObject item = heldItems[i];
                    if (item == null)
                        continue;

                    totalWeight += item.Weight;
                }

                return totalWeight;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            ConfigureAgentCapacityIfNeeded();
            SanitizeHeldItems();
            //if (CanModifyHeldItemTransforms())
            //    RefreshHeldItems();
        }

        private void OnValidate()
        {
            itemCapacity = Mathf.Max(0, itemCapacity);
            maxWeight = Mathf.Max(0f, maxWeight);
            pickupRadiusTiles = Mathf.Max(0f, pickupRadiusTiles);
            dogItemCapacity = Mathf.Max(0, dogItemCapacity);
            humanItemCapacity = Mathf.Max(0, humanItemCapacity);

            SanitizeHeldItems();
            //if (!Application.isPlaying)
            //    RefreshHeldItems();       // will be done in Awake()
        }

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            TryAutoPickupNearbyItem();
        }

        public bool CanAccessContents(out string reason)
        {
            if (isLocked)
            {
                reason = $"{worldObject.DisplayName} is locked.";
                return false;
            }

            if (isClosed)
            {
                reason = $"{worldObject.DisplayName} is closed.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool CanReceiveItem(WorldObject item, out string reason)
        {
            return CanReceiveItem(item, true, out reason);
        }

        public bool CanReceiveItem(WorldObject item, bool enforceMaxWeight, out string reason)
        {
            if (!CanAccessContents(out reason))
                return false;

            if (item == null)
            {
                reason = "Cannot receive a null item.";
                return false;
            }

            if (item == worldObject)
            {
                reason = "A container cannot hold itself.";
                return false;
            }

            if (heldItems.Contains(item))
            {
                reason = $"{item.DisplayName} is already in {worldObject.DisplayName}.";
                return false;
            }

            if (heldItems.Count >= itemCapacity)
            {
                reason = $"{worldObject.DisplayName} is full.";
                return false;
            }

            if (enforceMaxWeight && CurrentWeight + item.Weight > maxWeight)
            {
                reason = $"{item.DisplayName} would exceed the max weight of {maxWeight}.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool ReceiveItem(WorldObject item)
        {
            return ReceiveItem(item, out _);
        }

        public bool ReceiveItem(WorldObject item, out string reason)
        {
            return ReceiveItem(item, true, out reason);
        }

        public bool ReceiveItem(WorldObject item, bool enforceMaxWeight, out string reason)
        {
            SanitizeHeldItems();

            if (!CanReceiveItem(item, enforceMaxWeight, out reason))
                return false;

            heldItems.Add(item);
            autoPickupSuppressedItems.Remove(item);
            ApplyHeldItemState(item);
            return true;
        }

        public bool ReleaseItem(WorldObject item)
        {
            return ReleaseItem(item, out _);
        }

        public bool ReleaseItem(WorldObject item, out string reason)
        {
            SanitizeHeldItems();

            if (!CanAccessContents(out reason))
                return false;

            if (item == null)
            {
                reason = "Cannot release a null item.";
                return false;
            }

            if (!heldItems.Remove(item))
            {
                reason = $"{item.DisplayName} is not held by {worldObject.DisplayName}.";
                return false;
            }

            ReleaseHeldItemState(item);
            reason = string.Empty;
            return true;
        }

        public bool DropItemOnGround(WorldObject item, Vector3 worldPosition, out string reason)
        {
            if (!ReleaseItem(item, out reason))
                return false;

            item.transform.position = worldPosition;
            item.transform.SetParent(null, true);
            SetItemVisible(item, true);
            SetItemCollidersEnabled(item, true);
            SuppressAutoPickupUntilSeparated(item);

            reason = string.Empty;
            return true;
        }

        public WorldObject ReleaseItemAt(int index)
        {
            if (!CanAccessContents(out _))
                return null;

            SanitizeHeldItems();

            if (index < 0 || index >= heldItems.Count)
                return null;

            WorldObject item = heldItems[index];
            heldItems.RemoveAt(index);
            ReleaseHeldItemState(item);
            return item;
        }

        public bool ExchangeItem(WorldObject itemToRelease, WorldObject itemToReceive)
        {
            return ExchangeItem(itemToRelease, itemToReceive, out _);
        }

        public bool ExchangeItem(WorldObject itemToRelease, WorldObject itemToReceive, out string reason)
        {
            SanitizeHeldItems();

            if (!CanAccessContents(out reason))
                return false;

            if (itemToRelease == null)
            {
                reason = "Cannot exchange out a null item.";
                return false;
            }

            if (itemToReceive == null)
            {
                reason = "Cannot exchange in a null item.";
                return false;
            }

            int releaseIndex = heldItems.IndexOf(itemToRelease);
            if (releaseIndex < 0)
            {
                reason = $"{itemToRelease.DisplayName} is not held by {worldObject.DisplayName}.";
                return false;
            }

            heldItems.RemoveAt(releaseIndex);
            ReleaseHeldItemState(itemToRelease);

            if (ReceiveItem(itemToReceive, out reason))
                return true;

            heldItems.Insert(Mathf.Clamp(releaseIndex, 0, heldItems.Count), itemToRelease);
            ApplyHeldItemState(itemToRelease);
            return false;
        }

        public bool ActivateHeldItem(int index = 0)
        {
            SanitizeHeldItems();

            if (!CanAccessContents(out _))
                return false;

            if (index < 0 || index >= heldItems.Count)
                return false;

            WorldObject item = heldItems[index];
            if (item == null)
                return false;

            item.OnActivate();
            return true;
        }

        public bool ActivateHeldItem(WorldObject item)
        {
            SanitizeHeldItems();

            if (!CanAccessContents(out _))
                return false;

            if (item == null || !heldItems.Contains(item))
                return false;

            item.OnActivate();
            return true;
        }

        public bool TryPickupNearestItem(out WorldObject pickedUpItem, out string reason)
        {
            pickedUpItem = null;

            if (!IsAgentContainer())
            {
                reason = $"{worldObject.DisplayName} cannot pick up items.";
                return false;
            }

            if (IsFull)
            {
                reason = $"{worldObject.DisplayName} cannot carry any more items.";
                return false;
            }

            if (!CanAccessContents(out reason))
                return false;

            WorldObject nearestItem = FindNearestPickupItem();
            if (nearestItem == null)
            {
                reason = "No item close enough to pick up.";
                return false;
            }

            if (!ReceiveItem(nearestItem, false, out reason))
                return false;

            pickedUpItem = nearestItem;
            reason = string.Empty;
            return true;
        }

        public bool ContainsItem(WorldObject item)
        {
            return item != null && heldItems.Contains(item);
        }

        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }

        public void SetClosed(bool closed)
        {
            isClosed = closed;
        }

        public void ApplySavedContainerState(
            int savedItemCapacity,
            float savedMaxWeight,
            bool savedHeldItemsVisible,
            float savedHeldHeight,
            bool savedIsLocked,
            bool savedIsClosed,
            bool savedAutoPickupNearbyItems,
            float savedPickupRadiusTiles,
            bool savedAutoConfigureAgentCapacity,
            int savedDogItemCapacity,
            int savedHumanItemCapacity)
        {
            itemCapacity = Mathf.Max(0, savedItemCapacity);
            maxWeight = Mathf.Max(0f, savedMaxWeight);
            heldItemsVisible = savedHeldItemsVisible;
            heldHeight = savedHeldHeight;
            isLocked = savedIsLocked;
            isClosed = savedIsClosed;
            autoPickupNearbyItems = savedAutoPickupNearbyItems;
            pickupRadiusTiles = Mathf.Max(0f, savedPickupRadiusTiles);
            autoConfigureAgentCapacity = savedAutoConfigureAgentCapacity;
            dogItemCapacity = Mathf.Max(0, savedDogItemCapacity);
            humanItemCapacity = Mathf.Max(0, savedHumanItemCapacity);
        }

        public void RestoreSavedContents(List<WorldObject> savedHeldItems)
        {
            for (int i = 0; i < heldItems.Count; i++)
                ReleaseHeldItemState(heldItems[i]);

            heldItems.Clear();

            if (savedHeldItems == null)
                return;

            for (int i = 0; i < savedHeldItems.Count; i++)
            {
                WorldObject item = savedHeldItems[i];
                if (item == null || item == worldObject || heldItems.Contains(item))
                    continue;

                heldItems.Add(item);
                ApplyHeldItemState(item);
            }
        }

        public void RefreshHeldItems()
        {
            if (!CanModifyHeldItemTransforms())
                return;

            for (int i = 0; i < heldItems.Count; i++)
            {
                WorldObject item = heldItems[i];
                if (item == null)
                    continue;

                ApplyHeldItemState(item);
            }
        }

        private void SanitizeHeldItems()
        {
            heldItems.RemoveAll(item => item == null);

            if (itemCapacity <= 0 && heldItems.Count > 0)
            {
                for (int i = 0; i < heldItems.Count; i++)
                    ReleaseHeldItemState(heldItems[i]);

                heldItems.Clear();
            }

            while (heldItems.Count > itemCapacity)
            {
                WorldObject overflowItem = heldItems[heldItems.Count - 1];
                heldItems.RemoveAt(heldItems.Count - 1);
                ReleaseHeldItemState(overflowItem);
            }

            while (heldItems.Count > 0 && CurrentWeight > maxWeight)
            {
                WorldObject overflowItem = heldItems[heldItems.Count - 1];
                heldItems.RemoveAt(heldItems.Count - 1);
                ReleaseHeldItemState(overflowItem);
            }
        }

        private void ApplyHeldItemState(WorldObject item)
        {
            if (item == null || !CanModifyHeldItemTransforms())
                return;

            item.transform.SetParent(transform, false);
            item.transform.localPosition = Vector3.up * heldHeight;
            SetItemVisible(item, heldItemsVisible);
        }

        private void ReleaseHeldItemState(WorldObject item)
        {
            if (item == null || !CanModifyHeldItemTransforms())
                return;

            if (item.transform.parent == transform)
                item.transform.SetParent(null, true);

            SetItemVisible(item, true);
        }

        private static void SetItemVisible(WorldObject item, bool visible)
        {
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visible;
        }

        private static void SetItemCollidersEnabled(WorldObject item, bool enabled)
        {
            Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = enabled;
        }

        private bool CanModifyHeldItemTransforms()
        {
            return gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private void ConfigureAgentCapacityIfNeeded()
        {
            if (!autoConfigureAgentCapacity || !IsAgentContainer())
                return;

            AppearanceModule appearanceModule = GetComponent<AppearanceModule>();
            itemCapacity = appearanceModule != null &&
                           appearanceModule.animationVersion == AppearanceModule.AnimationVersion.human
                ? humanItemCapacity
                : dogItemCapacity;
        }

        private void TryAutoPickupNearbyItem()
        {
            if (!Application.isPlaying || !autoPickupNearbyItems)
                return;

            if (!IsAgentContainer() || IsFull)
                return;

            if (!CanAccessContents(out _))
                return;

            if (!TryPickupNearestItem(out WorldObject pickedUpItem, out _))
                return;

            BottomBanner.LogAgentInventoryMessage(worldObject, $"{worldObject.DisplayName} picked up {pickedUpItem.DisplayName}");
        }

        private WorldObject FindNearestPickupItem()
        {
            WorldObjectRegistry registry = WorldObjectRegistry.Instance;
            if (registry == null)
                return null;

            float pickupRadiusSqr = pickupRadiusTiles * pickupRadiusTiles;
            Vector3 agentPosition = worldObject.pos3d_map;
            PruneAutoPickupSuppression(agentPosition, pickupRadiusSqr);

            WorldObject nearestItem = null;
            float nearestDistanceSqr = float.PositiveInfinity;

            foreach (WorldObject candidate in registry.GetAllObjects())
            {
                if (!CanAutoPickupCandidate(candidate))
                    continue;

                Vector3 delta = candidate.pos3d_map - agentPosition;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > pickupRadiusSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = distanceSqr;
                nearestItem = candidate;
            }

            return nearestItem;
        }

        private bool CanAutoPickupCandidate(WorldObject candidate)
        {
            if (candidate == null || candidate == worldObject)
                return false;

            if (candidate.Kind != WorldObjectKind.Item)
                return false;

            if (!candidate.gameObject.activeInHierarchy)
                return false;

            if (autoPickupSuppressedItems.Contains(candidate))
                return false;

            if (candidate.transform.parent != null &&
                candidate.transform.parent.GetComponentInParent<WorldObject>() != null)
            {
                return false;
            }

            return !ContainsItem(candidate);
        }

        private void SuppressAutoPickupUntilSeparated(WorldObject item)
        {
            if (item != null)
                autoPickupSuppressedItems.Add(item);
        }

        private void PruneAutoPickupSuppression(Vector3 agentPosition, float pickupRadiusSqr)
        {
            if (autoPickupSuppressedItems.Count == 0)
                return;

            autoPickupSuppressionPruneBuffer.Clear();
            foreach (WorldObject item in autoPickupSuppressedItems)
            {
                if (item == null || !item.gameObject.activeInHierarchy)
                {
                    autoPickupSuppressionPruneBuffer.Add(item);
                    continue;
                }

                Vector3 delta = item.pos3d_map - agentPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude > pickupRadiusSqr)
                    autoPickupSuppressionPruneBuffer.Add(item);
            }

            for (int i = 0; i < autoPickupSuppressionPruneBuffer.Count; i++)
                autoPickupSuppressedItems.Remove(autoPickupSuppressionPruneBuffer[i]);

            autoPickupSuppressionPruneBuffer.Clear();
        }

        private bool IsAgentContainer()
        {
            return worldObject != null &&
                   (worldObject.Kind == WorldObjectKind.Agent || GetComponent<AgentModule>() != null);
        }
    }
}
