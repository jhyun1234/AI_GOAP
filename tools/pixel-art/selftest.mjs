// 도구 자체 검증 (M22-3차 W5a) — 게임 에셋을 한 장도 굽지 않고 파이프라인을 증명한다.
//
// 여기서 쓰는 레시피는 **합성 레시피**(임시)다. 진짜 그림은 W5b·W5c의 recipes/*.mjs다.
// 이 파일의 존재 이유: "검사가 실패할 수 있는가"를 증명하는 것 (전과 3회 — 실패 불가능한
// 테스트를 짜 놓고 통과했다고 말한 이력이 있다).
//
// 사용: node tools/pixel-art/selftest.mjs

import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { readFileSync } from 'node:fs';
import { createImage, readPng, writePng, setPixel } from './png.mjs';
import { packPath, SOURCE_SHEETS, loadPalette, color } from './palette.mjs';
import { renderRecipe, buildMeta, slicesOf, internalIdOf } from './build.mjs';
import { checkImage } from './check.mjs';

let pass = 0, fail = 0;
const ok = (cond, name, detail = '') => {
  if (cond) { pass++; console.log(`  ✓ ${name}`); }
  else { fail++; console.log(`  ✗ ${name}${detail ? ' — ' + detail : ''}`); }
};

console.log('[1] PNG 왕복 (디코더·인코더 동시 증명)');
{
  const src = packPath(SOURCE_SHEETS.fence);
  const img = readPng(src);
  const tmp = join(tmpdir(), 'pixelart-roundtrip.png');
  writePng(tmp, img);
  const back = readPng(tmp);
  ok(back.width === img.width && back.height === img.height, '크기 보존');
  ok(back.data.equals(img.data), '픽셀 바이트 일치');
  writePng(tmp + '.2.png', img);
  ok(readFileSync(tmp).equals(readFileSync(tmp + '.2.png')), '두 번 구워도 파일 바이트 동일');
}

console.log('[2] 팔레트 — 이름 없는 색은 못 쓴다 (색 발명 차단)');
{
  const p = loadPalette();
  ok(Object.keys(p).length > 0, `이름 붙은 색 ${Object.keys(p).length}개`);
  ok(color('wood_mid', p)[3] === 255, 'wood_mid 조회');
  let threw = false;
  try { color('made_up_color', p); } catch { threw = true; }
  ok(threw, '미등록 키는 예외를 던진다');
}

console.log('[3] 레시피 렌더 — blit(합성)과 pixels(신작)가 같은 문법');
const recipe = {
  name: 'SelfTest', width: 16, height: 16,
  slices: [{ name: 'SelfTest_0', x: 0, y: 0, w: 16, h: 8 },
           { name: 'SelfTest_1', x: 0, y: 8, w: 16, h: 8 }],
  layers: [
    { op: 'blit', from: SOURCE_SHEETS.fence, src: [0, 0, 16, 16], dst: [0, 0] },
    { op: 'pixels', at: [0, 0], key: { '#': 'wood_hi' }, map: ['#'] },
  ],
};
{
  const a = renderRecipe(recipe);
  const b = renderRecipe(recipe);
  ok(a.data.equals(b.data), '같은 레시피 → 같은 픽셀 (결정성)');
  ok(a.data.subarray(0, 4).equals(Buffer.from(color('wood_hi'))), 'pixels 레이어가 blit 위에 덮인다');
}

console.log('[4] meta — 좌표계 변환과 결정적 fileID');
{
  const m1 = buildMeta(recipe, slicesOf(recipe));
  const m2 = buildMeta(recipe, slicesOf(recipe));
  ok(m1 === m2, '두 번 만들어도 meta 바이트 동일');
  // 레시피는 좌상단 원점, Unity rect는 좌하단 원점 — 변환은 build.mjs 한 곳에서만 한다.
  // SelfTest_0: 레시피 y=0,h=8 → Unity y = 16-0-8 = 8 / SelfTest_1: y=8 → 16-8-8 = 0
  ok(/name: SelfTest_0[\s\S]*?y: 8/.test(m1), '좌상단 → 좌하단 변환 (윗줄이 Unity y=8)');
  ok(/name: SelfTest_1[\s\S]*?y: 0/.test(m1), '아랫줄이 Unity y=0');
  const id = internalIdOf('SelfTest/SelfTest_0');
  ok(id === internalIdOf('SelfTest/SelfTest_0') && id !== 0, `fileID 안정·0 아님 (${id})`);
  ok(id !== internalIdOf('SelfTest/SelfTest_1'), '이름이 다르면 fileID도 다르다');
  ok(m1.includes(`SelfTest_0: ${id}`), 'nameFileIdTable에 같은 값이 실린다');
  ok(!m1.includes('AssetOrigin'), '에셋스토어 출처 블록 없음 (우리가 만든 파일)');
}

console.log('[5] check — 일부러 만든 위반을 잡는가 (실패 가능성 증명)');
{
  const slices = slicesOf(recipe);
  const clean = renderRecipe(recipe);
  ok(checkImage(clean, recipe, slices).length === 0, '정상 그림은 통과');

  const stray = renderRecipe(recipe);
  setPixel(stray, 5, 5, [1, 2, 3, 255]); // 팩에 없는 색 1픽셀
  ok(checkImage(stray, recipe, slices).some(p => p.includes('팔레트 밖 색')), '팔레트 이탈 1픽셀 검출');

  const soft = renderRecipe(recipe);
  setPixel(soft, 6, 6, [...color('wood_mid').slice(0, 3), 128]); // 반투명 1픽셀
  ok(checkImage(soft, recipe, slices).some(p => p.includes('반투명')), '반투명 1픽셀 검출');

  const empty = createImage(16, 16);
  ok(checkImage(empty, recipe, slices).some(p => p.includes('전부 투명')), '빈 그림 검출');

  const bad = { ...recipe };
  ok(checkImage(clean, bad, [{ name: 'X', x: 0, y: 0, w: 99, h: 99 }])
       .some(p => p.includes('시트 밖')), '시트 밖 슬라이스 검출');
}

console.log(`\n결과: ${pass} 통과 / ${fail} 실패`);
process.exit(fail ? 1 : 0);
