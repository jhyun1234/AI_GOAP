// 늑대 (M22-3차 W5c → W5d 재작업) — 위협에 처음으로 얼굴이 생긴다.
//
// 🔴 참고본 처리 (2026-08-09, 2차): 사용자가 인터넷 늑대 스프라이트 시트를 참고로 주셨다.
// **픽셀은 베끼지 않는다** (남의 저작물 + 리포 공개). 가져온 것은 그 그림이 잘 하고 있는
// **구조 원칙**뿐이고, 색은 우리 팔레트(Kenmi 팩 추출)에서 참고본과 **다른 계열**로 뽑았다
// (참고본 = 청회색 / 우리 = 무채색 warm gray — 사용자 지시 "색깔은 다르게").
//
// 1차 시안(20×16 한 덩어리 픽셀맵)이 반려된 이유와, 참고본에서 읽어낸 해법:
//   ① 옆모습이 **수평 식빵** → 등선에 곡률을 준다: 어깨를 솟우고 허리를 잘록하게,
//      가슴은 깊게. 개과의 실루엣은 "직사각형"이 아니라 "앞이 무겁고 허리가 들어간 것".
//   ② 다리가 **떠 있는 네모 4개** → 다리는 몸통 밑변에 **붙어서** 시작하고, 위 두 칸을
//      넓혀 어깨·엉덩이를 만든다. 먼 쪽 다리는 어두운 톤(s)에 한 칸 짧게 = 깊이가 생긴다.
//   ③ 머리와 몸통이 **한 덩어리** → 주둥이를 3px **앞으로 내밀고** 머리를 등보다 높이 둔다.
//      목이 생기면 그때부터 머리가 머리로 읽힌다.
//   ④ 뒷모습이 **검은 삼각형** → 등은 균일한 중간톤, 밝은 조각은 실루엣 **밖**(늘어뜨린
//      꼬리)에만. 실루엣 안의 밝은 조각은 반드시 얼굴로 읽힌다 (1차의 진짜 실패 원인).
//   ⑤ 그림이 칸에 비해 작았다 (20×16) → 24×19. 다리를 길게 뽑을 자리가 이 4줄이었다.
//
// 🔑 명암은 **위→아래 5단 램프 하나**로 통일한다: K(윤곽) → s(등) → g(옆구리) → L(빛 받는
// 아랫면) → W(배). 참고본이 잘 읽히는 이유의 절반이 이것이다 — 톤을 부위마다 고르면
// 얼룩이 되고, 높이순으로 고르면 입체가 된다.
//
// ⚠️ 조각을 겹칠 때 **맞닿는 줄은 한 줄만** 쓴다. 머리 밑 윤곽과 몸통 위 윤곽이 둘 다
// 있으면 검은 띠가 생겨 머리·몸통이 **블록 두 개**로 분리된다 (1차 재작업에서 밟음).
//
// 규격: 32×32 칸 3×3. 행 = Down/Side/Up, 열 = 걷기A·걷기B·공격.
// Side는 오른쪽을 본다 — 왼쪽은 애니메이터가 뒤집는다 (SideFacesRight 약속).

import { animalSheet } from '../sheet.mjs';

const ART_W = 24, ART_H = 19;
const GROUND = ART_H - 1;   // 발끝이 놓이는 줄 — 세 방향이 같아야 돌 때 몸이 안 튄다

// 무채색 warm gray 5단계. 참고본(청회색 metal_*)과 계열을 갈랐고, 곰(갈색 hide_*)과도
// 겹치지 않는다. 밝은 몸 = 잔디(#3e8948) 위에서 실루엣이 사는 조건 (1·2차 시안의 교훈).
const K = {
  K: 'ink',          // 윤곽 (생물용 남보라빛 검정) — 벌린 입 안쪽도 이 색
  D: 'fur_dark',     // 귀 속·코·가장 깊은 그늘
  s: 'bone_dark',    // 등 안장·먼 쪽 다리
  g: 'bone_mid',     // 옆구리 (기본)
  L: 'bone_light',   // 빛 받는 아랫면
  W: 'bone_pale',    // 배·가슴·주둥이 (가장 밝게)
  r: 'flame_deep',   // 눈
  t: 'fur_white',    // 송곳니 — 어두운 입 안에서만 쓴다
};

/** 조각들을 캔버스에 순서대로 얹는다. at=[x,y], '.'는 건너뛴다(아래 조각이 산다). */
function place(...parts) {
  const rows = Array.from({ length: ART_H }, () => Array(ART_W).fill('.'));
  for (const { art, at: [ax, ay] } of parts)
    art.forEach((line, y) => [...line].forEach((ch, x) => {
      if (ch === '.' || ch === ' ') return;
      const px = ax + x, py = ay + y;
      if (px < 0 || px >= ART_W || py < 0 || py >= ART_H) return;
      rows[py][px] = ch;
    }));
  return rows.map(r => r.join(''));
}

// ══ Side (오른쪽을 본다) ══════════════════════════════════════════════════════

/** 머리 10×11 — 주둥이(hx7~9)가 두개골(hx0~6)보다 **앞으로 나온다**. 이 3px이 전부다. */
const sideHead = [
  '..KK......',   // 귀 끝
  '.KDsK.....',
  'KDDsLK....',
  'KDsggLK...',
  'KsgggLK...',
  'KsggrLK...',   // 눈
  'KsgggLLKKK',   // 주둥이 윗선
  'KsgggLLWWK',
  'KsggLLWWDK',   // 코
  '.KggLWWK..',
  '..KKKKK...',
];

/** 공격 = 입을 벌린다. **입 안은 윤곽색(어둡게)** — 송곳니 두 개만 희다.
 *  이빨을 흰 덩어리로 채우면 부리 달린 새가 된다 (재작업 1회차에서 밟음).
 *  송곳니는 위아래를 **한 칸 엇갈리게** 둔다 — 세로로 겹치면 흰 막대 하나로 뭉갠다. */
const sideHeadBite = [
  '..KK......',
  '.KDsK.....',
  'KDDsLK....',
  'KDsggLK...',
  'KsgggLK...',
  'KsggrLKKK.',   // 윗턱이 들린다
  'KsgggLWWWK',   // 윗턱
  'KsgggKKtK.',   // 윗 송곳니 (아래로)
  'KsggKKtKK.',   // 아랫 송곳니 (위로) · 사이는 입 안 어둠
  '.KggKWWWK.',   // 아랫턱
  '..KKKKKK..',
];

/** 몸통 13×8 — 등선: 엉덩이(y1) → 어깨(y0) / 배선: 가슴 깊게(y7) · 허리 잘록(y5).
 *  세로 톤은 s → g → L → W 한 방향 램프. */
const sideTorso = [
  '.......KKKK..',
  'KKKKKKKssssKK',
  'KsssssssssssK',   // 등 안장
  'KgggggggggggK',
  'KLLLLLLLLLLLK',
  'KWWKKKKWWWWWK',   // 허리 잘록 (가운데가 위로 들어간다)
  'KKK....KWWWKK',
  '........KKK..',
];

/** 꼬리 6×6 — **등선보다 위로** 치켜올린다 (공격적인 늑대의 신호). 몸통보다 나중에
 *  얹어 왼쪽 윤곽을 뚫고 이어진다. ⚠️ 몸 안쪽에는 윤곽(K)을 한 점도 남기지 않는다 —
 *  남기면 배 한가운데 검은 흠집으로 보인다. 꼬리 아랫선은 몸 가장자리에서 끝난다. */
const sideTail = [
  '.KK...',
  'KsLK..',
  'KssLK.',
  'KsssLK',
  '.KsssL',
  '..Kssg',
];

/** 가까운 쪽 다리 4×8 — 위 두 칸이 넓다 = 어깨·엉덩이. 몸통 밑변에 붙어서 시작한다. */
const legNear = ['KggK', 'KggK', '.KgK', '.KgK', '.KgK', '.KgK', '.KLK', '.KKK'];
/** 먼 쪽 다리 3×7 — 어둡고 한 칸 짧다 (깊이). */
const legFar = ['KsK', 'KsK', 'KsK', 'KsK', 'KsK', 'KsK', 'KKK'];

const LEG_TOP = GROUND - 7;   // 가까운 다리 8줄이 바닥선에서 끝나는 시작 줄

/** 네 다리. 🔑 **짝끼리 붙이고 앞뒤 짝은 멀리** 둔다 — 넷을 고르게 벌리면 네발짐승이
 *  아니라 지네가 된다 (재작업 1회차에서 밟음). A = 성큼, B = 모아 딛기. */
const sideLegs = (stride) => {
  const [hf, hn, ff, fn] = stride ? [1, 5, 9, 13] : [3, 4, 11, 11];
  return [
    { art: legFar, at: [hf, LEG_TOP] }, { art: legFar, at: [ff, LEG_TOP] },
    { art: legNear, at: [hn, LEG_TOP] }, { art: legNear, at: [fn, LEG_TOP] },
  ];
};

const sideFrame = (forward, head = sideHead) => place(
  { art: sideTorso, at: [2, 5] },
  { art: sideTail, at: [0, 2] },
  ...sideLegs(forward),
  { art: head, at: [13, 0] },
);

// ══ Down (이쪽을 본다) ════════════════════════════════════════════════════════
// 머리는 y0~10, 가슴은 y10~15 — **맞닿는 줄은 y10 하나뿐**이다 (이중 윤곽 방지).

/** 머리 12×11 — 아래 윤곽을 닫지 않는다. 가슴이 그 자리를 잇는다. */
const downHead = [
  '.KK......KK.',
  'KDsK....KsDK',
  'KDDsK..KsDDK',
  'KDssKKKKssDK',
  'KsggggggggsK',
  'KsggggggggsK',
  'KsgrggggrgsK',   // 눈 둘
  'KsggggggggsK',
  'KsggWWWWggsK',
  '.KsgWWWWgsK.',
  '..KKWDDWKK..',   // 코 — 여기서 가슴으로 넘어간다
];

/** 공격 = 정면으로 입을 벌린다. 입 안은 어둡고 송곳니 넷만 희다. */
const downHeadBite = [
  '.KK......KK.',
  'KDsK....KsDK',
  'KDDsK..KsDDK',
  'KDssKKKKssDK',
  'KsggggggggsK',
  'KsgrggggrgsK',
  'KsggggggggsK',
  'KsgKKKKKKgsK',   // 입 열림
  'KsKtKKKKtKsK',   // 윗 송곳니
  'KsKKKKKKKKsK',   // 입 안 (어둠)
  '.KsKtKKtKsK.',   // 아랫 송곳니
];

/** 가슴 14×5 — 머리보다 넓다. 어깨가 삐져나와야 머리만 뜬 '가면'으로 안 읽힌다.
 *  높이를 5로 줄인 것은 **다리 자리를 벌기 위해서다** — 가슴이 두꺼우면 다리가 발톱만
 *  남아 몸 밑에 붙은 점 넷이 된다 (재작업 1회차에서 밟음). */
const downChest = [
  '.KKKKKKKKKKKK.',
  'KsggggggggggsK',
  'KsggWWWWWWggsK',
  'KsgWWWWWWWWgsK',
  '.KKKKKKKKKKKK.',
];

/** 다리는 가슴 밑변보다 **한 칸 위에서** 시작한다 — 밑변 윤곽줄을 다리 속살이 뚫어야
 *  가슴 아래가 검은 띠 하나로 뭉치지 않는다 (재작업 1회차에서 밟음). 위 두 칸은 어깨.
 *  뒷다리는 어둡고 한 칸 짧다 = 뒤에 있다. */
const downLegF = ['KggK', 'KggK', '.KgK', '.KgK', '.KLK', '.KKK'];
const downLegFUp = ['KggK', 'KggK', '.KgK', '.KgK', '.KKK'];   // 든 다리
const downLegH = ['KsK', 'KsK', 'KsK', 'KsK', 'KKK'];

/** step = 어느 쪽 앞다리를 들었는가. */
const downFrame = (step, head = downHead) => place(
  { art: downChest, at: [5, 10] },
  { art: downLegH, at: [3, 13] }, { art: downLegH, at: [18, 13] },
  { art: step ? downLegFUp : downLegF, at: [7, 13] },
  { art: step ? downLegF : downLegFUp, at: [13, 13] },
  { art: head, at: [6, 0] },
);

// ══ Up (등을 보인다) ══════════════════════════════════════════════════════════
// 🔴 규칙: 실루엣 **안**은 균일하게, 밝은 조각은 **밖**으로만. 등 한가운데의 밝은 점은
// 반드시 눈·주둥이로 읽힌다 (1차 뒷모습이 얼굴로 보인 진짜 이유). 그래서 여기엔
// 눈도 주둥이도 없고, 뒷모습을 뒷모습으로 만드는 것은 **아래로 늘어뜨린 꼬리** 하나다.

/** 뒤통수 12×9 — 아래를 닫지 않는다 (등이 그대로 이어진다). 귀 속도 어둡지 않다.
 *  🔑 명암은 옆모습과 같은 **위→아래 램프**로 준다 (L → g → s). 가로로 밝은 띠를
 *  두르면 고립된 밝은 조각이 안 생겨 얼굴로 오독되지 않는다 — 세로 줄무늬로 넣으면
 *  가운데 밝은 덩어리가 되어 1차의 실패(뒷모습이 얼굴로 보임)를 그대로 재현한다. */
const upHead = [
  '.KK......KK.',
  'KssK....KssK',
  'KsssK..KsssK',
  'KsssKKKKsssK',
  'KsgLLLLLLgsK',
  'KsgLLLLLLgsK',
  'KsgLLLLLLgsK',
  'KsggLLLLggsK',
  'KsggggggggsK',
];

/** 등 14×7 — 윗줄은 뒤통수와 이어지고 양 끝만 어깨로 튀어나온다. 엉덩이로 갈수록 어둡다. */
const upBack = [
  'KKLLLLLLLLLLKK',   // 어깨 (빛)
  'KsgLLLLLLLLgsK',
  'KsggggggggggsK',
  'KsggggggggggsK',
  'KsgssssssssgsK',
  '.KssssssssssK.',   // 엉덩이 (그늘)
  '..KKKKKKKKKK..',
];

/** 꼬리 = 몸 **밖**으로 늘어뜨린 유일한 밝은 부위. 뒷다리와 두 칸 이상 띄우고, 속을
 *  W(가장 밝게)로 채운다 — 자리만 벌리면 세 번째 다리로 읽힌다 (재작업 1회차에서 밟음).
 *  다리는 g, 꼬리는 W = 실루엣이 같아도 톤이 갈라 준다. */
const upTail = ['KLWK', 'KLWK', 'KLWK', '.KWK', '..KK'];       // 4칸 폭을 오래 유지한다
const upLeg = ['KggK', '.KgK', '.KgK', '.KLK', '.KKK'];       // 곧바로 3칸으로 좁아진다
const upLegUp = ['KggK', '.KgK', '.KgK', '.KKK'];             // 든 다리

const upFrame = (step) => place(
  { art: upBack, at: [5, 8] },
  { art: step ? upLegUp : upLeg, at: [4, 14] },
  { art: step ? upLeg : upLegUp, at: [16, 14] },
  { art: upTail, at: [10, 14] },
  { art: upHead, at: [6, 0] },
);

// ══ 조립 ═════════════════════════════════════════════════════════════════════

const sheet = animalSheet('Wolf', {
  DownA: downFrame(0),
  DownB: downFrame(1),
  DownAtk: downFrame(0, downHeadBite),
  SideA: sideFrame(true),
  SideB: sideFrame(false),
  SideAtk: sideFrame(true, sideHeadBite),
  UpA: upFrame(0),
  UpB: upFrame(1),
  UpAtk: upFrame(0),
}, { artW: ART_W, artH: ART_H });

export default {
  name: 'Wolf',
  width: sheet.width,
  height: sheet.height,
  slices: sheet.slices,
  layers: [{ op: 'pixels', at: [0, 0], key: K, map: sheet.map }],
};
