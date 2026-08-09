// 망루 — 3차 시안 (M22-3차 W5b). 사용자 반려 2회의 진단을 그대로 반영한다.
//
// 폐기 이력:
//   ①16×32 합성 = 팩 건물 옆에서 가늘어 장난감처럼 보였다 → 크기·신작으로 전환
//   ②32×48 신작 = **주민이 올라서면 머리가 지붕에 겹친다** (사용자: "활 쏘는 애니메이션이
//     어색해 보일 것 같다"). 감시대 바로 위에 지붕을 얹은 것이 원인이었다.
//
// 이번 구조의 핵심 = **머리 공간**이다. 지붕을 위로 올리고 그 아래 정확히 한 타일(16px)을
// 비워, 주민 스프라이트가 통째로 들어가도 지붕·처마와 겹치지 않는다. 난간은 허리 높이라
// 주민이 그 앞에 서면 다리만 가려진다 (주민이 위에 그려지므로 자연스럽다).
//
// 좌표 규약: 발판 윗면 = 사람이 서는 면. 이 y가 곧 에셋의 탑승 높이(MountOffsetTiles)와
// 짝이다 — 그림을 고치면 그 값도 함께 고쳐야 한다 (W5b에서 코드 상수 0.45를 에셋으로 승격).

const W = 32, H = 56;
const FLOOR_TOP = 28;            // 발판 윗면 (사람이 서는 면) — 아래 주석의 산수 근거
const K = { S: 'wood_shadow', d: 'wood_dark', m: 'wood_mid', l: 'wood_light', h: 'wood_hi' };

const grid = Array.from({ length: H }, () => Array(W).fill('.'));
const put = (x, y, ch) => { if (x >= 0 && x < W && y >= 0 && y < H) grid[y][x] = ch; };
const hline = (x0, x1, y, ch) => { for (let x = x0; x <= x1; x++) put(x, y, ch); };

/** 판자 결 — 3픽셀마다 세로 이음매, 왼쪽 밝고 오른쪽 어둡게 (팩 지붕·벽 문법). */
const plank = (x) => (x % 3 === 1 ? 'S' : x < 16 ? (x % 3 === 2 ? 'l' : 'm') : (x % 3 === 2 ? 'd' : 'm'));

// ── 지붕 (y0..9) — 사각뿔. 아래로 갈수록 좌우로 벌어진다 ────────────────────────
for (let y = 0; y <= 9; y++) {
  const half = Math.round(2 + y * 1.45);
  const L = 16 - half, R = 15 + half;
  for (let x = L; x <= R; x++) put(x, y, x === L || x === R ? 'S' : plank(x));
  if (R - L >= 5) { put(L + 1, y, 'h'); put(L + 2, y, 'l'); }   // 왼쪽 빛 모서리
}
// 처마 — 지붕보다 한 칸 더 내밀어 그늘을 만든다 (건물로 읽히는 지점)
hline(0, W - 1, 10, 'S');
for (let x = 1; x < W - 1; x++) put(x, 11, x % 3 === 1 ? 'S' : x < 16 ? 'l' : 'd');
hline(0, W - 1, 12, 'S');

// ── 머리 공간 (y13..27 = 15px) — 비운다. 여기에 주민이 통째로 들어간다 ──────────
// 네 귀퉁이 기둥만 세워 지붕을 받친다 (주민 폭 ~10px보다 바깥이라 겹치지 않는다)
for (let y = 13; y < FLOOR_TOP; y++) {
  put(2, y, 'S'); put(3, y, 'm'); put(4, y, 'S');        // 왼 앞기둥
  put(27, y, 'S'); put(28, y, 'm'); put(29, y, 'S');     // 오른 앞기둥
}
// 난간 — 허리 높이 가로대 (주민이 서면 다리만 가려진다)
hline(4, 27, 23, 'S');
for (let x = 5; x <= 26; x++) put(x, 24, x % 3 === 1 ? 'S' : 'h');
hline(4, 27, 25, 'S');

// ── 발판 (y28..31) — 사람이 서는 면 + 두께 ─────────────────────────────────────
hline(1, 30, FLOOR_TOP, 'S');
for (let x = 2; x <= 29; x++) put(x, FLOOR_TOP + 1, x % 3 === 1 ? 'S' : 'm');
for (let x = 2; x <= 29; x++) put(x, FLOOR_TOP + 2, x % 3 === 1 ? 'S' : 'd');
hline(1, 30, FLOOR_TOP + 3, 'S');

// ── 다리 (y32..55) — 앞 굵은 기둥 둘 + 뒤 가는 기둥 둘 (원근) ──────────────────
const FRONT = [5, 23], BACK = [12, 18];
for (let y = 32; y < H; y++) {
  for (const x of BACK) { put(x, y, 'S'); put(x + 1, y, 'd'); put(x + 2, y, 'S'); }
  for (const x of FRONT) { put(x, y, 'S'); put(x + 1, y, 'l'); put(x + 2, y, 'm'); put(x + 3, y, 'S'); }
}
// X 가새 — 앞다리 둘을 대각으로 묶는다 (구조물의 뼈대)
for (let i = 0; i <= 14; i++) { put(9 + i, 34 + i, 'd'); put(23 - i, 34 + i, 'd'); }
// 가로 띠 — 대각선만 있으면 어수선하다
for (const y of [33, 49]) for (let x = 5; x <= 26; x++) put(x, y, x % 3 === 1 ? 'S' : 'm');
// 사다리 — 올라가는 곳이 보여야 "사람이 오르는 망루"다
for (let y = 32; y < H; y++) { put(0, y, 'S'); put(4, y, 'S'); }
for (let y = 35; y <= 54; y += 3) for (let x = 1; x <= 3; x++) put(x, y, 'l');

export default {
  name: 'Watchtower',
  width: W,
  height: H,
  // 피벗 y = 바닥에서 반 타일(8px) — BuildingVisualizer가 타일 **중심**에 놓기 때문에,
  // 이래야 밑단 한 타일이 자기 타일을 덮고 나머지가 위로 선다 (W5b 실사).
  slices: [{ name: 'Watchtower_0', x: 0, y: 0, w: W, h: H, pivot: [0.5, 8 / H] }],
  layers: [{ op: 'pixels', at: [0, 0], key: K, map: grid.map(r => r.join('')) }],
};

// 탑승 높이 산수 (에셋 MountOffsetTiles와 짝 — 그림을 고치면 여기도 고친다):
//   스프라이트 밑단 = 타일중심 − 0.5타일. 발판 윗면은 밑단에서 (H − FLOOR_TOP)=28px = 1.75타일.
//   ⇒ 발 높이 = 타일중심 + 1.25타일. 주민 스프라이트는 중심 정렬이라 발보다 반 타일 위가 중심.
//   ⇒ MountOffsetTiles ≈ 1.75 (Play에서 미세 조정)
