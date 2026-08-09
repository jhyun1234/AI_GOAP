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
// 그림은 20×16 픽셀맵으로 그려 칸 안에 앉힌다 (팩 동물이 32칸 안에서 차지하는 비율과 같다).

export const CELL = 32;
export const ART_W = 20, ART_H = 16;
const OFF_X = (CELL - ART_W) >> 1;   // 6 — 가로 가운데
const OFF_Y = 9;                      // 세로: 발이 칸 아래쪽에 오게 (팩 동물 판독값)

export const DIRS = ['Down', 'Side', 'Up'];
export const COLS = ['A', 'B', 'Atk'];

/**
 * 3×3 시트 조립. frames = { DownA, DownB, DownAtk, SideA, ... } 각 값은 문자열 배열(20×16).
 * 반환 = { width, height, slices, map } — 레시피가 그대로 펼쳐 쓰면 된다.
 */
export function animalSheet(name, frames) {
  const width = CELL * 3, height = CELL * 3;
  const grid = Array.from({ length: height }, () => Array(width).fill('.'));
  const slices = [];

  DIRS.forEach((dir, row) => {
    COLS.forEach((col, colIdx) => {
      const key = `${dir}${col}`;
      const art = frames[key];
      if (!art) throw new Error(`${name}: 프레임 '${key}'가 없다 (9칸 전부 채울 것)`);
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
