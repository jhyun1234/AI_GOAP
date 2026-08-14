/* 모션 어휘집 — 태그 하나가 세 가지를 한자리에 묶는다.

     ① 그리는 규칙의 파라미터 (주기·보폭 — figure.js 의 pose 가 읽는다)
     ② 그 동작을 만들 때 **볼 것**  (`q` — refs.mjs 가 자동 검색에 쓴다)
     ③ 대본이 그 동작을 요구했는지 알아보는 **낱말** (`reads`)

   셋이 흩어지면 대조가 성립을 안 한다. ep16s-1 에서 난 사고가 정확히 그 모양이었다 —
   훅의 `reads` 는 「걸어와」라고 적혀 있고, 그림은 다리를 세워 뒀고, 레퍼런스는 마네킹
   워크사이클을 받아 놓고도 아무 데도 안 이어졌다. 셋 다 있었는데 서로를 몰랐다.

   🔴 **왜 이 파일만 `.mjs` 인가.** node(`refs.mjs`)와 브라우저(`figure.js`)가 **같은 표**를
      읽어야 하는데 `package.json` 이 `"type": "commonjs"` 라 node 는 `.js` 를 CJS 로 읽는다.
      확장자를 `.mjs` 로 두면 node 는 ESM 으로 읽고 브라우저는 MIME 만 맞으면 그만이다
      (`serve.js` TYPES 에 `.mjs` 를 넣어 뒀다). 표를 양쪽에 베껴 두는 것보다 싸다.
   🔴 여기는 **의존성이 없다.** lib.js 를 import 하는 순간 같은 CJS 벽에 다시 부딪힌다.

   🔴 태그를 늘리기 전에 그 동작을 **쓰는 샷이 실제로 있는지** 먼저 봐라. 지금 넷은 전부
      ep16s-1~5 의 reads 에서 나온 것이다. 안 쓰는 동작을 미리 채우면 어휘집이 아니라 짐이다.

   ⚠️ `reads` 낱말은 **힌트지 판정이 아니다.** 우리 대본은 사람 아닌 것도 걷고 앉는 것처럼
      쓴다 — 「동그라미가 내려앉고」·「점선이 흘러 들어가고」·「점선이 뻗었다」. 첫 판에서
      `앉`·`들어가`·`뻗` 을 넣었더니 사람이 한 명도 없는 샷 넷이 전부 걸렸다. 그래서
      **주어가 사람일 때만 쓰는 꼴**로 좁혔다. 판정은 scene.json 의 `shot.motions` 선언이
      지고, 낱말은 그 선언을 빠뜨렸을 때 **경고로만** 뜬다(refs.mjs scanEp). */

export const MOTIONS = {
  walk: {
    cyclic: true, hz: 1.15, stride: 74,
    q: 'mannequin walk cycle reference',
    reads: ['걸어', '걷는', '걷다', '걸으'],
  },
  idle: {
    cyclic: true, hz: 0.26,
    q: 'idle breathing standing animation reference',
    reads: ['서 있', '가만히', '기다리'],
  },
  sit: {
    cyclic: true, hz: 0.24,
    q: 'sitting at desk typing animation reference',
    reads: ['앉아', '앉은', '앉는', '책상에', '타이핑', '키보드'],
  },
  reach: {
    cyclic: false, dur: 0.9,
    q: 'reach and grab arm animation reference',
    reads: ['손을 뻗', '팔을 뻗', '건네', '집어 든', '내밀'],
  },

  /* ── 제스처 ──────────────────────────────────────────────────
     사용자 지시(2026-08-12): 「의문문 · 감탄문 · 강조가 들어갈 때 몸짓이 보이게」.
     이 넷은 **자막 문장부호가 자동으로 고른다**(figure.js gestureTag) — `reads` 로도
     안 줍고 선언으로도 안 받는다. 대본에 물음표가 있으면 물음표 몸짓이고 없으면 없다.
     🔑 그래서 `reads: []` 다. 힌트 경고가 뜨면 안 된다 — 이건 선언하는 것이 아니라
        **문장에서 자동으로 나오는** 것이고, 게이트가 「선언이 빠졌다」고 말하면 거짓말이다. */
  think: {
    cyclic: false, dur: 0.8, gesture: true, reads: [],
    q: '3d character thinking pose hand on chin animation',
  },
  cheer: {
    cyclic: false, dur: 0.7, gesture: true, reads: [],
    q: '3d character excited fist raise animation',
  },
  point: {
    cyclic: false, dur: 0.7, gesture: true, reads: [],
    q: '3d character pointing up gesture animation',
  },
  shrug: {
    cyclic: false, dur: 0.8, gesture: true, reads: [],
    q: '3d character shrug gesture animation',
  },
  present: {
    cyclic: false, dur: 0.8, gesture: true, reads: [],
    q: '3d character presenting board gesture animation',
  },
};
