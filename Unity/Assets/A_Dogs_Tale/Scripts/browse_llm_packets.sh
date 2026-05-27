#!/usr/bin/env bash
set -euo pipefail

ROOT="${DOGS_TALE_LLM_PACKETS:-$HOME/DogsTaleSaves/LLM_Packets}"

timestamps=()
agents=()
request_ids=()
dirs=()
request_files=()
response_files=()
decode_files=()

selected=0
scroll=0
mode="list"
file_index=0
screen_rows=24
screen_cols=80
pressed_key=''
list_message=''

cleanup() {
    tput cnorm 2>/dev/null || true
    stty sane 2>/dev/null || true
}
trap cleanup EXIT INT TERM

usage() {
    cat <<USAGE
Usage: $(basename "$0") [packet-root]

Browse A Dog's Tale LLM packet logs.

Defaults to:
  \$DOGS_TALE_LLM_PACKETS, or $HOME/DogsTaleSaves/LLM_Packets

Keys:
  List:    Up/Down select, Enter/Right opens request, Delete clears logs, q exits
  Viewer:  Up/Down scroll, PgUp/PgDn scroll pages, Right next file,
           Left previous file or list, Home/End jump, q exits
USAGE
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
fi

if [[ $# -gt 0 ]]; then
    ROOT="$1"
fi

sanitize_terminal() {
    screen_rows=$(tput lines 2>/dev/null || printf '24')
    screen_cols=$(tput cols 2>/dev/null || printf '80')
}

find_first_file() {
    local dir="$1"
    local kind="$2"
    find "$dir" -maxdepth 1 -type f -name "*_${kind}.*" | sort | head -n 1
}

format_timestamp() {
    local raw="$1"
    if [[ "$raw" =~ ^([0-9]{4})([0-9]{2})([0-9]{2})_([0-9]{2})([0-9]{2})([0-9]{2})_([0-9]{3})$ ]]; then
        printf '%s-%s-%s %s:%s:%s.%s' \
            "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}" "${BASH_REMATCH[3]}" \
            "${BASH_REMATCH[4]}" "${BASH_REMATCH[5]}" "${BASH_REMATCH[6]}" "${BASH_REMATCH[7]}"
    else
        printf '%s' "$raw"
    fi
}

file_timestamp() {
    local file
    file=$(basename "$1")
    if [[ "$file" =~ ^([0-9]{8}_[0-9]{6}_[0-9]{3})_ ]]; then
        printf '%s' "${BASH_REMATCH[1]}"
    else
        printf '00000000_000000_000'
    fi
}

load_entries() {
    timestamps=()
    agents=()
    request_ids=()
    dirs=()
    request_files=()
    response_files=()
    decode_files=()

    [[ -d "$ROOT" ]] || return 0

    local line timestamp agent request_id dir request response decode
    while IFS=$'\t' read -r timestamp agent request_id dir request response decode; do
        timestamps+=("$timestamp")
        agents+=("$agent")
        request_ids+=("$request_id")
        dirs+=("$dir")
        request_files+=("$request")
        response_files+=("$response")
        decode_files+=("$decode")
    done < <(
        while IFS= read -r -d '' request; do
            dir=$(dirname "$request")
            request_id=$(basename "$dir")
            agent=$(basename "$(dirname "$dir")")
            timestamp=$(file_timestamp "$request")
            response=$(find_first_file "$dir" "response")
            decode=$(find_first_file "$dir" "decode")
            printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
                "$timestamp" "$agent" "$request_id" "$dir" "$request" "$response" "$decode"
        done < <(find "$ROOT" -mindepth 3 -maxdepth 3 -type f -name '*_request.*' -print0) | sort
    )

    if (( selected >= ${#dirs[@]} )); then
        selected=$((${#dirs[@]} - 1))
    fi
    if (( selected < 0 )); then
        selected=0
    fi
}

refresh_entries_preserving_selection() {
    local selected_agent='' selected_request_id='' i
    if (( ${#dirs[@]} > 0 )); then
        selected_agent="${agents[$selected]}"
        selected_request_id="${request_ids[$selected]}"
    fi

    load_entries

    if [[ -z "$selected_agent" || -z "$selected_request_id" ]]; then
        return
    fi

    for ((i = 0; i < ${#dirs[@]}; i++)); do
        if [[ "${agents[$i]}" == "$selected_agent" && "${request_ids[$i]}" == "$selected_request_id" ]]; then
            selected=$i
            return
        fi
    done
}

clear_log_entries() {
    if [[ -z "$ROOT" || "$ROOT" == "/" ]]; then
        list_message="Refusing to clear unsafe log root: '$ROOT'"
        return
    fi

    if [[ ! -d "$ROOT" ]]; then
        list_message="Log root does not exist: $ROOT"
        return
    fi

    find "$ROOT" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
    selected=0
    load_entries
    list_message="Cleared all entries under $ROOT"
}

draw_header() {
    local title="$1"
    printf '\033[H\033[2J'
    printf '%s\n' "$title"
    printf '%*s\n' "$screen_cols" '' | tr ' ' '-'
}

draw_list() {
    sanitize_terminal
    draw_header "A Dog's Tale LLM packets: $ROOT"

    if (( ${#dirs[@]} == 0 )); then
        printf 'No request packets found.\n\n'
        if [[ -n "$list_message" ]]; then
            printf '%s\n\n' "$list_message"
        fi
        printf 'Expected files under: %s/<agent>/<requestId>/*_request.*\n\n' "$ROOT"
        printf 'Press q to exit.\n'
        return
    fi

    local visible_rows start end i marker formatted
    visible_rows=$((screen_rows - 4))
    start=$((selected - visible_rows / 2))
    if (( start < 0 )); then start=0; fi
    end=$((start + visible_rows))
    if (( end > ${#dirs[@]} )); then end=${#dirs[@]}; fi
    if (( end - start < visible_rows )); then
        start=$((end - visible_rows))
        if (( start < 0 )); then start=0; fi
    fi

    printf 'Use Up/Down, Enter/Right to open, Delete clears logs, q to quit. Total: %d\n' "${#dirs[@]}"
    if [[ -n "$list_message" ]]; then
        printf '%s\n' "$list_message"
    else
        printf '\n'
    fi
    printf '  %-23s  %-24s  %-3s %-3s %-3s  %s\n' "Timestamp" "Agent" "Req" "Rsp" "Dec" "Request ID"
    for ((i = start; i < end; i++)); do
        marker=' '
        if (( i == selected )); then marker='>'; fi
        formatted=$(format_timestamp "${timestamps[$i]}")
        local has_request has_response has_decode
        has_request='no'
        has_response='no'
        has_decode='no'
        if [[ -n "${request_files[$i]}" && -f "${request_files[$i]}" ]]; then has_request='yes'; fi
        if [[ -n "${response_files[$i]}" && -f "${response_files[$i]}" ]]; then has_response='yes'; fi
        if [[ -n "${decode_files[$i]}" && -f "${decode_files[$i]}" ]]; then has_decode='yes'; fi
        printf '%s %-23s  %-24s  %-3s %-3s %-3s  %s\n' \
            "$marker" "$formatted" "${agents[$i]}" "$has_request" "$has_response" "$has_decode" "${request_ids[$i]}"
    done
}

current_file_path() {
    case "$file_index" in
        0) printf '%s' "${request_files[$selected]}" ;;
        1) printf '%s' "${response_files[$selected]}" ;;
        *) printf '%s' "${decode_files[$selected]}" ;;
    esac
}

current_file_label() {
    case "$file_index" in
        0) printf 'request' ;;
        1) printf 'response' ;;
        *) printf 'decoded' ;;
    esac
}

current_file_line_count() {
    local file="$1"
    if [[ -n "$file" && -f "$file" ]]; then
        wc -l < "$file" | tr -d ' '
    else
        printf '1'
    fi
}

draw_viewer() {
    sanitize_terminal
    local file label total body_rows max_scroll formatted
    file=$(current_file_path)
    label=$(current_file_label)
    total=$(current_file_line_count "$file")
    body_rows=$((screen_rows - 5))
    if (( body_rows < 1 )); then body_rows=1; fi
    max_scroll=$((total - body_rows))
    if (( max_scroll < 0 )); then max_scroll=0; fi
    if (( scroll > max_scroll )); then scroll=$max_scroll; fi
    if (( scroll < 0 )); then scroll=0; fi
    formatted=$(format_timestamp "${timestamps[$selected]}")

    draw_header "$formatted  ${agents[$selected]}  ${request_ids[$selected]}  [$label]"
    printf 'Left previous/list, Right next, Up/Down scroll, PgUp/PgDn page, Home/End jump, q quit\n'
    if [[ -n "$file" && -f "$file" ]]; then
        printf '%s  lines %d-%d of %d\n\n' "$file" "$((scroll + 1))" "$((scroll + body_rows < total ? scroll + body_rows : total))" "$total"
        sed -n "$((scroll + 1)),$((scroll + body_rows))p" "$file"
    else
        printf 'No %s file found for:\n\n  %s\n' "$label" "${dirs[$selected]}"
    fi
}

read_key() {
    local timeout="${1:-}"
    local key next
    pressed_key=''
    if [[ -n "$timeout" ]]; then
        IFS= read -rsn1 -t "$timeout" key || return 1
    else
        IFS= read -rsn1 key || return 1
    fi
    if [[ "$key" == $'\033' ]]; then
        while IFS= read -rsn1 -t 1 next; do
            key+="$next"
            case "$next" in
                A|B|C|D|F|H|~) break ;;
            esac
        done
    fi
    pressed_key="$key"
}

open_selected() {
    mode="viewer"
    file_index=0
    scroll=0
}

handle_list_key() {
    local key="$1"
    case "$key" in
        q|Q) exit 0 ;;
        ''|$'\n'|$'\r'|$'\033[C') if (( ${#dirs[@]} > 0 )); then open_selected; fi ;;
        $'\033[3~'|$'\177') clear_log_entries ;;
        $'\033[A') if (( selected > 0 )); then selected=$((selected - 1)); fi ;;
        $'\033[B') if (( selected + 1 < ${#dirs[@]} )); then selected=$((selected + 1)); fi ;;
    esac
}

handle_viewer_key() {
    local key="$1"
    local file total body_rows max_scroll
    file=$(current_file_path)
    total=$(current_file_line_count "$file")
    body_rows=$((screen_rows - 5))
    if (( body_rows < 1 )); then body_rows=1; fi
    max_scroll=$((total - body_rows))
    if (( max_scroll < 0 )); then max_scroll=0; fi

    case "$key" in
        q|Q) exit 0 ;;
        $'\033[A') if (( scroll > 0 )); then scroll=$((scroll - 1)); fi ;;
        $'\033[B') if (( scroll < max_scroll )); then scroll=$((scroll + 1)); fi ;;
        $'\033[5~') scroll=$((scroll - body_rows)); if (( scroll < 0 )); then scroll=0; fi ;;
        $'\033[6~') scroll=$((scroll + body_rows)); if (( scroll > max_scroll )); then scroll=$max_scroll; fi ;;
        $'\033[C') if (( file_index < 2 )); then file_index=$((file_index + 1)); scroll=0; fi ;;
        $'\033[D')
            if (( file_index > 0 )); then
                file_index=$((file_index - 1))
                scroll=0
            else
                mode="list"
                refresh_entries_preserving_selection
            fi
            ;;
        $'\033[H'|$'\033[1~') scroll=0 ;;
        $'\033[F'|$'\033[4~') scroll=$max_scroll ;;
    esac
}

main() {
    if [[ ! -t 0 || ! -t 1 ]]; then
        printf 'This viewer must be run in an interactive terminal.\n' >&2
        exit 1
    fi

    load_entries
    stty -echo -icanon time 0 min 1
    tput civis 2>/dev/null || true

    while true; do
        if [[ "$mode" == "list" ]]; then
            draw_list
            if read_key 10; then
                handle_list_key "$pressed_key"
            else
                refresh_entries_preserving_selection
            fi
        else
            draw_viewer
            read_key || true
            handle_viewer_key "$pressed_key"
        fi
    done
}

main
