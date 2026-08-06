---
name: en-subtitle-referent
description: 영어 자막은 「앞줄 + 이 줄」을 붙여 읽고 지시어가 무엇을 가리키는지 확인한다 — 한 줄씩 검사하면 뜻이 뒤집힌 줄을 못 잡는다
metadata:
  type: feedback
---

`lines[].en` 을 검사할 때 **한 줄만 보지 말고 앞줄과 붙여 읽어라.** 특히 `That` · `It` ·
`The X` 로 시작하는 줄은 **앞줄이 방금 명명한 것**을 가리키게 되어 있다.

**Why:** ep08s-2 가 이걸로 반려됐다(2026-08-06).

| | |
|---|---|
| 앞줄 en | `I swapped the math, and they spread from 62.6 all the way to 82.8.` |
| 이 줄 text | 눈엔 있어 **보였던** 편차라, 이제 기계가 매번 검사해요. |
| 🔴 1차 en | `That spread was only real to my eyes…` |

`That spread` 가 **앞줄이 명명한 spread(= 고친 뒤의 진짜 편차)** 를 가리켜
*"방금 만든 그 편차는 내 눈에만 있었다"* 가 됐다 — **원문과 정반대.**
원인은 한국어 「있어 **보였던**」의 **회상 관형형이 '옛 것'을 가리키는 표지**인데 영어가
그 표지를 버린 것. 영어에는 시제·부정으로 다시 세워야 한다
(확정본: `The variance I thought I saw was never there — so now a machine checks it every run.`).

🔴 **그림이 오독을 강화한다.** 그 자막 구간의 화면은 벌어진 다섯과 `62.6`·`82.8` 이 밝게 떠
있었다. 영어 시청자는 **그 그림을 보면서** 문장을 읽는다 — 자막의 지시어는 화면이 가리키는
쪽으로 끌려간다.

🔑 **긴 글에서는 맞히고 두 줄 자막에서 틀린다.** 같은 회차의 `youtube.en.blurb` 는 정확했다
(*"What my eyes had read as 'there is variance' is now checked by a machine"*). 자막은 앞뒤가
잘려 있어 지시어가 어디에 붙는지를 눈으로 확인할 수 없기 때문이다.

**How to apply:**
- `en` 을 다 쓴 뒤 **연속한 두 줄씩 이어 읽는 패스를 한 번 더 돈다.** 특히 ①대명사·지시어로
  시작하는 줄 ②앞줄에 고유명사나 수치가 있는 줄.
- 한국어의 **회상 관형형(~던/~았던)** · **대조 조사(은/는)** 는 영어에 자동으로 안 넘어간다.
  뜻을 지는 표지가 무엇인지 짚고, 영어에서 그 일을 할 장치를 따로 세워라.
- ⚠️ 이 항목은 **기계가 못 본다**(ADR-V-18). 게이트를 만들지 마라.
- 🔴 반려가 `en` 한 줄이면 **한국어 `text`·`say` 는 건드리지 마라** — TTS 재생성이 필요해지고
  25초 상한이 다시 흔들린다.

관련: [[video-25s-budget]]
