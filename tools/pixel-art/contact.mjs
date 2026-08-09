// 대조 시트 (M22-3차 W5a) — **사람 판정을 싸게 만드는 장치**.
//
// 새 그림을 기존 팩 그림 옆에, 게임과 같은 풀색 바닥 위에 나란히 놓고 확대해 한 장으로
// 굽는다. Play를 켜지 않고 "2번"이라고만 답하면 되게 하는 것이 목적이다 (사용자 확정
// 판정 방식, 2026-08-09).
//
// ⚠️ 이 그림은 판정용이지 게임 에셋이 아니다 — Assets/ 밖(스크래치)으로 나간다.
//
// 사용:
//   node tools/pixel-art/contact.mjs <출력경로> <항목> [항목 ...]
//     항목 = recipe:<이름>            (레시피를 즉석 렌더)
//          | pack:<팩상대경로>[@x,y,w,h] (팩 시트 조각 — 기준 그림)
//   예) node tools/pixel-art/contact.mjs out.png recipe:watchtower "pack:Outdoor decoration/Fences.png@32,32,16,16"

import { createImage, readPng, writePng, getPixel, setPixel, fromHex } from './png.mjs';
import { loadPalette, packPath } from './palette.mjs';
import { loadRecipe, renderRecipe } from './build.mjs';

const SCALE = 6;      // 확대 배율 (16px → 96px — 픽셀 하나가 눈에 보이는 크기)
const PAD = 8;        // 항목 사이 여백(원본 px 기준)
const MARGIN = 6;     // 바깥 여백

/** 최근접 확대 — 픽셀아트는 보간하면 안 된다 (Point 필터와 같은 규칙). */
function scaleUp(img, scale) {
  const out = createImage(img.width * scale, img.height * scale);
  for (let y = 0; y < img.height; y++)
    for (let x = 0; x < img.width; x++) {
      const p = getPixel(img, x, y);
      for (let dy = 0; dy < scale; dy++)
        for (let dx = 0; dx < scale; dx++)
          setPixel(out, x * scale + dx, y * scale + dy, p);
    }
  return out;
}

async function resolveItem(spec) {
  if (spec.startsWith('recipe:')) {
    const recipe = await loadRecipe(`${spec.slice(7)}.mjs`);
    return { label: recipe.name, img: renderRecipe(recipe) };
  }
  if (spec.startsWith('pack:')) {
    const [rel, rect] = spec.slice(5).split('@');
    const src = readPng(packPath(rel));
    if (!rect) return { label: rel, img: src };
    const [x, y, w, h] = rect.split(',').map(Number);
    const cut = createImage(w, h);
    for (let j = 0; j < h; j++)
      for (let i = 0; i < w; i++) setPixel(cut, i, j, getPixel(src, x + i, y + j));
    return { label: `${rel}@${rect}`, img: cut };
  }
  throw new Error(`알 수 없는 항목: ${spec} (recipe: 또는 pack: 로 시작해야 한다)`);
}

export async function buildContactSheet(outPath, specs) {
  const items = [];
  for (const s of specs) items.push(await resolveItem(s));

  const cellW = Math.max(...items.map(i => i.img.width));
  const cellH = Math.max(...items.map(i => i.img.height));
  const cols = items.length;
  const sheetW = MARGIN * 2 + cols * cellW + (cols - 1) * PAD;
  const sheetH = MARGIN * 2 + cellH;

  // 바닥 = 게임 화면의 풀색 (판정 조건을 실전과 맞춘다 — 흰 배경에서는 다 어울려 보인다)
  const bg = fromHex(loadPalette().grass);
  const sheet = createImage(sheetW, sheetH);
  for (let y = 0; y < sheetH; y++)
    for (let x = 0; x < sheetW; x++) setPixel(sheet, x, y, bg);

  items.forEach((it, idx) => {
    const cx = MARGIN + idx * (cellW + PAD);
    const ox = cx + Math.floor((cellW - it.img.width) / 2);  // 가로 가운데
    const oy = MARGIN + (cellH - it.img.height);             // 바닥 정렬 (땅에 선 것처럼)
    for (let y = 0; y < it.img.height; y++)
      for (let x = 0; x < it.img.width; x++) {
        const p = getPixel(it.img, x, y);
        if (p[3] === 0) continue;
        setPixel(sheet, ox + x, oy + y, p);
      }
  });

  writePng(outPath, scaleUp(sheet, SCALE));
  return items.map((it, i) => `${i + 1}. ${it.label}`);
}

if (process.argv[1] && process.argv[1].endsWith('contact.mjs')) {
  const [out, ...specs] = process.argv.slice(2);
  if (!out || !specs.length) {
    console.log('사용: node contact.mjs <출력경로> <recipe:이름|pack:경로[@x,y,w,h]> ...');
    process.exit(1);
  }
  const labels = await buildContactSheet(out, specs);
  console.log(`대조 시트 → ${out} (배율 ${SCALE}x, 왼쪽부터)`);
  for (const l of labels) console.log('  ' + l);
}
