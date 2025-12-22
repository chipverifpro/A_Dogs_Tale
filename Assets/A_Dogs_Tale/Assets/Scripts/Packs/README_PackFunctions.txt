Functions related to Pack

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
