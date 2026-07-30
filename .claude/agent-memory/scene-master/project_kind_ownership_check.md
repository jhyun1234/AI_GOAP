---
name: kind-ownership-check
description: 그림이 회차 소유가 된 뒤의 회귀 검증 절차 — kind 해석 경로·타 회차 check·ep01 기존 실패의 정체
metadata:
  type: project
---

`db8325a` 이후 **그림(kind)은 회차가 소유한다** — `episodes/<ep>/kinds/`. ep01s 재제작(2026-07-29)이 이 구조의 첫 실전 검증이었고 통과했다.

**Why:** 공용 `engine/kinds/` 시절에는 한 회차가 그림을 고치면 다른 회차가 조용히 바뀌었다.
지금은 안 바뀌는 것이 정상이고, **그 "안 바뀜"을 매번 증명해야** 삭제 작업을 승인할 수 있다.

**How to apply (회귀 검증 4단계):**
1. 해석 경로를 코드로 확인 — `check.mjs:35`(`path.join(ROOT,'episodes',EP,'kinds')`) 와
   `engine.js:44`(`import('../episodes/${EP}/kinds/…')`) 둘 다 회차 폴더로만 간다.
2. `git status` 로 삭제가 그 회차 폴더 안에서만 일어났는지 본다. **미추적 파일 삭제는 흔적이 안 남는다**
   (`spoke.js` 가 그랬다) — 씬이 쓰는 kind 목록과 실제 파일 목록을 직접 비교해 고아/누락 0 을 확인할 것.
3. `engine/`·`check.mjs` 가 안 바뀐 것을 확인. **바뀌었다면 다른 회차 `check.mjs` 를 전부 돌린다**([[verdict-ep00s]]).
4. 그래도 `check.mjs` 를 다른 회차에 한 번은 돌린다 — 논증보다 실행이 싸다.

**알아 둘 것 — `ep01`(장편 정본)은 지금 `check.mjs` 🔴 1건 실패다:**
`정적 구간 F 6.8s` + `아래 가장자리 잘림 HOOK 159px, H 98px`. **기존 상태이고 ep01s 작업과 무관하다**
(자기 `kinds/` 7종을 쓰고 워킹트리 변경 0건). `schedule.json.order` 는 쇼츠 14편뿐이라 발행 대상도 아니다.
👉 다음에 이 실패를 보면 놀라지 말 것. 다만 ep01 을 언젠가 손보게 되면 HOOK/H 샷 아래 잘림부터다.

관련: [[verdict-ep01s]]
