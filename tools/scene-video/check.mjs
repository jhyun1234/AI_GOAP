/* 회차가 youtube-editor 가이드와 프로젝트 규약을 지키는지 기계로 점검한다.
   사용: node tools/scene-video/check.mjs ep01s [--fps 5] [--json]

   왜 필요한가: 가이드는 4,545줄이고 회차마다 사람이 다시 훑을 수는 없다. 기계가 볼 수 있는
   것만이라도 매번 보게 만든다. 못 보는 것(구성이 재미있는가, 그림이 뜻을 전하는가)은
   여전히 사람 몫이다 — 이 스크립트는 그걸 대신한다고 주장하지 않는다.

   예외는 씬 JSON 에 사유와 함께 적는다:
     shot.checks = { "palette": "제품 마크는 브랜드 색", "edge": "파편이 화면 밖으로 날아감" }
   값이 곧 사유다. 사유 없이 끄는 길은 두지 않았다.
*/
import fs from 'fs';
import path from 'path';
import { ROOT, openEngine, epScene, epBuild } from './lib-node.mjs';

const argv = process.argv.slice(2);
const EP = argv.find(a => !a.startsWith('--')) || 'ep01s';
const FPS = Number((argv[argv.indexOf('--fps') + 1] > 0 && argv.includes('--fps')) ? argv[argv.indexOf('--fps') + 1] : 5);
const AS_JSON = argv.includes('--json');

const scene = JSON.parse(fs.readFileSync(epScene(EP), 'utf8'));
const timedPath = epBuild(EP, 'timed.json');
const timed = fs.existsSync(timedPath) ? JSON.parse(fs.readFileSync(timedPath, 'utf8')) : null;

const results = [];
const add = (ok, name, detail, level = 'fail') =>
  results.push({ ok, name, detail, level: ok ? 'pass' : level });

/* ── A. 씬·대본 (파일만 보면 되는 것) ───────────────── */

const missingMeta = scene.shots.filter(s => !s.reads || !s.source || !s.kind).map(s => s.id);
add(missingMeta.length === 0, 'shot 메타 완비',
  missingMeta.length ? `reads/source/kind 누락: ${missingMeta.join(', ')}` : `${scene.shots.length}샷 전부 있음`);

const kindsDir = path.join(ROOT, 'episodes', EP, 'kinds');
const badKind = [...new Set(scene.shots.map(s => s.kind))].filter(k => !fs.existsSync(path.join(kindsDir, `${k}.js`)));
add(badKind.length === 0, 'kind 파일 존재', badKind.length ? `없음: ${badKind.join(', ')}` : '전부 존재');

const allLines = scene.shots.flatMap(s => s.lines.map(l => ({ ...l, shot: s.id })));

const tooManyLines = allLines.filter(l => l.text.split('\n').length > 2);
add(tooManyLines.length === 0, '자막 2줄 상한',
  tooManyLines.length ? `${tooManyLines.length}줄이 3줄 이상 (${tooManyLines[0].shot})` : `${allLines.length}줄 전부 2줄 이하`);

const bigPauses = allLines.filter(l => (l.pauseAfter ?? 0) >= 600);
add(bigPauses.length <= 2, '큰 쉼 회차당 2회 이하',
  `${bigPauses.length}회` + (bigPauses.length ? ` (${bigPauses.map(l => l.shot).join(', ')})` : ''),
  'warn');

if (timed) {
  const flat = timed.shots.flatMap((s, si) => s.lines.map((l, li) => ({ ...l, shot: scene.shots[si].id })));
  const tooShort = flat.filter(l => (l.dur + (l.pause || 0)) < 2000);
  add(tooShort.length === 0, '자막 2초 미만 없음',
    tooShort.length ? `${tooShort.length}줄 (${tooShort.map(l => `${l.shot}:${((l.dur + l.pause) / 1000).toFixed(1)}s`).join(', ')})` : '전부 2초 이상',
    'warn');

  const cps = timed.summary?.charsPerSec;
  add(cps >= 6.0 && cps <= 7.2, '말 속도 6.0~7.2자/초', `${cps}자/초 (참고 영상 6.93)`, 'warn');
} else {
  add(false, '실측 타임라인 존재', `episodes/${EP}/build/timed.json 이 없다 — tts.mjs 를 먼저 돌려라`);
}

/* ── B. 실제 프레임 (헤드리스에서 그려 보고 판정) ───── */

const paletteSkip = {}, edgeSkip = {};
for (const s of scene.shots) {
  if (s.checks?.palette) paletteSkip[s.id] = s.checks.palette;
  if (s.checks?.edge) edgeSkip[s.id] = s.checks.edge;
}

let frame = null;
const { cdp, close } = await openEngine(EP, { quiet: AS_JSON });
try {
  /* 읽기 경로 점검.
     🔴 여기서 한 번 크게 틀렸다. 투명 캔버스에 rgba(255,255,255,0.28) 을 칠하고 색 채널을
     읽으면 255 가 나온다 — 알파가 이진화된 게 아니라, 투명 배경 위에서는 색이 그대로 남고
     농도가 알파 채널에 들어가기 때문이다(검정을 먼저 깔았을 때만 71 이 된다).
     이걸 "이진화"로 오독해서 한동안 픽셀 계측 전체를 못 믿을 것으로 판단했었다.
     그래서 점검은 검정을 깔고 한다 — 실제 화면과 같은 조건이다. */
  const sane = await cdp.evaluate(`(()=>{
    const c=document.createElement('canvas');c.width=c.height=4;
    const x=c.getContext('2d');
    x.fillStyle='#000';x.fillRect(0,0,4,4);
    x.fillStyle='rgba(255,255,255,0.28)';x.fillRect(0,0,4,4);
    return x.getImageData(0,0,1,1).data[1];
  })()`);
  add(Math.abs(sane - 71) <= 8, '픽셀 읽기 신뢰 가능', `검정 위 알파 0.28 → ${sane} (기대 71±8)`);

  /* 🔴 그림이 실제로 로드됐는가. 파일이 있는 것과 import 가 되는 것은 다르다 —
     kinds 를 회차 폴더로 옮기며 lib.js 상대경로가 깨졌을 때, 파일은 전부 제자리에 있었고
     엔진은 조용히 빈 화면으로 넘어갔고, 이 점검은 "정적 구간 0.0초"로 통과시켰다.
     아무것도 안 그려진 화면이 가장 조용하다. */
  const failed = await cdp.evaluate('window.__failedKinds ? window.__failedKinds() : []');
  add(!failed?.length, '그림 로드 성공',
    failed?.length ? `import 실패: ${failed.join(', ')}` : '전 kind 로드됨');

  frame = await cdp.evaluate(`(async () => {
    const FPS = ${FPS}, N = Math.max(2, Math.floor(window.TOTAL/1000*FPS));
    const per = {};
    let prev = null, prevId = null;
    for (let i = 0; i < N; i++) {
      window.seek(i / FPS * 1000);
      const el = document.querySelector('.shot.on');
      const id = el?.dataset.id; if (!id) continue;
      const cv = el.querySelector('canvas');
      if (!per[id]) per[id] = { frames:0, bad:0, total:0, staticRun:0, maxStatic:0, edgeBot:0 };
      const P = per[id]; P.frames++;
      if (!cv || !cv.width) { prev = null; prevId = id; continue; }
      const g = cv.getContext('2d');
      const d = g.getImageData(0,0,cv.width,cv.height).data;

      /* 3색 판정은 '색상(hue)'으로 본다.
         🔴 처음엔 정확한 값 비교로 짰다가 멀쩡한 화면이 무더기로 걸렸다. 흰 막대 위에
         그린 글자를 얹으면 안티에일리어싱 경계에 두 색의 중간값이 생기는데, 그건
         팔레트 위반이 아니라 허용된 두 색이 섞인 자리다. 채도가 낮으면(회색 계열)
         통과, 채도가 있으면 그린(#00FF88, 색상 152도) 근처인지만 본다.
         주황(색상 17도)인 제품 마크는 이 검사에 정확히 걸린다 — 그래서 예외를 선언한다.

         밝기는 알파를 곱해서 낸다. 캔버스가 투명 배경이라 색 채널만 보면 흐릿한 칠도
         255 로 읽혀 움직임이 없는 것처럼 보인다. */
      let bad = 0, tot = 0;
      const L = new Uint8Array((d.length>>2));
      for (let p = 0; p < d.length; p += 4) {
        const r=d[p], gg=d[p+1], b=d[p+2], a=d[p+3];
        L[p>>2] = (((r*77+gg*151+b*28)>>8) * a / 255) | 0;
        /* 알파 64 미만은 세지 않는다. 8비트 프리멀티플라이드 합성은 아주 옅은 칠에서
           색이 흔들린다 — 실측으로 (0,255,136,a=12) 이 (0,255,212) 로 읽혔다(색상 152→170).
           검정 위 알파 64 미만은 밝기가 25% 미만이라 색이 뜻을 갖지 못한다. 반올림 잡음을
           위반으로 세면 점검이 늑대 소년이 된다. */
        if (a < 64) continue;
        tot++;
        const mx=Math.max(r,gg,b), mn=Math.min(r,gg,b);
        /* 검정. mx===0 만 거르면 안 된다 — rgb(0,1,0) 같은 값이 채도 1.0 · 색상 120도로
           계산돼 위반으로 잡힌다(실측: ep00s 한 회차에서 8샷 41만 픽셀). 눈에는 검정이다.
           최대 채널이 9% 미만이면 색이 아니라 검정으로 본다. */
        if (mx < 24) continue;
        const sat = (mx-mn)/mx;
        if (sat <= 0.14) continue;                    // 회색 계열 = 검정~흰색 사이
        let hue;                                      // 0~360
        const c = mx-mn;
        if (mx === r) hue = 60*(((gg-b)/c)%6);
        else if (mx === gg) hue = 60*(((b-r)/c)+2);
        else hue = 60*(((r-gg)/c)+4);
        if (hue < 0) hue += 360;
        const accent = hue >= 138 && hue <= 168;      // #00FF88 = 152도
        if (!accent) bad++;
      }
      P.bad += bad; P.total += tot;

      // 아래 가장자리에 칠이 닿는가 = 밖으로 나간 것이 잘린 흔적
      const W=cv.width, H=cv.height;
      let e=0; const row=g.getImageData(0,H-1,W,1).data;
      for (let x=0;x<W;x++) if (row[x*4+3] > 10) e++;
      P.edgeBot = Math.max(P.edgeBot, e);

      // 정적 구간
      if (prev && prevId === id && prev.length === L.length) {
        let diff = 0;
        for (let j=0;j<L.length;j+=3) diff += Math.abs(L[j]-prev[j]);
        const m = diff/(L.length/3)/255;
        if (m < 0.0008) { P.staticRun++; P.maxStatic = Math.max(P.maxStatic, P.staticRun); }
        else P.staticRun = 0;
      } else P.staticRun = 0;
      prev = L; prevId = id;
    }
    for (const k in per) per[k].maxStaticSec = +(per[k].maxStatic / FPS).toFixed(1);
    return { fps: FPS, per };
  })()`);
  /* 결정성 — 이 프로젝트의 1번 규약이자 mp4 추출의 전제다.
     같은 시각을 어느 순서로 그려도 같은 그림이어야 한다. 깨지면 프레임이 흔들린다.
     회차마다 자동으로 보게 둔다 — 사람이 기억해서 돌릴 규칙은 언젠가 안 돌린다. */
  const det = await cdp.evaluate(`(()=>{
    const FPS=${FPS}, N=Math.max(2, Math.floor(window.TOTAL/1000*FPS));
    const sig=()=>{const el=document.querySelector('.shot.on');const cv=el?.querySelector('canvas');
      let h=2166136261;
      if(cv&&cv.width){const d=cv.getContext('2d').getImageData(0,0,cv.width,cv.height).data;
        for(let i=0;i<d.length;i+=11){h^=d[i];h=Math.imul(h,16777619);}}
      const s=(document.getElementById('cap').textContent||'')+'|'+(el?.style.transform||'')+'|'+(el?.innerHTML||'');
      for(let i=0;i<s.length;i++){h^=s.charCodeAt(i);h=Math.imul(h,16777619);}
      return (h>>>0).toString(36);};
    const pass=o=>{const r=[];for(const i of o){window.seek(i/FPS*1000);r.push(sig());}return r;};
    const fwd=[...Array(N).keys()];
    const a=pass(fwd), b=pass([...fwd].reverse()).reverse();
    const co=[...fwd].filter((_,i)=>i%3===0).concat([...fwd].filter((_,i)=>i%3!==0));
    const c=pass(co); const cm={}; co.forEach((f,i)=>cm[f]=c[i]);
    let m=0; for(const f of fwd) if(a[f]!==b[f]||a[f]!==cm[f]) m++;
    return {frames:N, mismatch:m};
  })()`);
  add(det.mismatch === 0, '결정성 (3패스)',
    det.mismatch ? `${det.frames}프레임 중 ${det.mismatch} 불일치` : `${det.frames}프레임 불일치 0`);
} finally { close(); }

if (frame) {
  const rows = Object.entries(frame.per);

  const violations = rows
    .filter(([id]) => !paletteSkip[id])
    .map(([id, v]) => ({ id, pct: v.total ? v.bad / v.total * 100 : 0 }))
    .filter(v => v.pct > 0.05);
  add(violations.length === 0, '3색 팔레트',
    violations.length
      ? violations.map(v => `${v.id} ${v.pct.toFixed(2)}%`).join(', ')
      : `위반 없음` + (Object.keys(paletteSkip).length ? ` (선언 예외: ${Object.entries(paletteSkip).map(([k, r]) => `${k}=${r}`).join(', ')})` : ''));

  const stat = rows.filter(([, v]) => v.maxStaticSec > 3.0);
  add(stat.length === 0, '정적 구간 3초 이하',
    stat.length ? stat.map(([id, v]) => `${id} ${v.maxStaticSec}s`).join(', ')
      : `최대 ${Math.max(...rows.map(([, v]) => v.maxStaticSec)).toFixed(1)}s`, 'warn');

  const bled = rows.filter(([id, v]) => v.edgeBot > 0 && !edgeSkip[id]);
  add(bled.length === 0, '아래 가장자리 잘림 없음',
    bled.length ? bled.map(([id, v]) => `${id} ${v.edgeBot}px`).join(', ')
      : '없음' + (Object.keys(edgeSkip).length ? ` (선언 예외: ${Object.entries(edgeSkip).map(([k, r]) => `${k}=${r}`).join(', ')})` : ''));
}

/* ── 보고 ────────────────────────────────────────── */
const fails = results.filter(r => r.level === 'fail');
const warns = results.filter(r => r.level === 'warn');

if (AS_JSON) {
  console.log(JSON.stringify({ ep: EP, pass: fails.length === 0, results }, null, 2));
} else {
  console.log(`\n가이드 점검 · ${EP}\n${'─'.repeat(52)}`);
  for (const r of results) {
    const mark = r.level === 'pass' ? '  OK ' : r.level === 'warn' ? '  ⚠  ' : '  🔴 ';
    console.log(`${mark}${r.name.padEnd(22)} ${r.detail}`);
  }
  console.log('─'.repeat(52));
  console.log(fails.length === 0
    ? (warns.length ? `통과 — 경고 ${warns.length}건은 확인 권장` : '전부 통과')
    : `🔴 ${fails.length}건 실패`);
}
process.exit(fails.length ? 1 : 0);
