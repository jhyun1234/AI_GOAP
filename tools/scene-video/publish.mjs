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
import os from 'os';
import path from 'path';
import { spawn, spawnSync } from 'child_process';
import readline from 'readline';
import { fileURLToPath } from 'url';
import { epDir, epScene, epBuild } from './lib-node.mjs';

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

const scenePath = epScene(EP);

/* ── 0. 대본이 없으면 만든다 ─────────────────────────
   루틴에서만 돈다. 손으로 --prepare 를 부를 때는 아래 안내만 찍고 멈춘다 —
   사람이 앉아 있는 자리에서 40분짜리 에이전트 사슬을 말없이 시작하면 안 된다.

   🔴 여기가 오래 비어 있던 자리다. 스케줄러는 이 스크립트만 불렀고 이 스크립트는
   scene.json 이 이미 있어야 돌았다. 즉 자동이었던 것은 기계 절반(음성·렌더·점검)뿐이고
   대본 절반은 사람이 Claude Code 를 열어야 했다. 그 사실을 2026-07-30 에 발견했다.

   블로그 파이프라인은 원격 routine 이라 이런 게 필요 없지만 영상은 로컬이어야 한다 —
   TTS 모델 645MB · 크롬 · ffmpeg · 폰트가 전부 이 PC 에 있다. 그래서 로컬 헤드리스로 부른다. */
const REPO = path.resolve(ROOT, '..', '..');

/* CLI 실행 파일 찾기.
   🔴 윈도에서 `claude.cmd` 를 spawnSync 로 부르면 **EINVAL** 이 난다. Node 18.20+ 가
   CVE-2024-27980 를 막으면서 shell 없이 .cmd/.bat 실행을 거부하게 됐다(실측 2026-07-30).
   그렇다고 shell:true 를 쓰면 이번엔 cmd.exe 가 `Bash(node:*)` 의 괄호와 * 를 망친다.
   → 둘 다 피한다. claude.cmd 는 결국 bin/claude.exe 를 부르므로 그 exe 를 직접 찾는다. */
function findClaude() {
  if (process.platform !== 'win32') return 'claude';
  const rel = ['node_modules', '@anthropic-ai', 'claude-code', 'bin', 'claude.exe'];
  const cands = [
    path.join(process.env.APPDATA || '', 'npm', ...rel),
    path.join(os.homedir(), 'AppData', 'Roaming', 'npm', ...rel),
    path.join(process.env.LOCALAPPDATA || '', 'npm', ...rel),
    path.join(os.homedir(), '.local', 'bin', 'claude.exe'),   // 네이티브 설치 경로
  ];
  return cands.find(p => p && fs.existsSync(p)) || 'claude.cmd';
}
const CLAUDE = findClaude();

/* 🔴 인증 선점검. 이게 없어서 실패 방식이 최악이었다.
   2026-07-30 실측: `.credentials.json` 의 accessToken 이 7/13, refreshToken 이 7/26 에 만료돼
   있었는데 **아무도 몰랐다.** 데스크톱 앱은 인증을 호스트 안에서 갱신하고 이 파일에는
   안 써 주기 때문이다(CLAUDE_CODE_SDK_HAS_HOST_AUTH_REFRESH). 앱에서 쓰는 동안은 멀쩡하고
   파일만 16일째 썩는다. 그런데 스케줄러 09:00 에는 앱이 없어서 이 파일만 읽는다.
   → 리프레시 토큰까지 죽으면 CLI 가 스스로 못 살아난다. 사람이 다시 로그인해야 한다.
   그러니 40분짜리 사슬을 시작하기 전에 여기서 먼저 확인하고, 로그만 보고도 알게 적는다. */
function preflightAuth() {
  if (process.env.ANTHROPIC_API_KEY) return;          // API 키를 쓰면 만료가 없다
  const f = path.join(os.homedir(), '.claude', '.credentials.json');
  let o;
  try { o = JSON.parse(fs.readFileSync(f, 'utf8')).claudeAiOauth; } catch { return; } // 못 읽으면 CLI 에 맡긴다
  if (!o) return;
  const now = Date.now();
  const dead = o.refreshTokenExpiresAt && o.refreshTokenExpiresAt < now;
  const stale = o.expiresAt && o.expiresAt < now;
  if (!dead && !stale) return;
  console.error(`\n🔴 claude CLI 인증이 만료됐다 — 대본을 만들 수 없다.`);
  console.error(`   accessToken  ${o.expiresAt ? new Date(o.expiresAt).toISOString() : '?'} 만료`);
  console.error(`   refreshToken ${o.refreshTokenExpiresAt ? new Date(o.refreshTokenExpiresAt).toISOString() : '?'} ${dead ? '만료 — 자동 갱신 불가' : '유효'}`);
  if (dead) {
    console.error(`\n   🔑 사람이 해야 한다: 일반 터미널(Claude Code 앱 밖)에서 claude 를 실행하고 /login.`);
    console.error(`      데스크톱 앱은 인증을 앱 안에서만 갱신하고 이 파일에는 안 쓴다 —`);
    console.error(`      앱이 멀쩡해 보여도 스케줄러는 이 파일만 읽는다.`);
    console.error(`      로그인하면 ${f} 의 수정 시각이 바뀐다. 그걸로 확인해라.`);
    process.exit(1);
  }
  console.error(`   accessToken 만 만료 — CLI 가 스스로 갱신을 시도한다. 계속 진행.`);
}

/* 🔴 씬이 안 쓰는 kind 파일을 치운다.
   작성팀은 반려를 받으면 그림을 갈아엎는데, 버린 파일은 남는다(ep02s 첫 무인 실행에서
   `blank.js`·`scoped.js` 둘이 남았다). 사람이 조율할 때는 내가 손으로 지웠지만
   (ep01s 의 `spoke.js`) **무인이면 아무도 안 지운다.**

   왜 남으면 안 되나: 이 프로젝트가 통째로 싸우고 있는 게 그림 돌려막기이고,
   다음 회차 작성팀이 폴더를 열었을 때 쓰지도 않는 그림이 놓여 있으면 그게 바로
   "여기서 골라 쓰라"는 신호가 된다. 공용 `engine/kinds/` 를 없앤 이유와 같다.

   지워도 안전한 근거: `check.mjs` 가 "씬이 쓰는 kind 파일이 전부 있는가"를 이미 보므로
   여기 걸리는 것은 **정의상 아무도 참조하지 않는 파일**이다. 설계 의도는 notes/writer.md 에 남는다. */
function pruneOrphanKinds(ep) {
  const dir = path.join(epDir(ep), 'kinds');
  if (!fs.existsSync(dir)) return;
  const used = new Set(JSON.parse(fs.readFileSync(epScene(ep), 'utf8')).shots.map(s => s.kind));
  const orphans = fs.readdirSync(dir).filter(f => f.endsWith('.js') && !used.has(f.slice(0, -3)));
  for (const f of orphans) fs.unlinkSync(path.join(dir, f));
  if (orphans.length) console.log(`정리     안 쓰는 그림 ${orphans.length}개 삭제: ${orphans.join(', ')}`);
}

/* stream-json 한 줄 → 사람이 읽을 한 줄(또는 null 이면 안 찍는다).
   목적은 "살아 있나 · 지금 어느 부서인가 · 무엇을 건드렸나" 셋뿐이다.
   로그는 격일로 쌓이므로 짧게 남긴다 — 원시 이벤트를 다 남기면 아무도 안 읽는다. */
function renderEvent(line) {
  let e; try { e = JSON.parse(line); } catch { return null; }
  const at = new Date().toTimeString().slice(0, 8);
  const cut = (s, n = 96) => {
    s = String(s ?? '').replace(/\s+/g, ' ').trim();
    return s.length > n ? s.slice(0, n - 1) + '…' : s;
  };

  if (e.type === 'system' && e.subtype === 'init') return `  ${at} 세션 시작 · ${e.model || ''}`;

  /* 사용 한도. 평소엔 status:"allowed" 라 안 찍는다 — 막혔을 때만 시끄럽게.
     무인 실행이 한밤중에 멈춰 있으면 원인이 이것일 수 있는데, 안 남기면 알 길이 없다. */
  if (e.type === 'rate_limit_event') {
    const i = e.rate_limit_info || {};
    if (!i.status || i.status === 'allowed') return null;
    const reset = i.resetsAt ? new Date(i.resetsAt * 1000).toLocaleString() : '?';
    return `  ${at} ⏳ 사용 한도 ${i.status} (${i.rateLimitType || '?'}) · 해제 ${reset}`;
  }

  if (e.type === 'assistant') {
    const out = [];
    for (const c of e.message?.content || []) {
      if (c.type === 'text' && c.text?.trim()) out.push(`  ${at} 💬 ${cut(c.text)}`);
      if (c.type !== 'tool_use') continue;
      const i = c.input || {};
      /* 🔴 처음엔 "아는 도구만 찍고 나머지는 버린다"로 짰다가 **가장 중요한 걸 놓쳤다** —
         서브에이전트를 띄우는 호출이 로그에 한 번도 안 나왔다(세 실행 전부 0건).
         `Task` 라는 이름을 가정했는데 아니었고, 모르는 이름은 조용히 버려졌다.
         그래서 "지금 어느 부서가 도는가"가 안 보였다 — 이 로깅의 존재 이유였는데.
         → 뒤집었다. **버릴 것만 명시하고 나머지는 전부 찍는다.** 모르는 도구가 와도 보인다. */
      const SKIP = ['Read', 'Glob', 'Grep', 'TodoWrite', 'NotebookRead'];
      if (SKIP.includes(c.name)) continue;          // 수십 건이라 로그만 불린다
      if (i.subagent_type || i.description) {
        out.push(`  ${at} 🤖 ${i.subagent_type || c.name} ← ${cut(i.description || i.prompt, 60)}`);
      } else if (c.name === 'Write' || c.name === 'Edit') out.push(`  ${at} 📝 ${cut(i.file_path, 80)}`);
      else if (c.name === 'Bash') out.push(`  ${at} $  ${cut(i.command, 80)}`);
      else if (c.name === 'WebFetch') out.push(`  ${at} 🌐 ${cut(i.url, 80)}`);
      else out.push(`  ${at} 🔧 ${c.name} ${cut(i.command || i.file_path || i.url || i.pattern || '', 60)}`);
    }
    return out.length ? out.join('\n') : null;
  }

  if (e.type === 'result') {
    const min = e.duration_ms ? (e.duration_ms / 60000).toFixed(1) + '분' : '?';
    const cost = e.total_cost_usd != null ? ` · $${e.total_cost_usd.toFixed(2)}` : '';
    const head = e.subtype === 'success' ? '✅ 대본 단계 끝' : `🔴 대본 단계 이상 (${e.subtype})`;
    const out = [`  ${at} ${head} · ${min} · ${e.num_turns ?? '?'}턴${cost}`];
    /* 🔴 허용 목록에 없는 도구를 에이전트가 부르면 조용히 거부되고 그대로 진행된다.
       무인이라 아무도 못 보므로 여기서 반드시 남긴다 — 검수팀이 증거를 못 만들었는데
       "통과"로 끝나는 일이 이 경로로 생긴다. 자주 뜨면 allowedTools 에 더해라. */
    if (e.permission_denials?.length) {
      /* 이름만 찍었더니 무엇을 허용 목록에 넣어야 할지 알 수 없었다(ep02s 수정 실행에서
         "Write, Bash 8건"만 남아 원인을 못 짚었다). 무엇을 하려다 막혔는지까지 남긴다. */
      const what = d => {
        const i = d.tool_input || d.input || {};
        return `${d.tool_name || d.tool || '?'}(${cut(i.command || i.file_path || i.url || '', 40)})`;
      };
      const uniq = [...new Set(e.permission_denials.map(what))];
      out.push(`  ${at} ⚠️ 거부된 도구 ${e.permission_denials.length}건`);
      for (const u of uniq.slice(0, 6)) out.push(`  ${at}    · ${u}`);
      if (uniq.length > 6) out.push(`  ${at}    · … 외 ${uniq.length - 6}종`);
    }
    out.push(`  ${at} └ ${cut(e.result, 200)}`);
    return out.join('\n');
  }
  return null;
}

async function writeScript(ep) {
  const src = sched.sources?.[ep] || {};
  const prompt = `tools/scene-video/routine-prompt.md 의 절차대로 **${ep}** 회차의 대본을 만들어라.

이건 스케줄러가 부른 무인 실행이다. 사람이 안 보고 있다.

## 하는 일
routine-prompt.md 의 4개 부서를 그 순서대로 Agent 도구로 돌린다:
scene-planner → scene-writer → scene-reviewer → scene-master.
각 단계 산출은 반드시 episodes/${ep}/notes/ 에 파일로 남긴다(서브에이전트는 무상태 세션이라
앞 단계 결과를 대화로 못 물려받는다).

## 하지 말 것
- 🔴 렌더하지 마라. tts.mjs · render.mjs · publish.mjs 를 부르지 마라.
  이 명령이 끝나면 그 스크립트가 이어서 음성·mp4·점검을 한다.
  (scene-reviewer 가 증거용으로 check.mjs 와 render.mjs --still 을 쓰는 것은 정상이다)
- 🔴 cd 를 쓰지 마라. 이 세션은 **이미 리포 루트**에서 돈다(모든 경로는 루트 기준).
  허용 목록에 cd 가 없어서 "cd X && node …" 같은 명령은 통째로 거부된다 — 실제로 그것 때문에
  셸 위치를 잃고 턴을 낭비한 실행이 있었다(2026-07-30).
- 🔴 유튜브에 올리지 마라. 업로드는 언제나 사람이 한다.
- 🔴 Docs/영상_시리즈_구성안.md 를 수정하지 마라. 읽기 전용이다.
- 다른 회차의 kinds/*.js 를 복사해 오지 마라. 그림은 회차가 소유한다.

## 멈춰야 하는 조건 (routine-prompt.md 와 같다)
기획팀 SKIP · 검수 반려 3회 · 마스터 반려 3회.
멈출 때는 episodes/${ep}/notes/ 에 사유를 남기고 scene.json 을 만들지 마라 —
scene.json 이 없으면 뒤 단계가 알아서 건너뛴다. **애매하면 통과시키지 말고 멈춰라.**

## 이 회차
원본 글 ${src.local ? `= tools/blog-automation/published/${src.local}` : `사본이 리포에 없다. url 을 WebFetch 로 받아라: ${src.url}`}
${src.note ? `참고: ${src.note}` : ''}

끝나면 만들었는지 건너뛰었는지 한 줄로 보고해라.`;

  return runAgents(prompt, '대본 (scene-* 에이전트 4개 · 오래 걸린다)');
}

/* 에이전트 세션 한 번. 대본을 처음 쓸 때와 점검 실패를 고칠 때가 이 함수를 공유한다. */
/* 서버측 일시 오류는 스스로 다시 시도한다.
   🔴 실측(2026-07-30): ep02s 재작업이 **연속 두 번** 서버 오류로 죽었다(500 → 529 Overloaded).
   무인이면 그때마다 그 회차가 그냥 없어진다 — 격일 스케줄이니 다음 기회는 이틀 뒤다.
   프롬프트가 "notes/ 에 있으면 읽고 이어서 해라"고 지시하므로 재시도가 처음부터 다시 하는 건 아니다.
   ⚠️ 인증 실패·한도 초과는 재시도해도 같으므로 여기서 걸러 낸다. 기다려서 풀릴 것만 다시 한다. */
const TRANSIENT = /\b5\d\d\b|internal server|overloaded|timeout|ECONNRESET|socket hang up/i;

/* 기다리는 간격.
   🔴 처음엔 60초·120초로 잡았다가 실측에서 부족했다 — 2026-07-30 에 529 Overloaded 가
   **10분 넘게 이어져** 세 번 다 1턴 만에 죽었다(각 $0.00 이라 시도 자체는 싸다).
   장애는 분 단위가 아니라 십분 단위로 이어진다. 무인 실행이 여기서 포기하면 격일 스케줄상
   그 회차를 **이틀 놓친다** — 기다리는 편이 압도적으로 싸다. 총 인내 약 38분.
   손으로 부를 때 오래 기다리는 게 싫으면 Ctrl-C 로 끊고 나중에 다시 부르면 된다. */
const RETRY_WAIT_MIN = [3, 10, 25];

async function runAgents(prompt, label, tries = RETRY_WAIT_MIN.length + 1) {
  for (let n = 1; ; n++) {
    const r = await runAgentsOnce(prompt, n > 1 ? `${label} · 재시도 ${n - 1}` : label);
    if (r.ok) return r;
    if (n >= tries || !TRANSIENT.test(r.result)) return r;
    const min = RETRY_WAIT_MIN[n - 1];
    console.error(`   ⏳ 서버측 일시 오류다. ${min}분 뒤 다시 시도한다 (${n}/${tries - 1}).`);
    await new Promise(res => setTimeout(res, min * 60000));
  }
}

async function runAgentsOnce(prompt, label) {
  preflightAuth();
  console.log(`\n▶ ${label}`);
  const child = spawn(CLAUDE, [
    /* 이 세션은 에이전트 4개를 순서대로 부르고, 반려를 작성팀에 되돌리고, 산출을 정리한다.
       작성·검수·마스터는 각자 정의서에서 이미 claude-opus-5 를 쓴다(여기서 안 정한다).

       🔴 처음엔 "조율만 하니 sonnet 이면 된다"고 뒀다가 **사용자 지시로 opus 로 올렸다.**
       근거: 이 자리는 기계적이지 않다. 사람이 이 자리를 맡았던 회차(ep00s·ep01s)에서
       고아 kind 파일 삭제 · 문서와 코드가 어긋난 것 · HUD 표기 불일치를 여기서 잡았다.
       무인이면 이걸 놓쳐도 아무도 안 본다.

       ⚠️ 별칭(`sonnet`/`opus`)은 CLI 버전에 따라 다른 세대로 풀린다 — 실측에서 `sonnet` 이
       claude-sonnet-4-6 으로 갔다. 그래서 별칭 말고 id 를 박는다. */
    '--model', 'claude-opus-5',
    '--permission-mode', 'acceptEdits',
    /* 🔴 기본 출력(text)은 **끝에 한 번에** 나온다. 40분짜리 실행에서 로그가 40분간 비어 있고
       살아 있는지 죽었는지 알 수 없다. stream-json 으로 바꿔 진행을 흘린다.
       (stream-json 은 --verbose 를 요구한다)
       ⚠️ 원시 NDJSON 을 그대로 로그에 부으면 더 못 읽는다 — 아래에서 파싱해 사람 줄로 줄인다. */
    '--output-format', 'stream-json', '--verbose',
    '-p',
    /* 에이전트들이 쓰는 것만. 넓게 열지 않는다 — 무인이라 사람이 막을 자리가 없다.
       🔴 처음엔 node·git 만 열었더니 첫 명령이 `ls` 라 막혔다(ep02s 수정 실행, 거부 8건).
       진행은 됐지만 에이전트가 사라진 도구를 우회하느라 턴을 쓴다. 읽기만 하는 셋을 더 열었다.
       파일을 지우거나 옮기는 명령(rm·mv)은 **일부러 안 연다** — 고아 그림 정리는
       pruneOrphanKinds 가 하고, 무인 세션이 파일을 지울 일은 없어야 한다. */
    '--allowedTools', 'Read', 'Write', 'Edit', 'Glob', 'Grep', 'Task', 'WebFetch',
    'Bash(node:*)', 'Bash(git:*)', 'Bash(ls:*)', 'Bash(mkdir:*)', 'Bash(find:*)'
  ], {
    stdio: ['pipe', 'pipe', 'inherit'], cwd: REPO,
    /* 부모 세션의 흔적을 지우고 부른다. 이 스크립트를 Claude Code 안에서 손으로 실행하면
       자식이 CLAUDECODE=1 · 세션 id · ANTHROPIC_BASE_URL 을 물려받아 "중첩 실행"으로 보인다.
       스케줄러에서는 애초에 없는 변수들이므로, 지워야 두 경로가 같은 조건이 된다.
       (ANTHROPIC_API_KEY 는 남긴다 — 그건 사용자가 일부러 넣은 인증 수단이다) */
    env: Object.fromEntries(Object.entries(process.env)
      .filter(([k]) => !/^CLAUDE(CODE)?($|_)/.test(k) && k !== 'ANTHROPIC_BASE_URL'))
  });

  const spawnFailed = await new Promise(res => {
    child.on('error', e => res(e));
    child.stdin.on('error', () => {});          // 자식이 먼저 죽으면 EPIPE — 위 error 가 받는다
    child.stdin.end(prompt, () => res(null));
  });
  if (spawnFailed) {
    console.error(`🔴 claude 를 실행하지 못했다: ${spawnFailed.message}`);
    console.error(`   CLI 가 PATH 에 있나? (npm i -g @anthropic-ai/claude-code)`);
    process.exit(1);
  }

  let lastResult = '';
  for await (const line of readline.createInterface({ input: child.stdout })) {
    const s = renderEvent(line);
    if (s) console.log(s);
    // 마지막 result 이벤트의 본문을 남긴다 — 실패 원인을 골라서 안내하는 데 쓴다.
    try { const e = JSON.parse(line); if (e.type === 'result') lastResult = String(e.result ?? ''); } catch {}
  }
  const r = { status: await new Promise(res => child.on('close', res)) };

  if (r.status === 0) return { ok: true, result: lastResult };

  {
    console.error(`🔴 대본 단계 실패 (exit ${r.status})`);
    /* 🔴 원인을 골라서 말한다. 처음엔 무조건 "인증 만료"를 지목했는데, 실제로 API 500 으로
       죽은 실행에서 엉뚱한 안내가 나왔다(2026-07-30). 틀린 진단은 없는 진단보다 나쁘다. */
    const t = lastResult.toLowerCase();
    if (/401|authenticate|oauth/.test(t)) {
      console.error(`   인증 문제다. 일반 터미널(Claude Code 앱 밖)에서 claude 를 실행해 /login 해라.`);
    } else if (/\b5\d\d\b|internal server|overloaded|timeout/.test(t)) {
      console.error(`   서버측 일시 오류로 보인다(우리 코드 문제가 아니다). 잠시 뒤 그대로 다시 돌려라.`);
      console.error(`   status.claude.com 에서 장애 여부를 볼 수 있다.`);
    } else if (/rate.?limit|한도/.test(t)) {
      console.error(`   사용 한도로 보인다. 위 ⏳ 줄의 해제 시각 뒤에 다시 돌려라.`);
    } else {
      console.error(`   위 출력의 마지막 줄이 원인이다. 인증(401)·서버오류(5xx)·한도 중 어느 것도`);
      console.error(`   아니면 씬이나 그림 쪽 문제일 수 있으니 notes/ 를 봐라.`);
    }
    // 여기서 죽이지 않는다. 일시 오류면 runAgents 가 기다렸다 다시 부른다.
    return { ok: false, result: lastResult };
  }
}

/* 사람이 지시한 재작업. 점검이 잡은 게 아니라 사람이 "이걸 고쳐라"고 준 경우다.
   `--redo "<지시>"` 로 부른다. 점검 되돌림(fixScript)과 같은 세션 구조를 쓴다. */
async function redoScript(ep, order) {
  return runAgents(`**${ep}** 회차를 사람이 다시 손보라고 했다. 아래 지시대로 고쳐라.

## 지시
${order}

## 하는 일
\`tools/scene-video/routine-prompt.md\` 의 검수팀 → 작성팀 → 마스터 순서를 따른다.
산출은 \`episodes/${ep}/notes/\` 에 남기되 기존 파일을 덮지 말고 \`-redo\` 를 붙여라.

## 하지 말 것
- 🔴 렌더하지 마라(\`--still\` 로 눈으로 보는 것은 정상). 끝나면 이 스크립트가 렌더한다.
- 🔴 cd 를 쓰지 마라. 이 세션은 **이미 리포 루트**에서 돈다(모든 경로는 루트 기준).
  허용 목록에 cd 가 없어서 "cd X && node …" 같은 명령은 통째로 거부된다 — 실제로 그것 때문에
  셸 위치를 잃고 턴을 낭비한 실행이 있었다(2026-07-30).
- 🔴 유튜브에 올리지 마라.
- 🔴 원문에 없는 문자열을 화면에 넣지 마라. 늘리다가 지어내는 것이 가장 흔한 사고다.
  늘릴 재료는 **원문에서만** 가져온다. 없으면 억지로 늘리지 말고 사유를 남겨라.

끝나면 무엇을 어떻게 바꿨는지, 길이가 얼마가 됐는지 한 줄로 보고해라.`, `사람 지시 재작업 (${ep})`);
}

/* 점검이 잡은 것을 에이전트에게 되돌린다.
   `routine-prompt.md` 는 처음부터 "점검이 실패하면 검수팀 단계로 되돌아간다"고 적어 뒀는데
   그 길이 구현돼 있지 않았다 — 실패하면 그냥 exit 1 이었다. 무인이면 거기서 끝이다.
   ep02s 첫 완주가 잘린 글자를 달고 나온 뒤 이 자리를 채웠다. */
async function fixScript(ep, report) {
  return runAgents(`**${ep}** 회차가 렌더까지 갔는데 \`check.mjs\` 가 잡았다. 고쳐라.

이건 스케줄러가 부른 무인 실행이다. 사람이 안 보고 있다.

## 점검 출력 (그대로)
\`\`\`
${report.trim()}
\`\`\`

## 하는 일
\`tools/scene-video/routine-prompt.md\` 의 검수팀 → 작성팀 → 마스터 순서를 따르되,
**대본 전체를 다시 쓰지 마라.** 이미 마스터 승인을 받은 회차다. 걸린 항목만 고친다.
Agent 도구로 \`scene-reviewer\` 에게 원인을 짚게 하고, \`scene-writer\` 가 고치고,
\`scene-master\` 가 승인한다. 각 단계 산출은 \`episodes/${ep}/notes/\` 에 파일로 남긴다
(기존 \`review.md\`·\`verdict.md\` 를 덮지 말고 \`-fix\` 를 붙여라).

## 판단이 필요한 자리
**"좌우 가장자리 잘림"은 두 가지가 섞여 걸린다.** 스틸로 눈으로 보고 갈라라
(\`node tools/scene-video/render.mjs ${ep} --still <초>\`):
- **글자가 잘렸다** → 그림을 고친다. 폭을 재서 넣거나 기준 크기를 줄인다.
  가운데 정렬 요소가 쓸 수 있는 폭은 캔버스 폭이 아니라 **가까운 가장자리까지 거리의 두 배**다.
- **일부러 화면 밖으로 내보내는 연출이다**(카드가 흘러 나가는 캐러셀 등) → 고치지 말고
  그 샷의 \`checks.edge\` 에 **사유**를 적는다. \`palette\` 와 \`edge\` 두 키만 실제로 읽힌다.
  ⚠️ 편해서 선언하지 마라. 선언은 "이건 의도다"라는 기록이고 다음 회차가 인용한다.

## 하지 말 것
- 🔴 렌더하지 마라(\`--still\` 로 눈으로 보는 것은 정상). 이 명령이 끝나면 다시 렌더한다.
- 🔴 cd 를 쓰지 마라. 이 세션은 **이미 리포 루트**에서 돈다(모든 경로는 루트 기준).
  허용 목록에 cd 가 없어서 "cd X && node …" 같은 명령은 통째로 거부된다 — 실제로 그것 때문에
  셸 위치를 잃고 턴을 낭비한 실행이 있었다(2026-07-30).
- 🔴 유튜브에 올리지 마라.
- 🔴 원문에 없는 문자열을 화면에 넣지 마라. 고치다가 새로 지어내는 게 가장 흔한 사고다.
- 대본(\`lines\`)은 건드리지 마라 — 음성이 다시 구워지고 승인이 무효가 된다.
  꼭 필요하면 사유를 \`notes/\` 에 남겨라.

끝나면 무엇을 어떻게 고쳤는지 한 줄로 보고해라.`, `점검 반려 수정 (${ep})`);
}

if (!fs.existsSync(scenePath) && ROUTINE && !DRY) {
  const w = await writeScript(EP);
  if (!w.ok) { console.error(`
🔴 대본 단계를 끝내지 못했다 — 이 회차는 만들지 않는다.`); process.exit(1); }
  pruneOrphanKinds(EP);
}

/* --redo "<지시>" — 이미 만든 회차를 사람 지시로 다시 손본다.
   여기서 음성·렌더를 부르지 않는다. 아래 평소 경로가 mtime 으로 낡음을 판정해
   scene.json·kinds 가 새로우면 알아서 다시 굽는다 — 두 번 렌더하지 않으려고 그 판정에 맡긴다.
   ⚠️ 회차 id 를 반드시 함께 준다: publish.mjs ep02s --routine --force --redo "지시" */
const REDO = argv.includes('--redo') ? argv[argv.indexOf('--redo') + 1] : null;
if (REDO && fs.existsSync(scenePath) && !DRY) {
  const d = await redoScript(EP, REDO);
  if (!d.ok) { console.error(`
🔴 재작업을 끝내지 못했다 — 씬은 그대로 두고 멈춘다.`); process.exit(1); }
  pruneOrphanKinds(EP);
}

if (!fs.existsSync(scenePath)) {
  if (ROUTINE) {
    // 에이전트가 정상 종료했는데 씬이 없다 = 기획팀 SKIP 또는 반려 한도 초과.
    // 사고가 아니라 설계된 결말이므로 0 으로 끝낸다. 사유는 notes/ 에 있다.
    console.log(`대본 없음 — 만들지 않고 끝났다. 사유는 episodes/${EP}/notes/ 에 있다.`);
    process.exit(0);
  }
  console.error(`episodes/${EP}/scene.json 이 없다 — 대본이 아직 없는 회차다.`);
  console.error(`대본은 scene-* 에이전트가 쓴다: tools/scene-video/routine-prompt.md`);
  console.error(`(기획 → 작성 → 검수 → 마스터 승인 → 그다음 이 스크립트)`);
  console.error(`루틴(--routine)은 이 단계까지 자동으로 한다.`);
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
const timedPath = epBuild(EP, 'timed.json');
const needTts = !fs.existsSync(timedPath) ||
  fs.statSync(timedPath).mtimeMs < fs.statSync(scenePath).mtimeMs;
if (needTts) run(process.execPath, [path.join(ROOT, 'tts.mjs'), EP], '음성 생성');
else console.log('음성     최신 — 건너뜀');

/* ── 2. 영상 ─────────────────────────────────────── */
const mp4 = epBuild(EP, 'video.mp4');
// 엔진이 바뀌면 그림이 바뀐다. 씬·타임라인만 보면 엔진 수정이 반영 안 된 mp4 를 올리게 된다.
const newestEngine = ['engine', `episodes/${EP}/kinds`].flatMap(d => {
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
   실패하면 규칙을 어긴 영상을 올리지 않는다. 다만 루틴에서는 그냥 죽지 않고
   **에이전트에게 되돌려 한 번 고치게 한다** — 무인이면 여기서 멈추는 게 곧 "그 회차 없음"이다.
   한 번만이다. 두 번 연속 실패는 지시가 잘못됐다는 신호이고, 그건 사람이 봐야 한다
   (routine-prompt.md 의 "같은 항목 두 번 연속 실패" 정지 조건). */
function runCheck() {
  console.log('\n▶ 가이드 점검');
  const c = spawnSync(process.execPath, [path.join(ROOT, 'check.mjs'), EP],
    { cwd: path.dirname(ROOT), encoding: 'utf8' });
  const out = (c.stdout || '') + (c.stderr || '');
  process.stdout.write(out);
  return { ok: c.status === 0, out };
}

if (!SKIP_CHECK) {
  let { ok, out } = runCheck();

  if (!ok && ROUTINE && !DRY) {
    const f = await fixScript(EP, out);
    if (!f.ok) { console.error(`
🔴 점검 반려 수정을 끝내지 못했다.`); process.exit(1); }
    pruneOrphanKinds(EP);
    // 그림·씬이 바뀌었으니 음성부터 다시. 음성은 내용 해시 캐시라 안 바뀐 줄은 그대로 쓴다.
    run(process.execPath, [path.join(ROOT, 'tts.mjs'), EP], '음성 재생성');
    run(process.execPath, [path.join(ROOT, 'render.mjs'), EP], '영상 재렌더');
    ({ ok, out } = runCheck());
  }

  if (!ok) {
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
]
  // 빈 항목(예: 원문 URL 이 없는 회차)이 빈 줄로 남는다 — 연속 빈 줄을 하나로 접는다
  .join('\n').replace(/\n{3,}/g, '\n\n').trim();

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
console.log(`파일    ${path.relative(path.dirname(ROOT), mp4)}  ${sizeMb}MB`);
console.log(`태그    ${meta.snippet.tags.join(', ')}`);
console.log('설명    ' + meta.snippet.description.split('\n').join('\n        '));
console.log('────────────────────────────────────────');

if (DRY) { console.log('\n--dry 라 여기서 멈춘다. 실제로 올리려면 --dry 를 빼라.'); process.exit(0); }

/* ── 4. 준비 (기본 운영 경로) ────────────────────
   API 업로드를 안 쓰는 이유: 감사를 통과하지 않은 프로젝트로 올린 영상은 YouTube 가
   비공개로 '잠그고' 스튜디오에서도 못 푼다. 다시 올리는 수밖에 없어서, 자동 업로드가
   오히려 일을 늘린다. 그래서 붙여넣을 것까지만 만들어 두고 마지막 한 걸음은 사람이 한다. */
if (PREPARE) {
  const txt = epBuild(EP, 'upload.txt');
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
  if (process.platform === 'win32') spawnSync('explorer', [epBuild(EP)], { stdio: 'ignore' });
  process.exit(0);
}

/* ── 4. 업로드 ───────────────────────────────────── */
const metaFile = epBuild(EP, 'youtube.json');
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
