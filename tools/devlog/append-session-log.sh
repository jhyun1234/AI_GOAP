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

# 커밋 본문(%b)에서 트레일러·잡음을 걷어내고 앞뒤 빈 줄을 정리한다.
# 이 프로젝트의 커밋 본문에는 "왜 그렇게 했나"가 이미 쓰여 있는데, 2026-07-28까지
# 훅이 %s(제목)만 읽고 본문을 통째로 버리고 있었다 — 세션 로그에 서사가 0줄이던
# 근본 원인. blog-planner의 1차 소스가 이 파일이므로 버리면 안 된다.
get_body() {
    git log -1 --format='%b' "$1" \
        | sed -E '/^(Co-Authored-By|Signed-off-by|Co-authored-by):/d' \
        | sed -E '/^🤖 Generated with/d' \
        | sed -E 's/[[:space:]]+$//' \
        | awk 'NF {p=1} p' \
        | awk '{ lines[NR]=$0 } END { last=0; for (i=1;i<=NR;i++) if (lines[i] ~ /[^[:space:]]/) last=i; for (i=1;i<=last;i++) print lines[i] }'
}

process_commit() {
    local hash="$1"
    local msg body date_str files today hhmm tag session_file
    msg=$(git log -1 --format='%s' "$hash")
    body=$(get_body "$hash")
    date_str=$(git log -1 --format='%ci' "$hash")
    # core.quotepath=false: 끄지 않으면 한글 파일명이 "Docs/\352\260\234..." 8진 이스케이프로
    # 기록된다 (2026-07-28 발견 — 이 프로젝트는 Docs/ 아래 한글 파일명이 다수라 실피해).
    files=$(git -c core.quotepath=false diff-tree --no-commit-id --name-only -r "$hash")

    # 🔴 세션 로그만 건드린 커밋은 항목을 만들지 않는다 (2026-08-03 버그 수정).
    # 안 그러면 순환이 끝나지 않는다 — 커밋하면 이 훅이 항목을 붙여 작업 트리가 더러워지고,
    # 그 항목을 커밋하면 훅이 그 커밋에 대한 항목을 또 붙인다. 실제로 이날 세 바퀴 돌았고
    # ("ep04s 재구성 커밋 항목 추가" → "로그 전용 커밋 항목 추가" → ...) Stop 훅이 매 턴
    # 깨끗한 트리를 요구해서 멈추지 않았다. 로그가 로그를 기록하는 항목은 blog-planner 의
    # 1차 소스로서도 값이 0 이라 아예 안 만드는 것이 맞다.
    # grep -qv = "패턴에 안 맞는 줄이 있나". 하나도 없으면(전부 세션 로그면) 1 을 반환하고
    # ! 가 뒤집어 참이 되어 건너뛴다.
    if [ -n "$files" ] && ! echo "$files" | grep -qvE '^devlog/sessions/[^/]+\.md$'; then
        return 0
    fi

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
            if [ -n "$body" ]; then
                printf '%s\n' "$body" | while IFS= read -r bl; do
                    printf '  %s\n' "$bl"
                done
            fi
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
            # 커밋 본문 = 작성자가 이미 남긴 "왜/어떻게". 있으면 그대로 싣는다.
            if [ -n "$body" ]; then
                printf '**왜 이렇게 했나 (커밋 본문):**\n'
                printf '%s\n' "$body"
                printf '\n'
            fi
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
