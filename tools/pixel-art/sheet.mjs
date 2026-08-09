// 시트 조립 헬퍼 (M22-3차 W5c) — 네발짐승 3방 시트를 한 문법으로 찍어낸다.
//
// 왜 헬퍼인가: 늑대·곰·(앞으로의 짐승들)은 **그림만 다르고 배치는 같다**. 격자·슬라이스
// 이름·피벗을 매번 손으로 적으면 그 자리가 오타의 집이 된다 (M23 fileID 손배선의 교훈).
//
// 격자: 3열 × 3행, 칸 32×32 (팩 동물 시트와 같은 규격 — Pig/Sheep 판독으로 확인).
//   행 = 방향 (Down / Side / Up) · 열 = 걷기A · 걷기B · 공격
//   ⚠️ Side는 **오른쪽을 본다** (AgentSpriteSetSO.SideFacesRight = true의 약속).
//      왼쪽은 애니메이터가 뒤집어 쓴다 — 왼쪽 프레임을 따로 그리지 않는다.
//
// 그림은 픽셀맵으로 그려 칸 안에 앉힌다. 크기는 **짐승마다 다르다** (artW/artH) —
// 늑대는 길고 곰은 뭉툭하다. 기본값 20×16은 첫 두 짐승(W5c)의 값이라 그대로 둔다.
//
// ⚠️ 발이 닿는 바닥선은 크기와 무관하게 고정이다 (y=25). 그림이 커지면 위로 자란다 —
// 그래야 같은 타일 위에 선 짐승들의 발끝이 한 줄에 놓인다. offY 는 그 규칙의 식일 뿐이다.

export const CELL = 32;
export const ART_W = 20, ART_H = 16;
const GROUND = 25;                    // 발끝이 놓이는 칸 안 y (팩 동물 판독값)

export const DIRS = ['Down', 'Side', 'Up'];
export const COLS = ['A', 'B', 'Atk'];

/**
 * 3×3 시트 조립. frames = { DownA, DownB, DownAtk, SideA, ... } 각 값은 문자열 배열.
 * opts.artW / opts.artH = 그림 칸 크기 (기본 20×16).
 * 반환 = { width, height, slices, map } — 레시피가 그대로 펼쳐 쓰면 된다.
 */
export function animalSheet(name, frames, opts = {}) {
  const artW = opts.artW ?? ART_W, artH = opts.artH ?? ART_H;
  if (artW > CELL || artH > CELL) throw new Error(`${name}: 그림(${artW}x${artH})이 칸 ${CELL}보다 크다`);
  const OFF_X = (CELL - artW) >> 1;    // 가로 가운데
  const OFF_Y = GROUND - artH;         // 세로 = 바닥선 고정
  if (OFF_Y < 0) throw new Error(`${name}: 그림 높이 ${artH}는 바닥선 ${GROUND}을 넘는다`);
  const width = CELL * 3, height = CELL * 3;
  const grid = Array.from({ length: height }, () => Array(width).fill('.'));
  const slices = [];

  DIRS.forEach((dir, row) => {
    COLS.forEach((col, colIdx) => {
      const key = `${dir}${col}`;
      const art = frames[key];
      if (!art) throw new Error(`${name}: 프레임 '${key}'가 없다 (9칸 전부 채울 것)`);
      // 🔴 넘침 검사: 한 줄이 길거나 줄이 많으면 **옆·아래 칸에 조용히 그려진다** —
      // 다 그린 뒤 "왜 이웃 프레임에 점이 있지"로 만나는 종류의 사고라 여기서 막는다.
      if (art.length > artH) throw new Error(`${name}/${key}: ${art.length}줄 — 칸은 ${artH}줄`);
      art.forEach((line, y) => {
        if (line.length > artW) throw new Error(`${name}/${key} ${y}행: ${line.length}칸 — 폭은 ${artW}`);
      });
      const baseX = colIdx * CELL + OFF_X;
      const baseY = row * CELL + OFF_Y;
      art.forEach((line, y) => {
        [...line].forEach((ch, x) => {
          if (ch === '.' || ch === ' ') return;
          grid[baseY + y][baseX + x] = ch;
        });
      });
      slices.push({
        name: `${name}_${key}`,
        x: colIdx * CELL, y: row * CELL, w: CELL, h: CELL,
        // 피벗 = 칸 가운데 (짐승은 자기 타일 위에 선다 — 망루 같은 높은 구조물이 아니다)
      });
    });
  });

  return { width, height, slices, map: grid.map(r => r.join('')) };
}
