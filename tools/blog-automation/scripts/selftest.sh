#!/usr/bin/env bash
# 블로그 자동화 파이프라인 자가 점검.
# 2026-07-28 신설 — 그날 실제로 파이프라인을 망가뜨린 결함들을 회귀 테스트로 고정한다.
# 발행하지 않는다. 읽기와 순수 계산만 수행한다.
#
#   bash tools/blog-automation/scripts/selftest.sh
#
# 종료 코드 0 = 전부 통과. 1 = 하나라도 실패.

cd "$(dirname "$0")/../../.." || exit 2
PASS=0; FAIL=0
ok()   { printf '  \033[32mPASS\033[0m  %s\n' "$1"; PASS=$((PASS+1)); }
bad()  { printf '  \033[31mFAIL\033[0m  %s\n' "$1"; FAIL=$((FAIL+1)); }
sect() { printf '\n\033[1m%s\033[0m\n' "$1"; }

STATE=tools/blog-automation/state
AGENTS=.claude/agents

sect "1. 분량 계측 — 로케일 미지정 시 바이트 오염 (07-28 잘못된 축약 지시의 원인)"
printf '가나다' > /tmp/_st.txt
[ "$(LC_ALL=C.UTF-8 wc -m < /tmp/_st.txt)" = "3" ] \
  && ok "LC_ALL=C.UTF-8 wc -m 이 문자수를 센다" \
  || bad "LC_ALL=C.UTF-8 wc -m 이 3을 반환하지 않음"
for f in "$AGENTS/blog-writer.md" "$AGENTS/blog-reviewer.md" tools/blog-automation/routine-prompt.md; do
  if grep -q 'LC_ALL=C.UTF-8 wc -m' "$f"; then ok "$(basename "$f") 가 로케일 지정 명령을 쓴다"
  else bad "$(basename "$f") 에 LC_ALL=C.UTF-8 이 없다"; fi
done
rm -f /tmp/_st.txt

sect "2. 멀티바이트 스트림 — 청크 경계 문자 깨짐 (07-22·07-28 관측)"
# 주석은 제외한다 — 수정 이력을 설명하는 주석이 옛 패턴을 인용하고 있어 오탐이 난다.
if grep -hvE '^\s*(//|\*|/\*)' tools/blog-automation/scripts/*.js \
     | grep -qE "data \+= (c|chunk)"; then
  bad "구 방식(data += chunk)이 코드에 남아 있다 — 한글이 깨진다"
else ok "모든 스크립트가 Buffer.concat 방식을 쓴다 (주석 제외 검사)"; fi
node -e '
const b=Buffer.from("안전","utf8"), c=[b.subarray(0,1),b.subarray(1)];
let bad=""; for(const x of c) bad+=x;
const good=Buffer.concat(c).toString("utf8");
if(bad.includes("�") && good==="안전") process.exit(0); process.exit(1);
' && ok "청크 분할 시 구 방식은 깨지고 신 방식은 보존된다" \
  || bad "멀티바이트 청크 테스트 실패"

sect "3. 상태 파일 무결성 (07-28 draft가 소재를 삼킨 사고)"
LAST=$(grep -oP '`latest_commit`:\s*`\K[0-9a-f]+' $STATE/blog_last_published_commit.md | head -1)
PUB=$(grep -oP '`publish_status`:\s*\**\K[A-Z]+' $STATE/blog_last_published_commit.md | head -1)
[ -n "$LAST" ] && ok "latest_commit 파싱됨: $LAST" || bad "latest_commit 파싱 실패 — 게이트가 작동 못 함"
git cat-file -e "$LAST^{commit}" 2>/dev/null \
  && ok "latest_commit 이 실존하는 커밋이다" || bad "latest_commit 이 저장소에 없다"
[ "$PUB" = "PUBLISHED" ] \
  && ok "publish_status = PUBLISHED (소재 소비 상태 정상)" \
  || bad "publish_status = $PUB — DRAFT면 소재 미소비인데 latest_commit이 갱신된 것"
grep -q 'DRAFTED_STATE' tools/blog-automation/routine-prompt.md \
  && ok "게이트에 publish_status 무결성 가드가 있다" \
  || bad "게이트가 publish_status 를 읽지 않는다"

sect "4. 우선 소재 상태값 (07-28 미정의 DRAFT_PENDING 사고)"
N=$(grep -c '^\*\*STATUS: ACTIVE' $STATE/blog_next_material_priority.md)
[ "$N" -le 1 ] && ok "살아있는 ACTIVE 마커 $N 개 (0 또는 1이어야 함)" \
              || bad "ACTIVE 마커가 $N 개 — 옛 항목이 오탐된다"
if grep -qE '^\*\*STATUS: (ACTIVE|CONSUMED|이력)' $STATE/blog_next_material_priority.md \
   && ! grep -q 'STATUS: DRAFT_PENDING' $STATE/blog_next_material_priority.md; then
  ok "정의된 상태값만 쓰인다 (ACTIVE/CONSUMED/이력)"
else bad "미정의 상태값이 있다"; fi

sect "5. 옛 정책 잔존 — draft 폴백 이전 문구"
STALE=0
for pat in '게시를 조용히 포기' '자동 중단.*하고 .memory/' '`--draft` 플래그 없이'; do
  if grep -rqE "$pat" $AGENTS/blog-*.md tools/blog-automation/*.md Docs/블로그_자동화_수익화_기획서.md 2>/dev/null; then
    bad "옛 문구 잔존: $pat"; STALE=1
  fi
done
[ $STALE = 0 ] && ok "옛 '게시 포기' 정책 문구 0건"
if grep -rq 'memory/blog_' $AGENTS/blog-*.md tools/blog-automation/README.md \
     tools/blog-automation/routine-prompt.md Docs/블로그_자동화_수익화_기획서.md 2>/dev/null; then
  bad "옛 memory/ 상태 경로가 남아 있다"
else ok "상태 경로가 전부 state/ 로 통일됨"; fi

sect "6. 지시서 정합"
C=$(grep -cE '^[0-9]+\. \*\*' $AGENTS/blog-reviewer.md)
V=$(grep -oP '위 \K[0-9]+(?=개 항목)' $AGENTS/blog-reviewer.md | head -1)
[ "$C" = "$V" ] && ok "검수 항목 수($C)와 판정 문구($V)가 일치" \
               || bad "검수 항목 $C 개인데 판정 문구는 $V 개라고 말한다"
grep -q 'PIPELINE_RESULT: <PUBLISHED|SKIPPED|DRAFTED' tools/blog-automation/routine-prompt.md \
  && ok "PIPELINE_RESULT enum 에 DRAFTED 가 있다" || bad "DRAFTED 가 enum 에 없다"
grep -q 'verify_at' $AGENTS/blog-planner.md && grep -q 'verify_at' $AGENTS/blog-reviewer.md \
  && grep -q 'verify_at' $AGENTS/blog-master.md \
  && ok "대조 기준 시점(verify_at)이 기획·검수·마스터에 모두 있다" \
  || bad "verify_at 규칙이 일부 지시서에 없다"

sect "7. 에이전트 model 필드"
BADM=0
for f in $AGENTS/*.md; do
  M=$(grep -m1 '^model:' "$f" | sed 's/^model: *//')
  case "$M" in
    sonnet|opus|haiku|inherit|claude-*) ;;
    *) bad "$(basename "$f"): 유효하지 않은 model '$M'"; BADM=1 ;;
  esac
done
[ $BADM = 0 ] && ok "모든 에이전트 model 값이 유효하다"

sect "8. Blogger 자격증명·연결"
if [ -f tools/blog-automation/credentials/blogger_token.json ]; then
  ok "자격증명 파일 존재"
  if node tools/blog-automation/scripts/blogger-client.js get-blog >/tmp/_gb.json 2>&1; then
    grep -q '"kind": "blogger#blog"' /tmp/_gb.json && ok "Blogger API 연결 정상" || bad "get-blog 응답 형식 이상"
    grep -q '\xef\xbf\xbd' /tmp/_gb.json && bad "응답에 U+FFFD 있음 — 인코딩 깨짐" || ok "응답 인코딩 정상(U+FFFD 0건)"
  else bad "Blogger API 호출 실패 — 토큰 만료 가능"; fi
  rm -f /tmp/_gb.json
else bad "자격증명 없음"; fi

sect "9. devlog 1차 소재 공급 (blog-planner가 굶지 않는 조건)"
H=tools/devlog/append-session-log.sh
grep -q "format='%b'" $H \
  && ok "훅이 커밋 본문(%b)을 읽는다 — 서사 공급원" \
  || bad "훅이 %b를 안 읽는다 — 세션 로그에 왜/어떻게가 0줄이 된다"
grep -q 'core.quotepath=false' $H \
  && ok "한글 파일명이 8진 이스케이프로 깨지지 않는다" \
  || bad "core.quotepath 미지정 — 한글 파일명이 깨진다"
[ -f tools/devlog/check-narrative.sh ] \
  && ok "서사 보강 트리거 스크립트 존재" || bad "check-narrative.sh 없음"
node -e '
const j=require("./.claude/settings.json");
const s=JSON.stringify(j.hooks||{});
process.exit(s.includes("check-narrative") && s.includes("append-session-log") ? 0 : 1);
' && ok "훅 2종이 settings.json에 배선됨 (PostToolUse + Stop)" \
  || bad "훅 배선 누락 — settings.json 확인"

printf '\n\033[1m결과: %d PASS / %d FAIL\033[0m\n' "$PASS" "$FAIL"
[ "$FAIL" = 0 ] || exit 1
