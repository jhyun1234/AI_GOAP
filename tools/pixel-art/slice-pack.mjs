// 팩 시트 격자 슬라이스 + 스프라이트 세트 생성 (M24-1차 W6).
//
// 🔑 이 도구의 존재 이유는 **fileID 손배선의 재발 방지**다. M23은 팩 서브스프라이트 94개의
// fileID를 손으로 역산해 배선했고 "한 자리 오타 = null 로드"였다. 여기서는 사람이 적는 것이
// 「시트, 격자, 어느 행이 무엇인가」뿐이고 나머지는 전부 계산된다.
//
// 하는 일 셋:
//   ① 팩 PNG의 .meta 를 **균일 격자**로 다시 자른다 (guid 는 보존 — 참조가 안 깨진다)
//      + 우리 임포트 규격 강제: filterMode 0(Point) · PPU 16 · spriteMode 2
//      ⚠️ 팩 기본값은 Bilinear·PPU 100 이라 그대로 쓰면 픽셀아트가 뭉개진다.
//   ② 빈 칸은 건너뛴다 — 애니메이션 배열에 투명 프레임이 섞이면 캐릭터가 깜빡인다.
//   ③ AgentSpriteSetSO 에셋 YAML 을 찍어 낸다 (Idle/Walk 3방 + Attack 3방).
//
// 🔴 팩 PNG·meta 는 `Assets/Kenmi/`(gitignore) 안에 있다 — 재배포 금지 라이선스라 그대로 둔다.
// 이 도구는 **그 자리에서** meta 만 고치므로 공개 리포에 팩 픽셀이 한 장도 안 올라간다.
// 생성되는 세트 에셋(Assets/M0Config/Sprites/)은 GUID+fileID 참조만 담는다.
//
// 🔴 **팩을 다시 받으면 이 도구를 다시 돌려야 한다.** 팩 meta 는 gitignore 대상이라
// 새 기기·재임포트에서는 팩 기본값(Bilinear·PPU 100·다른 fileID)으로 돌아오고, 그러면
// Assets/M0Config/Sprites/*.asset 의 참조가 전부 null 이 된다 (게이트 M22_T16 이 red 로 잡는다).
// 산식이 결정적이라 다시 돌리면 같은 fileID 가 복원된다 — 재배선은 필요 없다.
//
// 사용: AGENTSPRITESET_GUID=<AgentSpriteSetSO.cs.meta 의 guid> node tools/pixel-art/slice-pack.mjs

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { readPng, getPixel } from './png.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = join(HERE, '..', '..');
const PACK = join(REPO, 'Assets', 'Kenmi', 'Cute_Fantasy_Characters');
const OUT = join(REPO, 'Assets', 'M0Config', 'Sprites');

/** internalID (signed int32, 0 금지) — build.mjs 와 같은 FNV-1a. 이름만 같으면 값이 같다. */
function internalIdOf(key) {
  let h = 0x811c9dc5;
  for (let i = 0; i < key.length; i++) { h ^= key.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
  const s = h | 0;
  return s === 0 ? 1 : s;
}
const guidOf = (k) => createHash('md5').update(`AI_GOAP/M24/${k}`).digest('hex');

// ── 배선표 — 사람이 적는 유일한 것 ──────────────────────────────────────────
//
// 시트 판독 (2026-08-10, 실물 확대 확인): 모든 6×13 / 8×13 시트가 같은 행 구조다.
//   행 0·1·2 = 대기  Down / Side / Up
//   행 3·4·5 = 걷기  Down / Side / Up
//   행 6·7·8 = 공격  Down / Side / Up
//   행 9~12  = 피격·사망 (이번 범위 밖 — M13 표현층 몫)
// Side 는 오른쪽을 본다 → SideFacesRight = true.
//
// ⚠️ 오크 대형 시트(Orc_Chief/Grunt/Peon)와 고블린 48px 판은 격자가 달라 제외했다.
//    한 종족에 한 시트면 충분하다 — 등급은 스탯이 가르지 그림이 가르지 않는다 (이번 범위).
const SETS = [
  { race: 'Goblin', sheet: 'Goblins/Goblin_Thief.png',  cell: 32, cols: 6, scale: 0.75 },
  { race: 'Orc',    sheet: 'Orcs/Orc_Archer.png',       cell: 48, cols: 6, scale: 0.50 },
  { race: 'Knight', sheet: 'Knights/Templar.png',       cell: 48, cols: 6, scale: 0.50 },
  { race: 'Demon',  sheet: 'Angels/Angel_2.png',        cell: 64, cols: 8, scale: 0.375 },
];
// scale 은 "화면에 서는 크기"를 맞춘 값이다 (W5d 교훈: 배율 1 고정이 아니라 크기가 불변식).
// PPU 16 이므로 32px=2유닛·48px=3·64px=4 → 전부 1.5유닛으로 수렴시킨다.

const ROWS = { IdleDown: 0, IdleSide: 1, IdleUp: 2, WalkDown: 3, WalkSide: 4, WalkUp: 5,
               AtkDown: 6, AtkSide: 7, AtkUp: 8 };

/** 이 칸에 그림이 있는가 (불투명 픽셀 1개라도). */
function cellHasArt(img, cx, cy, cell) {
  for (let y = 0; y < cell; y++)
    for (let x = 0; x < cell; x++) {
      const px = cx * cell + x, py = cy * cell + y;
      if (px >= img.width || py >= img.height) continue;
      if (getPixel(img, px, py)[3] !== 0) return true;
    }
  return false;
}

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

const META_TAIL = (table) => `    outline: []
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
${table}
  spritePackingTag:
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:
  assetBundleName:
  assetBundleVariant:
`;

function sliceSheet(cfg) {
  const png = join(PACK, cfg.sheet);
  const metaPath = png + '.meta';
  const img = readPng(png);
  const rows = Math.floor(img.height / cfg.cell);
  // guid 보존 — 기존 참조(있다면)와 재실행 간 안정성의 핵심
  const guid = (readFileSync(metaPath, 'utf8').match(/^guid: ([a-f0-9]{32})/m) || [])[1];
  if (!guid) throw new Error(`${cfg.sheet}: 기존 meta 에서 guid 를 못 읽었다`);

  let sprites = '', table = [];
  const byCell = new Map();
  for (let r = 0; r < rows; r++)
    for (let c = 0; c < cfg.cols; c++) {
      if (!cellHasArt(img, c, r, cfg.cell)) continue;   // 빈 칸 건너뜀
      const name = `${cfg.race}_${r}_${c}`;
      const id = internalIdOf(`AI_GOAP/M24/${cfg.race}/${r}_${c}`);
      const unityY = img.height - (r + 1) * cfg.cell;   // 좌상단 → 좌하단 (유일 변환 지점)
      byCell.set(`${r}_${c}`, id);
      table.push(`      ${name}: ${id}`);
      sprites +=
`    - serializedVersion: 2
      name: ${name}
      rect:
        serializedVersion: 2
        x: ${c * cfg.cell}
        y: ${unityY}
        width: ${cfg.cell}
        height: ${cfg.cell}
      alignment: 0
      pivot: {x: 0, y: 0}
      border: {x: 0, y: 0, z: 0, w: 0}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: ${createHash('md5').update(`AI_GOAP/M24/sprite/${name}`).digest('hex')}
      internalID: ${id}
      vertices: []
      indices:
      edges: []
      weights: []
`;
    }
  writeFileSync(metaPath, META_HEAD(guid) + sprites + META_TAIL(table.sort().join('\n')));
  return { guid, rows, byCell, count: table.length };
}

/** 한 행의 프레임 참조 목록 (빈 칸은 이미 빠져 있다).
 * ⚠️ `indent` 필수 — YAML 배열 항목은 **자기 키와 같은 열**에 와야 한다. 중첩 배열
 * (`Actions[0].Down`)을 최상위와 같은 2칸으로 찍으면 Unity가 그 키를 **빈 배열로 읽고**
 * 항목들은 바깥 배열의 원소가 된다. 컴파일도 임포트도 조용히 통과하고 게이트에서야
 * "공격 Down 비어 있음"으로 드러난다 (M24-1차 W6에서 실제로 밟았다). */
const rowRefs = (sl, guid, row, cols, indent) => {
  const out = [];
  for (let c = 0; c < cols; c++) {
    const id = sl.byCell.get(`${row}_${c}`);
    if (id !== undefined) out.push(`${indent}- {fileID: ${id}, guid: ${guid}, type: 3}`);
  }
  return out.join('\n');
};

const SET_SCRIPT_GUID = process.env.AGENTSPRITESET_GUID;

function main() {
  if (!existsSync(OUT)) mkdirSync(OUT, { recursive: true });
  for (const cfg of SETS) {
    const sl = sliceSheet(cfg);
    const g = sl.guid;
    const R = (row) => rowRefs(sl, g, row, cfg.cols, '  ');    // 최상위 배열 (IdleDown 등)
    const A = (row) => rowRefs(sl, g, row, cfg.cols, '    ');  // 중첩 배열 (Actions[].Down 등)
    const assetName = `Race_${cfg.race}_Sprites`;
    const yaml = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SET_SCRIPT_GUID}, type: 3}
  m_Name: ${assetName}
  m_EditorClassIdentifier:
  IdleDown:
${R(ROWS.IdleDown)}
  IdleSide:
${R(ROWS.IdleSide)}
  IdleUp:
${R(ROWS.IdleUp)}
  WalkDown:
${R(ROWS.WalkDown)}
  WalkSide:
${R(ROWS.WalkSide)}
  WalkUp:
${R(ROWS.WalkUp)}
  Actions:
  - Kind: 5   # AnimKind.Attack (ActionSO.cs — None0 Chop1 Mine2 Hammer3 Water4 Attack5)
    Down:
${A(ROWS.AtkDown)}
    Side:
${A(ROWS.AtkSide)}
    Up:
${A(ROWS.AtkUp)}
  FramesPerSecond: 8
  Scale: ${cfg.scale}
  SideFacesRight: 1
  TintStrength: 0
`;
    writeFileSync(join(OUT, assetName + '.asset'), yaml);
    writeFileSync(join(OUT, assetName + '.asset.meta'),
`fileFormatVersion: 2
guid: ${guidOf(assetName)}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
`);
    console.log(`${assetName.padEnd(24)} ${cfg.sheet.padEnd(30)} 칸 ${sl.count}/${sl.rows * cfg.cols}  guid=${guidOf(assetName)}`);
  }
}

main();
