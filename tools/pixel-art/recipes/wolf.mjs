// 늑대 (M22-3차 W5c) — 위협에 처음으로 얼굴이 생긴다 (지금까지는 회색 원이었다).
//
// 🔴 참고본 처리 (2026-08-09): 사용자가 인터넷에서 가져온 늑대 스프라이트를 참고로 주셨다.
// **픽셀은 베끼지 않는다** (남의 저작물 + 리포 공개). 가져온 것은 그 그림이 잘 하고 있는
// **구조 원칙 넷**뿐이고, 색은 전부 우리 팔레트(Kenmi 팩 추출)에서 뽑았다:
//   ① 몸이 밝다 — 어두운 몸은 잔디 위에서 실루엣이 죽는다 (1·2차 시안의 실패 원인)
//   ② 꼬리가 굵다 — 특히 **뒤모습에서 꼬리가 아래로 뻗어** "늑대의 뒤"라고 말해 준다
//      (Up 프레임이 덩어리로 읽히던 문제의 진짜 해법)
//   ③ 머리가 크고 다리는 가늘게 **떨어져** 있다 (몸통에 붙이면 뭉갠다)
//   ④ 중간톤을 줄이고 대비를 키운다 — 어두운 윤곽 · 밝은 몸 · 흰 배
//
// 규격: 32×32 칸 3×3 (팩 Pig·Sheep 판독값). 행 = Down/Side/Up, 열 = 걷기A·걷기B·공격.
// Side는 오른쪽을 본다 — 왼쪽은 애니메이터가 뒤집는다 (SideFacesRight 약속).

import { animalSheet } from '../sheet.mjs';

const K = {
  K: 'ink',            // 윤곽
  s: 'metal_slate',    // 등·그림자 (푸른 기 도는 짙은 회색)
  g: 'metal_gray',     // 털 (기본)
  L: 'metal_light',    // 밝은 면
  W: 'bone_pale',      // 배·주둥이 (가장 밝게)
  r: 'flame_deep',     // 눈
  t: 'fur_white',      // 이빨
};

// ── Side (오른쪽을 본다): 굵은 꼬리 왼쪽, 큰 머리 오른쪽 ──────────────────────
// 🔴 옆모습 교정: 머리가 몸통과 같은 높이·같은 밝기라 "빵 덩어리"로 읽혔다.
// 참고본이 잘 하는 것 = **머리를 등보다 높이 두고 주둥이를 앞으로 내민다** — 목선이
// 생기면서 몸과 머리가 분리된다.
const sideBody = [
  '....................',
  'KgK...........KKK...',   // 꼬리 끝 · 귀
  'KLgK.........KgLgK..',
  'KLLgK.KKKKK.KgLLLLgK',   // 머리가 등보다 한 칸 위
  'KLLgKKsssssKKgLLLLLK',   // 등 (짙게)
  '.KLgggggggggggLLLrWK',   // 눈
  '..KLLLLLLLLLLLLLLWWK',   // 주둥이 (앞으로)
  '..KWWWWWWWWWWWWWKKKK',   // 배 (가장 밝게)
  '...KKWWWWWWWWWWKK...',
];
const sideLegsA = [
  '..K..KK....KK..KK...',
  '..KL.KLK...KLK.KLK..',
  '..KL.KLK...KLK.KLK..',
  '..KK.KKK...KKK.KKK..',
];
const sideLegsB = [
  '.....KKK....KKK.....',
  '.....KLK....KLK.....',
  '.....KLK....KLK.....',
  '.....KKK....KKK.....',
];
const sideAtk = [
  '....................',
  'KgK...........KKK...',
  'KLgK.........KgLgK..',
  'KLLgK.KKKKK.KgLLLLgK',
  'KLLgKKsssssKKgLLLLLK',
  '.KLgggggggggggLLLrWK',
  '..KLLLLLLLLLLLLLttWK',   // 입을 벌린다
  '..KWWWWWWWWWWWWKttK.',
  '...KKWWWWWWWWWKKKK..',
  '.....KKK....KKK.....',
  '.....KLK....KLK.....',
  '....KKLKK..KKLKK....',
  '....KKKKK..KKKKK....',
];

// ── Down (이쪽을 본다): 큰 머리, 밝은 가슴 ────────────────────────────────────
const downBody = [
  '....................',
  '...K..........K.....',
  '..KgK........KgK....',   // 뾰족 귀
  '..KLgK......KLgK....',
  '.KKLLgKKKKKKgLLKK...',
  '.KLLggggggggggLLK...',
  '.KLLgrWgggggWrgLLK..',   // 눈 둘
  '.KLLggggWWggggLLK...',   // 주둥이
  '..KLLLgWWWWgLLLK....',
  '..KLLWWWWWWWWLLK....',   // 가슴 (밝게)
  '...KKWWWWWWWWKK.....',
  '....KLWWWWWWLK......',
];
const downLegsA = [
  '...KKK......KKK.....',
  '...KLK......KLK.....',
  '...KKK......KKK.....',
];
const downLegsB = [
  '..KKK........KKK....',
  '..KLK........KLK....',
  '..KKK........KKK....',
];
const downAtk = [
  '....................',
  '...K..........K.....',
  '..KgK........KgK....',
  '..KLgK......KLgK....',
  '.KKLLgKKKKKKgLLKK...',
  '.KLLggggggggggLLK...',
  '.KLLgrWgggggWrgLLK..',
  '.KLLggKttttKggLLK...',   // 크게 벌린 입
  '..KLLKttttttKLLK....',
  '..KLLKttttttKLLK....',
  '...KKLKttttKLKK.....',
  '....KLWWWWWWLK......',
  '...KKK......KKK.....',
  '...KLK......KLK.....',
  '...KKK......KKK.....',
];

// ── Up (등을 보인다): **굵은 꼬리가 아래로** — 이 하나가 뒤모습을 늑대로 만든다 ──
// 🔴 뒤모습이 계속 "얼굴"로 읽힌 진짜 이유: 어두운 등 한가운데에 밝은 부분이 있으면
// 눈·주둥이로 보인다. 등은 **끝까지 어둡게** 두고, 밝은 것은 몸 **밖으로** 내민 꼬리
// 하나뿐이어야 한다 — 밝은 조각이 실루엣 안에 있으면 얼굴, 밖에 있으면 부위로 읽힌다.
const upBody = [
  '....................',
  '..KgK........KgK....',
  '..KLgK......KLgK....',
  '.KKLLgKKKKKKgLLKK...',
  '.KLssssssssssssLK...',
  '.KLssssssssssssLK...',   // 등 (한가운데까지 어둡게)
  '.KLssssssssssssLK...',
  '..KLssssssssssLK....',
  '...KKLssssssLKK.....',
  '.....KKssssKK.......',
];
// 🔴 꼬리와 다리가 **붙어서** 한 덩어리로 뭉갰다 (첫 배치: 다리 x2-4 · 꼬리 x5-11 = 맞닿음).
// 픽셀아트에서 두 부위가 인접하면 윤곽선이 사라져 하나로 읽힌다 — 반드시 빈 칸을 남긴다.
// 꼬리 = 몸 실루엣 **밖으로** 내민 유일한 밝은 부위 (위 주석의 원리).
const upTailA = [
  '.......KgLgK........',
  '.......KgLgK........',
  '.......KgLgK........',
  '........KgK.........',
  '.........K..........',
];
const upTailB = [
  '........KgLgK.......',
  '........KgLgK.......',
  '........KgLgK.......',
  '.........KgK........',
  '..........K.........',
];
/** 뒤모습의 다리 — 몸통 가장자리 **아래**에 붙인다. 너무 바깥으로 빼면 몸에서 떨어져
 * 공중에 뜬 네모가 된다 (3차 시안에서 밟은 함정). 꼬리(x8~12)와는 3칸 이상 띄운다. */
const upLegsA = ['..KKK........KKK....', '..KLK........KLK....', '..KKK........KKK....'];
const upLegsB = ['.KKK..........KKK...', '.KLK..........KLK...', '.KKK..........KKK...'];

/** 몸통 위에 꼬리·다리를 겹쳐 한 프레임(16줄)으로. 뒤 레이어가 앞을 덮지 않게 '.'는 건너뛴다. */
const stack = (...parts) => {
  const rows = Array.from({ length: 16 }, () => Array(20).fill('.'));
  let cursor = 0;
  for (const part of parts) {
    part.layer.forEach((line, i) => {
      const y = (part.at ?? cursor) + i;
      if (y < 0 || y >= 16) return;
      [...line.padEnd(20, '.')].forEach((ch, x) => { if (ch !== '.' && ch !== ' ') rows[y][x] = ch; });
    });
    if (part.at === undefined) cursor += part.layer.length;
  }
  return rows.map(r => r.join(''));
};

const frame = (body, legs = []) => stack({ layer: body }, { layer: legs });

const sheet = animalSheet('Wolf', {
  DownA: frame(downBody, downLegsA),
  DownB: frame(downBody, downLegsB),
  DownAtk: frame(downAtk),
  SideA: frame(sideBody, sideLegsA),
  SideB: frame(sideBody, sideLegsB),
  SideAtk: frame(sideAtk),
  // 뒤모습만 3층: 몸 → 다리 → 꼬리(가운데, 맨 위)
  UpA: stack({ layer: upBody }, { layer: upLegsA, at: 11 }, { layer: upTailA, at: 10 }),
  UpB: stack({ layer: upBody }, { layer: upLegsB, at: 11 }, { layer: upTailB, at: 10 }),
  UpAtk: stack({ layer: upBody }, { layer: upLegsA, at: 11 }, { layer: upTailA, at: 10 }),
});

export default {
  name: 'Wolf',
  width: sheet.width,
  height: sheet.height,
  slices: sheet.slices,
  layers: [{ op: 'pixels', at: [0, 0], key: K, map: sheet.map }],
};
