---
name: second-memory-store
description: scene-writer 메모리 창고가 둘이다 — 이 인덱스에 안 잡히는 tools/scene-video 쪽 store 가 실재하고, 형제 편이 작성 중에 그 파일을 고친다
metadata:
  type: reference
---

**`scene-writer` 의 메모리는 두 곳에 있다. 이 인덱스는 그중 하나만 안다.**

| 창고 | 경로 | 성격 |
|---|---|---|
| 루트(이 인덱스) | `.claude/agent-memory/scene-writer/` | 세션에 **자동 로드**된다 |
| 트랙 | `tools/scene-video/.claude/agent-memory/scene-writer/` | **자동 로드 안 된다.** 직접 열어야 보인다 |

트랙 쪽에 있는 것(2026-08-11 기준): `feedback_sfx_uniqueness_is_the_arg_tuple.md`(효과음 겹침
판정법) · `project_length_grail_beats_estimate.md`(길이 그릇) · `procedure_4dan_retrofit_traps.md` ·
`feedback_overlap_audit_two_axes.md`(겹침 두 축) · `project_dnns_frozen_window_stale_numbers.md`
(dNNs 원문이 오늘도 자란다) · `reference_legacy_memory_store.md`(루트를 가리키는 역포인터).

**Why:** 두 창고가 **같은 주제를 다르게 적어 둔다.** `d01s-2`(2026-08-11)가 트랙 쪽의
`latch = seed 사실상 단독` 표를 근거로 `dur 0.245` 를 골랐다가 |r| 0.8652 로 반려됐는데,
루트 쪽 `project_sfx_latch_seed.md` 는 **그 전날(ep15s-2 3차)부터 이미** *"`dur` 을 최댓값 밖으로
늘리면 오히려 더 붙는다"* 고 적어 두고 있었다. **읽은 창고가 하나뿐이라 반대 결론에 도달했다.**

## 🔴🔴 더 나쁜 것 — **로드된 인덱스 스냅숏이 세션 도중에 낡는다**

`d01s-2` 를 쓰는 동안 **형제 편 `d01s-1` 이 같은 사유로 반려되면서 두 창고를 모두 갱신했다.**
내 세션에 실린 `MEMORY.md` 는 **착수 시각의 스냅숏**이라 그 갱신이 안 보였고, 제출 시점에는
루트 인덱스 줄이 이미 「latch = seed 단독은 반증됐다 · 상한 0.220」으로 바뀌어 있었다.
🔑 **병렬 작성에서는 형제 census 만 낡는 게 아니라 공유 메모리도 낡는다.**

**How to apply:**
- 🔴 착수할 때 **두 창고를 다 열어라.** 주제가 겹치면 **루트 쪽이 더 깊고 최신인 경우가 많다**
  (검수·마스터가 반려문을 루트에 적는다).
- 🔴 **제출 직전에 「내가 근거로 인용한 메모리 줄」을 파일에서 다시 읽어라.** 컨텍스트에 실린
  인덱스가 아니라 **디스크의 파일**을. grep 한 번이고, 형제 census 를 다시 세는 것과 같은 절차다.
- 🔴 **근거를 「메모리에 그렇게 적혀 있었다」에 걸지 마라.** 걸 곳은 **소스와 실측**이다 —
  `d01s-2` 는 `sfx.mjs:73-84`(몸통 340/680Hz 고정)를 자기 `why` 에 인용해 놓고도 거기서
  나오는 결론(길수록 붙는다)을 반대로 적었다. 표를 믿고 소스를 안 읽은 것이다.
- 새 메모리를 쓸 때 **어느 창고에 넣을지 정하고 반대쪽에 한 줄 포인터를 남겨라.**

관련: [[sfx-latch-needs-seed]] · [[dnns-frozen-window-stale-numbers]](트랙 창고)
