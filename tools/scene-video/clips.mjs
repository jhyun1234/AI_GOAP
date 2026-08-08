/* 촬영 수확물 인제스트 (롱폼 트랙 W4, ADR-LF-4).
   사용:
     node clips.mjs ingest <세션폴더|세션이름>   raw.mp4 + cuts.json → 사건별 클립 + 카탈로그
     node clips.mjs stills <clipId>              클립에서 스틸 3장 (빈 컷 검사용)
     node clips.mjs list                         카탈로그 요약 (대본 작업의 창구)

   원본 raw.mp4 는 지우지 않는다 — 컷 경계를 다시 잡을 수 있어야 한다.
   슬라이스는 재인코딩이다 — 스트림 카피(-c copy)는 키프레임 경계라 시각이 민다 (명세 ⚠️).
   기본은 attended(카메라가 간) 컷만 — 안 간 컷은 화면에 사건이 없다. --all 로 전부.

   경로: 기본 D:\AI_GOAP-videos\clips (환경변수 SCENE_CLIPS_ROOT 로 교체 —
   "D: 가 없는 기기" 갈래를 실제로 돌려 보기 위한 구멍이지 편의가 아니다). */
import fs from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';
import { findFfmpeg } from './lib-node.mjs';

const CLIPS_ROOT = process.env.SCENE_CLIPS_ROOT || 'D:\\AI_GOAP-videos\\clips';
const LIB_DIR = path.join(CLIPS_ROOT, 'library');
const LIB_FILE = path.join(LIB_DIR, 'library.json');

// 컷 경계 (명세 W4 제안치) — 사건 시각 앞 4초·뒤 3초. 카메라 도착(러프 ~1초)과
// Recorder 시작 오프셋이 이 여유 안에서 흡수된다.
const PRE_S = 4, POST_S = 3;

const argv = process.argv.slice(2);
const cmd = argv[0];
const ALL = argv.includes('--all');

const ffmpeg = findFfmpeg();
if (!ffmpeg) { console.error('🔴 ffmpeg 을 못 찾았다'); process.exit(1); }

const loadLib = () => fs.existsSync(LIB_FILE)
  ? JSON.parse(fs.readFileSync(LIB_FILE, 'utf8')) : { clips: [] };
const saveLib = lib => {
  fs.mkdirSync(LIB_DIR, { recursive: true });
  fs.writeFileSync(LIB_FILE, JSON.stringify(lib, null, 2));
};

function ingest(arg) {
  const dir = fs.existsSync(arg) ? arg : path.join(CLIPS_ROOT, arg);
  const raw = path.join(dir, 'raw.mp4');
  const cutsFile = path.join(dir, 'cuts.json');
  if (!fs.existsSync(raw) || !fs.existsSync(cutsFile)) {
    console.error(`🔴 세션 폴더가 아니다 (raw.mp4/cuts.json 필요): ${dir}`); process.exit(1);
  }
  const session = path.basename(dir);
  const cuts = JSON.parse(fs.readFileSync(cutsFile, 'utf8')).cuts ?? [];
  const lib = loadLib();
  const have = new Set(lib.clips.map(c => c.id));
  fs.mkdirSync(LIB_DIR, { recursive: true });

  let made = 0, skipped = 0, unattended = 0;
  cuts.forEach((c, i) => {
    if (!c.attended && !ALL) { unattended++; return; }
    const id = `${c.kind}-${session}-${String(i).padStart(2, '0')}`;
    const out = path.join(LIB_DIR, `${id}.mp4`);
    if (have.has(id) && fs.existsSync(out)) { skipped++; return; }   // 멱등 (W4 DoD)
    const start = Math.max(0, c.recSec - PRE_S);
    const dur = (c.recSec - start) + POST_S;
    const r = spawnSync(ffmpeg, [
      '-y', '-loglevel', 'error',
      '-ss', start.toFixed(2), '-i', raw, '-t', dur.toFixed(2),
      '-c:v', 'libx264', '-crf', '18', '-preset', 'medium', '-an',
      out,
    ], { stdio: 'inherit' });
    if (r.status !== 0) { console.error(`🔴 슬라이스 실패: ${id}`); return; }
    lib.clips = lib.clips.filter(x => x.id !== id);
    lib.clips.push({
      id, kind: c.kind, detail: c.detail, session,
      recSec: c.recSec, durSec: Number(dur.toFixed(2)),
      camDist: c.camDist, attended: c.attended,
      src: path.relative(CLIPS_ROOT, out).replace(/\\/g, '/'),
    });
    made++;
  });
  saveLib(lib);
  console.log(`인제스트 ${session} — 신규 ${made} · 재사용 ${skipped}` +
    (ALL ? '' : ` · 미출석 제외 ${unattended} (--all 로 포함)`) +
    `\n카탈로그  ${LIB_FILE} (${lib.clips.length}개)`);
}

/* 재슬라이스 — 원본 raw.mp4 를 보존하는 이유가 이것이다. 7초 기본 컷이 샷 길이보다
   짧으면(클립이 끝나고 마지막 프레임에 멈춤 → 정적 검사에 걸림) 같은 사건을 더 길게
   다시 자른다. 카탈로그의 recSec·session 으로 원본 위치를 찾는다. */
function reslice(clipId, preS, postS) {
  const lib = loadLib();
  const c = lib.clips.find(x => x.id === clipId);
  if (!c) { console.error(`🔴 카탈로그에 없다: ${clipId}`); process.exit(1); }
  const raw = path.join(CLIPS_ROOT, c.session, 'raw.mp4');
  if (!fs.existsSync(raw)) { console.error(`🔴 원본이 없다: ${raw}`); process.exit(1); }
  const out = path.join(CLIPS_ROOT, c.src);
  const start = Math.max(0, c.recSec - preS);
  const dur = (c.recSec - start) + postS;
  const r = spawnSync(ffmpeg, [
    '-y', '-loglevel', 'error',
    '-ss', start.toFixed(2), '-i', raw, '-t', dur.toFixed(2),
    '-c:v', 'libx264', '-crf', '18', '-preset', 'medium', '-an',
    out,
  ], { stdio: 'inherit' });
  if (r.status !== 0) { console.error(`🔴 재슬라이스 실패: ${clipId}`); process.exit(1); }
  c.durSec = Number(dur.toFixed(2));
  c.preS = preS;   // 사건 시각 = 클립 안 preS 초 지점 (in 오프셋 계산용)
  saveLib(lib);
  console.log(`재슬라이스 ${clipId} — ${c.durSec}s (사건은 ${preS}s 지점)`);
}

function stills(clipId) {
  const lib = loadLib();
  const c = lib.clips.find(x => x.id === clipId);
  if (!c) { console.error(`🔴 카탈로그에 없다: ${clipId}`); process.exit(1); }
  const src = path.join(CLIPS_ROOT, c.src);
  const dir = path.join(LIB_DIR, 'stills');
  fs.mkdirSync(dir, { recursive: true });
  for (const [tag, t] of [['a', 0], ['b', c.durSec / 2], ['c', Math.max(0, c.durSec - 0.2)]]) {
    const out = path.join(dir, `${clipId}-${tag}.png`);
    spawnSync(ffmpeg, ['-y', '-loglevel', 'error', '-ss', t.toFixed(2), '-i', src,
      '-frames:v', '1', out], { stdio: 'inherit' });
    console.log(`스틸  ${out}`);
  }
}

function list() {
  const lib = loadLib();
  if (!lib.clips.length) { console.log('카탈로그가 비어 있다 — 먼저 ingest'); return; }
  for (const c of lib.clips)
    console.log(`${c.id}  ${c.durSec}s  dist=${c.camDist}  ${c.detail}`);
  console.log(`— ${lib.clips.length}개 · ${LIB_FILE}`);
}

if (cmd === 'ingest' && argv[1]) ingest(argv[1]);
else if (cmd === 'reslice' && argv[1]) reslice(argv[1], Number(argv[2] ?? PRE_S), Number(argv[3] ?? POST_S));
else if (cmd === 'stills' && argv[1]) stills(argv[1]);
else if (cmd === 'list') list();
else {
  console.log('사용: node clips.mjs ingest <세션> | reslice <clipId> [pre] [post] | stills <clipId> | list  [--all]');
  process.exit(cmd ? 1 : 0);
}
