# 블로그 호스트 허용하는 법 — 그리고 왜 "막은 적 없는데" 막혔나

사용자 확인(2026-08-03): *"이전까지 대본 만들 때 내 블로그 글을 막은 적이 없다."*
**맞다. 막으신 적 없다.** 새로 막힌 게 아니라 **이번이 처음으로 필요해진 것**이다.

---

## 1. 진단 — 왜 지금 처음 터졌나

`scene.json` 을 누가 언제 커밋했는지 보면 그대로 나온다.

| 회차 | 커밋 시각 | 커밋한 사람 | 어디서 돌았나 |
|---|---|---|---|
| ep00s | 2026-07-29T15:56+09:00 | 최종현 | **로컬 PC** |
| ep01s | 2026-07-30T12:38+09:00 | 최종현 | **로컬 PC** |
| ep02s | 2026-07-31T13:27+09:00 | 최종현 | **로컬 PC** ← `local: null` 이라 WebFetch 를 썼고, **통했다** |
| ep03s | 2026-08-02T02:04+00:00 | Claude | **클라우드**(첫 무인 실행) ← 리포에 사본이 있어 **네트워크가 필요 없었다** |
| ep04s | — | — | **클라우드** ← `local: null` **이면서** 클라우드인 **첫 회차** |

즉 **ep04s 가 "클라우드에서 바깥 네트워크가 필요한 최초의 회차"** 다.
ep02s 때 WebFetch 가 통한 건 그게 **사용자 PC 에서 돌았기 때문**이다. 그 PC 엔 이그레스
프록시가 없다. 클라우드는 처음부터 제한 정책이었고, 지금까지 그걸 건드릴 일이 없었을 뿐이다.

### 실측 — 지금 무엇이 열려 있고 무엇이 막혔나

```
api.github.com              200   ← 열림
registry.npmjs.org          200   ← 열림
code.claude.com             302   ← 열림
claude.ai                   403   ← 열림(403 은 사이트가 낸 응답)
www.googleapis.com          404   ← 열림  🔑 블로그 발행이 여기로 나간다
oauth2.googleapis.com       404   ← 열림
gamedevclaude.blogspot.com  000   ← 🔴 CONNECT 거부 (프록시가 막음)
www.blogger.com             000   ← 🔴 CONNECT 거부
```

이 패턴이 정확히 **`Trusted` 네트워크 접근 수준**이다 — 공식 문서의 기본 허용 목록에
GitHub · 패키지 레지스트리 · `*.googleapis.com` 은 있고 `blogspot.com` 은 없다.

### 🔑 그래서 블로그 파이프라인은 멀쩡히 돌았다

`tools/blog-automation/scripts/blogger-client.js` 139·165·198·222행이 발행에 쓰는 주소는
**`https://www.googleapis.com/blogger/v3/…`** 다. `*.googleapis.com` 이 기본 허용 목록에
있으므로 **글 쓰는 쪽은 한 번도 막힌 적이 없다.**
막힌 건 **발행된 글을 다시 읽어오는 쪽**(`blogspot.com`)이고, 그건 영상 파이프라인이
`local` 사본 없는 회차를 클라우드에서 만들 때만 필요하다. 이번이 그 첫 사례다.

---

## 2. 고치는 법 — 환경 하나만 고치면 된다

이 계정의 클라우드 환경은 **`Default` 하나뿐**이고(`env_011dy96U4KfgKbckWYWVqzN1`),
영상 루틴과 블로그 루틴이 **둘 다 그 환경**을 쓴다.

| 루틴 | 크론 | 환경 |
|---|---|---|
| `Video script generator` | `0 0 * * *` (매일 09:00 KST) | **Default** |
| `aigoap-blog-daily-gated` | `3 4 2-30/2 * *` (격일 13:03 KST) | **Default** |

### 절차

1. <https://claude.ai/code> 를 연다.
2. 메시지 입력창 **바로 위 줄의 구름 아이콘**(현재 환경 이름 `Default` 이 적혀 있다)을 누른다.
   🔑 **설정 페이지나 직통 URL 이 없다.** 이 선택기가 유일한 입구다.
3. 목록의 `Default` 위에 마우스를 올리면 오른쪽에 **톱니바퀴**가 뜬다. 그걸 누른다.
4. **Network access** 를 `Trusted` → **`Custom`** 으로 바꾼다.
5. **Allowed domains** 칸에 **한 줄에 하나씩** 적는다:

   ```
   gamedevclaude.blogspot.com
   ```

   넉넉히 하려면 `*.blogspot.com` 으로 적어도 된다(앞의 `*.` 는 모든 서브도메인을 뜻한다).
6. 🔴🔴 **「Also include default list of common package managers」 를 반드시 체크한다.**
   **이걸 빠뜨리면 적은 도메인만 남고 기본 목록이 통째로 사라진다.** 그러면:
   - `*.googleapis.com` 이 죽어 **블로그 발행 루틴이 그날부터 실패**하고,
   - `registry.npmjs.org` 이 죽어 의존성 설치가 깨지고,
   - GitHub 도 목록에서 빠진다(그쪽은 별도 프록시라 git 자체는 살지만 기대지 말 것).

   **이 한 칸이 이번 변경에서 가장 위험한 자리다.** 도메인을 더하는 설정이 아니라
   **목록을 갈아치우는 설정**이기 때문이다.
7. 저장한다.

### 저장 후 알아 둘 것

- **이미 돌고 있는 세션에는 적용되지 않는다.** 다음에 시작하는 세션부터다.
  이 루틴의 다음 실행은 **2026-08-04 00:08 UTC(= 09:08 KST)** 이므로 그때 자동으로 적용된다.
- 허용 호스트를 바꾸면 **환경 캐시가 다시 만들어진다**(setup script 재실행). `Default` 에는
  setup script 가 없으므로 이번엔 아무 일도 일어나지 않는다.
- MCP 커넥터(Notion · Google Drive)는 이 목록과 무관하다 — Anthropic 서버를 거쳐 나간다.

---

## 3. 고친 뒤 파이프라인이 어떻게 이어지나 (손댈 것 없음)

다음 실행에서 **자동으로 여기까지 간다.**

```
09:08 KST  클라우드 루틴
  ├ 간격 게이트   마지막 scene.json 커밋(ep03s, 08-02)에서 12시간 넘음 → 통과
  ├ backlog.mjs   PENDING / ep04s        (schedule.json 그대로, 편입 없음)
  ├ 기획팀        WebFetch 성공 → 본문 확보 → PROCEED
  │                🔑 WebFetch 로 받은 본문엔 표·pre·svg 제거 정규식이 안 걸린다.
  │                   기획팀이 `|` 조각·CSS 조각을 브리프 맨 위에 보고하게 돼 있다.
  ├ 작성팀        scene.json + kinds/*.js
  ├ 검수팀 → 마스터
  └ push → scene-script-auto-merge.yml 이 main 에 fast-forward
15:00 KST  로컬 PC (routine.cmd)
  └ git pull → tts.mjs → render.mjs → check.mjs 13종 → build/upload.txt
사람       스튜디오에 mp4 끌어다 놓고 upload.txt 붙여넣고 공개 전환
```

**코드도 설정도 더 고칠 것이 없다.** `schedule.json` 도 그대로 두면 된다 —
`sources.ep04s.local` 은 `null` 인 채로 두고 `url` 로 받아오는 게 정상 동작이다.

### 이번 한 번이면 끝나는 이유

`local: null` 인 회차는 **ep02s · ep04s 둘뿐이고 ep02s 는 이미 끝났다.** 그리고 앞으로
재발하지 않는다 — 게시팀이 발행 성공 시 `published/` 에 사본을 남기고(`blog-publisher.md` 59행),
`backlog.mjs --extend` 가 편입하는 회차는 `published/` 에 파일이 있는 글만 고른다(구조상
`local` 이 빌 수 없다). **ep04s 가 마지막 고아다.**

다만 **허용은 그대로 두는 게 낫다.** 앞으로도 클라우드에서 발행 글을 다시 읽어야 할 일
(사실 대조·URL 확인 등)이 생기면 그때 또 막히기 때문이고, 열어 두는 비용은 없다.

---

## 4. 대안(권장 안 함) — 리포에 사본을 넣는 길

`notes/unblock-decision.md` 에 적은 그대로다. `tools/blog-automation/published/` 는
**블로그 파이프라인의 정본 디렉터리**이고, `blog-writer.md` 27행·`blog-reviewer.md` 108행이
그 안의 **"최근 3편"** 을 인트로 중복 검사 입력으로 쓴다. 그 3편을 고르는 건 결정적 코드가
아니라 에이전트 판단이라 mtime 으로 고르면 갓 넣은 파일이 1등으로 올라온다.
**이후 발행글에 영향이 없는 방향**을 택하라는 사용자 기준에 따라 이 길은 버렸다.
