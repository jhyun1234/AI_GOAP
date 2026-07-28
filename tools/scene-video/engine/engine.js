/* scene-video 엔진
   ─────────────────────────────────────────────────────────────
   설계 규약 3개 (전부 프레임 단위 mp4 추출을 위한 것)

   1. seek(t) 는 순수 함수다. 어느 시각으로 뛰어도 같은 그림이 나온다.
      CSS transition / animation / setTimeout 으로 화면을 바꾸지 않는다.
   2. kind 추가 = kinds/ 에 파일 1개. 다른 파일은 손대지 않는다.
      씬 JSON 이 쓰는 kind 이름을 보고 동적으로 import 한다.
   3. 타임라인은 사람이 쓰지 않는다. 지금은 글자 수로 임시 계산하고,
      TTS 가 붙으면 실측 길이가 build/<ep>.timed.json 에서 들어온다.
*/

const PROVISIONAL_CPS = 5.6;   // 한국어 TTS 대략 속도(글자/초) — 실측 전 임시값
const LINE_TAIL = 0.5;         // 한 줄 끝난 뒤 숨 (초)
const SHOT_TAIL = 0.35;        // 샷 끝 여운 (초)
const CAP_FADE = 0.18;         // 자막 페이드 (초)

const $ = id => document.getElementById(id);
const qs = new URLSearchParams(location.search);
const EP = qs.get('ep') || 'ep01';

let scene = null, shots = [], lines = [], TOTAL = 0, kinds = {};

/* ── 로드 ─────────────────────────────────────────── */
async function boot() {
  scene = await (await fetch(`../scenes/${EP}.json`)).json();

  // timed 본이 있으면 실측 타임라인을 쓴다 (없으면 임시 계산)
  let timed = null;
  try {
    const r = await fetch(`../build/${EP}.timed.json`);
    if (r.ok) timed = await r.json();
  } catch { /* 없으면 그만 */ }

  const names = [...new Set(scene.shots.map(s => s.kind))];
  for (const n of names) {
    try { kinds[n] = (await import(`./kinds/${n}.js`)).default; }
    catch (e) { console.warn(`[kind] ${n} 없음 — 빈 화면으로 대체`, e); kinds[n] = null; }
  }

  buildTimeline(timed);
  buildDom();
  wire();

  // ── 초벌 렌더 ──────────────────────────────────────────
  // 캔버스의 '첫 draw' 는 폰트가 확정되기 전이라 이후 draw 와 그림이 미세하게
  // 다르다. 프레임 추출에서는 각 샷의 첫 프레임만 틀어지는 형태로 나타난다.
  // 그래서 준비 완료(__ready)를 선언하기 전에 모든 샷을 한 번씩 그려 둔다.
  // 한 지점만 그리면 조건 분기 안에서만 쓰이는 폰트가 초벌에서 빠진다
  // (예: 예산 소진 후에만 찍히는 'NoSolution'). 그래서 샷마다 여러 지점을 훑는다.
  if (document.fonts?.ready) await document.fonts.ready;
  prime();
  seek(0);
  $('hint').textContent =
    `${EP} · ${shots.length}샷 · 자막 ${lines.length}줄 · ${(TOTAL / 1000).toFixed(1)}초` +
    (timed ? ' · TTS 실측' : ' · 임시 타이밍(글자수 추정)');
  window.__ready = true;
}

/* ── 타임라인 ─────────────────────────────────────── */
function provisionalDur(say) {
  return Math.max(1700, Math.round((say.length / PROVISIONAL_CPS + LINE_TAIL) * 1000));
}

function buildTimeline(timed) {
  const durOf = (si, li, say) =>
    timed?.shots?.[si]?.lines?.[li]?.dur ?? provisionalDur(say);

  let t = 0; shots = []; lines = [];
  scene.shots.forEach((s, si) => {
    const ls = s.lines.map((l, li) => {
      // dur = 말하는 시간, pauseAfter = 말이 끝난 뒤의 침묵.
      // 침묵 동안 자막은 그대로 머문다 — 사람이 말을 멈춘 것이지 자막이 끝난 게 아니다.
      // 간격을 균일하게 두면 그 규칙성 자체가 기계처럼 들린다.
      const dur = durOf(si, li, l.say || l.text) + (l.pauseAfter ?? 0);
      const rec = { ...l, t: t, dur, shot: si, file: timed?.shots?.[si]?.lines?.[li]?.file };
      t += dur; lines.push(rec); return rec;
    });
    const dur = (ls.at(-1).t + ls.at(-1).dur) - ls[0].t + SHOT_TAIL * 1000;
    shots.push({ ...s, i: si, t: ls[0].t, dur, lines: ls });
    t = ls[0].t + dur;
  });
  TOTAL = t;
  window.TOTAL = TOTAL;
}

/* ── DOM ──────────────────────────────────────────── */
function buildDom() {
  $('hudTitle').textContent = scene.hud.title;
  $('hudAiText').textContent = scene.hud.aiHook;
  $('hudOutro').textContent = scene.hud.outro;

  const vis = $('vis'); vis.innerHTML = '';
  shots.forEach(s => {
    const el = document.createElement('div');
    el.className = 'shot'; el.dataset.id = s.id;
    vis.appendChild(el);
    s.el = el;
    kinds[s.kind]?.build?.(el, { spec: s.spec || {}, shot: s, scene });
  });
}

/** 초벌 렌더. 캔버스는 '한 번도 그린 적 없는 경로'를 처음 그릴 때 결과가
 *  미세하게 달라진다(폰트 확정 등). 추출 전에 전 구간을 한 번 훑어 두면
 *  이후 seek 은 어느 순서로 뛰어도 같은 그림을 낸다. */
function prime(step = 400) {
  for (let ms = 0; ms <= TOTAL; ms += step) seek(ms);
  for (const s of shots) { seek(s.t); seek(s.t + s.dur - 1); }

  // 캔버스는 '처음 읽어내는' 순간 백킹스토어가 한 번 바뀐다. 그래서 초벌은
  // 그리기만 해선 부족하고, 실제 추출과 똑같이 읽어내면서 한 바퀴 돌아야 한다.
  // 이 사본을 거치지 않으면 각 캔버스 샷의 '첫 프레임 하나'만 나머지와 어긋난다.
  for (let ms = 0; ms <= TOTAL; ms += step) {
    seek(ms);
    const cv = document.querySelector('.shot.on canvas');
    if (cv && cv.width) cv.getContext('2d').getImageData(0, 0, cv.width, cv.height);
  }
}

/* ── seek : 유일한 그리기 경로 ────────────────────── */
function seek(t) {
  t = Math.max(0, Math.min(TOTAL, t));
  cur = t;

  $('prog').style.width = (t / TOTAL * 100) + '%';
  $('time').textContent = (t / 1000).toFixed(1) + 's';
  $('scrub').value = Math.round(t / TOTAL * 1000);

  // 활성 샷 — 마지막 샷은 끝까지 유지
  let act = shots[0];
  for (const s of shots) if (t >= s.t) act = s;

  for (const s of shots) {
    const on = s === act;
    s.el.classList.toggle('on', on);
    if (!on) continue;
    const p = Math.min(1, (t - s.t) / s.dur);
    kinds[s.kind]?.draw?.(s.el, {
      spec: s.spec || {}, shot: s, scene,
      p, t: (t - s.t) / 1000, dur: s.dur / 1000, abs: t / 1000
    });
  }

  const chip = $('chip');
  chip.textContent = act.chapter;
  chip.classList.toggle('ok', ['해결', '증명', '결과'].includes(act.chapter));

  // 자막 — 시간의 함수로만 결정한다
  let line = lines[0], li = 0;
  lines.forEach((l, i) => { if (t >= l.t) { line = l; li = i; } });
  syncAudio(li, (t - line.t) / 1000);
  const cap = $('cap');
  cap.textContent = line.text;
  const age = (t - line.t) / 1000;
  const k = Math.min(1, age / CAP_FADE);
  cap.style.opacity = k;
  // 위에서 내려와 자리를 잡는다. 아래에서 올라오게 하면 페이드 동안 자막 바닥이
  // 앵커(1550)보다 내려가 쇼츠 가림 영역 쪽으로 밀린다 — 바닥 고정이 깨진다.
  cap.style.transform = `translateY(${(k - 1) * 5}px)`;
}

/* ── 나레이션 (미리보기 전용) ─────────────────────
   최종 mp4 는 ffmpeg 가 오디오를 따로 붙인다. 여기서 소리를 내는 이유는 하나,
   "대본이 사람처럼 들리는가"를 화면과 같이 놓고 판정하기 위해서다.
   재생 중일 때만 소리가 난다 — prime() 은 seek 을 수천 번 부르므로 그때 울리면 안 된다. */
let audioOn = true, playingLine = -1;

function stopAudio() {
  for (const l of lines) if (l.el) { l.el.pause(); l.el.currentTime = 0; }
  playingLine = -1;
}
function syncAudio(idx, offsetSec) {
  if (!audioOn || !raf) return;
  if (idx === playingLine) return;
  stopAudio();
  playingLine = idx;
  const l = lines[idx];
  if (!l?.file) return;
  if (!l.el) { l.el = new Audio('../' + l.file); l.el.preload = 'auto'; }
  l.el.currentTime = Math.min(offsetSec, 0.2);   // 줄 중간부터 재생하면 어색하니 앞부분만 허용
  l.el.play().catch(() => { /* 사용자 제스처 전이면 조용히 실패 */ });
}

/* ── 컨트롤 (미리보기 전용, 렌더에는 관여하지 않음) ─ */
let cur = 0, raf = null, last = 0, rate = 1;

function tick(ts) {
  if (!last) last = ts;
  const next = cur + (ts - last) * rate; last = ts;
  if (next >= TOTAL) { seek(TOTAL); stop(); return; }
  seek(next); raf = requestAnimationFrame(tick);
}
function stop() { cancelAnimationFrame(raf); raf = null; last = 0; stopAudio(); $('play').textContent = '재생'; }

function wire() {
  $('play').onclick = () => {
    if (raf) return stop();
    if (cur >= TOTAL) cur = 0;
    $('play').textContent = '정지'; last = 0; playingLine = -1;
    raf = requestAnimationFrame(tick);
  };
  $('speed').onclick = e => {
    rate = rate === 1 ? 2 : rate === 2 ? 0.5 : 1;
    e.target.textContent = '×' + rate;
    stopAudio();                                  // 배속에서 음성은 맞지 않는다
    audioOn = rate === 1;
    $('voice').textContent = audioOn ? '나레이션 켬' : '배속 — 음성 꺼짐';
  };
  $('voice').onclick = e => {
    audioOn = !audioOn; if (!audioOn) stopAudio(); else playingLine = -1;
    e.target.textContent = audioOn ? '나레이션 켬' : '나레이션 끔';
  };
  $('scrub').oninput = e => { if (raf) stop(); seek(e.target.value / 1000 * TOTAL); };
  addEventListener('resize', () => seek(cur));
}

/* Playwright 프레임 추출용 창구 */
window.seek = ms => seek(ms);
window.prime = prime;
window.__ready = false;

boot();
