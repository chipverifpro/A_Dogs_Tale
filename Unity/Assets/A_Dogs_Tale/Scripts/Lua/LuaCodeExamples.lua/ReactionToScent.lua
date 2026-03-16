state = {
    mode = "idle",
    scent = {
        category = nil,
        name = nil,
        searchingEdge = false
    }
}

function beginScentFollow(event)
    state.mode = "following_scent"
    state.scent.category = event.scentCategory
    state.scent.name = event.scentName
    state.scent.searchingEdge = false

    Bark(1)
    Sniff(1.0)
    FollowEventScent()
end

function handleFollowingScent(event)
    if event.scentLost then
        Bark(1)
        StopFollowEventScent()
        AskLLMNextAction()
        resetState()
        return
    end

    if event.scentSourceFound then
        Bark(2)
        StopFollowEventScent()
        resetState()
        return
    end

    if event.scentMaximumWithoutSourceFound then
        Bark(3)
        state.scent.searchingEdge = true
        SetSearchEdgeOfScentPool(true)
        return
    end
end

function resetState()
    state.mode = "idle"
    state.scent.category = nil
    state.scent.name = nil
    state.scent.searchingEdge = false
end

function react(event)
    if event == nil then
        return
    end

    if state.mode == "idle" then
        if event.hasScent then
            beginScentFollow(event)
            return
        end
    elseif state.mode == "following_scent" then
        handleFollowingScent(event)
        return
    end
end
