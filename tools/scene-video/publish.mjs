/* 회차 하나를 끝까지 — 음성 → mp4 → (가이드 점검) → 업로드 준비 / 업로드.
   사용: node tools/scene-video/publish.mjs ep01s [옵션]

     --prepare        ⭐ 기본 운영 경로. mp4 와 붙여넣을 메타데이터까지 만들고 폴더를 연다.
                      업로드는 사람이 스튜디오에서 한다 — API 업로드는 감사 전까지
                      영상이 비공개로 '잠겨' 스튜디오에서도 못 푸는 탓에 쓰지 않는다.
     --routine        스케줄러가 부르는 진입점. 2일 간격 게이트를 확인하고 --prepare 를 한다.
     --dry            올리지 않는다. 만들 메타데이터만 보여준다
     --force          이미 처리한 회차도 다시 한다
     --skip-render    mp4 가 이미 있으면 다시 뽑지 않는다(기본은 낡았으면 다시 뽑음)
     --skip-check     가이드 점검을 건너뛴다(권장하지 않음)
     --allow-public   API 업로드 시 공개 상태를 명시적으로 허용한다

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
const has = f => argv.includes('--' + f);
const DRY = has('dry'), FORCE = has('force');
const SKIP_RENDER = has('skip-render'), ALLOW_PUBLIC = has('allow-public');
const SKIP_CHECK = has('skip-check');
const ROUTINE = has('routine');
const PREPARE = has('prepare') || ROUTINE;    // 루틴은 언제나 준비까지만 한다

const STATE = path.join(ROOT, 'state', 'uploads.json');
fs.mkdirSync(path.dirname(STATE), { recursive: true });
const state = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, 'utf8')) : {};

/* ── 회차 고르기 ─────────────────────────────────
   루틴은 회차 이름을 모른다. state/schedule.json 의 순서에서 아직 안 만든 첫 회차를 집는다.
   그 파일이 없으면 만들어 둔다 — 회차가 늘 때 사람이 한 줄 추가하는 자리다. */
const SCHED = path.join(ROOT, 'state', 'schedule.json');
if (!fs.existsSync(SCHED)) fs.writeFileSync(SCHED, JSON.stringify({
  _: '영상 순서. Docs/영상_시리즈_구성안.md 의 재생 순서를 따른다. 씬 JSON 이 있는 것만 만들 수 있다.',
  everyDays: 2,
  order: ['ep01s']
}, null, 2));
const sched = JSON.parse(fs.readFileSync(SCHED, 'utf8'));

let EP = argv.find(a => !a.startsWith('--'));
if (!EP) {
  EP = sched.order.find(id => !state[id]) || sched.order.at(-1);
  if (ROUTINE) console.log(`회차     ${EP} (schedule.json 에서 자동 선택)`);
}

/* ── 2일 간격 게이트 ─────────────────────────────
   크론은 홀수일에 돌지만 달이 바뀌면 31일 다음이 1일이라 이틀이 아니라 하루 만에 돈다.
   달력에 기대지 말고 "마지막으로 만든 날로부터 며칠 지났나"를 직접 본다.
   실행이 한 번 밀려도 다음 날 알아서 따라잡는다. */
if (ROUTINE && !FORCE) {
  const last = Object.values(state).map(v => v.preparedAt || v.uploadedAt).filter(Boolean).sort().at(-1);
  if (last) {
    const days = (Date.now() - new Date(last).getTime()) / 86400000;
    if (days < (sched.everyDays ?? 2) - 0.5) {
      console.log(`아직 이르다 — 마지막 제작 ${days.toFixed(1)}일 전 (간격 ${sched.everyDays ?? 2}일). 종료.`);
      process.exit(0);
    }
  }
  if (state[EP]) { console.log(`만들 회차가 없다 — ${EP} 까지 전부 처리됨. 씬을 추가해라.`); process.exit(0); }
}

const scenePath = path.join(ROOT, 'scenes', `${EP}.json`);
if (!fs.existsSync(scenePath)) {
  console.error(`scenes/${EP}.json 이 없다 — 대본이 아직 없는 회차다.`);
  console.error(`대본은 scene-* 에이전트가 쓴다: tools/scene-video/routine-prompt.md`);
  console.error(`(기획 → 작성 → 검수 → 마스터 승인 → 그다음 이 스크립트)`);
  process.exit(1);
}
const scene = JSON.parse(fs.readFileSync(scenePath, 'utf8'));

if (state[EP] && !FORCE && !DRY) {
  const s = state[EP];
  console.log(`이미 처리했다: ${EP} (${s.preparedAt || s.uploadedAt})${s.url ? ` → ${s.url}` : ''}`);
  console.log('다시 하려면 --force');
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

/* ── 2.5 가이드 점검 ─────────────────────────────
   실패하면 여기서 멈춘다. 규칙을 어긴 영상을 올릴 바에 안 만드는 게 낫다. */
if (!SKIP_CHECK) {
  console.log('\n▶ 가이드 점검');
  const c = spawnSync(process.execPath, [path.join(ROOT, 'check.mjs'), EP],
    { stdio: 'inherit', cwd: path.dirname(ROOT) });
  if (c.status !== 0) {
    console.error('\n🔴 가이드 점검 실패 — 올리지 않는다. 고치거나, 사유를 씬 JSON 의 checks 에 적어라.');
    process.exit(1);
  }
}

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
if (!PREPARE) console.log(`공개    ${privacy}${ALLOW_PUBLIC ? '' : '  (--allow-public 없으면 항상 private)'}`);
console.log(`파일    build/${EP}.mp4  ${sizeMb}MB`);
console.log(`태그    ${meta.snippet.tags.join(', ')}`);
console.log('설명    ' + meta.snippet.description.split('\n').join('\n        '));
console.log('────────────────────────────────────────');

if (DRY) { console.log('\n--dry 라 여기서 멈춘다. 실제로 올리려면 --dry 를 빼라.'); process.exit(0); }

/* ── 4. 준비 (기본 운영 경로) ────────────────────
   API 업로드를 안 쓰는 이유: 감사를 통과하지 않은 프로젝트로 올린 영상은 YouTube 가
   비공개로 '잠그고' 스튜디오에서도 못 푼다. 다시 올리는 수밖에 없어서, 자동 업로드가
   오히려 일을 늘린다. 그래서 붙여넣을 것까지만 만들어 두고 마지막 한 걸음은 사람이 한다. */
if (PREPARE) {
  const txt = path.join(ROOT, 'build', `${EP}.upload.txt`);
  fs.writeFileSync(txt, [
    '── 제목 ────────────────────────────────', meta.snippet.title, '',
    '── 설명 ────────────────────────────────', meta.snippet.description, '',
    '── 태그 (쉼표로 붙여넣기) ───────────────', meta.snippet.tags.join(', '), '',
    '── 설정 ────────────────────────────────',
    '카테고리: 과학기술',
    '아동용 아님',
    '언어: 한국어',
    `파일: ${path.relative(path.dirname(ROOT), mp4)}  (${sizeMb}MB)`
  ].join('\n'), 'utf8');

  state[EP] = { ...(state[EP] || {}), preparedAt: new Date().toISOString(), title: meta.snippet.title };
  fs.writeFileSync(STATE, JSON.stringify(state, null, 2));

  console.log(`\n준비 완료`);
  console.log(`  영상   ${path.relative(path.dirname(ROOT), mp4)}`);
  console.log(`  붙여넣기 ${path.relative(path.dirname(ROOT), txt)}`);
  console.log(`\n  스튜디오(https://studio.youtube.com)에 mp4 를 끌어다 놓고`);
  console.log(`  위 txt 내용을 붙여넣으면 끝이다.`);
  // 폴더를 열어 준다 — 스튜디오로 끌어다 놓기 좋게
  if (process.platform === 'win32') spawnSync('explorer', [path.join(ROOT, 'build')], { stdio: 'ignore' });
  process.exit(0);
}

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
