# ep04s — 클라우드 루틴이 멈춘 자리 (기획팀 SKIP 접수)

2026-08-03 클라우드 루틴. `notes/planner.md` 의 **SKIP (BLOCKED)** 를 그대로 받아
**작성·검수·마스터를 부르지 않고 멈췄다.** `scene.json` 을 만들지 않았으므로 로컬 렌더는
"대본 대기"로 조용히 넘어간다(설계된 결말이지 사고가 아니다).

- 간격 게이트: **통과** — `order` 안 회차의 마지막 `scene.json` 커밋 `2026-08-02T02:04:51Z`
  기준 **22.08시간** 경과(한도 12시간).
- `node tools/scene-video/backlog.mjs --extend` → **PENDING / ep04s**. `schedule.json` 은
  건드리지 않았다(편입이 일어나지 않았으므로 커밋 대상도 아니다).
- 반려 횟수: **검수 0회 · 마스터 0회.** 두 단계에 도달하지 못했다.

## 막힌 것 — 원문에 손이 닿지 않는다

`sources.ep04s.local` 이 `null` 이라 공개 URL 을 받아와야 하는데, 이 세션의 이그레스 정책이
`gamedevclaude.blogspot.com` 을 막는다. 루틴 세션에서 직접 재확인했다:

```
curl https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-ai-m1.html
  → curl: (56) CONNECT tunnel failed, response 403

$HTTPS_PROXY/__agentproxy/status → recentRelayFailures
  { "kind": "connect_rejected",
    "detail": "gateway answered 403 to CONNECT (policy denial or upstream failure)",
    "host": "gamedevclaude.blogspot.com:443" }
```

우회는 시도하지 않았다 — `/root/.ccr/README.md` 가 막힌 호스트는 보고하라고 못 박는다.

## 🔑 정정 1 — "shallow 라 옛 커밋을 못 본다"는 이제 해당 없다

`planner.md` 43행이 shallow 를 이유로 커밋 복원 가능성을 열어 두었는데, 루틴이
`git fetch --unshallow` 로 **685커밋 전체를 받아 다시 확인했다.** 결론은 바뀌지 않고 더 단단해졌다:

```
git log --all --pretty=format: --name-only --diff-filter=ACDMR -- 'tools/blog-automation/published/*'
```

역대 등장한 published 파일 **16개 전수**에 M1 글이 없다(삭제된 사본도, 다른 브랜치에도 없다).
즉 **리포에서 복원할 방법이 없다는 것이 확정**이다. 남은 길은 사람이 넣는 것뿐이다.

## 🔑 정정 2 — 이 컨테이너에 크로미움이 **있다**

`planner.md` 164행이 "크로미움 없음"이라고 적었지만 사실이 아니다.
`episodes/ep03s/notes/cloud-pixel-check.md` 가 이미 남겨 둔 그대로다:

- 실체: `/opt/pw-browsers/chromium-1194/chrome-linux/chrome` (Chromium 141.0.7390.37)
- `lib-node.mjs` 의 `findBrowser()` 목록에 없어서 "없다"고 오인된다.
  `--no-sandbox` 를 붙인 래퍼를 `/usr/bin/chromium` 에 두면 그대로 돈다(컨테이너 한정, 리포에 안 남김).
- 이번 루틴이 실제로 그렇게 두고 `node tools/scene-video/check.mjs ep03s` 를 돌려 확인했다 —
  결정성 3패스 · 3색 팔레트 · 안전영역 · 좌우/아래 잘림 전부 OK, 실패는 `timed.json`
  한 건뿐(TTS 모델 645MB 가 없어 이 환경에서 못 만든다).

**다음 회차 클라우드 루틴은 픽셀 판정을 미리 포기하지 마라.** 검수·마스터가 닫을 수 있는
항목이 문서가 말하는 것보다 많다. 여전히 못 하는 것은 TTS(→ 실측 자막 길이·말 속도)와
mp4 렌더뿐이다.

## 사람이 할 일 — 둘 중 하나면 다음 실행이 바로 PROCEED

1. **(권장)** 발행된 M1 글 HTML 을 `tools/blog-automation/published/` 에 커밋하고
   `schedule.json` 의 `sources.ep04s.local` 을 그 파일명으로 채운다.
   표·`pre`·`svg` 제거 방어선까지 같이 살아나므로 근본 처방이다.
2. 이 환경의 이그레스 정책에서 `gamedevclaude.blogspot.com` 을 허용한다.

⚠️ `order` 에서 `local: null` 인 회차는 **ep04s 하나뿐**이다(ep02s 는 이미 만들어졌다).
그러니 이 한 건만 풀리면 ep05s~ep13s 는 전부 리포 사본으로 진행된다.

🔴 **ep04s 를 건너뛰고 ep05s 를 먼저 만들지 않았다.** 순서를 흔들면 예고 사슬이 끊기고,
구성안 55~58행이 3막에 대해 "여기서 순서를 흔들면 바로 어려워진다"고 못 박고 있다.
`backlog.mjs` 도 `order` 순으로만 고른다.
