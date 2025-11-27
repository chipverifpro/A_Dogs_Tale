

/*
WorldObjects README
-------------------
Hierarchy of files:
WORLDOBJECTS/
    WorldObject                             --Objects class - contains pointers to all attached modules
    WorldObjectRegistry                     --Manages creation/registration of objects
    MODULES/
        AGENT_MODULES/
            AgentModule                     --Base management of agent modes/sub-modules
            AGENTDECISION_MODULES/
                AgentDecisionModuleBase     --Base class determining what behavior is desired
                FollowDecisionModule        --Detailed AI
                PlayerDecisionModule        --Detailed AI equivalent (player controls)
                WanderDecisionModule        --Detailed AI
            AGENTINTERFACE_MODULES/
                AgentMovementModule         --Helpers to PHYSICAL_MODULES
                AgentSenseModule            --Helpers/Aggregator to SENSORY_MODULES
                AgentPackMemberModule       --Link to pack, role in pack
                AgentBlackboardView         --Blackboard overlay to abstract out lookups
        DATA_MODULES/
            BlackboardModule                --Dictionary of name/value pairs
            BlackboardViews                 --Abstractions for different uses (maybe move to Agent/Quest/etc.)
            PlacementModule                 --World Builder information
        PHYSICAL_MODULES/
            ActivatorModule                 --Event driven activators
            InteractionModule               --Generic use object beyond Activator or including it
            LocationModule                  --Where am I?
            MotionModule                    --Where am I going, how to get there?
            ContainerModule                 --What am I carrying?  Get/drop/use hooks
            AppearanceModule                --Render/animation, transitions, lighting
        QUEST_MODULES/
            QuestModuleBase                 --Base class holding quest/puzzle sequences, parameters
            QUESTS
                FetchQuest                  --Generic randomized deliver item A to agent B, get reward C
                LaserPointerQuest           --Custom quest sequence
                ...
        SENSORY_MODULES/
            ScentEmitterModule              --Parameters for scent depositing system
            SoundEmitterModule              --What sounds to emit based on current actions
            VisionModule                    --What can be seen
            HearingModule                   --What can be heard
            SmellModule                     --What can be smelled
            EatModule                       --Manage hunger, food consumption
            StatusModule                    --Health, hunger, stats




*/
