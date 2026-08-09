// 팔레트 (M22-3차 W5a) — **색의 단일 출처**.
//
// 규칙: 레시피는 `palette.json`에 **이름이 붙은 색만** 쓴다. 이름 없는 색을 쓰려면 먼저
// 팩에서 추출해 이름을 붙여야 한다 — 이 한 단계가 "색 발명"을 구조적으로 막는다
// (ADR-M22-15: 신규 색 발명 금지 = 이질감의 제1 원인).
//
// 추출은 자동, 이름은 사람. 추출 결과(palette.extracted.json)는 참고 자료일 뿐이고,
// 게임에 굽히는 것은 palette.json에 이름이 실린 색뿐이다.
//
// 사용:
//   node tools/pixel-art/palette.mjs extract   # 팩 시트 → palette.extracted.json 갱신
//   node tools/pixel-art/palette.mjs list      # 이름 붙은 팔레트 출력

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { readPng, getPixel, toHex, fromHex } from './png.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
export const REPO = join(HERE, '..', '..');
const PACK = join(REPO, 'Assets', 'Kenmi',
                  'Cute Fantasy RPG - 16x16 top down pixel art asset pack', 'Sprites');

/** 추출 대상 시트 — 우리가 그릴 것들의 이웃(같은 화면에 함께 보이는 그림)만 고른다. */
export const SOURCE_SHEETS = {
  fence:   'Outdoor decoration/Fences.png',
  fenceBig:'Outdoor decoration/Fence_Big.png',
  well:    'Outdoor decoration/Well.png',
  posts:   'Outdoor decoration/Lanter_Posts.png',
  decor:   'Outdoor decoration/Outdoor_Decor.png',
  ores:    'Outdoor decoration/Ores.png',
  grass:   'Tiles/Grass/Grass_1_Middle.png',
  pig:     'Animals/Pig/Pig_01.png',
  sheep:   'Animals/Sheep/Sheep_01.png',   // 무채색 털 — 늑대 후보 톤
  horse:   'Animals/Horse/Horse_01.png',   // 갈색 털 — 늑대 대안 톤
  skeleton:'Enemies/Skeleton/Skeleton.png',// 밝은 회색 4단계 (뼈)
};

export const packPath = (rel) => join(PACK, rel);

/** 시트별 색 빈도 (알파 0 제외). 반환 = { 시트키: [{hex, count}, ...] } 빈도 내림차순. */
export function extract(sheets = SOURCE_SHEETS) {
  const out = {};
  for (const [key, rel] of Object.entries(sheets)) {
    const path = packPath(rel);
    if (!existsSync(path)) { out[key] = { error: '파일 없음', path: rel }; continue; }
    const img = readPng(path);
    const counts = new Map();
    for (let y = 0; y < img.height; y++)
      for (let x = 0; x < img.width; x++) {
        const p = getPixel(img, x, y);
        if (p[3] === 0) continue;
        const hex = toHex(p);
        counts.set(hex, (counts.get(hex) ?? 0) + 1);
      }
    out[key] = {
      source: rel,
      colors: [...counts.entries()].sort((a, b) => b[1] - a[1]).map(([hex, count]) => ({ hex, count })),
    };
  }
  return out;
}

const PALETTE_PATH = join(HERE, 'palette.json');

/** 이름 붙은 팔레트 로드 — 레시피가 쓰는 유일한 색 창구. */
export function loadPalette() {
  const json = JSON.parse(readFileSync(PALETTE_PATH, 'utf8'));
  return json.colors;
}

/** 팔레트 키 또는 'transparent' → RGBA. 미등록 키는 던진다 (색 발명 차단). */
export function color(key, palette = loadPalette()) {
  if (key === 'transparent' || key === '.') return [0, 0, 0, 0];
  const hex = palette[key];
  if (!hex) throw new Error(`팔레트에 없는 색 키: '${key}' — palette.json에 먼저 이름을 붙일 것`);
  return fromHex(hex);
}

/** 팔레트에 있는 모든 색의 hex 집합 (check.mjs의 이탈 검사용). */
export function paletteHexSet(palette = loadPalette()) {
  return new Set(Object.values(palette).map(h => h.toLowerCase()));
}

if (process.argv[1] && process.argv[1].endsWith('palette.mjs')) {
  const cmd = process.argv[2] ?? 'list';
  if (cmd === 'extract') {
    const data = extract();
    writeFileSync(join(HERE, 'palette.extracted.json'), JSON.stringify(data, null, 2) + '\n');
    for (const [k, v] of Object.entries(data)) {
      if (v.error) { console.log(`${k}: ${v.error} (${v.path})`); continue; }
      console.log(`\n[${k}] ${v.source} — ${v.colors.length}색`);
      for (const c of v.colors.slice(0, 12)) console.log(`  ${c.hex}  ${c.count}`);
    }
  } else {
    const p = loadPalette();
    for (const [k, v] of Object.entries(p)) console.log(`${k.padEnd(16)} ${v}`);
    console.log(`\n총 ${Object.keys(p).length}색`);
  }
}
