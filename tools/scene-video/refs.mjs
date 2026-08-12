/* 레퍼런스 파이프라인 — 영상을 만들 때 볼 것을 **찾아서 받고 재고 붙여 놓는다.**

   사용
     node tools/scene-video/refs.mjs find "2d 액션 애니메이션 원화" --n 3 --ep ep16s-1
     node tools/scene-video/refs.mjs add  https://youtu.be/<id> --ep ep16s-1
     node tools/scene-video/refs.mjs measure ep16s-1        # 우리 회차를 같은 잣대로
     node tools/scene-video/refs.mjs list

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

function measure(file, fps = 30, from = 0, to = 1e9) {
  const r = run(FF, ['-v', 'error', '-i', file,
    '-vf', `fps=${fps},scale=${W}:${H},format=gray`,
    '-f', 'rawvideo', '-pix_fmt', 'gray', '-'], { encoding: 'buffer' });
  const b = r.stdout;
  const F = [];
  for (let i = 0; i + N <= b.length; i += N) F.push(b.subarray(i, i + N));
  const cut = F.slice(Math.round(from * fps), Math.round(to * fps));
  if (cut.length < 3) return null;

  const d = [], lum = [];
  for (let i = 1; i < cut.length; i++) {
    let s = 0;
    for (let j = 0; j < N; j++) s += Math.abs(cut[i][j] - cut[i - 1][j]);
    d.push(s / N / 255);
  }
  for (const f of cut) { let s = 0; for (let j = 0; j < N; j++) s += f[j]; lum.push(s / N / 255); }

  const HOLD = 0.004;                       // 압축 잡음 위, 실제 그림 교체 아래
  const steps = []; let runLen = 1;
  for (const v of d) { if (v < HOLD) runLen++; else { steps.push(runLen); runLen = 1; } }
  steps.push(runLen);                        // 마지막 구간도 센다
  const hist = {};
  for (const s of steps) hist[Math.min(s, 9)] = (hist[Math.min(s, 9)] || 0) + 1;

  const sorted = [...d].sort((a, b) => a - b);
  const med = sorted[sorted.length >> 1] || 1e-6;
  const cuts = d.map((v, i) => [i, v]).filter(([, v]) => v > Math.max(0.09, med * 8));
  const gaps = cuts.slice(1).map((c, i) => (c[0] - cuts[i][0]) / fps).sort((a, b) => a - b);

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
    cuts: cuts.length, cutGapMed: +((gaps[gaps.length >> 1] || 0).toFixed(2)),
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

function add(url, ep) {
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
  const m = measure(mp4, Math.min(fps, 60));
  sheet(mp4, path.join(dir, 'sheet.png'), 7, 4, dur);

  const info = {
    slug, url, title: j.title || slug, channel: j.channel || '', duration: dur,
    width: j.width, height: j.height, fps, measured: m, at: new Date().toISOString(),
  };
  fs.writeFileSync(path.join(dir, 'ref.json'), JSON.stringify(info, null, 2), 'utf8');
  fs.writeFileSync(path.join(dir, 'report.txt'), report(info.title, m) + '\n', 'utf8');
  console.log('\n' + report(info.title, m));
  console.log(`   컨택트 시트  ${path.relative(ROOT, path.join(dir, 'sheet.png'))}`);

  if (ep) writeEpNotes(ep);
  return info;
}

function allRefs() {
  if (!fs.existsSync(REFS)) return [];
  return fs.readdirSync(REFS)
    .map(d => path.join(REFS, d, 'ref.json'))
    .filter(p => fs.existsSync(p))
    .map(p => JSON.parse(fs.readFileSync(p, 'utf8')));
}

/** 회차 notes 에 붙인다 — 대본·검수가 읽는 자리에 근거가 있어야 쓰인다. */
function writeEpNotes(ep) {
  const dir = path.join(ROOT, 'episodes', ep, 'notes');
  if (!fs.existsSync(dir)) { console.log(`(${ep} notes 폴더가 없다 — 건너뜀)`); return; }
  const ours = ourMeasure(ep);
  const lines = ['# 레퍼런스 실측 (refs.mjs)', '',
    '눈대중을 안 남긴다. 아래는 전부 같은 함수로 잰 값이다(×1000).', '',
    '| | 움직임 평균 | 상위5% | 최대 | 실효 초당 장수 | 0.25초+ 홀드 최장 | 임팩트 |',
    '|---|---|---|---|---|---|---|'];
  for (const r of allRefs()) {
    const m = r.measured; if (!m) continue;
    lines.push(`| ${r.title.slice(0, 40)} | ${m.move} | ${m.p95} | ${m.max} | ${m.perSec} | ${m.holdMax}s | ${m.flashes} |`);
  }
  if (ours) lines.push(`| **우리 ${ep}** | **${ours.move}** | **${ours.p95}** | **${ours.max}** | **${ours.perSec}** | **${ours.holdMax}s** | **${ours.flashes}** |`);
  lines.push('', '컨택트 시트는 `tools/scene-video/refs/<slug>/sheet.png`.',
    '영상 원본은 리포에 안 담는다 — 남는 것은 측정치와 시트뿐이다.', '');
  fs.writeFileSync(path.join(dir, 'refs.md'), lines.join('\n'), 'utf8');
  console.log(`   회차 노트  episodes/${ep}/notes/refs.md`);
}

function ourMeasure(ep) {
  const mp4 = path.join(ROOT, 'episodes', ep, 'build', 'video.mp4');
  return fs.existsSync(mp4) ? measure(mp4, 30) : null;
}

switch (cmd) {
  case 'find': {
    if (!YTD) throw new Error('yt-dlp 을 못 찾았다');
    const q = argv[1];
    if (!q) throw new Error('검색어가 없다');
    const n = Number(flag('n', 3));
    console.log(`찾는 중  "${q}" · 상위 ${n}편`);
    const r = run(YTD, ['--no-warnings', '--flat-playlist', '-J', `ytsearch${n}:${q}`]);
    const list = (JSON.parse(r.stdout || '{}').entries || []);
    for (const e of list) {
      console.log(`\n▶ ${e.title}  (${Math.round(e.duration || 0)}초 · ${e.channel || ''})`);
      try { add(`https://youtu.be/${e.id}`, flag('ep')); }
      catch (err) { console.log('   건너뜀:', err.message); }
    }
    break;
  }
  case 'add':
    add(argv[1], flag('ep'));
    break;
  case 'measure': {
    const ep = argv[1];
    const m = ourMeasure(ep);
    console.log(report(`우리 ${ep}`, m));
    for (const r of allRefs()) console.log('\n' + report(r.title, r.measured));
    if (flag('ep', ep)) writeEpNotes(ep);
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
