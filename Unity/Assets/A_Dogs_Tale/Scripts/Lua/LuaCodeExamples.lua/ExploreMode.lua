state = {
    roomPath = {},
    enteredByDoor = {},
    usedDoors = {},
    centeredRooms = {},
    pendingAction = nil,
    pendingDoorId = nil,
    lastLog = nil
}

local function topRoomId()
    return state.roomPath[#state.roomPath]
end

local function log(message)
    if state.lastLog ~= message then
        -- print("[ExploreLua] " .. message)
        state.lastLog = message
    end
end

local function clearPendingAction()
    state.pendingAction = nil
    state.pendingDoorId = nil
end

local function consumeExploreActionCancelled()
    local wasCancelled = ExploreActionCancelled == true
    ExploreActionCancelled = false
    return wasCancelled
end

local function doorExistsInCurrentRoom(doorId)
    if doorId == nil or Room == nil or not Room.IsValid then
        return false
    end

    for i = 1, Room.DoorCount do
        if Room.GetDoorId(i) == doorId then
            return true
        end
    end

    return false
end

local function resetPathToCurrentRoom(message)
    state.roomPath = { Room.Id }
    state.enteredByDoor = {}
    log(message)
    clearPendingAction()
end

local function validateCurrentEntryDoor()
    local entryDoorId = state.enteredByDoor[#state.enteredByDoor]
    if entryDoorId ~= nil and not doorExistsInCurrentRoom(entryDoorId) then
        resetPathToCurrentRoom("Remembered entry door " .. tostring(entryDoorId) .. " is not in current room " .. tostring(Room.Id) .. "; resetting explore path but keeping used-door memory")
    end
end

local function syncCurrentRoom()
    if Room == nil or not Room.IsValid then
        log("Room invalid; waiting for room state")
        return false
    end

    local currentRoomId = Room.Id
    local topRoom = topRoomId()
    local actionCancelled = consumeExploreActionCancelled()

    if topRoom == nil then
        state.roomPath[1] = currentRoomId
        state.enteredByDoor[1] = nil
        log("Starting explore in room " .. tostring(currentRoomId) .. " with " .. tostring(Room.DoorCount) .. " doors")
        clearPendingAction()
        return true
    end

    if topRoom == currentRoomId then
        validateCurrentEntryDoor()
        if state.pendingAction == "center" then
            if actionCancelled then
                log("Interrupted before reaching room center for room " .. tostring(currentRoomId) .. "; will retry later")
            else
                state.centeredRooms[currentRoomId] = true
                log("Reached room center for room " .. tostring(currentRoomId))
            end
        end
        clearPendingAction()
        return true
    end

    if state.pendingAction == "forward" then
        state.roomPath[#state.roomPath + 1] = currentRoomId
        state.enteredByDoor[#state.enteredByDoor + 1] = state.pendingDoorId
        if state.pendingDoorId ~= nil then
            state.usedDoors[state.pendingDoorId] = true
        end
        log("Entered room " .. tostring(currentRoomId) .. " through door " .. tostring(state.pendingDoorId))
    elseif state.pendingAction == "backtrack" then
        if #state.roomPath > 1 then
            state.roomPath[#state.roomPath] = nil
            state.enteredByDoor[#state.enteredByDoor] = nil
        end

        if topRoomId() ~= currentRoomId then
            state.roomPath[#state.roomPath + 1] = currentRoomId
            state.enteredByDoor[#state.enteredByDoor + 1] = state.pendingDoorId
        end
        if state.pendingDoorId ~= nil then
            state.usedDoors[state.pendingDoorId] = true
        end
        log("Backtracked into room " .. tostring(currentRoomId) .. " through door " .. tostring(state.pendingDoorId))
    else
        resetPathToCurrentRoom("Room changed without pending action; resetting path in room " .. tostring(currentRoomId))
        return true
    end

    validateCurrentEntryDoor()
    clearPendingAction()
    return true
end

local function chooseNearestUnusedDoor()
    if Room == nil or not Room.IsValid then
        return nil
    end

    for i = 1, Room.DoorCount do
        local doorId = Room.GetDoorId(i)
        if doorId ~= nil and doorId >= 0 and not state.usedDoors[doorId] then
            return doorId
        end
    end

    return nil
end

function tick()
    if not syncCurrentRoom() then
        return
    end

    if VisitRoomCenterBeforeBacktracking and not state.centeredRooms[Room.Id] then
        state.pendingAction = "center"
        log("First visit to room " .. tostring(Room.Id) .. "; issuing GoToRoomCenter()")
        GoToRoomCenter()
        return
    end

    local nextDoorId = chooseNearestUnusedDoor()
    if nextDoorId ~= nil then
        state.pendingAction = "forward"
        state.pendingDoorId = nextDoorId
        log("Issuing GoThroughDoor(" .. tostring(nextDoorId) .. ")")
        GoThroughDoor(nextDoorId)
        return
    end

    local entryDoorId = state.enteredByDoor[#state.enteredByDoor]
    if entryDoorId ~= nil then
        if not doorExistsInCurrentRoom(entryDoorId) then
            resetPathToCurrentRoom("Cannot backtrack through stale entry door " .. tostring(entryDoorId) .. " from room " .. tostring(Room.Id) .. "; idle here")
            return
        end

        state.pendingAction = "backtrack"
        state.pendingDoorId = entryDoorId
        log("Backtracking through door " .. tostring(entryDoorId))
        GoThroughDoor(entryDoorId)
        return
    end

    log("No unused doors and no entry door to backtrack through; idle in room " .. tostring(Room.Id))
end
