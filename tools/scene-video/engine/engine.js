/* scene-video 엔진
   ─────────────────────────────────────────────────────────────
   설계 규약 3개 (전부 프레임 단위 mp4 추출을 위한 것)

   1. seek(t) 는 순수 함수다. 어느 시각으로 뛰어도 같은 그림이 나온다.
      CSS transition / animation / setTimeout 으로 화면을 바꾸지 않는다.
   2. 🔴 그림(kind)은 회차가 소유한다 — episodes/<ep>/kinds/ 에서 불러온다.
      전에는 engine/kinds/ 공용 라이브러리였고, 그래서 회차마다 같은 그림이 돌아왔다.
      구조가 돌려막기를 기본값으로 만들고 있었다. 공유하는 것은 lib.js(붓)뿐이다.
   3. 타임라인은 사람이 쓰지 않는다. 지금은 글자 수로 임시 계산하고,
      TTS 가 붙으면 실측 길이가 episodes/<ep>/build/timed.json 에서 들어온다.
*/

const PROVISIONAL_CPS = 5.6;   // 한국어 TTS 대략 속도(글자/초) — 실측 전 임시값
const LINE_TAIL = 0.5;         // 한 줄 끝난 뒤 숨 (초)
const SHOT_TAIL = 0.35;        // 샷 끝 여운 (초)
const CAP_FADE = 0.18;         // 자막 페이드 (초)
const ENTER = 0.26;            // 샷 진입 모션 (초)

const $ = id => document.getElementById(id);
const qs = new URLSearchParams(location.search);
const EP = qs.get('ep') || 'ep01';
/* 언어. ko 는 회차 폴더의 정본을 그대로 읽고, 그 외는 Node 가 구워 둔 병합본을 읽는다
   (병합을 여기서도 하면 Node 쪽과 갈라진다 — lib-node.mjs 의 bakeScene 이 유일한 구현이다).
   🔴 그림(kinds/)은 언제나 회차 폴더에서 온다. 언어별로 가르지 않는다. */
const LANG = qs.get('lang') || 'ko';
const EPD = `../episodes/${EP}`;
const LDIR = LANG === 'ko' ? `${EPD}/build` : `${EPD}/build/${LANG}`;

let scene = null, shots = [], lines = [], TOTAL = 0, kinds = {};
let clipLayer = null, clipCanvas = null;   // 게임 클립 레이어 (롱폼 W5) — buildDom 이 만든다
const failedKinds = [];   // 로드에 실패한 그림 — check.mjs 가 읽는다

/* ── 로드 ─────────────────────────────────────────── */
async function boot() {
  // 추출 모드 — 컨트롤을 숨기고 무대를 기준 크기(392px)로 못박는다.
  // 창 폭에 따라 무대가 줄면 글자 크기 대비 화면 비율이 달라져 다른 그림이 나온다.
  if (qs.has('render')) document.body.classList.add('render');

  scene = await (await fetch(
    LANG === 'ko' ? `${EPD}/scene.json` : `${LDIR}/scene.json`)).json();

  // 가로(wide) 프로필 — CSS 파일 자체를 갈아 끼운다. style.css 는 불변(ADR-LF-1).
  // 스타일이 붙기 전에 그리면 그 프레임만 다른 그림이 되므로 로드 완료를 기다린다.
  if (scene.format === 'wide') {
    await new Promise((res, rej) => {
      const link = document.querySelector('link[rel="stylesheet"]');
      link.addEventListener('load', res, { once: true });
      link.addEventListener('error',
        () => rej(new Error('wide.css 로드 실패')), { once: true });
      link.href = 'wide.css';
    });
  }

  // timed 본이 있으면 실측 타임라인을 쓴다 (없으면 임시 계산)
  let timed = null;
  try {
    const r = await fetch(`${LDIR}/timed.json`);
    if (r.ok) timed = await r.json();
  } catch { /* 없으면 그만 */ }

  // 클립 샷(롱폼 W5)은 kind 가 없을 수 있다 — 그림 대신 게임 녹화가 화면을 진다
  const names = [...new Set(scene.shots.filter(s => s.kind).map(s => s.kind))];
  for (const n of names) {
    try { kinds[n] = (await import(`../episodes/${EP}/kinds/${n}.js`)).default; }
    catch (e) {
      /* 🔴 조용히 빈 화면으로 넘어가면 점검이 통과해 버린다. 실제로 그랬다 —
         kinds 를 회차 폴더로 옮기며 lib.js 경로가 깨졌는데, 모든 샷이 빈 화면이 된 채
         check.mjs 가 "정적 구간 0.0초"로 통과시켰다. 실패는 기록으로 남아야 한다. */
      console.warn(`[kind] ${n} 로드 실패`, e);
      failedKinds.push(n); kinds[n] = null;
    }
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
  await loadFonts();
  await loadAssets();
  await loadClips();
  prime();
  seek(0);
  $('hint').textContent =
    `${EP} · ${shots.length}샷 · 자막 ${lines.length}줄 · ${(TOTAL / 1000).toFixed(1)}초` +
    (timed ? ' · TTS 실측' : ' · 임시 타이밍(글자수 추정)');
  window.__ready = true;
}

/* ── 폰트 ──────────────────────────────────────────
   🔴 캔버스는 폰트를 '쓰기만' 해서는 @font-face 로드를 유발하지 않는다.
   kind 들은 전부 ctx.font 로만 쓰므로, 여기서 명시적으로 요청하지 않으면 초반 프레임이
   폴백으로 그려지고 폰트가 도착한 뒤부터 글자가 바뀐다. document.fonts.ready 를
   기다리는 것만으로는 부족하다 — 아직 아무도 요청하지 않았으니 기다릴 로드가 없어
   즉시 resolve 된다. 크기는 로드 대상 선정과 무관하고 family/weight/style 만 본다. */
const FONT_FACES = [
  '700 16px Pretendard', '800 16px Pretendard', '900 16px Pretendard',
  '700 16px SceneMono'
];

/** 폰트가 실제로 적용되는지 — check() 는 폴백으로도 true 를 주므로 폭을 재서 본다 */
function applies(spec, probe = '4,096 무해 ABC') {
  const c = document.createElement('canvas').getContext('2d');
  const [head] = [spec.replace(/\s+\S+$/, '')];        // 'weight size' 부분
  const fam = spec.slice(head.length + 1);
  c.font = `${head} "__NoSuchFont__", sans-serif`;
  const base = c.measureText(probe).width;
  c.font = `${head} ${fam}, "__NoSuchFont__", sans-serif`;
  return Math.abs(c.measureText(probe).width - base) > 0.5;
}

async function loadFonts() {
  if (!document.fonts) return;
  await Promise.all(FONT_FACES.map(f => document.fonts.load(f).catch(() => { })));
  await document.fonts.ready;

  const missing = FONT_FACES.filter(f => !applies(f));
  if (missing.length) console.warn('[font] 적용 안 됨 — 폴백으로 그려진다:', missing);

  /* 한자는 Pretendard 에 없다(한글·라틴 전용). term kind 가 無解 를 그리므로
     여기가 시스템 폰트에 남은 유일한 의존이다. 렌더 머신에 한자 폰트가 없으면 두부가 된다.

     🔴 이건 폭으로 판정할 수 없다 — 한자 글리프도 두부 상자도 똑같이 1em 이라
     '無' 와 '￿' 가 40px 폰트에서 둘 다 정확히 40.00px 로 나온다(실측). 처음 이렇게
     짰다가 멀쩡한 글자에 경고가 떴다. 실제로 그려서 잉크 양을 비교해야 한다. */
  if (!renders('無')) console.warn('[font] 한자(無解)를 그릴 폰트가 없다 — term 샷이 두부로 나온다');
}

/** 글자가 진짜 그려지는가 — 두부 상자와 잉크 양이 다른지로 본다 */
function renders(ch, fam = 'Pretendard, "Malgun Gothic", serif') {
  const ink = txt => {
    const cv = document.createElement('canvas'); cv.width = cv.height = 48;
    const x = cv.getContext('2d');
    x.fillStyle = '#fff'; x.font = `900 40px ${fam}`;
    x.textBaseline = 'middle'; x.textAlign = 'center';
    x.fillText(txt, 24, 24);
    const d = x.getImageData(0, 0, 48, 48).data;
    let n = 0; for (let i = 0; i < d.length; i += 4) if (d[i + 3] > 40) n++;
    return n;
  };
  return Math.abs(ink(ch) - ink('￿')) > 20;
}

/* ── 이미지 ────────────────────────────────────────
   씬 JSON 의 assets 를 미리 받아 둔다. 폰트와 같은 이유다 — 디코딩이 끝나기 전에
   그리면 그 프레임만 빈 자리로 추출된다. decode() 까지 기다린 뒤에야 prime() 이 돈다. */
const images = {};
async function loadAssets() {
  for (const [name, src] of Object.entries(scene.assets || {})) {
    const img = new Image();
    img.src = src;
    try { await img.decode(); images[name] = img; }
    catch { console.warn(`[asset] ${name} 을 못 읽었다: ${src}`); }
  }
}

/* ── 게임 클립 (롱폼 W5) ────────────────────────────
   shot.clip = { id, in, out } — clips.mjs 카탈로그의 클립을 무대 전면에 깐다.
   폰트·이미지와 같은 이유로 loadeddata 까지 기다린 뒤에야 준비를 선언한다.
   실패는 조용히 넘기지 않는다 — failedKinds 채널에 실어 check.mjs 가 잡는다
   (그림 로드 실패와 같은 문이다: 빈 화면이 가장 조용하다). */
async function loadClips() {
  for (const s of shots) {
    if (!s.clip || !s.clipVideo) continue;
    const v = s.clipVideo;
    await new Promise(res => {
      v.addEventListener('loadeddata', res, { once: true });
      v.addEventListener('error', () => {
        console.warn(`[clip] ${s.clip.id} 로드 실패`);
        failedKinds.push(`clip:${s.clip.id}`);
        res();
      }, { once: true });
      v.src = `/clips/${encodeURIComponent(s.clip.id)}.mp4`;
    });
  }
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
    // 샷 시작 기준 상대 시각 — kind 가 "몇 번째 자막이 말해질 때" 를 알 수 있게 한다
    const rel = ls.map(l => ({ t: (l.t - ls[0].t) / 1000, dur: l.dur / 1000 }));
    shots.push({ ...s, i: si, t: ls[0].t, dur, lines: ls, rel });
    t = ls[0].t + dur;
  });
  TOTAL = t;
  window.TOTAL = TOTAL;
}

/* ── DOM ──────────────────────────────────────────── */
/* 아웃트로 카드가 뜨는 구간.
   🔴 2600 → 3000 (2026-08-08, ADR-V25-10 — 사용자 "아웃트로 3초 정도로 늘려도 된다").
   2600 이던 이유(카드가 덮는 구간의 정적이 3초 검사를 넘던 문제)는 2026-08-04 에
   check.mjs 가 덮인 구간을 정적 검사에서 빼면서 이미 사라졌다 — 지금은 안전하다.
   check.mjs 의 같은 이름 상수와 맞춰라. */
const OUTRO_MS = 3000;

function buildDom() {
  $('hudTitle').textContent = scene.hud.title;

  /* 🔴 상단에서 AI 한 줄을 뺀다(2026-08-03). 세로 화면 최상단은 훅이 쓸 자리인데
     영상 내내 안 바뀌는 텍스트가 점유하고 있었다.
     🔴 2026-08-08 개정: 이 줄(hud.outro)의 내용이 **블로그 URL → 브랜딩 문구**로 바뀌었다
     (사용자: "블로그 주소는 그닥 중요하지 않는 거 같아"). 옛 주석("어느 지점에서 이탈해도
     URL 은 남는다")은 그 판정으로 뒤집혔다 — URL 은 유튜브 설명란에만 남는다.
     엔진은 그대로 scene.hud.outro 를 찍을 뿐이고 문안은 회차 JSON 이 진다(§2-C). */
  $('hudAi').hidden = true;
  $('hudOutro').textContent = scene.hud.outro;

  /* 아웃트로 카드 채우기. `scene.outro` 가 없으면(기존 회차) AI 문구만 뜬다 —
     필드를 안 쓰는 회차가 깨지지 않아야 한다. */
  const o = scene.outro || {};
  /* 훅은 카드가 맡는다(2026-08-04). outro.hook 으로 회차별 재정의를 열어 두고,
     없으면 scene.hook 을 그대로 쓴다 — 기존 회차가 필드 없이도 동작해야 한다. */
  const ocHook = o.hook ?? scene.hook ?? '';
  $('ocHook').textContent = ocHook;
  $('ocHook').hidden = !ocHook;
  $('ocSrc').textContent = [o.source && `「${o.source}」`, o.part].filter(Boolean).join(' · ');
  $('ocSrc').hidden = !o.source && !o.part;
  const sibs = $('ocSibs'); sibs.innerHTML = '';
  for (const s of o.siblings || []) {
    const li = document.createElement('li');
    li.textContent = typeof s === 'string' ? s : (s.label || '');
    if (s.me) li.className = 'me';
    sibs.appendChild(li);
  }
  sibs.hidden = !(o.siblings || []).length;
  /* 다음 편 (2026-08-06, 25초 전환 W2·ADR-V25-2). 음성 예고를 1초 이하로 줄이거나
     아예 빼는 대신, 안 뺀 쪽을 이 카드가 글자로 진다.
     🔴 없으면 안 그린다 — 옛 회차는 이 필드가 없고 그대로 돌아야 한다.
     🔑 `siblings` 와 다르다. siblings 는 형제 편 목록이고 이건 **다음 회차 하나**다. */
  const ocNext = String(o.next ?? '').trim();
  $('ocNext').textContent = ocNext ? `다음 편 · ${ocNext}` : '';
  $('ocNext').hidden = !ocNext;
  $('ocAi').textContent = scene.hud.aiHook || '';

  const vis = $('vis'); vis.innerHTML = '';
  shots.forEach(s => {
    const el = document.createElement('div');
    el.className = 'shot'; el.dataset.id = s.id;
    vis.appendChild(el);
    s.el = el;
    kinds[s.kind]?.build?.(el, { spec: s.spec || {}, shot: s, scene });
  });

  /* ── 클립 레이어 (롱폼 W5) ─────────────────────────
     게임 녹화 컷은 .vis 상자(약 2.6:1)에 안 넣는다 — 컨테인이면 좌우 기둥,
     커버면 세로 1/3 이 잘린다. 무대가 wide 에서 정확히 16:9 = 클립 비율이므로
     **무대 전면**에 깐다. z 는 카드(3)·자막(4)·HUD(5) 아래 — 셋 다 클립 위에 선다.
     스타일이 인라인인 이유: 이 레이어는 프로필 무관 동작이라 CSS 파일(프로필별
     교체 대상)에 걸면 두 곳에 적는 셈이 된다. */
  const stage = document.querySelector('.stage');
  clipLayer = document.createElement('div');
  Object.assign(clipLayer.style,
    { position: 'absolute', inset: '0', zIndex: '2', background: '#000' });
  clipLayer.hidden = true;
  stage.appendChild(clipLayer);
  /* 비디오 요소는 화면에 안 세운다 — 디코더로만 쓰고, 프레임은 seeked 뒤에
     우리 캔버스에 drawImage 한다. <video> 를 직접 보이면 컴포지터가 언제
     그 프레임을 칠했는지 캡처가 보장 못 해 결정성이 깨진다
     (실측 2026-08-09: 2회 렌더에서 클립 구간 147프레임 불일치). */
  clipCanvas = document.createElement('canvas');
  clipCanvas.width = 1920; clipCanvas.height = 1080;
  Object.assign(clipCanvas.style, { width: '100%', height: '100%', display: 'block' });
  clipLayer.appendChild(clipCanvas);
  shots.forEach(s => {
    if (!s.clip) return;
    const v = document.createElement('video');
    v.muted = true; v.preload = 'auto'; v.playsInline = true;
    v.style.display = 'none';
    clipLayer.appendChild(v);
    s.clipVideo = v;
  });
  /* 배지 — 사실 정직성(ADR-LF-7): 클립은 과거 사건의 재현이 아니라 지금 빌드의
     관측이다. 엔진이 자동으로 달고 대본이 끌 수 없다 (레이어와 함께만 보인다). */
  const badge = document.createElement('div');
  badge.textContent = '지금 빌드 화면';
  Object.assign(badge.style, {
    // 우하단 — 우상단은 챕터 칩 자리다 (실측 2026-08-09: top 10.5% 가 칩에 덮였다).
    // bottom 22.5% = 자막영역 경계(21.3%) 바로 위, 콘텐츠 안전영역 안.
    position: 'absolute', right: '24px', bottom: '22.5%',
    font: '800 11px Pretendard', letterSpacing: '.06em', color: '#FFFFFF',
    background: 'rgba(0,0,0,.55)', padding: '4px 9px', borderRadius: '3px',
  });
  clipLayer.appendChild(badge);
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

  // 아웃트로 카드 — seek 이 유일한 그리기 경로이므로 여기서만 켠다.
  // 이렇게 해야 스크럽으로 뒤로 감아도 상태가 맞는다(렌더는 seek 을 임의 순서로 부른다).
  $('outroCard').hidden = !(t >= TOTAL - OUTRO_MS);

  // 활성 샷 — 마지막 샷은 끝까지 유지
  let act = shots[0];
  for (const s of shots) if (t >= s.t) act = s;

  /* 클립 샷 (롱폼 W5) — seek 이 비디오 프레임 정합까지 책임진다.
     렌더는 프레임마다 seek 을 await 하므로(awaitPromise) currentTime 이 실제로
     그 프레임에 도착한 뒤 캡처된다 — play() 로 흘리면 결정성이 깨진다 (명세 ⚠️). */
  let clipWait = null;
  if (clipLayer) {
    const clipOn = !!(act.clip && act.clipVideo && act.clipVideo.readyState >= 2);
    clipLayer.hidden = !clipOn;
    if (clipOn) {
      const v = act.clipVideo;
      const tIn = act.clip.in ?? 0;
      const tOut = act.clip.out ?? v.duration ?? Infinity;
      /* 프레임 경계(n/30) 정시를 요청하면 반올림이 실행마다 다른 프레임에 앉는다 —
         반 프레임(1/60) 안쪽을 요청해 항상 같은 프레임 한가운데 떨어뜨린다 (결정성). */
      const want = Math.min(tOut - 1 / 120, tIn + (t - act.t) / 1000 + 1 / 60);
      const draw = () => clipCanvas.getContext('2d')
        .drawImage(v, 0, 0, clipCanvas.width, clipCanvas.height);
      if (Math.abs(v.currentTime - want) > 1 / 240) {
        clipWait = new Promise(res => {
          const done = () => { draw(); res(); };
          v.addEventListener('seeked', done, { once: true });
          setTimeout(done, 500);   // seeked 가 유실돼도 렌더가 영원히 멈추지는 않게
        });
        v.currentTime = want;
      } else draw();
    }
  }

  for (const s of shots) {
    const on = s === act;
    s.el.classList.toggle('on', on);
    if (!on) continue;
    const p = Math.min(1, (t - s.t) / s.dur);
    const ts = (t - s.t) / 1000;

    /* 샷 진입 — 컷이 아니라 짧게 밀어 넣는다. 하드 컷만 이어 붙이면 샷이 바뀐 걸
       놓치는 프레임이 생긴다. 가이드 한도(translateY ±30px) 안이고, ts 의 함수라
       seek 의 순수성은 그대로다 — 어느 시각으로 뛰어도 같은 그림.

       🔴 여기에 scale() 을 걸면 안 된다. kind 의 캔버스는 fitCanvas 가
       getBoundingClientRect() 로 크기를 잡는데, 이 값은 transform 이 적용된 뒤의
       크기다. 즉 진입 0.26초 동안 백킹스토어 크기가 매 프레임 달라지고 그림이
       흔들린다(실측: 30fps 2,924프레임 중 74프레임 불일치). translate 는 박스
       크기를 바꾸지 않으므로 안전하다. */
    const ek = Math.min(1, ts / ENTER), e = ek * ek * (3 - 2 * ek);
    s.el.style.opacity = 0.12 + 0.88 * e;
    s.el.style.transform = `translateY(${((1 - e) * 12).toFixed(2)}px)`;

    /* cue(i) — i번째 자막에 맞물린 0~1 진행률.
       가이드(SKILL.md): 모든 reveal 은 그 내용을 말하는 자막 시작 ±20프레임 안에서 터져야 한다.
       lead 만큼 미리 시작해 말과 그림이 동시에 도착하게 한다(0.15s = 30fps 기준 4.5프레임).
       span 은 그 자막 길이의 몇 할을 쓸지 — 0.7 이면 자막이 끝나기 전에 그림이 완성된다. */
    const cue = (i, lead = 0.15, span = 0.7) => {
      const l = s.rel[Math.min(i, s.rel.length - 1)];
      if (!l) return 0;
      const a = l.t - lead, b = l.t + l.dur * span;
      return Math.max(0, Math.min(1, (ts - a) / (b - a || 1e-6)));
    };
    /** i번째 자막이 시작된 뒤 흐른 초. 아직이면 음수. */
    const since = i => (s.rel[i] ? ts - s.rel[i].t : -1);

    kinds[s.kind]?.draw?.(s.el, {
      spec: s.spec || {}, shot: s, scene, images,
      p, t: ts, dur: s.dur / 1000, abs: t / 1000,
      lines: s.rel, cue, since, nLines: s.rel.length
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

  // 클립 샷이면 비디오 seek 완료 약속을 돌려준다 — render/check 가 await 한다 (W5)
  return clipWait;
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

/* ── 추출용 창구 (render.mjs 가 쓴다) ────────────── */
window.seek = ms => seek(ms);
/** 지금 화면을 지고 있는 클립 비디오 (없으면 null) — check.mjs 의 정적 검사용 (W5) */
window.__activeClipVideo = () => {
  if (!clipLayer || clipLayer.hidden) return null;
  const act = shots.find(s => s.el?.classList.contains('on'));
  return act?.clipVideo ?? null;
};
window.prime = prime;
window.__ready = false;
window.__failedKinds = () => failedKinds;

/* 🔴 소리 트랙을 만들 때 쓰는 자리 시간표.
   build/<ep>.full.wav 를 그대로 붙이면 안 된다 — 그건 줄과 호흡만 이어 붙인 것이고,
   엔진 타임라인은 샷이 끝날 때마다 SHOT_TAIL(0.35초) 여운을 더 넣는다.
   12샷이면 4.2초가 밀린다(실측: 통짜 105.5s vs 타임라인 109.7s).
   그러니 시간표는 엔진이 불러 주고, render.mjs 는 그 자리에 줄별 wav 를 놓는다. */
window.__timeline = () => ({
  totalMs: TOTAL,
  lines: lines.map(l => ({ t: l.t, dur: l.dur, file: l.file || null }))
});

boot();
