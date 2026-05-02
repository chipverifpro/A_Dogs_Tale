using UnityEngine;
using DogGame.AI;
using DogGame.LLM;  // if your AgentDecisionModuleBase lives here
// using DogGame.World; // if you need WorldObject, etc.
using DogGame.Tasks;
using DogGame.LLM.Execution;
using DogGame.LLM.Agent;
using System.Threading;
using DogGame.LLM.Core;
using System;
using Unity.Tutorials.Core.Editor;
using InspectorTools;

namespace DogGame.Modules
{
    [RequireComponent(typeof(TaskController))]
    [RequireComponent(typeof(LLMAgentFacade))]
    [InspectorNote("AgentDecision_Modules/Player Decision Module", "Manual control of this Agent.")]
    public class PlayerDecisionModule : AgentDecisionModuleBase
    {
        //    [Header("Input Source")]
        //    [Tooltip("Component that provides PlayerInputState (e.g. NewInputAdapter). Must implement IPlayerInputSource.")]
        //    [SerializeField] private MonoBehaviour inputSourceBehaviour;

        //    private IPlayerInputSource inputSource;
        public override AgentDecisionType DecisionType => AgentDecisionType.Player;

        private PlayerInputState inputState;   // a pointer, not a local copy
        private GameInputRouter gameInputRouter;
        private bool llmThinkSubscribed;

        [Header("Movement")]
        [SerializeField] private float manualTurnDegreesPerSecond = 240f;

        [Header("Camera Control")]
        [SerializeField] private Camera cameraForMovement;
        [SerializeField] private CameraModeSwitcher cameraModeSwitcher;
    
        //public NavigationSource navigationSource;     // moved to AgentDecisionModuleBase
        //public MotionControlMode motionControlMode;   // moved to MotionModule

        // these store the last given instructions regarding where to go.
        public Vector3? currentDestinationPosition = null;
 
        public WorldObject currentDestinationObject = null;
        public Vector3? currentManualWorldMoveDir = null;
        
        [Header("LLM Components")]
        public TaskController taskController = null;

        //public float thinkIntervalSeconds = 10f;
        //private float nextThinkTime = 0f;

        #region Unity

        protected override void Awake()
        {
            if (taskController == null)
                taskController = GetComponentInParent<TaskController>();
            
            llmConfig ??= worldObject.llmConfigModule;
            llmWorldState ??= worldObject.llmWorldStateModule;
            //llmWorldScheduler = dir.llmWorldScheduler;
        }

        private void OnEnable()
        {
            TryBindRuntimeReferences(logFailure: false);
            TrySubscribeToThinkModule();
        }

        private void Start()
        {
            TryBindRuntimeReferences(logFailure: true);
        }

        // Initialize called from WorldObject.Awake phase
        public override void Initialize(AgentModule agent)
        {
            base.Initialize(agent);
            //if (worldObject==null)
            //{
                //worldObject = GetComponent<WorldObject>();
                if (worldObject == null)
                    Debug.LogError($"[PlayerDecisionModule] could not get worldObject.");
            //}
            //if (inputAdapter == null)
            //    inputAdapter = FindFirstObjectByType<NewInputAdapter>();

            TryBindRuntimeReferences(logFailure: true);

            if (worldObject.agentMovementModule == null)
            {
                Debug.LogError($"[PlayerDecisionModule {worldObject.DisplayName}] No agentMovementModule found.", this);
            }

            if (cameraForMovement == null)
                cameraForMovement = Camera.main;
        }

        private bool TryBindRuntimeReferences(bool logFailure)
        {
            if (worldObject == null)
                return false;

            if (taskController == null)
                taskController = GetComponentInParent<TaskController>();

            if (gameInputRouter == null)
                gameInputRouter = GameInputRouter.Instance;

            if (gameInputRouter == null)
            {
                if (logFailure)
                    Debug.LogWarning("[PlayerDecisionModule] Waiting for GameInputRouter after reload.", this);
                return false;
            }

            inputState = gameInputRouter.InputState;
            if (inputState == null)
            {
                if (logFailure)
                    Debug.LogWarning($"[PlayerDecisionModule {worldObject.DisplayName}] Waiting for InputState after reload.", this);
                return false;
            }

            llmConfig ??= worldObject.llmConfigModule;
            llmWorldState ??= worldObject.llmWorldStateModule;
            TrySubscribeToThinkModule();
            return taskController != null;
        }

        private void TrySubscribeToThinkModule()
        {
            if (llmThinkSubscribed || worldObject == null || worldObject.llmThinkModule == null)
                return;

            worldObject.llmThinkModule.PlanJsonReceived += OnLLMResponseJson;
            llmThinkSubscribed = true;
            Debug.Log($"[PlayerDecisionModule] Subscribed to LLMThinkModule agent={worldObject.DisplayName}");
        }

        private void UnsubscribeFromThinkModule()
        {
            if (!llmThinkSubscribed || worldObject == null || worldObject.llmThinkModule == null)
                return;

            worldObject.llmThinkModule.PlanJsonReceived -= OnLLMResponseJson;
            llmThinkSubscribed = false;
            Debug.Log($"[PlayerDecisionModule] Unsubscribed from LLMThinkModule agent={worldObject.DisplayName}");
        }

        #endregion
        #region Tick
        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (!TryBindRuntimeReferences(logFailure: false))
            {
                return;
            }

            SetDirectInputWallConstraint(HasManualMoveInput(inputState));

            bool taskDrivingMovement = taskController != null && taskController.IsDrivingMovement;
            if (taskDrivingMovement)
            {
                //Debug.Log("taskController is driving movement.");
                taskController.Tick(deltaTime);
                //return; // IMPORTANT: don't also write motion inputs this tick
            }

            // Only act if THIS worldObject is the one currently controlled
            if (!gameInputRouter.IsControlled(worldObject))
            {
                SetDirectInputWallConstraint(false);
                Debug.LogWarning($"[{worldObject.DisplayName}] Tick: IsControlled == false");
                return;
            }

            //Debug.Log($"PlayerDecisionModule: ready to process inputState");

            HandleOneShotActions(inputState);

            // New click-to-move orders should replace the current movement task immediately.
            if (taskDrivingMovement && HasClickMoveInput(inputState))
            {
                taskController!.CancelAllTasks();
                taskDrivingMovement = false;

                currentDestinationPosition = null;
                currentDestinationObject = null;
            }

            // Manual movement should immediately reclaim control from click-to-move / queued movement tasks.
            if (taskDrivingMovement && HasManualMoveInput(inputState))
            {
                taskController!.CancelAllTasks();
                taskDrivingMovement = false;

                currentDestinationPosition = null;
                currentDestinationObject = null;
                inputState.hasClickTargetWorldObject = false;
                inputState.hasClickTargetLocationWorld = false;
                inputState.hasPendingClickTargetLocationWorld = false;
            }

            // Only do player controlled movement if LLM isn't driving movement
            if (!taskDrivingMovement)
            {
                //Debug.Log("PlayerDecisionModule is driving movement.");
                HandleMovement(inputState, deltaTime);
            }
            

            // if player has requested activating an object, it will be sent from a
            // central location, not here in playerInputModule.  Currently that location
            // is GameInputRouter.

            //if (worldObject.activatorModule!=null)
            //    worldObject.activatorModule.HandleActivate(inputState, deltaTime);

            // Periodically Think about what to do next...
//            if (Time.time >= nextThinkTime)
//            {
//                //string taskPrompt = $"Update at interval time {thinkIntervalSeconds}";
//                string taskPrompt = $"Explore the world.";
//                think.TryRequestPlan(taskPrompt, urgency: Normal, applyMode: Append, tag: "player_interval");
//                nextThinkTime = Time.time + thinkIntervalSeconds;
//            }
        }

        #endregion
        #region LLM

#nullable enable
        [Header("LLM Components required")]
        [SerializeField] private LLMConfigModule? llmConfig;
        [SerializeField] private LLMWorldStateModule? llmWorldState;

//        private LLMPlanRequestOnDemand BuildRequestForThisAgent(string userTaskPrompt)
//        {
//            if (worldObject.llmConfigModule == null) throw new InvalidOperationException("Missing LLMConfigModule");
//            if (worldObject.llmWorldStateModule == null) throw new InvalidOperationException("Missing LLMWorldStateModule");
//
//            string requestId = $"{gameObject.name}:{DateTime.UtcNow.Ticks}";
//            return llmConfig.BuildLLMRequest(worldObject.llmWorldStateModule, requestId, userTaskPrompt);
//        }

//        public void RequestPlanNow()
//        {
//            string prompt =
//                "Decide the next 1–3 actions for the next few seconds. " +
//                "If uncertain, request_observation or noop.";
//
//            var req = BuildRequestForThisAgent(prompt);
//            LLMWorldScheduler.Instance.EnqueueRequest(req);
//        }

        private LLMPlanRequestOnDemand BuildRequestForThisAgent(
                string userTaskPrompt,
                LLMPlanUrgency urgency = LLMPlanUrgency.Normal,
                LLMApplyMode applyMode = LLMApplyMode.Append,
                string tag = "player_request",
                Vector2Int? eventCell = null,
                Vector3? eventWorld = null)
        {
            if (llmConfig == null) throw new InvalidOperationException("Missing LLMConfigModule");
            if (llmWorldState == null) throw new InvalidOperationException("Missing LLMWorldStateModule");

            // Choose tier/profile the same way config does (boss/combat/distance etc.)
            // Your config decides Sophistication and applies overrides; but it doesn’t expose tier directly.
            // So we select a tier for scheduler based on world state signals:
            // (Keep it simple: close/combat/quest => higher tier.)
            Sophistication sophistication = ChooseSophistication(llmWorldState, llmConfig.identity.isBoss, llmConfig.identity.isSimpleCreature);

            return new LLMPlanRequestOnDemand(
                agentId: llmConfig.identity.ResolveAgentId(gameObject),
                prompt: userTaskPrompt,
                eventCell: eventCell,
                eventWorld: eventWorld,
                urgency: urgency,
                applyMode: applyMode,
                tag: tag,
                sophistication: sophistication,
                onResponseJson: OnLLMResponseJson
            );
        }

        private static Sophistication ChooseSophistication(
            LLMWorldStateModule ws,
            bool isBoss,
            bool isSimpleCreature)
        {
            if (isSimpleCreature)
                return Sophistication.Low;

            if (isBoss || ws.isQuestCritical || ws.isInCombat)
                return Sophistication.High;

            if (ws.distanceToPlayerMeters <= 10f || ws.isPlayerFocusingThisNpc)
                return Sophistication.Medium;

            return Sophistication.Low;
        }

        public void RequestPlanNow()
        {
            if (worldObject.llmThinkModule == null)
                return;

            string prompt =
                "Decide the next 1-3 actions for the next few seconds.\n" +
                "If uncertain, request_observation or noop.\n" +
                "Prefer safe, reversible actions.";

            bool queued = worldObject.llmThinkModule.TryRequestPlan(
                userTaskPrompt: prompt,
                urgency: DogGame.LLM.LLMPlanUrgency.Normal,
                applyMode: DogGame.LLM.LLMApplyMode.Interrupt,
                tag: "player_manual"
            );

            // optional debug
            if (!queued) Debug.Log("[PlayerDecisionModule] Plan request blocked (inflight).");
        }

        private void OnLLMResponseJson(string planJson)
        {
            // This is where your Step 2/3/4 pipeline continues:
            // Parse -> Translate -> Instantiate -> Start task
            Debug.Log($"LLMWalkthrough2: PlayerDecisionModule got planJsonChars={planJson?.Length ?? 0}.  planJson={planJson}");
            if (planJson.IsNullOrEmpty())
            {
                Debug.LogError("OnLLMResponseJson: planJson is null or empty.");
                return;
            }

            TryBindRuntimeReferences(logFailure: true);

            if (taskController == null)
            {
                Debug.LogError($"[PlayerDecisionModule] No TaskController available for agent={worldObject?.DisplayName ?? gameObject.name}.");
                return;
            }

            Debug.Log($"[PlayerDecisionModule] Forwarding plan to TaskController agent={worldObject.DisplayName} chars={planJson!.Length}");
            bool applied = taskController.TryApplyPlanJson(planJson!);
            Debug.Log($"[PlayerDecisionModule] TaskController.TryApplyPlanJson returned {applied} for agent={worldObject.DisplayName}");
        }

        #endregion
        #region One-shot actions

        private void HandleOneShotActions(PlayerInputState state)
        {
            //Debug.Log("PlayerDecisionModule HandleOneShotActions");
            if (state.barkPressed && worldObject.noiseMakerModule != null)
            {
                worldObject.noiseMakerModule.Bark();
            }

            if (state.markTerritoryPressed && worldObject.scentEmitterModule != null)
            {
                worldObject.scentEmitterModule.EmitOnDemandScent(1.0f); // spread over 1 second
            }

            if (state.digPressed)
            {
                global::TerrainDigService.TryDigAt(worldObject);
            }

            // You can also use state.anyKeyOrButtonPressed to skip cutscenes,
            // advance dialogue, etc. Hook that into your game state manager.
        }

        #endregion

        #region Movement

        private void HandleMovement(PlayerInputState state, float deltaTime)
        {
            if (worldObject.agentMovementModule == null)
            {
                Debug.LogWarning($"[PlayerDecisionModule {worldObject.DisplayName}] No AgentagentMovementModule found.", this);
                return;
            }

            //Debug.Log($"PlayerDecisionModule HandleMovement(anyKey={state.anyKeyOrButtonDown}, {deltaTime})");

            Vector3 desiredWorldDir = Vector3.zero;

            // 1) Manual input:
            //    A/D or stick X rotates the dog.
            //    W/S or stick Y moves forward/back relative to the current facing.
            //    Q/E strafes without changing facing.
            bool hasManualInput = HasManualMoveInput(state);
            if (hasManualInput)
            {
                // First, manual controls disable current click-to-move status
                currentDestinationPosition = null;  // stop heading to location
                currentDestinationObject = null;    // stop heading to object

                ApplyManualTurn(state.moveAxis.x, deltaTime);
                desiredWorldDir = ConvertInputToWorldDirection(new Vector2(state.strafeAxis, state.moveAxis.y));
                navigationSource = NavigationSource.PlayerDirection;
                worldObject.motionModule.motionControlMode = MotionControlMode.DirectInput;
                worldObject.motionModule.facingMode = FacingMode.Manual;
                
                currentManualWorldMoveDir = desiredWorldDir;
                MovementHeadToDestination();
                return; // manual input wins over click-to-move for this frame
            }

            // No direct input this frame.
            currentManualWorldMoveDir = null;

            // 2) Click-to-move: if we have a click target location and no interact press,
            //    steer toward that point. (Very simple version: straight-line steering.)
            
            // 2B) New click target is an object: new orders
            if (state.hasClickTargetWorldObject && !state.interactPressed)
            {
                currentDestinationObject = state.clickTargetWorldObject; // head to object, new orders arrived
                SubmitMoveToTargetObjectTask(currentDestinationObject);
                return;
            }
            // 2A) New click target was a location: new orders
            if (state.hasPendingClickTargetLocationWorld && !state.interactPressed)
            {
                currentDestinationPosition = state.clickTargetLocationWorld; // head to location, new orders arrived
                currentDestinationObject = null;  // stop heading to object if we had been
                state.hasPendingClickTargetLocationWorld = false;
                SubmitMoveToTargetPositionTask((Vector3)currentDestinationPosition);
                return;
            }

            // Neither manual input nor click targets: stop residual direct movement intent.
            MovementHeadToDestination();

/*
            // current target is an object, let's figure out where it is now.
            if (currentDestinationObject!=null)
            {
                if (currentDestinationObject.locationModule != null)
                    currentDestinationPosition = currentDestinationObject.locationModule.pos3d_world;
            }

            // fianlly, head to destination location (or object's current location)
            if (currentDestinationPosition != null)
            {
                Vector3 toTarget = (Vector3)currentDestinationPosition - worldObject.transform.position;
                toTarget.y = 0f;

                const float stopDistance = 0.25f; // tweak as needed

                if (toTarget.sqrMagnitude > stopDistance * stopDistance)
                {
                    desiredWorldDir = toTarget.normalized;
                    navigationSource = NavigationSource.ClickToMove;
                    worldObject.motionModule.motionControlMode = MotionControlMode.GoalDirected;
                    worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;
                }
                else
                {
                    // Reached target; clear the desired move so we can stop
                    desiredWorldDir = Vector3.zero;
                    navigationSource = NavigationSource.None;
                    worldObject.motionModule.motionControlMode = MotionControlMode.GoalDirected;
                    worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;

                    // Send notification of arrival...
                    if (currentDestinationObject!=null)
                    {
                        // Send event Arrived at currentDestinationObject
                        // use-case: interact with object
                    }
                    else if (currentDestinationPosition!=null)
                    {
                        // Send event Arrived at currentDestinationPosition
                        // use-case: patrol between points allows changing to next point.
                    }
                    
                    // clear current destination
                    currentDestinationPosition = null;
                    currentDestinationObject = null;
                }
            }
            currentManualWorldMoveDir = desiredWorldDir;
            MovementHeadToDestination();
            */
        }

        private static bool HasManualMoveInput(PlayerInputState state)
        {
            if (state == null)
                return false;

            return state.moveAxis.sqrMagnitude > 0.0001f || Mathf.Abs(state.strafeAxis) > 0.0001f;
        }

        private void ApplyManualTurn(float turnAxis, float deltaTime)
        {
            if (Mathf.Abs(turnAxis) < 0.0001f || deltaTime <= 0f || worldObject == null)
                return;

            Transform bodyRoot = GetManualFacingTransform();
            if (bodyRoot == null)
                return;

            Vector3 currentEuler = bodyRoot.rotation.eulerAngles;
            bodyRoot.rotation = Quaternion.Euler(
                currentEuler.x,
                currentEuler.y + turnAxis * manualTurnDegreesPerSecond * deltaTime,
                currentEuler.z);
        }

        private static bool HasClickMoveInput(PlayerInputState state)
        {
            if (state == null || state.interactPressed)
                return false;

            return state.hasClickTargetWorldObject || state.hasPendingClickTargetLocationWorld;
        }

        private void SetDirectInputWallConstraint(bool enable)
        {
            if (worldObject?.motionModule == null)
                return;

            worldObject.motionModule.ConstrainToCellWalls = enable;
        }

        public void MovementHeadToDestination()
        {
            // bring in manual input desiredWorldDir
            Vector3 desiredWorldDir = currentManualWorldMoveDir==null ? Vector3.zero : (Vector3)currentManualWorldMoveDir;

            // 3) Feed intent into agentMovementModule
            if (currentDestinationObject != null)
            {
                // Move to target object, and keep tracking it.
                worldObject.agentMovementModule.SetDesiredTargetWorldObject(currentDestinationObject);

            }
            else if (desiredWorldDir.sqrMagnitude > 0.0001f)
            {
                // Move to target location by 1 step.
                // Keep the currently selected walk mode; keyboard input should not reset speed.
                worldObject.agentMovementModule.SetDesiredMove(desiredWorldDir, maxDistance:1.0f, speedFactor:1.0f, changeWalkMode:WalkMode.None);
            }
            else
            {
                // No active target and no input: decelerate to stop
                worldObject.agentMovementModule.ClearDesiredMove();
            }

            // NOTE:
            // We do NOT rotate the worldObject here anymore.
            // MotionModule (called by AgentagentMovementModule) handles facing the move direction.
        }

        public void SubmitMoveToTargetObjectTask(WorldObject targetWorldObject)
        {
            if (targetWorldObject != null)
            {
                currentDestinationPosition = null;
                worldObject.motionModule.motionControlMode = MotionControlMode.GoalDirected;
                worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;

                taskController.Submit(new TaskRequest(
                    task: new Task_MoveToObject(targetWorldObject),
                    priority: 100,
                    source: TaskSource.Player,
                    canInterrupt: true,
                    resumePrevious: false,
                    clearStackOnStart: true,
                    tag: "player_move_to_object"
                ));
                Debug.Log($"Move to target object {targetWorldObject.DisplayName}");
            } 
        }
        public void SubmitMoveToTargetPositionTask(Vector3 targetLocation)
        {
            {
                worldObject.motionModule.motionControlMode = MotionControlMode.GoalDirected;
                worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;

                taskController.Submit(new TaskRequest(
                    task: new Task_MoveToLocation(targetLocation.x, targetLocation.z),
                    priority: 100,
                    source: TaskSource.Player,
                    canInterrupt: true,
                    resumePrevious: false,
                    clearStackOnStart: true,
                    tag: "player_move_to_location"
                ));
                Debug.Log($"Move to target location {targetLocation}");
            }
        }
        private void UpdateFacingModeForDirectInput(Vector3 worldMoveDir)
        {
            var motion = worldObject.motionModule;

            // No movement? Don’t change facing.
            Vector3 flatMove = new Vector3(worldMoveDir.x, 0f, worldMoveDir.z);
            if (flatMove.sqrMagnitude < 0.0001f)
                return;

            flatMove.Normalize();

            // Current facing on the XZ plane
            Transform bodyRoot = motion.bodyRoot; // or however you access it
            Vector3 flatForward = bodyRoot.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;
            else
                flatForward.Normalize();

            float alignment = Vector3.Dot(flatForward, flatMove);
            // alignment:
            //  +1  = moving straight forward
            //   0  = pure strafe
            //  -1  = straight backward

            //const float forwardThreshold   =  0.4f;  // > this = "mostly forward"
            const float backpedalThreshold = -0.4f;  // < this = "mostly backward"

            //if (alignment > forwardThreshold)
            if (alignment > backpedalThreshold)
            {
                // Moving mostly forward: let MotionModule rotate with movement
                motion.facingMode = FacingMode.FaceMovementDirection;
            }
            else
            {
                // Strafing or backpedalling: keep facing where we already are
                motion.facingMode = FacingMode.Manual;
            }

            // If you want to treat strafe vs backpedal differently:
            // if (alignment < backpedalThreshold) { ... }
        }


        private Vector3 ConvertInputToWorldDirection(Vector2 moveAxis)
        {
            // If no input, early out
            if (moveAxis.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            // Use the same body root that manual turning rotates.
            Transform facingTransform = GetManualFacingTransform();
            if (facingTransform != null)
            {
                Vector3 forward = facingTransform.forward;
                forward.y = 0f;
                forward.Normalize();

                Vector3 right = facingTransform.right;
                right.y = 0f;
                right.Normalize();

                return forward * moveAxis.y + right * moveAxis.x;
            }

            // 3. Fallback: world-relative XZ
            Debug.LogWarning("this.transform == null, maybe not a good thing? ",this);
            return new Vector3(moveAxis.x, 0f, moveAxis.y);     // probably not what we want, so don't fail above!
        }

        private Transform GetManualFacingTransform()
        {
            if (worldObject == null)
                return transform;

            if (worldObject.motionModule != null && worldObject.motionModule.bodyRoot != null)
                return worldObject.motionModule.bodyRoot;

            return worldObject.transform;
        }
        #endregion

        #region Interaction
        // Not handled here...  GameInputRouter sends target the HandleActivate event.
        #endregion
        
        #region PackLoyalty
        // Mode A: Soft influence (blend / bias movement)
        // If the player is controlling movement, just bias desired velocity slightly back toward pack.
        public static Vector3 ApplyPackLoyaltyBias(
            Vector3 desiredWorldVelocity,
            Vector3 selfPosition,
            PackLoyaltyResult packLoyalty,
            float maxBiasStrength = 0.65f)
        {
            if (!packLoyalty.isActive || packLoyalty.directive == PackLoyaltyDirective.None)
                return desiredWorldVelocity;

            Vector3 toTarget = (packLoyalty.targetLocation - selfPosition);
            if (toTarget.sqrMagnitude < 0.001f)
                return desiredWorldVelocity;

            Vector3 loyaltyDirection = toTarget.normalized;

            // Blend: higher urge means more pull.
            float bias = Mathf.Clamp01(packLoyalty.urge01) * maxBiasStrength;

            Vector3 biased = Vector3.Lerp(desiredWorldVelocity, loyaltyDirection * desiredWorldVelocity.magnitude, bias);
            return biased;
        }

        // Mode B: Hard interrupt (autopilot overrides)
        // If separation is extreme, ignore player input and set a “return-to-pack” goal.
        
        // In your DecisionModule tick:
        public void ApplyPackLoyaltyOverride()
        {
            var pack = worldObject.motivationModule.latestPackLoyalty;

            if (pack.isActive && pack.urge01 > 0.85f) // tune later
            {
                worldObject.motionModule.motionControlMode = MotionControlMode.Autopilot;
                worldObject.agentMovementModule.SetDesiredTargetWorldObject(worldObject.packMemberModule.currentPack.packLeader); // whatever your API is
                worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;
                return;
            }
        }
        #endregion

        #region BeginEndDecisionModule
        // Run this when THIS decision module becomes active
        public override void BeginDecisionModule(bool resume=false)
        {
            SetDirectInputWallConstraint(false);
            if (!(taskController != null && taskController.IsDrivingMovement))
            {
                Debug.Log($"[{worldObject.DisplayName}] LLM was still driving movement when Player took over.");
            }
            if (resume)
            {
                currentManualWorldMoveDir = null;    // no need to resume manual input control.
                // resume actions/state: currentDestination
                if (currentDestinationObject!=null || currentDestinationPosition!=null || currentManualWorldMoveDir!=null)
                {
                    MovementHeadToDestination();    // resume prior movements from last time we were active
                }
            }
            else
            {
                // clear state left over from last time we were active
                currentDestinationPosition = null;
                currentDestinationObject = null;
                currentManualWorldMoveDir = null;
                // stop any in-progress movement
                StopMovementIntent();
            }            
        }

        // Run this when THIS decision module becomes inactive
        public override void EndDecisionModule()
        {
            SetDirectInputWallConstraint(false);
            // retain state (in case requested to resume): currentDestination*
            
            // stop actions in progress: Move
            StopMovementIntent();
        }
        #endregion

#nullable enable
        [Header("LLM")]
        [SerializeField] private LLMAgentFacade? llmFacade;

        [Header("Tasks")]
        [SerializeField] private MonoBehaviour? taskRunnerComponent; // should implement IAgentTaskRunner

        private System.Threading.CancellationTokenSource? planCts;
        private LLMPlanExecutor? planExecutor;

//        private void Awake()
//        {
//            if (llmFacade == null)
//                llmFacade = GetComponent<LLMAgentFacade>();
//
//            // Build executor once (typed factory)
//            planExecutor = new LLMPlanExecutor(new AgentTaskFactory());
//        }

        private IAgentTaskRunner? Runner =>
            taskRunnerComponent as IAgentTaskRunner;

/*
        /// <summary>
        /// Call this when you want the player agent to ask the LLM for a plan and execute it.
        /// </summary>
        public async Task RequestAndExecutePlan()
        {
            if (llmFacade == null)
            {
                Debug.LogWarning("[PlayerDecisionModule] Missing LLMAgentFacade.", this);
                return;
            }

            if (Runner == null)
            {
                Debug.LogWarning("[PlayerDecisionModule] taskRunnerComponent must implement IAgentTaskRunner.", this);
                return;
            }

            if (planExecutor == null)
            {
                Debug.LogWarning("[PlayerDecisionModule] planExecutor not initialized.", this);
                return;
            }

            // Cancel any in-flight plan request
            planCts?.Cancel();
            planCts?.Dispose();
            planCts = new CancellationTokenSource();

            string taskPrompt = BuildTaskPrompt();

            // Ask LLM
            var llmResponse = await llmFacade.RequestPlanAsync(taskPrompt, planCts.Token);
            if (!llmResponse.succeeded)
            {
                Debug.LogWarning($"[PlayerDecisionModule] LLM failed: {llmResponse.errorMessage}", this);
                return;
            }

            // Parse/validate/translate/instantiate
            var rootTask = planExecutor.BuildRootTaskFromJson(llmResponse.rawText);
            if (rootTask == null)
            {
                Debug.LogWarning("[PlayerDecisionModule] LLM plan was invalid; not executing.", this);
                return;
            }

            // Execute
            Runner.AbortAll("new_llm_plan");
            Runner.StartTask(rootTask);
        }
*/
        private string BuildTaskPrompt()
        {
            // Keep this short; your system blocks and world state carry the heavy context.
            return
    @"TASK:
    Decide the next 1–3 actions for the agent over the next few seconds.
    Prefer safe, plausible actions. If uncertain, request_observation or noop.";
        }

        private void OnDisable()
        {
            UnsubscribeFromThinkModule();
            planCts?.Cancel();
            planCts?.Dispose();
            planCts = null;
        }

        #region RequestAndExecutePlan
        //[SerializeField] private DogGame.LLM.Agent.LLMAgentFacade? llmFacade;
        //private CancellationTokenSource? planCts;

        public async void RequestAndExecutePlan()
        {
            if (llmFacade == null) llmFacade = GetComponent<DogGame.LLM.Agent.LLMAgentFacade>();
            if (llmFacade == null) return;

            planCts?.Cancel();
            planCts?.Dispose();
            planCts = new CancellationTokenSource();

            string taskPrompt =
        @"TASK:
        Decide the next 1–3 actions over the next few seconds.
        If uncertain, request_observation or noop.";

            Debug.Log($"LLMWalkthrough0: RequestAndExecutePlan {worldObject.DisplayName}, {taskPrompt}");

            var response = await llmFacade.RequestPlanAsync(taskPrompt, planCts.Token);
            if (!response.succeeded)
            {
                Debug.LogWarning(response.errorMessage);
                return;
            }

            // Next step: parse/translate/instantiate and push to task runner (we already built this part earlier)
        }
        #endregion

/*
        #region BeginEndDecisionModule
        // Example "real" routine submission
        public void Submit_curious_bark_sniff ()
        {
            var lib = DogGame.Routines.RoutineLibrary.Instance;
            TaskRequest request;
            string error = string.Empty;
            if (lib != null && lib.TryBuildRoutineRequest(
                    routineId: "curious_bark_sniff",
                    context: taskController.taskContext,
                    evt: null,
                    priority: 80,
                    source: TaskSource.Reaction,
                    canInterrupt: true,
                    resumePrevious: true,
                    clearStackOnStart: false,
                    out request,
                    out error))
            {
                taskController.Submit(request);
            }
            else
            {
                Debug.LogWarning($"Routine build failed: {error}");
            }
        }
        #endregion
*/
    }
}
