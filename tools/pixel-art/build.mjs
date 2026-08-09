// 레시피 → PNG + .png.meta (M22-3차 W5a). 결정적 빌드: 같은 레시피 → 같은 바이트.
//
// 🔑 이 도구의 존재 이유는 **fileID 손배선 지옥의 해소**다. M23은 팩 시트의 서브스프라이트
// fileID 94개를 손으로 역산해 배선했고 "한 자리 오타 = null 로드"였다. 우리가 굽는 시트는
// meta도 우리가 쓰므로, internalID를 **스프라이트 이름의 해시로 고정**한다 — 그림을 고쳐
// 다시 구워도 에셋 참조가 안 깨지고, 참조를 손으로 옮겨 적을 일도 없다.
//
// 좌표계 (⚠️ 함정): 레시피의 모든 좌표는 **좌상단 원점**이다 (PNG·픽셀맵과 같은 방향).
// Unity 스프라이트 rect는 좌하단 원점이라, 변환은 이 파일이 한 곳에서만 한다.
//
// 레시피 계약:
//   export default {
//     name: 'Watchtower',            // 시트 이름 = 파일명 = 스프라이트 접두
//     width, height,                 // 시트 크기(px)
//     slices: [{ name, x, y, w, h }],// 좌상단 기준. 이름 생략 시 `<name>_<i>`
//     layers: [
//       { op:'blit',   from:'Outdoor decoration/Well.png', src:[x,y,w,h], dst:[x,y],
//         swap:{ '#743f39':'wood_mid' } },        // 팩 조각 합성 (+색 치환, 선택)
//       { op:'pixels', at:[x,y], key:{ '#':'wood_mid' }, map:['..##..', ...] },  // 신작
//     ],
//   }
// blit(합성)과 pixels(신작)가 같은 레시피 안에 산다 — "합성으로 해보고 별로면 신작"이
// 파일을 갈아엎지 않고 레이어 교체로 일어난다 (사용자 방침, 2026-08-09).
//
// 사용: node tools/pixel-art/build.mjs [레시피이름 ...]   (인자 없으면 recipes/ 전부)

import { readdirSync, mkdirSync, existsSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join, basename } from 'node:path';
import { createImage, readPng, writePng, getPixel, setPixel, toHex } from './png.mjs';
import { loadPalette, color, packPath, REPO } from './palette.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const RECIPE_DIR = join(HERE, 'recipes');
export const ART_DIR = join(REPO, 'Assets', 'M0Config', 'Art');

// ── 결정적 식별자 ─────────────────────────────────────────────────────────

/** Unity GUID (32 hex) — 이름에서 결정적으로. 다시 구워도 같은 값이라 참조가 안 깨진다. */
const guidOf = (key) => createHash('md5').update(`AI_GOAP/pixel-art/${key}`).digest('hex');

/** 서브스프라이트 spriteID (32 hex). */
const spriteIdOf = (key) => createHash('md5').update(`AI_GOAP/sprite/${key}`).digest('hex');

/** internalID (signed int32, 0 금지) — FNV-1a. 이것이 에셋 YAML의 fileID가 된다. */
export function internalIdOf(key) {
  let h = 0x811c9dc5;
  for (let i = 0; i < key.length; i++) {
    h ^= key.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }
  const signed = h | 0;
  return signed === 0 ? 1 : signed;
}

// ── 레이어 합성 ───────────────────────────────────────────────────────────

function applyBlit(img, layer, palette) {
  const src = readPng(packPath(layer.from));
  const [sx, sy, sw, sh] = layer.src ?? [0, 0, src.width, src.height];
  const [dx, dy] = layer.dst ?? [0, 0];
  // 색 치환표: 팩 hex(소문자) → 팔레트 RGBA. 없으면 원본색 그대로.
  const swap = new Map(Object.entries(layer.swap ?? {})
    .map(([hex, key]) => [hex.toLowerCase(), color(key, palette)]));
  for (let y = 0; y < sh; y++)
    for (let x = 0; x < sw; x++) {
      const p = getPixel(src, sx + x, sy + y);
      if (p[3] === 0) continue;
      const rep = swap.get(toHex(p));
      setPixel(img, dx + x, dy + y, rep ?? p);
    }
}

function applyPixels(img, layer, palette) {
  const [ax, ay] = layer.at ?? [0, 0];
  const key = layer.key ?? {};
  layer.map.forEach((row, y) => {
    [...row].forEach((ch, x) => {
      if (ch === '.' || ch === ' ') return; // 투명 — 아래 레이어를 살린다
      const name = key[ch];
      if (!name) throw new Error(`픽셀맵 글자 '${ch}'에 팔레트 키가 없다 (key 표를 채울 것)`);
      setPixel(img, ax + x, ay + y, color(name, palette));
    });
  });
}

export function renderRecipe(recipe) {
  const palette = loadPalette();
  const img = createImage(recipe.width, recipe.height);
  for (const layer of recipe.layers ?? []) {
    if (layer.op === 'blit') applyBlit(img, layer, palette);
    else if (layer.op === 'pixels') applyPixels(img, layer, palette);
    else throw new Error(`알 수 없는 레시피 연산: ${layer.op} (blit·pixels 둘뿐)`);
  }
  return img;
}

// ── meta 생성 ─────────────────────────────────────────────────────────────
// 임포터 설정은 Kenmi 시트(Fences.png.meta)에서 그대로 가져왔다 — **같은 임포트 설정이라야
// 같은 룩**이다 (16 PPU · Point 필터 · Multiple 슬라이스 · alphaIsTransparency).
// AssetOrigin(에셋스토어 출처) 블록은 뺀다 — 우리가 만든 파일이다.

const META_HEAD = (guid) => `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 2
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 16
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:
`;

const META_TAIL = (nameTable) => `    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable:
${nameTable}
  spritePackingTag:
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:
  assetBundleName:
  assetBundleVariant:
`;

/** 슬라이스 목록 → meta 문자열. y는 레시피(좌상단) → Unity(좌하단) 변환이 여기 한 곳. */
export function buildMeta(recipe, slices) {
  const guid = guidOf(recipe.name);
  let sprites = '';
  const table = [];
  for (const s of slices) {
    const unityY = recipe.height - s.y - s.h; // ⚠️ 좌상단 → 좌하단 (유일한 변환 지점)
    const id = internalIdOf(`${recipe.name}/${s.name}`);
    table.push(`      ${s.name}: ${id}`);
    // 피벗 (M22-3차 W5b 실사): BuildingVisualizer는 스프라이트를 **타일 중심**에 놓고
    // FallbackSize로 배율만 준다. 기본 정렬(alignment 0 = Center)이면 2타일 높이 구조물의
    // 아래 절반이 땅 밑으로 내려간다 — 밑단이 타일을 덮게 하려면 피벗 y를 "바닥에서 반 타일"
    // (= 8px / 시트높이)로 내려야 한다. 레시피가 pivot을 주면 Custom(9)으로 쓴다.
    const pv = s.pivot;
    const align = pv ? 9 : 0;
    const pivot = pv ? `{x: ${pv[0]}, y: ${pv[1]}}` : '{x: 0, y: 0}';
    sprites +=
`    - serializedVersion: 2
      name: ${s.name}
      rect:
        serializedVersion: 2
        x: ${s.x}
        y: ${unityY}
        width: ${s.w}
        height: ${s.h}
      alignment: ${align}
      pivot: ${pivot}
      border: {x: 0, y: 0, z: 0, w: 0}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: ${spriteIdOf(`${recipe.name}/${s.name}`)}
      internalID: ${id}
      vertices: []
      indices:
      edges: []
      weights: []
`;
  }
  // nameFileIdTable은 이름 사전순 (Unity가 그렇게 쓴다 — 재생성 diff 안정)
  return META_HEAD(guid) + sprites + META_TAIL(table.sort().join('\n'));
}

/** 레시피의 슬라이스 목록 정규화 (없으면 시트 전체 1장). */
export function slicesOf(recipe) {
  const raw = recipe.slices ?? [{ x: 0, y: 0, w: recipe.width, h: recipe.height }];
  return raw.map((s, i) => ({ name: s.name ?? `${recipe.name}_${i}`, ...s }));
}

export async function loadRecipe(file) {
  const mod = await import(pathToFileURL(join(RECIPE_DIR, file)).href);
  return mod.default;
}

export async function buildOne(file) {
  const recipe = await loadRecipe(file);
  const img = renderRecipe(recipe);
  const slices = slicesOf(recipe);
  if (!existsSync(ART_DIR)) mkdirSync(ART_DIR, { recursive: true });
  const png = join(ART_DIR, `${recipe.name}.png`);
  const bytes = writePng(png, img);
  const { writeFileSync } = await import('node:fs');
  writeFileSync(`${png}.meta`, buildMeta(recipe, slices));
  return { recipe, png, bytes, slices };
}

if (process.argv[1] && process.argv[1].endsWith('build.mjs')) {
  const args = process.argv.slice(2);
  const files = args.length
    ? args.map(a => (a.endsWith('.mjs') ? a : `${a}.mjs`))
    : (existsSync(RECIPE_DIR) ? readdirSync(RECIPE_DIR).filter(f => f.endsWith('.mjs')) : []);
  if (!files.length) { console.log('레시피 없음 (recipes/*.mjs) — 도구만 준비된 상태'); process.exit(0); }
  for (const f of files) {
    const { recipe, png, bytes, slices } = await buildOne(f);
    console.log(`${basename(png)}  ${recipe.width}x${recipe.height}  ` +
                `슬라이스 ${slices.length}  ${bytes}B`);
    for (const s of slices)
      console.log(`   ${s.name}  fileID=${internalIdOf(`${recipe.name}/${s.name}`)}`);
  }
}
