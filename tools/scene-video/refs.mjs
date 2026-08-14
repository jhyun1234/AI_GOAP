/* 레퍼런스 파이프라인 — 영상을 만들 때 볼 것을 **찾아서 받고 재고 붙여 놓는다.**

   사용
     node tools/scene-video/refs.mjs plan ep16s-1           # ⭐ 회차마다 이것부터
     node tools/scene-video/refs.mjs find "2d 액션 애니메이션 원화" --n 3 --ep ep16s-1
     node tools/scene-video/refs.mjs add  https://youtu.be/<id> --ep ep16s-1 --tag walk
     node tools/scene-video/refs.mjs measure ep16s-1        # 우리 회차를 같은 잣대로
     node tools/scene-video/refs.mjs list

   ⭐ plan 이 회차마다 하는 일 (2026-08-12 신설 · 사용자 지시「필요한 3D 모델링·움직임
      레퍼런스를 자동으로 찾아서 대조하고 적용」)
        ① scene.json 의 `reads` 를 읽어 **이 회차가 요구한 동작**을 뽑는다
           (낱말표는 engine/motions.mjs 의 `MOTIONS[tag].reads` — 어휘집과 한 자리다)
        ② `kinds/*.js` 를 읽어 **그림이 실제로 쓴 동작**을 뽑는다
        ③ ①에 있는데 ②에 없으면 **어긋남**이다. check.mjs 가 여기서 멈춘다 —
           ep16s-1 의 훅이 정확히 이 상태였다(reads「걸어와」· 다리는 정지).
        ④ 그 동작의 레퍼런스가 refs/ 에 없으면 `MOTIONS[tag].q` 로 **자동으로 받아** 둔다.
        ⑤ 우리 회차를 같은 함수로 재서 편집 리듬 레퍼런스와 나란히 notes/refs.md 에 남긴다.
      결과는 `episodes/<ep>/notes/refs.json`(기계용) + `notes/refs.md`(사람용).

   왜 만들었나 (2026-08-12)
   레퍼런스 두 편을 손으로 받아 프레임을 뜯어 재 봤더니, 그 30분이 그때까지의 어떤 수정보다
   많은 것을 바꿨다 — 「on 2s」·「흑백 반전 임팩트」·「집중선」이 전부 거기서 나왔고,
   **우리 영상이 레퍼런스의 1/5 만 움직인다**는 사실도 같은 잣대로 재서야 보였다.
   그 30분이 매번 손으로 반복될 이유가 없다.

   🔑 이 도구가 지키는 것 하나: **눈대중을 안 남긴다.** 「역동적이다」 같은 말 대신
      움직임 평균·촬영 박자·컷 간격·임팩트 횟수를 숫자로 남기고, 우리 회차를 **같은 함수**로
      재서 나란히 놓는다. 비교가 안 되는 측정은 감상이지 근거가 아니다.
   🔴 받은 영상은 `refs/` 에 두고 리포에 안 담는다(.gitignore). 남는 것은 **측정치와
      컨택트 시트**뿐이다 — 남의 영상을 리포에 넣지 않으면서 근거는 남기는 선이다.
   🔴 결정성과 무관하다(렌더 경로가 아니다). 여기서 나온 수치는 사람이 읽고 판단한다. */

import fs from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';
import { findFfmpeg } from './lib-node.mjs';
/* 어휘집을 여기서 다시 적지 않는다 — 태그·검색어·낱말이 갈라지면 대조가 성립을 안 한다.
   engine/ 은 브라우저 모듈이지만 모듈 최상위에서 DOM 을 안 읽어 node 에서도 그대로 읽힌다. */
import { MOTIONS } from './engine/motions.mjs';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const REFS = path.join(ROOT, 'refs');
const FF = findFfmpeg();
if (!FF) throw new Error('ffmpeg 을 못 찾았다');

/** yt-dlp — winget 으로 깔면 PATH 에 없다. ffmpeg 과 같은 방식으로 훑는다. */
function findYtDlp() {
  const env = process.env.SCENE_YTDLP;
  if (env && fs.existsSync(env)) return env;
  const base = path.join(process.env.LOCALAPPDATA || '', 'Microsoft/WinGet/Packages');
  if (fs.existsSync(base)) {
    for (const pkg of fs.readdirSync(base).filter(d => /yt-dlp/i.test(d))) {
      const p = path.join(base, pkg, 'yt-dlp.exe');
      if (fs.existsSync(p)) return p;
    }
  }
  for (const d of (process.env.PATH || '').split(path.delimiter)) {
    for (const n of ['yt-dlp.exe', 'yt-dlp']) {
      const p = path.join(d, n);
      if (d && fs.existsSync(p)) return p;
    }
  }
  return null;
}
const YTD = findYtDlp();

const run = (bin, args, opts = {}) =>
  spawnSync(bin, args, { encoding: 'utf8', maxBuffer: 1 << 28, ...opts });

const slugOf = url => (url.match(/[?&]v=([\w-]{6,})|youtu\.be\/([\w-]{6,})/) || [])
  .slice(1).find(Boolean) || url.replace(/\W+/g, '').slice(-11);

/* ── 측정 ───────────────────────────────────────────
   프레임을 128×72 회색으로 뽑아 프레임 간 평균 밝기차를 낸다.
   이 넷이 '움직임'을 말할 때 실제로 쓰이는 값이다(레퍼런스 분석에서 확인). */
const W = 128, H = 72, N = W * H;

/* 한 픽셀이 「바뀌었다」고 볼 최소 계단(0~255). 압축 잡음 위, 실제 변화 아래. */
const CHANGED = 8;

/* 🔴 모든 영상을 **30 으로 다시 뽑아** 잰다(2026-08-14). 원본 fps 로 재면 60fps 영상은
   이웃 프레임이 더 닮아 움직임·면적이 낮게 나온다 — 24·30·60 을 섞어 놓고 밴드를
   만들면 그 밴드는 「프레임률이 낮은 영상일수록 활발하다」고 말하는 물건이 된다.
   대가: 촬영 박자(stepHist)가 60fps 원본에서 해상도를 잃는다. 우리 산출물이 30fps 라
   그쪽에 맞춘다. */
const SAMPLE_FPS = 30;

/* ── 컷 검출 ────────────────────────────────────────
   🔴 2026-08-14 손으로 만든 검출기를 **고치지 않고 버렸다.**
   옛 식: `밝기차 > max(0.09, 중앙값×8)`. 두 방향으로 동시에 틀린다 —
     · 상대 문턱(중앙값×8)이라 화면이 바쁠수록 문턱이 올라가 컷을 못 잡는다
       (Challacade 340초 → 0컷).
     · 절대 바닥(0.09)이 납작한 색면 영상엔 너무 높다
       (우리 ep16s-1 은 여섯 샷인데 3컷).
   즉 이 값으로 밴드를 굳혔으면 **근거 없는 숫자를 게이트에 박을 뻔했다.**

   대신 ffmpeg 의 `scene` 점수를 쓴다(이미 의존하는 도구가 하는 일을 다시 안 짓는다).
   🔑 프레임률을 먼저 30 으로 맞춘다 — 60fps 원본은 이웃 프레임이 더 닮아 점수가
      낮게 나온다. 이 정규화를 빼면 fps 가 다른 영상끼리 비교가 성립을 안 한다.

   문턱 0.10 · 불응기 0.4초는 **정답을 아는 판**으로 골랐다:
   ep16s-1 = 여섯 샷 + 꼬리 카드 = 컷 6회. 아래 selftest 가 이걸 지킨다. */
const SCENE_THR = 0.10, CUT_REFRACT_S = 0.4;

function cutTimes(file, fps = 30, thr = SCENE_THR) {
  const r = run(FF, ['-v', 'error', '-i', file,
    '-vf', `fps=${fps},scale=320:-2,select='gt(scene,${thr})',metadata=print:file=-`,
    '-f', 'null', '-']);
  const t = [...(r.stdout || '').matchAll(/pts_time:([\d.]+)/g)].map(m => +m[1]);
  /* 불응기 — 한 번의 전환이 두 프레임에 걸치면 옛 식은 컷 2 로 셌다. */
  const out = [];
  for (const v of t) if (!out.length || v - out.at(-1) >= CUT_REFRACT_S) out.push(v);
  return out;
}

function measure(file, fps = SAMPLE_FPS, from = 0, to = 1e9) {
  const r = run(FF, ['-v', 'error', '-i', file,
    '-vf', `fps=${fps},scale=${W}:${H},format=gray`,
    '-f', 'rawvideo', '-pix_fmt', 'gray', '-'], { encoding: 'buffer' });
  const b = r.stdout;
  const F = [];
  for (let i = 0; i + N <= b.length; i += N) F.push(b.subarray(i, i + N));
  const cut = F.slice(Math.round(from * fps), Math.round(to * fps));
  if (cut.length < 3) return null;

  const d = [], lum = [], area = [];
  for (let i = 1; i < cut.length; i++) {
    let s = 0, ch = 0;
    for (let j = 0; j < N; j++) {
      const dv = Math.abs(cut[i][j] - cut[i - 1][j]);
      s += dv;
      if (dv > CHANGED) ch++;
    }
    d.push(s / N / 255);
    /* 변화 **면적** — 평균 밝기차와 다른 것을 잰다. 작은 것이 크게 움직여도 평균은
       오르지만 면적은 안 오른다. ep16s-1 의 「인물이 화면 높이의 1/12」이 정확히
       그 자리였다: 움직임 평균은 레퍼런스와 비슷한데 화면 대부분이 안 변한다. */
    area.push(ch / N);
  }
  for (const f of cut) { let s = 0; for (let j = 0; j < N; j++) s += f[j]; lum.push(s / N / 255); }

  const HOLD = 0.004;                       // 압축 잡음 위, 실제 그림 교체 아래
  const steps = []; let runLen = 1;
  for (const v of d) { if (v < HOLD) runLen++; else { steps.push(runLen); runLen = 1; } }
  steps.push(runLen);                        // 마지막 구간도 센다
  const hist = {};
  for (const s of steps) hist[Math.min(s, 9)] = (hist[Math.min(s, 9)] || 0) + 1;

  const sorted = [...d].sort((a, b) => a - b);
  const cutsAt = cutTimes(file, 30);
  const gaps = cutsAt.slice(1).map((v, i) => v - cutsAt[i]).sort((a, b) => a - b);
  const areaSorted = [...area].sort((a, b) => a - b);

  const holds = []; let hs = -1;
  for (let i = 0; i < d.length; i++) {
    if (d[i] < HOLD) { if (hs < 0) hs = i; }
    else { if (hs >= 0 && i - hs >= fps * 0.25) holds.push(i - hs); hs = -1; }
  }
  let flashes = 0;
  for (let i = 1; i < lum.length - 1; i++)
    if (Math.abs(lum[i] - (lum[i - 1] + lum[i + 1]) / 2) > 0.10) flashes++;

  const tot = steps.length || 1;
  const avgStep = steps.reduce((a, b) => a + b, 0) / tot;
  return {
    fps, frames: cut.length,
    move: +(d.reduce((a, b) => a + b, 0) / d.length * 1000).toFixed(2),
    p95: +(sorted[Math.floor(sorted.length * 0.95)] * 1000).toFixed(2),
    max: +(sorted.at(-1) * 1000).toFixed(2),
    stepHist: hist, swaps: tot, perSec: avgStep > 0 ? +(fps / avgStep).toFixed(1) : 0,
    /* 면적은 **중앙값**으로 낸다 — 평균은 컷 한 번(전 화면 교체)에 끌려간다. */
    areaMed: +((areaSorted[areaSorted.length >> 1] || 0) * 100).toFixed(1),
    areaP90: +((areaSorted[Math.floor(areaSorted.length * 0.9)] || 0) * 100).toFixed(1),
    cuts: cutsAt.length, cutGapMed: +((gaps[gaps.length >> 1] || 0).toFixed(2)),
    holds: holds.length, holdMax: +((Math.max(0, ...holds) / fps).toFixed(2)),
    flashes,
  };
}

const bar = m => {
  const tot = m.swaps || 1;
  return Object.entries(m.stepHist).map(([k, c]) => {
    const pct = c / tot * 100;
    return `     ${k === '9' ? '9+' : k}프레임 (${(+k / m.fps * 1000).toFixed(0)}ms) ` +
      `${'█'.repeat(Math.round(pct / 3)).padEnd(20)} ${pct.toFixed(1)}%`;
  }).join('\n');
};

const report = (name, m) => !m ? `${name}: 측정 실패` : [
  `── ${name}  (${m.frames}프레임 @ ${m.fps}fps)`,
  `   움직임   평균 ${m.move} · 상위5% ${m.p95} · 최대 ${m.max}  (×1000)`,
  `   변화 면적 중앙 ${m.areaMed ?? '-'}% · 상위10% ${m.areaP90 ?? '-'}%  (화면의 몇 %가 매 프레임 바뀌나)`,
  `   촬영 박자 — 같은 그림을 몇 프레임 무는가 (교체 ${m.swaps}회 · 실효 초당 ${m.perSec}장)`,
  bar(m),
  `   컷 ${m.cuts}회 · 중앙 간격 ${m.cutGapMed}초`,
  `   0.25초+ 홀드 ${m.holds}회 · 최장 ${m.holdMax}초`,
  `   임팩트(밝기 튐) ${m.flashes}회`,
].join('\n');

/* ── 컨택트 시트 ─────────────────────────────────── */
function sheet(file, out, cols = 7, rows = 4, dur) {
  const every = Math.max(0.4, dur / (cols * rows));
  run(FF, ['-y', '-loglevel', 'error', '-i', file,
    '-vf', `fps=1/${every.toFixed(3)},scale=210:-1,tile=${cols}x${rows}`,
    '-frames:v', '1', out]);
}

/* ── 명령 ───────────────────────────────────────── */
const argv = process.argv.slice(2);
const cmd = argv[0];
const flag = (n, d) => { const i = argv.indexOf('--' + n); return i < 0 ? d : argv[i + 1]; };

function metaOf(url) {
  const r = run(YTD, ['--no-warnings', '-J', '--no-playlist', url]);
  try { return JSON.parse(r.stdout); } catch { return null; }
}

function add(url, ep, tag, role) {
  if (!YTD) throw new Error('yt-dlp 을 못 찾았다 (SCENE_YTDLP 로 지정 가능)');
  const slug = slugOf(url);
  const dir = path.join(REFS, slug);
  fs.mkdirSync(dir, { recursive: true });
  const mp4 = path.join(dir, 'ref.mp4');

  if (!fs.existsSync(mp4)) {
    console.log(`받는 중  ${url}`);
    const r = run(YTD, ['--no-warnings', '-f', 'bv*[height<=720]+ba/b[height<=720]',
      '--merge-output-format', 'mp4', '-o', mp4, url], { stdio: ['ignore', 'inherit', 'inherit'] });
    if (r.status !== 0 || !fs.existsSync(mp4)) throw new Error('내려받기 실패');
  }

  const j = metaOf(url) || {};
  const dur = Number(j.duration) || 60;
  const fps = Math.round(Number((j.fps || 30))) || 30;
  const m = measure(mp4);   // 항상 SAMPLE_FPS — 원본 fps 로 재면 비교가 성립을 안 한다
  sheet(mp4, path.join(dir, 'sheet.png'), 7, 4, dur);

  /* 역할을 갈라 둔다. 마네킹 워크사이클의 「움직임 평균」을 우리 회차 전체와 나란히 놓는 것은
     비교가 아니라 착시다 — 한쪽은 동작 하나의 루프고 우리는 여섯 샷짜리 편집물이다.
       motion  이 회차의 **동작**을 만들 때 보는 것 (태그로 찾는다 · 회차마다 받는다)
       pacing  편집 리듬의 잣대 (숫자 밴드는 이쪽하고만 잰다)
       grammar 처음 보는 사람이 이해하는 법의 잣대 (2026-08-14 신설).
               🔴 회차마다 받지 않는다 — 잣대가 회차마다 바뀌면 「지난 편보다 나아졌나」를
               못 잰다. 축이 바뀔 때만 사람이 골라 넣고, 그 숫자가 밴드로 굳는다. */
  const info = {
    slug, url, title: j.title || slug, channel: j.channel || '', duration: dur,
    width: j.width, height: j.height, fps, measured: m,
    tag: tag || null, role: role || (tag ? 'motion' : 'pacing'),
    at: new Date().toISOString(),
  };
  fs.writeFileSync(path.join(dir, 'ref.json'), JSON.stringify(info, null, 2), 'utf8');
  fs.writeFileSync(path.join(dir, 'report.txt'), report(info.title, m) + '\n', 'utf8');
  console.log('\n' + report(info.title, m));
  console.log(`   컨택트 시트  ${path.relative(ROOT, path.join(dir, 'sheet.png'))}`);

  if (ep) writeEpNotes(ep, scanEp(ep));
  return info;
}

function allRefs() {
  if (!fs.existsSync(REFS)) return [];
  return fs.readdirSync(REFS)
    .map(d => path.join(REFS, d, 'ref.json'))
    .filter(p => fs.existsSync(p))
    .map(p => JSON.parse(fs.readFileSync(p, 'utf8')));
}

/* ── 문법 밴드 ──────────────────────────────────────
   「처음 보는 사람이 이해하는가」의 잣대. 사용자가 시트를 보고 고른 두 편에서 뽑는다
   (2026-08-14):
     wC9iu7cuQjI  Challacade — 게임 화면을 유지한 채 **디버그 오버레이**로 설명한다
     a91EPM7TBX4  Starlight  — 게임 화면 위에 **이름표·대사창**을 얹는다

   🔑 밴드는 **두 편이 같은 말을 하는 축에만** 만든다. 갈리는 축에 밴드를 씌우면
      그 밴드는 둘 중 하나를 위반으로 판정하는 물건이 된다 — 근거가 아니라 숫자다.
      실제로 컷 간격이 그렇다: Challacade 271.7초 vs Starlight 4.6초. 두 편 다
      읽히는데 20배 넘게 갈린다. 그래서 **컷은 밴드가 아니다**(값만 남긴다).

   🔴 숫자를 여기 박지 않는다. 저장된 측정치에서 계산한다 — 검출기를 고치거나 축을
      더하면 밴드가 따라 움직여야 하고, 손으로 박은 값은 그때 조용히 낡는다. */
const BAND_SLUGS = ['wC9iu7cuQjI', 'a91EPM7TBX4'];

function grammarBand() {
  const g = allRefs().filter(r => BAND_SLUGS.includes(r.slug) && r.measured);
  if (g.length < 2) return null;
  const v = k => g.map(r => r.measured[k]);
  return {
    from: g.map(r => ({ slug: r.slug, title: r.title })),
    /* 하한 = 두 편 중 낮은 쪽. 「둘 다 이만큼은 한다」가 하한의 뜻이다. */
    moveMin: Math.min(...v('move')),
    areaP90Min: Math.min(...v('areaP90')),
    /* 상한은 홀드에만. 오래 멈춰 있는 것은 두 문법 다 하지만 한계가 있다. */
    holdMaxMax: Math.max(...v('holdMax')),
    /* 밴드 아님 — 값만 남긴다. 위 주석 참고. */
    cutGapMedRange: v('cutGapMed').sort((a, b) => a - b),
  };
}

function ourMeasure(ep) {
  const mp4 = path.join(ROOT, 'episodes', ep, 'build', 'video.mp4');
  return fs.existsSync(mp4) ? measure(mp4) : null;
}

/* ── 회차 대조 ───────────────────────────────────────
   🔑 여기서 재는 것은 **대본이 요구한 동작 ↔ 그림이 실제로 하는 동작**이다.
      ep16s-1 의 훅은 reads 에 「걸어와」라고 적어 놓고 다리가 정지해 있었는데, 게이트 33종이
      전부 통과했다 — 아무도 그 둘을 나란히 놓고 본 적이 없기 때문이다. 이 함수가 그 자리다. */

/* 두 가지를 **가른다**. 섞으면 게이트가 못 쓰게 된다.
     선언(`shot.motions`)  작성팀이 「이 샷에서 사람이 이걸 한다」고 적은 것 → **판정**.
     낱말(reads)           문장에서 주운 추정 → **경고**. 우리 대본은 사람 아닌 것도
                           걷고 앉는 것처럼 쓰기 때문이다(motions.mjs 주석). */

/** reads 에서 주운 힌트 태그. */
const hintOf = text => Object.entries(MOTIONS)
  .filter(([, m]) => m.reads.some(k => text.includes(k)))
  .map(([t]) => t);

/** kinds/*.js 소스에서 실제로 쓴 동작 태그를 뽑는다(그림이 진실이다). */
function usedOf(ep) {
  const kdir = path.join(ROOT, 'episodes', ep, 'kinds');
  const out = {};
  if (!fs.existsSync(kdir)) return out;
  for (const f of fs.readdirSync(kdir).filter(f => f.endsWith('.js'))) {
    const src = fs.readFileSync(path.join(kdir, f), 'utf8');
    out[f.replace(/\.js$/, '')] = [...src.matchAll(/motion:\s*['"](\w+)['"]/g)].map(m => m[1]);
  }
  return out;
}

function scanEp(ep) {
  const dir = path.join(ROOT, 'episodes', ep);
  const scene = JSON.parse(fs.readFileSync(path.join(dir, 'scene.json'), 'utf8'));
  const used = usedOf(ep);
  const world = used._world || [];        // kind 가 회차 공용 모듈에 위임했을 수 있다
  const shots = (scene.shots || []).map(s => {
    const declared = s.motions || [];
    const have = [...(used[s.kind] || []), ...world];
    const hints = hintOf(`${s.reads || ''}`).filter(t => !declared.includes(t) && !have.includes(t));
    return {
      id: s.id, kind: s.kind || null, declared, used: have, hints,
      missing: declared.filter(t => !have.includes(t)),
    };
  });
  /* 레퍼런스를 받아 둘 대상 = **선언됐거나 실제로 쓰는** 동작. 힌트로는 안 받는다 —
     오탐 하나에 남의 영상 한 편을 내려받게 되고, 그러면 아무도 이 명령을 안 돌린다. */
  const tags = [...new Set(shots.flatMap(s => [...s.declared, ...s.used]))].filter(t => MOTIONS[t]);
  const unknown = [...new Set(shots.flatMap(s => [...s.declared, ...s.used]))].filter(t => !MOTIONS[t]);
  return {
    ep, shots, tags, unknown,
    gaps: shots.filter(s => s.missing.length),
    hinted: shots.filter(s => s.hints.length),
  };
}

/** 어긋남을 사람이 읽는 자리에도 남긴다 — notes/ 는 검수·마스터가 실제로 여는 폴더다. */
function writeEpNotes(ep, scan) {
  const dir = path.join(ROOT, 'episodes', ep, 'notes');
  if (!fs.existsSync(dir)) { console.log(`(${ep} notes 폴더가 없다 — 건너뜀)`); return null; }
  const refs = allRefs();
  const byTag = Object.fromEntries(refs.filter(r => r.tag).map(r => [r.tag, r]));
  /* 🔴 `!== 'motion'` 이 아니라 `=== 'pacing'` 이다. grammar 가 들어온 뒤로 이 둘이 갈린다 —
     리듬 밴드에 설명형 영상이 섞이면 밴드가 늘어져 아무것도 못 막는다. */
  const pacing = refs.filter(r => r.role === 'pacing' && r.measured);
  const ours = ourMeasure(ep);

  const L = ['# 레퍼런스 대조 (refs.mjs plan)', '',
    '눈대중을 안 남긴다. 숫자는 전부 같은 함수로 잰 값이다(×1000).', '',
    '## 1. 선언한 동작 ↔ 그림이 하는 동작', '',
    '| 샷 | kind | scene.json 선언 | 그림이 쓰는 것 | 어긋남 | 레퍼런스 |',
    '|---|---|---|---|---|---|'];
  for (const s of scan.shots) {
    if (!s.declared.length && !s.used.length) continue;
    const tags = [...new Set([...s.declared, ...s.used])];
    L.push(`| ${s.id} | ${s.kind ?? '-'} | ${s.declared.join(', ') || '—'} | ${s.used.join(', ') || '—'} | ` +
      `${s.missing.length ? '🔴 ' + s.missing.join(', ') : '없음'} | ` +
      tags.map(t => byTag[t] ? `${t}=\`refs/${byTag[t].slug}/sheet.png\`` : `${t}=**없음**`).join(' · ') + ' |');
  }
  if (!scan.shots.some(s => s.declared.length || s.used.length)) {
    L.push('| — | — | (사람 동작을 선언한 샷이 없다) | — | — | — |');
  }
  L.push('', scan.gaps.length
    ? `🔴 **어긋남 ${scan.gaps.length}샷.** 선언한 동작을 그림이 안 한다 — 자막을 가리면 그 뜻이 안 읽힌다는 뜻이다.`
    : '🟢 어긋남 없음.');
  if (scan.hinted.length) {
    L.push('', '### 선언이 빠졌을 수 있는 자리 (경고 · reads 낱말에서 주움)', '');
    for (const s of scan.hinted) L.push(`- ${s.id}(${s.kind ?? '-'}) — reads 가 \`${s.hints.join(', ')}\` 를 말한다. 사람이 하는 동작이면 \`shot.motions\` 에 적어라. 사람이 아니면 무시해도 된다.`);
  }

  L.push('', '## 2. 편집 리듬', '',
    '| | 움직임 평균 | 상위5% | 최대 | 실효 초당 장수 | 0.25초+ 홀드 최장 | 임팩트 |',
    '|---|---|---|---|---|---|---|');
  for (const r of refs) {
    const m = r.measured; if (!m) continue;
    L.push(`| ${r.role === 'motion' ? `〈동작 ${r.tag}〉 ` : ''}${r.title.slice(0, 36)} | ${m.move} | ${m.p95} | ${m.max} | ${m.perSec} | ${m.holdMax}s | ${m.flashes} |`);
  }
  if (ours) L.push(`| **우리 ${ep}** | **${ours.move}** | **${ours.p95}** | **${ours.max}** | **${ours.perSec}** | **${ours.holdMax}s** | **${ours.flashes}** |`);
  else L.push(`| **우리 ${ep}** | (build/video.mp4 없음 — 렌더 뒤 다시 돌려라) | | | | | |`);
  if (ours && pacing.length) {
    const med = k => { const v = pacing.map(r => r.measured[k]).sort((a, b) => a - b); return v[v.length >> 1]; };
    L.push('', `홀드 최장 — 우리 ${ours.holdMax}초 / 리듬 레퍼런스 중앙 ${med('holdMax')}초. ` +
      '**박자가 아니라 사건 밀도**로 좁혀야 한다(3D_전환_인계 §6-3). 촬영 박자 양자화는 렉으로 읽혀 되돌린 길이다.');
  }
  const band = grammarBand();
  if (band) {
    L.push('', '## 3. 문법 밴드 — 처음 보는 사람이 이해하는가', '',
      `잣대: ${band.from.map(f => f.title.slice(0, 34)).join(' · ')}`, '',
      '| 축 | 밴드 | 우리 | |',
      '|---|---|---|---|');
    const row = (name, ours, ok, bandTxt) =>
      L.push(`| ${name} | ${bandTxt} | ${ours ?? '—'} | ${ours == null ? '' : ok ? '🟢' : '🔴'} |`);
    row('변화 면적 상위10% (%)', ours?.areaP90, ours && ours.areaP90 >= band.areaP90Min, `≥ ${band.areaP90Min}`);
    row('움직임 평균 (×1000)', ours?.move, ours && ours.move >= band.moveMin, `≥ ${band.moveMin}`);
    row('홀드 최장 (초)', ours?.holdMax, ours && ours.holdMax <= band.holdMaxMax, `≤ ${band.holdMaxMax}`);
    L.push(`| 컷 중앙 간격 (초) | **밴드 아님** (${band.cutGapMedRange.join(' ~ ')}) | ${ours?.cutGapMed ?? '—'} | — |`);
    L.push('', '🔑 컷 간격에 밴드가 없는 이유 — 두 레퍼런스가 정반대다. Challacade 는 한 화면을 ' +
      '유지한 채 오버레이를 바꿔 설명하고(컷 3회/340초), Starlight 는 장면을 갈아 끼운다(17회/123초). ' +
      '**둘 다 읽힌다.** 컷은 이해도의 원인이 아니다.', '',
      '변화 면적이 낮다 = 화면 대부분이 매 프레임 그대로다 = 볼 것이 작거나 없다. ' +
      'ep16s-1 의 「인물이 화면 높이의 1/12」이 이 축에 잡히는 자리다.');
  }
  L.push('', '컨택트 시트는 `tools/scene-video/refs/<slug>/sheet.png`.',
    '영상 원본은 리포에 안 담는다 — 남는 것은 측정치와 시트뿐이다.', '');
  fs.writeFileSync(path.join(dir, 'refs.md'), L.join('\n'), 'utf8');

  const json = {
    ep, at: new Date().toISOString(),
    sceneMtime: fs.statSync(path.join(ROOT, 'episodes', ep, 'scene.json')).mtimeMs,
    shots: scan.shots, tags: scan.tags, gaps: scan.gaps, hinted: scan.hinted, unknown: scan.unknown,
    refs: Object.fromEntries(Object.entries(byTag).map(([t, r]) => [t, { slug: r.slug, title: r.title, url: r.url }])),
    missingRefs: scan.tags.filter(t => !byTag[t]),
    ours, pacing: pacing.map(r => ({ slug: r.slug, holdMax: r.measured.holdMax, move: r.measured.move })),
    band,
  };
  fs.writeFileSync(path.join(dir, 'refs.json'), JSON.stringify(json, null, 2), 'utf8');
  console.log(`   회차 노트  episodes/${ep}/notes/refs.md · refs.json`);
  return json;
}

/** ⭐ 회차 하나를 통째로 — 뽑고 · 없으면 받고 · 대조해서 남긴다. */
function plan(ep, fetch = true) {
  const scan = scanEp(ep);
  console.log(`\n${ep} — 이 회차의 동작: ${scan.tags.join(', ') || '(없음)'}`);
  for (const g of scan.gaps) console.log(`   🔴 ${g.id}(${g.kind}) 가 선언한 ${g.missing.join(', ')} 를 그림이 안 쓴다`);
  for (const s of scan.hinted) console.log(`   ⚠ ${s.id}(${s.kind}) — reads 가 ${s.hints.join(', ')} 를 말하는데 선언이 없다(사람이 아니면 무시)`);
  if (scan.unknown.length) console.log(`   ⚠ 어휘집에 없는 태그: ${scan.unknown.join(', ')} — MOTIONS 에 넣든지 오타든지 둘 중 하나다`);

  const have = new Set(allRefs().filter(r => r.tag).map(r => r.tag));
  for (const t of scan.tags) {
    if (have.has(t)) continue;
    if (!fetch) { console.log(`   (${t} 레퍼런스 없음 — --no-fetch 라 건너뜀)`); continue; }
    console.log(`\n▶ ${t} 레퍼런스가 없다 — "${MOTIONS[t].q}" 로 받는다`);
    try {
      const r = run(YTD, ['--no-warnings', '--flat-playlist', '-J', `ytsearch1:${MOTIONS[t].q}`]);
      const e = (JSON.parse(r.stdout || '{}').entries || [])[0];
      if (!e) { console.log('   검색 결과 없음'); continue; }
      add(`https://youtu.be/${e.id}`, null, t);
    } catch (err) { console.log('   건너뜀:', err.message); }
  }
  return writeEpNotes(ep, scan);
}

switch (cmd) {
  case 'find': {
    if (!YTD) throw new Error('yt-dlp 을 못 찾았다');
    const q = argv[1];
    if (!q) throw new Error('검색어가 없다');
    const n = Number(flag('n', 3));
    /* --dry 는 **받지 않고 목록만** 낸다. 후보 정찰에 남의 영상을 5편씩 내려받을 이유가
       없다 — 고르는 것은 사람이고, 고르기 전에 필요한 것은 제목·길이·채널뿐이다. */
    const dry = argv.includes('--dry');
    console.log(`찾는 중  "${q}" · 상위 ${n}편${dry ? ' (--dry · 안 받는다)' : ''}`);
    const r = run(YTD, ['--no-warnings', '--flat-playlist', '-J', `ytsearch${n}:${q}`]);
    const list = (JSON.parse(r.stdout || '{}').entries || []);
    for (const e of list) {
      console.log(`\n▶ ${e.title}  (${Math.round(e.duration || 0)}초 · ${e.channel || ''})`);
      if (dry) { console.log(`   https://youtu.be/${e.id}`); continue; }
      try { add(`https://youtu.be/${e.id}`, flag('ep'), flag('tag'), flag('role')); }
      catch (err) { console.log('   건너뜀:', err.message); }
    }
    break;
  }
  case 'add':
    add(argv[1], flag('ep'), flag('tag'), flag('role'));
    break;
  case 'plan': {
    const ep = argv[1];
    if (!ep) throw new Error('회차가 없다 — refs.mjs plan ep16s-1');
    const j = plan(ep, !argv.includes('--no-fetch'));
    if (j?.gaps.length) process.exit(1);       // check.mjs 가 아니라 여기서도 바로 막는다
    break;
  }
  case 'measure': {
    const ep = argv[1];
    const m = ourMeasure(ep);
    console.log(report(`우리 ${ep}`, m));
    for (const r of allRefs()) console.log('\n' + report(r.title, r.measured));
    if (flag('ep', ep)) writeEpNotes(ep, scanEp(ep));
    break;
  }
  /* 검출기가 살아 있는지 재는 **하나뿐인 판**. 컷 검출은 문턱 하나로 서는 물건이라
     조용히 죽는다 — 0컷이 나와도 「컷이 없는 영상」과 구별이 안 된다. 그래서 정답을
     아는 판을 하나 박아 둔다. ep16s-1 = 여섯 샷 + 꼬리 카드 = 컷 6회. */
  case 'selftest': {
    const f = path.join(ROOT, 'episodes', 'ep16s-1', 'build', 'video.mp4');
    if (!fs.existsSync(f)) { console.log('건너뜀 — ep16s-1/build/video.mp4 가 없다'); break; }
    const t = cutTimes(f, 30);
    const ok = t.length === 6;
    console.log(`${ok ? '🟢' : '🔴'} ep16s-1 컷 ${t.length}회 (기대 6) — ${t.map(v => v.toFixed(1)).join(', ')}`);
    if (!ok) { console.log('   문턱(SCENE_THR)이나 불응기가 흘렀다. 밴드를 다시 굳히기 전에 여기부터.'); process.exit(1); }
    break;
  }
  /* 축을 더하거나 검출기를 고치면 **저장된 측정치가 낡는다.** 그때 다시 받을 이유는
     없다(mp4 는 그대로다) — 재는 것만 다시 한다. */
  case 'remeasure': {
    for (const r of allRefs()) {
      const mp4 = path.join(REFS, r.slug, 'ref.mp4');
      if (!fs.existsSync(mp4)) { console.log(`건너뜀 ${r.slug} — mp4 없음`); continue; }
      r.measured = measure(mp4);
      fs.writeFileSync(path.join(REFS, r.slug, 'ref.json'), JSON.stringify(r, null, 2), 'utf8');
      fs.writeFileSync(path.join(REFS, r.slug, 'report.txt'), report(r.title, r.measured) + '\n', 'utf8');
      console.log(report(`[${r.role}] ${r.title}`, r.measured) + '\n');
    }
    break;
  }
  case 'list':
    for (const r of allRefs())
      console.log(`${r.slug}  ${r.title}  (${r.duration}초)  움직임 ${r.measured?.move ?? '-'}`);
    break;
  default:
    console.log(fs.readFileSync(fileURLToPath(import.meta.url), 'utf8')
      .split('*/')[0].split('사용')[1].split('왜 만들었나')[0].trim());
}
