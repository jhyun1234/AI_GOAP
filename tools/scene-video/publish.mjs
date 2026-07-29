/* 회차 하나를 끝까지 — 음성 → mp4 → 유튜브 업로드.
   사용: node tools/scene-video/publish.mjs ep01s [옵션]

     --dry            올리지 않는다. 만들 메타데이터만 보여준다(기본 점검 경로)
     --force          이미 올린 회차도 다시 올린다
     --skip-render    mp4 가 이미 있으면 다시 뽑지 않는다(기본은 낡았으면 다시 뽑음)
     --allow-public   공개 상태로 올리는 것을 명시적으로 허용한다

   🔴 공개는 기본값이 아니다. 두 겹으로 막아 둔다.
      ① 씬 JSON 의 youtube.privacy 가 뭐라고 적혀 있든, --allow-public 없이는 private 로 간다.
      ② 애초에 API 감사를 통과하지 않은 프로젝트로 올린 영상은 YouTube 가 강제로 비공개로
         만든다. 그러니 정상 흐름은 "비공개로 올려두고 사람이 스튜디오에서 공개 전환" 이다.
      영상 공개는 되돌리기 어려운 바깥으로 나가는 행동이라 사람이 마지막 단추를 눌러야 한다.
*/
import fs from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const argv = process.argv.slice(2);
const EP = argv.find(a => !a.startsWith('--')) || 'ep01s';
const has = f => argv.includes('--' + f);
const DRY = has('dry'), FORCE = has('force');
const SKIP_RENDER = has('skip-render'), ALLOW_PUBLIC = has('allow-public');

const scenePath = path.join(ROOT, 'scenes', `${EP}.json`);
if (!fs.existsSync(scenePath)) { console.error(`scenes/${EP}.json 이 없다`); process.exit(1); }
const scene = JSON.parse(fs.readFileSync(scenePath, 'utf8'));

const STATE = path.join(ROOT, 'state', 'uploads.json');
fs.mkdirSync(path.dirname(STATE), { recursive: true });
const state = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, 'utf8')) : {};

if (state[EP] && !FORCE && !DRY) {
  console.log(`이미 올렸다: ${EP} → ${state[EP].url} (${state[EP].uploadedAt})`);
  console.log('다시 올리려면 --force');
  process.exit(0);
}

const run = (cmd, args, label) => {
  console.log(`\n▶ ${label}`);
  const r = spawnSync(cmd, args, { stdio: 'inherit', cwd: path.dirname(ROOT) });
  if (r.status !== 0) { console.error(`🔴 ${label} 실패`); process.exit(1); }
};

/* ── 1. 음성 ─────────────────────────────────────── */
const timedPath = path.join(ROOT, 'build', `${EP}.timed.json`);
const needTts = !fs.existsSync(timedPath) ||
  fs.statSync(timedPath).mtimeMs < fs.statSync(scenePath).mtimeMs;
if (needTts) run(process.execPath, [path.join(ROOT, 'tts.mjs'), EP], '음성 생성');
else console.log('음성     최신 — 건너뜀');

/* ── 2. 영상 ─────────────────────────────────────── */
const mp4 = path.join(ROOT, 'build', `${EP}.mp4`);
// 엔진이 바뀌면 그림이 바뀐다. 씬·타임라인만 보면 엔진 수정이 반영 안 된 mp4 를 올리게 된다.
const newestEngine = ['engine', 'engine/kinds'].flatMap(d => {
  const p = path.join(ROOT, d);
  return fs.existsSync(p) ? fs.readdirSync(p).filter(f => /\.(js|css|html)$/.test(f))
    .map(f => fs.statSync(path.join(p, f)).mtimeMs) : [];
}).reduce((a, b) => Math.max(a, b), 0);
const stale = !fs.existsSync(mp4) ||
  fs.statSync(mp4).mtimeMs < Math.max(fs.statSync(timedPath).mtimeMs, newestEngine);
if (stale && !SKIP_RENDER) run(process.execPath, [path.join(ROOT, 'render.mjs'), EP], '영상 렌더');
else console.log(stale ? '영상     낡았지만 --skip-render 로 건너뜀' : '영상     최신 — 건너뜀');
if (!fs.existsSync(mp4)) { console.error('🔴 mp4 가 없다'); process.exit(1); }

/* ── 3. 메타데이터 ───────────────────────────────── */
const y = scene.youtube || {};
const privacy = ALLOW_PUBLIC ? (y.privacy || 'private') : 'private';

const desc = [
  y.blurb || '',
  '',
  scene.source?.title ? `원문: ${scene.source.title}` : '',
  scene.source?.url || '',
  '',
  scene.act ? `시리즈: ${scene.act}` : '',
  'Claude Code 로 만드는 유니티 GOAP AI 개발일지입니다.',
  '',
  (y.tags || []).map(t => '#' + t.replace(/\s+/g, '')).join(' ')
].filter(l => l !== null).join('\n').trim();

const meta = {
  snippet: {
    title: (y.title || scene.hud?.title || EP).slice(0, 100),
    description: desc.slice(0, 5000),
    tags: y.tags || [],
    categoryId: y.categoryId || '28',        // 28 = 과학기술
    defaultLanguage: 'ko',
    defaultAudioLanguage: 'ko'
  },
  status: {
    privacyStatus: privacy,
    selfDeclaredMadeForKids: false,          // 필수 항목 — 빼면 거절당한다
    embeddable: true
  }
};

const sizeMb = (fs.statSync(mp4).size / 1048576).toFixed(1);
console.log('\n── 올릴 내용 ───────────────────────────');
console.log(`제목    ${meta.snippet.title}`);
console.log(`공개    ${privacy}${ALLOW_PUBLIC ? '' : '  (--allow-public 없으면 항상 private)'}`);
console.log(`파일    build/${EP}.mp4  ${sizeMb}MB`);
console.log(`태그    ${meta.snippet.tags.join(', ')}`);
console.log('설명    ' + meta.snippet.description.split('\n').join('\n        '));
console.log('────────────────────────────────────────');

if (DRY) { console.log('\n--dry 라 여기서 멈춘다. 실제로 올리려면 --dry 를 빼라.'); process.exit(0); }

/* ── 4. 업로드 ───────────────────────────────────── */
const metaFile = path.join(ROOT, 'build', `${EP}.youtube.json`);
fs.writeFileSync(metaFile, JSON.stringify(meta, null, 2));

const client = path.join(ROOT, 'upload', 'youtube-client.js');
const r = spawnSync(process.execPath, [client, 'upload', '--meta', metaFile, '--file', mp4],
  { encoding: 'utf8' });
process.stderr.write(r.stderr || '');
if (r.status !== 0) { console.error('🔴 업로드 실패'); process.exit(1); }

const out = JSON.parse(r.stdout);
console.log(`\n완료  ${out.url}`);
console.log(`      공개 상태 ${out.privacy} — 스튜디오에서 확인/전환: ${out.studio}`);

state[EP] = { id: out.id, url: out.url, privacy: out.privacy, title: out.title,
  uploadedAt: new Date().toISOString() };
fs.writeFileSync(STATE, JSON.stringify(state, null, 2));
console.log(`      기록: state/uploads.json`);
