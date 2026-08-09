// 곰 (M22-3차 W5c) — 래칫 3티어의 얼굴. 늑대와 **한눈에 구분**되는 것이 목표다.
//
// 구분 축 셋 (같은 규격·같은 문법이라 색만 바꾸면 늑대의 재탕이 된다):
//   ① 색 = Horse 시트의 갈색 4단계 (늑대는 무채색)
//   ② 실루엣 = 둥근 귀·굵은 몸·짧은 다리, 꼬리 없음 (늑대는 뾰족 귀·긴 몸·꼬리)
//   ③ 덩치 = 칸을 거의 꽉 채운다 (늑대는 여백이 있다) — 티어가 올라간 것이 그림으로 보인다
//
// 규격·문법은 늑대와 동일 (32×32 칸 3×3, Side는 오른쪽). 그래야 애니메이터 배선이 하나다.

import { animalSheet } from '../sheet.mjs';

const K = {
  K: 'ink',
  b: 'hide_dark',    // 그림자·등
  d: 'hide_mid',     // 털 (기본)
  m: 'hide_light',   // 옆구리 밝은 면
  l: 'hide_hi',      // 주둥이·가슴
  W: 'fur_white',    // 이빨·발톱
  r: 'flame_deep',   // 눈
};

// ── Side (오른쪽을 본다): 굵은 몸통, 짧은 다리, 꼬리 없음 ─────────────────────
// 🔴 곰 옆모습 교정: 머리가 몸통에 묻혀 "갈색 덩어리"로 보였다. 곰의 옆 실루엣을 만드는
// 것은 **어깨 혹**이다 — 등 앞쪽을 솟구고 머리를 그보다 낮게 두면 늑대(수평한 등)와
// 한눈에 갈린다.
const sideBody = [
  '....................',
  '....KKKK......KKK...',   // 어깨 혹 · 둥근 귀
  '...KbbbbKK...KddK...',
  '..KbbbbbbbKKKKddbK..',
  '.KbddddddddddddmrWK.',   // 눈
  '.KbddddddddddddmllK.',
  '.KmdddddddddddddllK.',   // 주둥이
  '.KmmmmmmmmmmmmmmKKK.',
  '..KmmmmmmmmmmmmmK...',
  '...KKKKKKKKKKKKK....',
];
const sideLegsA = [
  '.KKK..KKK...KKK.....',
  '.KdK..KdK...KdK.....',
  '.KWK..KWK...KWK.....',   // 발톱
];
const sideLegsB = [
  '..KKK...KKK.KKK.....',
  '..KdK...KdK.KdK.....',
  '..KWK...KWK.KWK.....',
];
const sideAtk = [
  '....................',
  '....KKKK......KKK...',
  '...KbbbbKK...KddK...',
  '..KbbbbbbbKKKKddbK..',
  '.KbddddddddddddmrWK.',
  '.KbdddddddddddWWWWK.',   // 입을 벌린다
  '.KmddddddddddKWWWK..',
  '.KmmmmmmmmmmmKWWKK..',
  '..KmmmmmmmmmmmmK....',
  '...KKKKKKKKKKKK.....',
  '.KKK..KKK...KKK.....',
  '.KWK..KdK...KWK.....',
  '.KWK..KWK...KWK.....',
];

// ── Down (이쪽을 본다): 넓은 어깨, 둥근 귀 ────────────────────────────────────
const downBody = [
  '....................',
  '..KKK........KKK....',
  '.KddK........KddK...',   // 둥근 귀
  '.KdbKKKKKKKKKKdbK...',
  'KKdddddddddddddddKK.',
  'KbddmmddddddmmddbK..',
  'KbddrWddddddWrddbK..',   // 눈
  'KbdddddKllKdddddbK..',
  'KbddddKlWWlKddddbK..',   // 주둥이
  'KmdddddlllldddddmK..',
  'KmmdddddddddddmmK...',
  '.KmmmddddddddmmK....',
  '..KKmmmmmmmmmKK.....',
];
const downLegsA = [
  '..KKK........KKK....',
  '..KdK........KdK....',
  '..KWK........KWK....',
];
const downLegsB = [
  '.KKK..........KKK...',
  '.KdK..........KdK...',
  '.KWK..........KWK...',
];
const downAtk = [
  '....................',
  '..KKK........KKK....',
  '.KddK........KddK...',
  '.KdbKKKKKKKKKKdbK...',
  'KKdddddddddddddddKK.',
  'KbddmmddddddmmddbK..',
  'KbddrWddddddWrddbK..',
  'KbddddKWWWWWKdddbK..',   // 크게 벌린 입
  'KbdddKWWWWWWWKddbK..',
  'KmdddKWWWWWWWKddmK..',
  'KmmddKWWWWWWWKdmmK..',
  '.KmmmdKWWWWWKdmmK...',
  '..KKmmmmmmmmmKK.....',
  '..KKK........KKK....',
  '..KWK........KWK....',
  '..KKK........KKK....',
];

// ── Up (등을 보인다): 넓고 둥근 등, 꼬리 없음 ─────────────────────────────────
const upBody = [
  '....................',
  '..KKK........KKK....',
  '.KddK........KddK...',
  '.KdbKKKKKKKKKKdbK...',
  'KKdddddddddddddddKK.',
  'KmdddbbbbbbbbdddmK..',
  'KmddbbbbbbbbbbddmK..',   // 등
  'KmddbbbbbbbbbbddmK..',
  'KmdddbbbbbbbbdddmK..',
  'KmmdddbbbbbbdddmmK..',
  '.KmmmddddddddmmmK...',
  '..KKmmmmmmmmmmKK....',
];
const upLegsA = [
  '..KKK........KKK....',
  '..KdK........KdK....',
  '..KKK........KKK....',
];
const upLegsB = [
  '.KKK..........KKK...',
  '.KdK..........KdK...',
  '.KKK..........KKK...',
];
const upAtk = [
  '....................',
  '..KKK........KKK....',
  '.KddK........KddK...',
  '.KdbKKKKKKKKKKdbK...',
  'KKdddddddddddddddKK.',
  'KmdddbbbbbbbbdddmK..',
  'KmddbbbbbbbbbbddmK..',
  'KmddbbbbbbbbbbddmK..',
  'KmdddbbbbbbbbdddmK..',
  'KmmdddbbbbbbdddmmK..',
  '.KmmmddddddddmmmK...',
  '..KKmmmmmmmmmmKK....',
  '..KKK........KKK....',
  '..KWK........KWK....',
  '..KKK........KKK....',
];

const frame = (body, legs = []) => {
  const rows = [...body, ...legs].map(r => (r + '....................').slice(0, 20));
  while (rows.length < 16) rows.push('.'.repeat(20));
  return rows.slice(0, 16);
};

const sheet = animalSheet('Bear', {
  DownA: frame(downBody, downLegsA),
  DownB: frame(downBody, downLegsB),
  DownAtk: frame(downAtk),
  SideA: frame(sideBody, sideLegsA),
  SideB: frame(sideBody, sideLegsB),
  SideAtk: frame(sideAtk),
  UpA: frame(upBody, upLegsA),
  UpB: frame(upBody, upLegsB),
  UpAtk: frame(upAtk),
});

export default {
  name: 'Bear',
  width: sheet.width,
  height: sheet.height,
  slices: sheet.slices,
  layers: [{ op: 'pixels', at: [0, 0], key: K, map: sheet.map }],
};
