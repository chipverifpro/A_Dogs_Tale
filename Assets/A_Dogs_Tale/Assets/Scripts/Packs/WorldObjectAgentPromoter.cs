using System;
using UnityEngine;
using DogGame.Modules;
using DogGame.AI;



    [System.Serializable]
    public struct PromoteToPackMemberOptions
    {
        public bool addColliderIfMissing;      // for clicking
        public bool addRigidBodyIfMissing;     // usually false for static props
        public bool enableNavAgentIfPresent;   // if your motion uses NavMeshAgent
        public bool setFollowerDefaults;

        public static PromoteToPackMemberOptions Defaults => new PromoteToPackMemberOptions
        {
            addColliderIfMissing = false,
            addRigidBodyIfMissing = false,
            enableNavAgentIfPresent = true,
            setFollowerDefaults = true
        };
    }


    public static class WorldObjectAgentPromoter
    {
        static Directory dir;

        public static bool PromoteToFollower(GameObject targetObject, PromoteToPackMemberOptions options)
        {
            if (dir == null) dir = Directory.Instance;

            if (targetObject == null) return false;

            // 1) Ensure clickability if desired (optional)
            if (options.addColliderIfMissing && targetObject.GetComponent<Collider>() == null)
            {
                // Conservative default: BoxCollider sized to render bounds (rough)
                var box = targetObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }

            if (options.addRigidBodyIfMissing && targetObject.GetComponent<Rigidbody>() == null)
            {
                var rb = targetObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // 2) Ensure your agent root module exists
            // Rename these types to match your project:
            var worldObject = EnsureComponent<WorldObject>(targetObject);

            if (worldObject==null) Debug.LogWarning($"WorldObject = {worldObject}");
            
            ModuleFlags enables = ModuleFlagsTemplates.FullAgent;
            worldObject.CreateModulesIfNeeded(enables);

            // pre-create these before agentModule
        //    EnsureComponent<FollowerDecisionModule>(targetObject); // concrete, non-abstract
        //    EnsureComponent<PlayerDecisionModule>(targetObject); // concrete, non-abstract
        //    EnsureComponent<WandererDecisionModule>(targetObject); // concrete, non-abstract
             
            // 3) Ensure required sub-modules exist
        //    var motionModule      = EnsureComponent<MotionModule>(targetObject);
        //    if (motionModule==null) Debug.LogWarning($"motionModule = {motionModule}");
        //    var agentMovementModule  = EnsureComponent<AgentMovementModule>(targetObject);
        //    if (agentMovementModule==null) Debug.LogWarning($"agentMovementModule = {agentMovementModule}");
        //    var motivationModule  = EnsureComponent<MotivationModule>(targetObject);
        //    if (motivationModule==null) Debug.LogWarning($"motivationModule = {motivationModule}");
        //    var packMemberModule  = EnsureComponent<AgentPackMemberModule>(targetObject);
        //    if (packMemberModule==null) Debug.LogWarning($"packMemberModule = {packMemberModule}");

        //    var agentModule = EnsureComponent<AgentModule>(targetObject);
        //    if (agentModule==null) Debug.LogWarning($"agentModule = {agentModule}");

        //    Debug.LogWarning($"agentModule = {agentModule}, dir={agentModule.dir}, worldObject={agentModule.worldObject}");
            
        //    if (agentModule.dir == null) dir = Directory.Instance;
        //    if (motionModule.dir == null) dir = Directory.Instance;
        //    if (packMemberModule.dir == null) dir = Directory.Instance;
        //    if (motivationModule.dir == null) dir = Directory.Instance;

        //    Debug.LogWarning($"agentModule = {agentModule}, dir={agentModule.dir}, worldObject={agentModule.worldObject}");
            
            // 4) Enable/initialize minimal defaults
            if (options.setFollowerDefaults)
            {
                ApplyFollowerDefaults(worldObject);
            }

            // 5) If you have NavMeshAgent-based motion, enable it if present
        //    if (options.enableNavAgentIfPresent)
        //    {
        //        var navAgent = targetObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
        //        if (navAgent != null) navAgent.enabled = true;
        //    }

            // 6) Finally, join the pack as follower
            // (You’ll implement this in your pack system)
            if (!TryJoinPackAsFollower(worldObject.agentModule))
                return false;

            return true;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null) return component;
            
            Type componentType = typeof(T);
            
            // Prevent Unity from trying to add abstract behaviours
            if (typeof(MonoBehaviour).IsAssignableFrom(componentType) && componentType.IsAbstract)
            {
                Debug.Log(
                    $"Cannot AddComponent for abstract MonoBehaviour type '{componentType.Name}'. " +
                    $"You must add a concrete subclass instead.",
                    go);
                return null;
            }
            return go.AddComponent<T>();
        }

        private static void ApplyFollowerDefaults(WorldObject wo)
        {
            Debug.Log("Applying follower defaults");
            // Safe defaults: avoid null refs / weird behavior.
            wo.agentModule.enabled = true;

            // Decision module selection should NOT use "new" if it's a MonoBehaviour.
            // Make decision modules Components or ScriptableObjects (your call).
            //packMemberModule.JoinPack(dir.playerPack, false);

            wo.motionModule.motionControlMode = DogGame.Modules.MotionControlMode.Autopilot;
            wo.motionModule.facingMode = DogGame.Modules.FacingMode.FaceMovementDirection;

            // Motivation defaults: mild pack pull, high distraction tolerance
            wo.motivationModule.trainingProfile.obedience = 0.35f;
            wo.motivationModule.trainingProfile.focus     = 0.35f;

            // packMemberModule.role = PackRole.Follower;
            //wo.agentPackMemberModule.currentPack = dir.playerPack;

            // Mark debug agents clearly
            wo.agentModule.agentName = $"{wo.agentModule.agentName} (DEBUG Follower)";
        }

        private static bool TryJoinPackAsFollower(DogGame.Modules.AgentModule agentModule)
        {
            // Replace this with your actual access path to the pack system.
            // E.g. agentModule.dir.worldModule.packSystem.JoinAsFollower(agentModule)
            var packMemberModule = agentModule.worldObject.agentPackMemberModule;
            
            if (packMemberModule == null)
            {
                Debug.LogError("packMemberModule not found in scene; cannot join pack.");
                return false;
            }

            Debug.Log($"Joining pack as follower: {agentModule.agentName}");
            Debug.Log($"Joining to pack {dir}");
            Debug.Log($"Pack = {dir.packManager.playerPack}");
            Debug.Log($"PackLeader = {dir.packManager.playerPack.packLeader.DisplayName}");
            Debug.Log($"Joining to pack {dir.playerPack}");
            Debug.Log($"Joining to pack {dir.playerPack.packLeader.DisplayName}");
            Debug.Log($"Joining to pack {dir.playerPack.packName}");
            //packSystem.JoinAsFollower(agentModule);
            packMemberModule.JoinPack(dir.playerPack, false);
            
            return true;
        }

/*
        public static bool PromoteToFollower_SAFE(GameObject targetObject, PromoteToPackMemberOptions options)
        {
            if (targetObject == null)
            {
                Debug.LogError("PromoteToFollower: targetObject is null.");
                return false;
            }

            // Ensure core modules
            var worldObject = EnsureComponent<WorldObject>(targetObject);
            if (worldObject == null)
            {
                Debug.LogError($"PromoteToFollower: Failed to ensure WorldObject on '{targetObject.name}'.", targetObject);
                return false;
            }

            var agentModule = EnsureComponent<AgentModule>(targetObject);
            if (agentModule == null)
            {
                Debug.LogError($"PromoteToFollower: Failed to ensure AgentModule on '{targetObject.name}'.", targetObject);
                return false;
            }

            var motionModule = EnsureComponent<MotionModule>(targetObject);
            if (motionModule == null)
            {
                Debug.LogError($"PromoteToFollower: Failed to ensure MotionModule on '{targetObject.name}'.", targetObject);
                return false;
            }

            var motivationModule = EnsureComponent<MotivationModule>(targetObject);
            if (motivationModule == null)
            {
                Debug.LogError($"PromoteToFollower: Failed to ensure MotivationModule on '{targetObject.name}'.", targetObject);
                return false;
            }

            var packMemberModule = EnsureComponent<PackMemberModule>(targetObject);
            if (packMemberModule == null)
            {
                Debug.LogError($"PromoteToFollower: Failed to ensure PackMemberModule on '{targetObject.name}'.", targetObject);
                return false;
            }

            // SAFEST: apply defaults in a way that cannot NRE
            ApplyFollowerDefaultsSafe(targetObject, agentModule, motionModule, motivationModule, packMemberModule);

            // Join pack
            var packSystem = Object.FindFirstObjectByType<PackSystem>();
            if (packSystem == null)
            {
                Debug.LogError("PromoteToFollower: PackSystem not found in scene.");
                return false;
            }

            packSystem.JoinAsFollower(agentModule);
            return true;
        }

        private static void ApplyFollowerDefaultsSafe(
            GameObject targetObject,
            DogGame.Modules.AgentModule agentModule,
            DogGame.Modules.MotionModule motionModule,
            DogGame.Modules.MotivationModule motivationModule,
            DogGame.Modules.PackMemberModule packMemberModule)
        {
            // Never assume optional fields exist; keep it bulletproof.

            // Pack role
            packMemberModule.role = DogGame.Modules.PackRole.Follower;

            // Motion defaults (only if enums/fields exist)
            motionModule.motionControlMode = DogGame.Modules.MotionControlMode.Autopilot;
            motionModule.facingMode = DogGame.Modules.FacingMode.FaceMovementDirection;

            // Training defaults
            motivationModule.trainingProfile.obedience = 0.35f;
            motivationModule.trainingProfile.focus = 0.35f;

            // Name tag: don't assume agentModule.agentName is initialized
            if (!string.IsNullOrEmpty(targetObject.name) && agentModule != null)
            {
                // If you have agentModule.agentName, set it; otherwise skip.
                // Example guarded set:
                // agentModule.agentName = $"{targetObject.name} (DEBUG Follower)";
            }
        }
        */
    }
