// 자동 검사 (M22-3차 W5a) — **기계가 잡을 수 있는 것만** 잡는다.
//
// 여기서 통과했다고 "어울린다"는 뜻이 아니다. 어울림은 수치로 안 잡히므로 사람 눈이
// 판정한다 (대조 시트 — ADR-M22-15, ADR-V-7 "미측정 주장으로 규칙 만들기 금지").
// 이 검사가 막는 것은 **조용한 사고**다:
//   ① 팔레트 이탈 — 팩에 없는 색이 한 픽셀이라도 섞이면 그 자리가 뜬다
//   ② 반투명 픽셀 — 픽셀아트의 알파는 이진(0 또는 255)이다. 중간값은 스케일 시 뭉갠다
//   ③ 규격 위반 — 시트 크기·슬라이스가 시트 밖으로 나가는 것
//   ④ 빈 그림 — 전부 투명한 PNG (레시피 오타의 흔한 결말)
//
// 사용: node tools/pixel-art/check.mjs [레시피이름 ...]

import { readdirSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { getPixel, toHex } from './png.mjs';
import { paletteHexSet } from './palette.mjs';
import { loadRecipe, renderRecipe, slicesOf } from './build.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const RECIPE_DIR = join(HERE, 'recipes');

/** 이미지 하나 검사 → 위반 문자열 배열 (빈 배열 = 통과). */
export function checkImage(img, recipe, slices) {
  const allowed = paletteHexSet();
  const problems = [];
  const strays = new Map();
  let opaque = 0, translucent = 0;

  for (let y = 0; y < img.height; y++)
    for (let x = 0; x < img.width; x++) {
      const p = getPixel(img, x, y);
      if (p[3] === 0) continue;
      if (p[3] !== 255) { translucent++; continue; } // 반투명은 색 검사 이전에 위반
      opaque++;
      const hex = toHex(p).toLowerCase();
      if (!allowed.has(hex)) strays.set(hex, (strays.get(hex) ?? 0) + 1);
    }

  if (translucent > 0)
    problems.push(`반투명 픽셀 ${translucent}개 — 픽셀아트의 알파는 0 또는 255다`);
  if (strays.size > 0)
    problems.push(`팔레트 밖 색 ${strays.size}종: ` +
      [...strays.entries()].map(([h, n]) => `${h}(${n}px)`).join(' ') +
      ' — palette.json에 이름을 붙이거나 레시피를 고칠 것');
  if (opaque === 0)
    problems.push('전부 투명 — 빈 그림 (레시피 좌표·키 오타?)');

  for (const s of slices) {
    if (s.x < 0 || s.y < 0 || s.x + s.w > recipe.width || s.y + s.h > recipe.height)
      problems.push(`슬라이스 '${s.name}'가 시트 밖 (${s.x},${s.y},${s.w},${s.h} / ` +
                    `시트 ${recipe.width}x${recipe.height})`);
  }
  return problems;
}

export async function checkRecipe(file) {
  const recipe = await loadRecipe(file);
  return { recipe, problems: checkImage(renderRecipe(recipe), recipe, slicesOf(recipe)) };
}

if (process.argv[1] && process.argv[1].endsWith('check.mjs')) {
  const args = process.argv.slice(2);
  const files = args.length
    ? args.map(a => (a.endsWith('.mjs') ? a : `${a}.mjs`))
    : (existsSync(RECIPE_DIR) ? readdirSync(RECIPE_DIR).filter(f => f.endsWith('.mjs')) : []);
  if (!files.length) { console.log('레시피 없음 — 검사할 것이 없다'); process.exit(0); }
  let failed = 0;
  for (const f of files) {
    const { recipe, problems } = await checkRecipe(f);
    if (problems.length) {
      failed++;
      console.log(`✗ ${recipe.name}`);
      for (const p of problems) console.log(`    ${p}`);
    } else {
      console.log(`✓ ${recipe.name}`);
    }
  }
  process.exit(failed ? 1 : 0);
}
