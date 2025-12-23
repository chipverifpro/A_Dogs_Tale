Functions related to Pack
-------------------------
Several functions are duplicated between multiple locations, causing confusion over what is used.
This list is to identify where functions belong.
* Already exists in correct place.
- Doesn't need to be here.
++ Add function here or move existing function here.
-?- Unknown what to do with this yet.

CameraModeSwitcher.cs
	++UpdateCameraFollowAgent
	++ChangeCameraEffects

AppearanceModule.cs
	++CameraRefreshNeeded
	++SetCameraFollowMe

PackManager.cs
	*FindPackByName
	*CreateNewPack

Pack.cs
	-TeleportToLeader
	*AddMember
	-SetFollower
	-SetLeader
	+SetPackFollowChain
	*RemoveMember
	*SetFormation
	*GetFormation
	++CycleFormation
	-GetPositionInPack

PackMemberModule.cs
	++TeleportToLeader
	*RequestJoinPack	
	*RequestLeavePack	
	-RequestBecomeControlledAgent
	*RequestBecomeLeader
	-SetFormation
	-GetFormation
	-CycleFormation
	*GetPositionInPack
	*GetMyFormationOffset
	*HandleRequestToJoinPack

AgentModule.cs
	*SwitchDecisionModule

WorldObject.cs
	*EnsureComponent
	*CreateModulesIfNeeded
	-?-ApplyFollowerDefaults ??
	*Activate


// Functionality owned by...  (left column is implementation, right is closer to user)
//
// CreatePack - PackManager.cs
// FindPackByName - PackManager.cs
//
//      * HandlePackEvent - PackMemberModule.cs << USER
//      * RequestJoinPack - PackMemberModule.cs << USER
//      * RequestLeavePack - PackMemberModule.cs << USER
//   AddMember(isLeader) - Pack.cs
//   RemoveMember - Pack.cs
//      * RequestLeadershipChange - PackMemberModule.cs << USER
//   ChangeLeader - PackMemberModule.cs
//   ResetPackFollowChain - Pack.cs (auto after any change)
//
//   SetFormation - Pack.cs << USER
//   GetFormation - Pack.cs << USER
//   GetPositionInPack - Pack.cs
//      * RequestFormationOffset - PackMember.cs << Pathfinding.cs
//   GetFormationOffset - Pack.cs
//
//      * RequestGoToLeader(Teleport?) - PackMemberModule.cs << STARTUP / DecosionModule.csd
//      * RequestDistanceFromLeader - PackMemberModule
//   GetLeaderPosition - Pack.cs
//
// SetCameraFollower - CameraModeSwitcher.cs
//
// CreateModulesIfNeeded - WorldObject.cs
// EnsureComponent - WorldObject.cs
