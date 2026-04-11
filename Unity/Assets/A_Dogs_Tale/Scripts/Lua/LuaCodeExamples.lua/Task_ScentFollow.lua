state = {
    initialized = false,
    knownMap = {},
    lastCellKey = nil,
    lastHeadingX = 0,
    lastHeadingY = 0
}

local STRENGTH_IMPROVEMENT_THRESHOLD = 0.02
local INERTIA_EPSILON = 0.0001

local function log(message)
    print("[ScentFollowLua] " .. message)
end

local function key(x, y)
    return tostring(x) .. "," .. tostring(y)
end

local function sign(v)
    if v > 0 then return 1 end
    if v < 0 then return -1 end
    return 0
end

local function mergeCells(cells)
    local changed = false
    if cells == nil then
        return false
    end

    for _, cell in ipairs(cells) do
        local k = key(cell.x, cell.y)
        local existing = state.knownMap[k]

        if existing == nil then
            state.knownMap[k] = {
                x = cell.x,
                y = cell.y,
                scentStrength = cell.scentStrength,
                timestamp = cell.timestamp
            }
            changed = true
        else
            local previousTimestamp = existing.timestamp

            if cell.scentStrength > existing.scentStrength + STRENGTH_IMPROVEMENT_THRESHOLD then
                existing.scentStrength = cell.scentStrength
                existing.timestamp = cell.timestamp

                local neighbors = {
                    { x = cell.x, y = cell.y + 1 },
                    { x = cell.x + 1, y = cell.y },
                    { x = cell.x, y = cell.y - 1 },
                    { x = cell.x - 1, y = cell.y }
                }

                for _, neighbor in ipairs(neighbors) do
                    local nk = key(neighbor.x, neighbor.y)
                    local adjacent = state.knownMap[nk]
                    if adjacent ~= nil and adjacent.timestamp < previousTimestamp then
                        state.knownMap[nk] = nil
                    end
                end

                changed = true
            elseif cell.scentStrength < existing.scentStrength then
                existing.scentStrength = cell.scentStrength
                existing.timestamp = cell.timestamp
            elseif cell.timestamp > existing.timestamp then
                existing.timestamp = cell.timestamp
            end
        end
    end

    return changed
end

local function mergeMiniSniffForCurrentCell()
    local currentKey = key(CurrentX, CurrentY)
    if state.lastCellKey == currentKey then
        return false
    end

    state.lastCellKey = currentKey
    return mergeCells(getMiniSniff(CurrentX, CurrentY))
end

local function isPerimeterCell(cell)
    local neighbors = {
        key(cell.x, cell.y + 1),
        key(cell.x + 1, cell.y),
        key(cell.x, cell.y - 1),
        key(cell.x - 1, cell.y)
    }

    for _, neighborKey in ipairs(neighbors) do
        if state.knownMap[neighborKey] == nil then
            return false
        end
    end

    return true
end

local function scoreCell(cell)
    local dx = cell.x - CurrentX
    local dy = cell.y - CurrentY
    local distance = math.sqrt((dx * dx) + (dy * dy))
    local distanceWeight = 1.0 / math.max((distance / 3.0) - 1.0, 1.0)
    local score = cell.scentStrength * distanceWeight

    local headingX = sign(dx)
    local headingY = sign(dy)
    if headingX == state.lastHeadingX and headingY == state.lastHeadingY then
        score = score + INERTIA_EPSILON
    end

    return score
end

local function chooseBestPerimeterCell()
    local bestCell = nil
    local bestScore = -1.0

    for _, cell in pairs(state.knownMap) do
        if not (cell.x == CurrentX and cell.y == CurrentY) and isPerimeterCell(cell) then
            local score = scoreCell(cell)
            if score > bestScore then
                bestScore = score
                bestCell = cell
            end
        end
    end

    return bestCell, bestScore
end

function tick()
    if not state.initialized then
        state.initialized = true
        state.lastCellKey = key(CurrentX, CurrentY)
        mergeCells(getSniff(CurrentX, CurrentY))
        log("initialized knownMap at " .. tostring(CurrentX) .. "," .. tostring(CurrentY))
    end

    mergeMiniSniffForCurrentCell()

    if IsAdjacentToScentSource then
        log("adjacent to scent source")
        Response_FoundScentTarget()
        return
    end

    if MoveInProgress then
        return
    end

    local bestCell, bestScore = chooseBestPerimeterCell()
    if bestCell ~= nil and bestScore > MinThreshold then
        state.lastHeadingX = sign(bestCell.x - CurrentX)
        state.lastHeadingY = sign(bestCell.y - CurrentY)
        log("move to perimeter " .. tostring(bestCell.x) .. "," .. tostring(bestCell.y) .. " score=" .. tostring(bestScore))
        moveToXYwithMiniSniff(bestCell.x, bestCell.y)
        return
    end

    local foundNewTrail = mergeCells(getSniff(CurrentX, CurrentY))
    if not foundNewTrail then
        log("lost scent after sniff")
        Response_LostScent()
        return
    end

    bestCell, bestScore = chooseBestPerimeterCell()
    if bestCell == nil or bestScore <= MinThreshold then
        log("no perimeter cell above threshold after sniff")
        Response_LostScent()
        return
    end

    state.lastHeadingX = sign(bestCell.x - CurrentX)
    state.lastHeadingY = sign(bestCell.y - CurrentY)
    log("move after sniff to " .. tostring(bestCell.x) .. "," .. tostring(bestCell.y) .. " score=" .. tostring(bestScore))
    moveToXYwithMiniSniff(bestCell.x, bestCell.y)
end
