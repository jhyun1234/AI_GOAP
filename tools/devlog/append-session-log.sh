#!/usr/bin/env bash
# append-session-log.sh — Claude Code PostToolUse Bash 훅.
# HEAD 이동 감지 시 devlog/sessions/YYYY-MM-DD.md에 append/merge.
# 실패해도 exit 0 (훅이 Claude 워크플로우를 차단하지 않게).

set +e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$PROJ_ROOT" || exit 0

STATE_FILE="tools/devlog/.last-processed-commit"
ERR_FILE="tools/devlog/.hook-errors.log"

log_err() {
    printf '%s %s\n' "$(date -Iseconds 2>/dev/null || date)" "$1" >> "$ERR_FILE" 2>/dev/null
}

# ⚠️ ERR 트랩에서 exit 하지 않는다 (2026-07-24 버그 수정): 예전엔 'exit 0'이 있어
# 첫 non-zero 명령(예: 빈 줄에서 [ -n "$f" ]=false)에 훅이 조기 종료 → 맨 끝의
# STATE_FILE 갱신에 도달 못 함 → 상태가 고정돼 매 Bash 호출마다 같은 커밋 범위를
# 무한 재처리(세션 로그 수백 배 중복). set +e가 이미 중단을 막으므로 트랩은 로깅만 한다.
trap 'log_err "line $LINENO: $BASH_COMMAND"' ERR

current_head=$(git rev-parse HEAD 2>/dev/null)
[ -z "$current_head" ] && exit 0

mkdir -p tools/devlog

last_processed=""
if [ -f "$STATE_FILE" ]; then
    last_processed=$(tr -d '[:space:]' < "$STATE_FILE")
fi

[ "$current_head" = "$last_processed" ] && exit 0

if [ -n "$last_processed" ]; then
    new_commits=$(git rev-list --reverse "$last_processed..HEAD" 2>/dev/null)
else
    new_commits="$current_head"
fi

if [ -z "$new_commits" ]; then
    printf '%s' "$current_head" > "$STATE_FILE"
    exit 0
fi

get_tag() {
    local files="$1"
    if echo "$files" | grep -q 'Assets/Scripts/Core/GOAP/'; then echo '#planner'; return; fi
    if echo "$files" | grep -qE 'ActionDatabase|GOAPActionRegistry'; then echo '#action-system'; return; fi
    if echo "$files" | grep -qE 'WorldState|PlanningSlots'; then echo '#world-state'; return; fi
    if echo "$files" | grep -qE 'GoalArbiter|GoalSelector'; then echo '#goal-selection'; return; fi
    if echo "$files" | grep -qE 'Sensor|Perception'; then echo '#sensor'; return; fi
    if echo "$files" | grep -q 'Assets/Tests/'; then echo '#debug-viz'; return; fi
    if echo "$files" | grep -qE 'PlannerJob|JPSPathfinder|FlowField'; then echo '#performance'; return; fi
    if echo "$files" | grep -qE 'MessageBus|Faction'; then echo '#multi-agent'; return; fi
    echo '#misc'
}

process_commit() {
    local hash="$1"
    local msg date_str files today hhmm tag session_file
    msg=$(git log -1 --format='%s' "$hash")
    date_str=$(git log -1 --format='%ci' "$hash")
    files=$(git diff-tree --no-commit-id --name-only -r "$hash")
    today=$(echo "$date_str" | awk '{print $1}')
    hhmm=$(echo "$date_str" | awk '{print substr($2,1,5)}')
    tag=$(get_tag "$files")

    mkdir -p devlog/sessions
    session_file="devlog/sessions/$today.md"

    # 병합 판정 (60분 이내 + 태그 완전 일치)
    local should_merge=0
    if [ -f "$session_file" ]; then
        local last_hdr last_line_num last_hh last_mm last_min new_hh new_mm new_min diff last_tag
        last_hdr=$(grep -nE '^## \[[0-9]{2}:[0-9]{2}\] ' "$session_file" | tail -1)
        if [ -n "$last_hdr" ]; then
            last_line_num=$(echo "$last_hdr" | cut -d: -f1)
            last_hh=$(echo "$last_hdr" | sed -nE 's/^[0-9]+:## \[([0-9]{2}):([0-9]{2})\].*/\1/p')
            last_mm=$(echo "$last_hdr" | sed -nE 's/^[0-9]+:## \[([0-9]{2}):([0-9]{2})\].*/\2/p')
            new_hh=$(echo "$hhmm" | cut -d: -f1)
            new_mm=$(echo "$hhmm" | cut -d: -f2)
            last_min=$((10#$last_hh * 60 + 10#$last_mm))
            new_min=$((10#$new_hh * 60 + 10#$new_mm))
            diff=$((new_min - last_min))
            last_tag=$(awk "NR>=$last_line_num" "$session_file" \
                | grep -m1 -E '^\*\*태그:\*\*' \
                | sed -E 's/^\*\*태그:\*\* *//' \
                | tr -d '\r' \
                | sed -E 's/[[:space:]]+$//')
            if [ "$diff" -ge 0 ] && [ "$diff" -le 60 ] && [ "$last_tag" = "$tag" ]; then
                should_merge=1
            fi
        fi
    fi

    if [ "$should_merge" -eq 1 ]; then
        {
            printf -- '- (+%s) %s\n' "$hhmm" "$msg"
            printf '%s\n' "$files" | while IFS= read -r f; do
                if [ -n "$f" ]; then printf '  - %s\n' "$f"; fi
            done
        } >> "$session_file"
    else
        {
            if [ -s "$session_file" ]; then printf '\n'; fi
            printf '## [%s] %s\n\n' "$hhmm" "$msg"
            printf '**태그:** %s\n\n' "$tag"
            printf '**무엇을 했나:**\n- %s\n\n' "$msg"
            printf '**변경 파일:**\n'
            printf '%s\n' "$files" | while IFS= read -r f; do
                if [ -n "$f" ]; then printf '  - %s\n' "$f"; fi
            done
            printf '\n'
        } >> "$session_file"
    fi
}

for c in $new_commits; do
    process_commit "$c"
done

printf '%s' "$current_head" > "$STATE_FILE"
exit 0
