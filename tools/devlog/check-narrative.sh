#!/usr/bin/env bash
# check-narrative.sh — Claude Code Stop 훅.
#
# 세션 로그의 "서사 보강" 트리거. append-session-log.sh 훅은 커밋에서 뽑을 수 있는
# 것(제목·본문·파일·태그)만 쓴다. 템플릿(Docs/devlog-workflow.md)의 나머지 두 칸
# — **막혔던 문제 / 해결 방법**, **다음에 할 일** — 은 대화에만 있어서 사람/Claude가
# 채워야 하는데, 2026-07-28까지 **그걸 부르는 장치가 없었다.**
# 결과: 서술은 누가 기억한 날(07-15·17·27)에만 남고, 코딩이 바쁜 날일수록 비었다.
# 하필 바쁜 날이 블로그 1차 소재로 가장 값진 날이다.
#
# 동작: 하루에 최대 1회만 발화한다(날짜 기준 가드). 발화 시 exit 2 —
# Stop 훅에서 exit 2는 종료를 막고 stderr를 Claude에게 전달한다.
#
# 끄려면: DEVLOG_NUDGE=off
#
# 점검만 하고 싶으면 (훅 아닌 수동 실행):  bash tools/devlog/check-narrative.sh --dry

set +e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$PROJ_ROOT" || exit 0

[ "$DEVLOG_NUDGE" = "off" ] && exit 0

# 🔴 무인 블로그 routine 세션에서는 절대 발화하지 않는다.
# routine 샌드박스는 BLOGGER_* / BLOG_CONFIG env var로 자격증명을 받는다(로컬에는 없다).
# 여기서 exit 2를 내면 파이프라인 종료를 막아 PIPELINE_RESULT 출력이 어그러진다.
# 커밋 종류 필터만으로는 부족하다 — 같은 날 개발 커밋이 먼저 올라가 있으면 routine
# 세션이 그걸 "실작업"으로 세기 때문이다.
if [ -n "$BLOGGER_TOKEN" ] || [ -n "$BLOG_CONFIG" ] || [ -n "$BLOGGER_CLIENT_SECRET" ]; then
    [ "$1" = "--dry" ] && echo "SKIP — 무인 routine 환경 감지 (BLOGGER_* env var)"
    exit 0
fi

DRY=0
[ "$1" = "--dry" ] && DRY=1

GUARD="tools/devlog/.narrative-nudged"
TODAY=$(date +%Y-%m-%d)
LOG="devlog/sessions/$TODAY.md"

# 하루 1회 가드 — 먼저 확인한다. HEAD 기준으로 잡으면 보강 커밋이 HEAD를 바꿔
# 다시 발화하는 루프가 생긴다.
if [ "$DRY" -eq 0 ] && [ -f "$GUARD" ] && [ "$(tr -d '[:space:]' < "$GUARD")" = "$TODAY" ]; then
    exit 0
fi

# 오늘 커밋 중 파이프라인 자기 커밋을 제외한 실작업 커밋 수.
# blog 자동화 routine 세션은 chore(blog)/docs(blog)만 만들므로 여기서 0이 되어
# 무인 실행을 방해하지 않는다.
REAL=$(git log --since="$TODAY 00:00" --until="$TODAY 23:59" --format='%s' 2>/dev/null \
       | grep -vcE '^(chore|docs)\(blog\)')
[ -z "$REAL" ] && REAL=0

# 실작업이 적은 날은 굳이 부르지 않는다.
if [ "$REAL" -lt 3 ]; then
    [ "$DRY" -eq 1 ] && echo "SKIP — 오늘 실작업 커밋 $REAL건 (<3)"
    exit 0
fi

if [ ! -f "$LOG" ]; then
    BLOCKS=0; STUCK=0; NEXT=0
else
    # ⚠️ 반드시 '필드 헤더'로만 센다 (`^**필드명`). 느슨하게 본문 전체를 검색하면,
    # 커밋 본문이 필드 이름을 **언급**하기만 해도 보강된 것으로 오판한다 —
    # 2026-07-28 실제 발생: 이 트리거를 만든 커밋 본문이 "막혔던 문제 / 해결 방법"을
    # 인용했고, 훅이 본문을 로그에 실으면서 트리거가 스스로를 눈멀게 했다.
    # 병합 블록의 본문은 2칸 들여쓰기되므로 `^\*\*` 앵커가 자연히 걸러 준다.
    BLOCKS=$(grep -c '^## \[' "$LOG")
    STUCK=$(grep -c '^\*\*막혔던 문제' "$LOG")
    NEXT=$(grep -c '^\*\*다음에 할 일' "$LOG")
fi

if [ "$DRY" -eq 1 ]; then
    echo "날짜=$TODAY  실작업커밋=$REAL  블록=$BLOCKS  막힘절=$STUCK  다음절=$NEXT"
fi

# 두 칸이 모두 0일 때만 부른다 — 하나라도 있으면 이미 보강한 날이다.
if [ "$STUCK" -gt 0 ] || [ "$NEXT" -gt 0 ]; then
    [ "$DRY" -eq 1 ] && echo "OK — 보강 흔적 있음"
    exit 0
fi

if [ "$DRY" -eq 1 ]; then
    echo "NUDGE — 보강 필요 (exit 2 상당)"
    exit 0
fi

# 가드를 먼저 쓴다. 이후 어떤 경로로 끝나든 오늘 다시 발화하지 않는다.
printf '%s' "$TODAY" > "$GUARD" 2>/dev/null

cat >&2 <<EOF
[devlog] 오늘 세션 로그에 서사 두 칸이 비어 있습니다 — $LOG
  실작업 커밋 $REAL건 / 로그 블록 $BLOCKS개 / "막혔던 문제" 0 / "다음에 할 일" 0

훅은 커밋에서 뽑을 수 있는 것만 씁니다. 아래 두 칸은 대화에만 있어서 훅이 채울 수
없습니다. 이 파일의 마지막 블록에 지금 덧붙여 주세요 (없으면 "해당 없음"이라고 쓰고 넘어가면
됩니다 — 빈 칸으로 두는 것만 피하면 됩니다):

  **막혔던 문제 / 해결 방법:** 오늘 진단에 시간이 걸린 것, 처음 접근이 틀렸던 것
  **다음에 할 일:** 다음 세션이 이어받을 지점

이 로그는 blog-planner의 1차 소재입니다. 비면 다음 회차가 명세서로 우회하거나 굶습니다.
(이 알림은 하루 1회만 뜹니다. 끄려면 DEVLOG_NUDGE=off)
EOF
exit 2
