---
name: project-m10-post-status
description: M10 "야생 위협과 방랑자" 블로그 글(2026-07-28 로컬 수동 사이클) 파이프라인 상태 — Step 4·Step 6 모두 1회 APPROVED, 게시팀 위임
metadata:
  type: project
---

소재: M10 "야생 위협과 방랑자" (`db82ffd`~`caa23b0`). 2026-07-28 로컬 수동 사이클.

- Step 4: APPROVED (1회). 스냅샷 = `.claude/agent-memory/blog-master/snapshots/step4_2026-07-28_m10-threat-wanderer.md`
- Step 6: APPROVED (1회). 스냅샷 = `.claude/agent-memory/blog-master/snapshots/step6_2026-07-28_m10-threat-wanderer.md`
- 게시팀(blog-publisher)에 Step 7 위임. 판정서 = `tools/blog-automation/.staging/06_verdict.md`

**Why:** 직전 사이클(2026-07-28 원격 auto-run)에서 같은 M10 소재가 연속 3회 반려로
draft 강등됐다. 반려 진원지는 세 지점 — 성격 표시명(`새침` 오기), 방랑자 수락 시한
(`0.7일`), 명세서 코드 스케치의 플레이스홀더 문자열. 이번 회차는 세 지점을 전부 원본에서
직접 확인해 통과시켰고, 반려 0회로 끝났다.

**How to apply:**
- 이 글의 화면 문자열·수치는 **`caa23b0` 시점** 기준이지 HEAD 기준이 아니다. 사후에
  "값이 틀렸다"는 지적이 들어오면 HEAD가 아니라 `git show caa23b0:<경로>`로 대조할 것.
- 게시 성공 후 `blog_last_published_commit.md`의 `latest_commit`이 `caa23b0`으로 갱신됐는지
  확인. 직전 draft 강등 때 이 값이 잘못 갱신됐다가 되돌려진 이력이 있다(파일 상단 원복 기록).
- 경로 정정: 방랑자 프롬프트 출처는 `Assets/Scripts/M0/World/WandererService.cs:123`이다
  (`Assets/Scripts/M0/Services/…` 아님). 관련 = [[feedback-correction-value-sourcing]],
  [[feedback-persist-approved-snapshots]]
