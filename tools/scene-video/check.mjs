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

/* ── 제목 이행 — 제목이 약속한 것이 첫 두 줄 안에 나오는가 ──────────────
   🔴 이 채널 최대의 이탈 원인이다(Docs/영상_이탈률_개선_실행명세서.md §관측②).
   실측 절대 시청 시간이 16~27초인데 ep02s 는 제목이 약속한 장면이 34.3초에야 나왔다 —
   시청자 대다수가 그 장면을 한 번도 못 보고 나간다.

   기준을 `hook` 이 아니라 `youtube.title` 로 잡는 이유는 ADR-V-3 이다:
   시청자가 실제로 보고 누른 것이 제목이지 화면 안 문구가 아니다.

   `text`(자막)와 `say`(음성)를 **둘 다** 본다. 같은 수를 자막은 "4,096" 으로,
   음성은 "사천 구십육" 으로 적기 때문이다 — 한쪽만 보면 통과할 것을 반려한다.

   ⚠️ 검사가 거칠다. 형태소 분석 없이 어절만 대조하므로 동의어·활용형을 못 잡는다.
   그런데도 과거 5편에 돌리면 이행이 늦은 셋(ep01s·ep02s·ep03s)만 정확히 걸린다.
   의존성 0 이 이 파이프라인의 원칙이라(render.mjs 5행) 거친 채로 둔다.
   🔴 오탐이 나면 게이트를 무르게 하지 말고 제목이나 첫 줄을 고쳐라 —
   이 검사는 5편 중 3편을 잡아야 옳다. 전부 통과하면 검사가 망가진 것이다. */
const TITLE_STOP = new Set(['유니티', 'Unity', 'GOAP', '개발일지', 'shorts', 'Shorts', '특별편']);
const titleWords = s => [...new Set(
  (s || '')
    .replace(/(\d),(\d)/g, '$1$2')                 // 4,096 을 한 덩어리로 (쉼표가 어절을 쪼개지 않게)
    .replace(/[·#,.…?!"'“”‘’·]/g, ' ')
    .split(/\s+/).filter(Boolean)
    .map(w => w.replace(/[을를이가은는의에서도만로]$/, ''))   // 흔한 조사만 턴다
    .filter(w => w.length >= 2)
    .filter(w => !TITLE_STOP.has(w))
    .filter(w => !/^\d+(-\d+)?편$/.test(w))        // "2편"·"5-2편" 은 회차 번호지 약속이 아니다
)];

const titleKeys = titleWords(scene.youtube?.title);
const headText = allLines.slice(0, 2).map(l => `${l.text ?? ''} ${l.say ?? ''}`).join(' ');
const headKeys = titleWords(headText);
const titleHit = titleKeys.filter(k =>
  headKeys.some(h => h === k || h.includes(k) || k.includes(h)));

add(titleKeys.length > 0 && titleHit.length > 0, '제목 이행 (첫 두 줄)',
  titleKeys.length === 0
    ? `🔴 youtube.title 에서 핵심어를 못 뽑았다 — 제목이 비었거나 상투어뿐이다`
    : titleHit.length
      ? `겹침: ${titleHit.join(', ')}`
      : `🔴 제목 핵심어 [${titleKeys.join(', ')}] 가 첫 두 줄에 없다 — `
      + `제목이 약속한 것을 30초 뒤에 주면 대부분 못 보고 나간다`);

/* ── 다음 편 예고 (2026-07-30 사용자 요청 · 2026-08-03 길이 확정) ──
   예고 자체는 사용자 요청이라 있어야 하고, 길이만 잡는다 — 1차본들이 10~14초를 써서
   페이오프 뒤가 늘어졌다. 명세서와 충돌하지 않아 그대로 둔다(명세서는 예고를 다루지 않는다).
   🔴 같은 커밋에 있던 "편당 길이 75/90"과 "첫 자막 3.5초"는 버렸다 —
   앞엣것은 명세서 §관측①(길이는 원인이 아니다)이 기각했고 W3(50초/30~45초)이 대신한다.
   뒤엣것은 §0 틀린 전제 ②("ep02s 는 도입부 빌드업이 길어 초반 이탈")가 실사로 기각된
   추측에 기대고 있었다. 근거 없는 규칙을 만들지 않는다(ADR-V-7). */
const TEASER_MAX = 4.0;
/* 🔴 2026-08-04. 이 선언이 한때 사라져 check.mjs 가 통째로 죽었다. 명세서를 정본으로
   병합하며 "편당 길이 75/90" 블록을 버렸는데 CPS_REF 를 거기서 선언하고 있었고,
   아래 예고 길이 계산이 그걸 쓴다. teaserLines 가 비면 콜백이 안 불려 조용히 지나가지만,
   바로 아래 항목이 예고를 fail 수준으로 **요구**하므로 규칙을 지킨 회차만 골라서 터진다 —
   가장 나쁜 형태의 고장이다. 작성팀이 ep04s-2 를 쓰다 발견해 보고했다.
   🔑 이 값은 예고 한 줄의 길이를 재는 데만 쓴다. 회차 총 길이는 timed.json 실측으로 재고
   (W3), 산정치로 길이를 판정하지 않는다 — 그건 §관측①이 기각한 접근이다. */
const CPS_REF = 6.7;   // ep02s 실측: 발화 84.18초에 564자
const lastShot = scene.shots.at(-1);
const teaserRe = /다음\s*(편|회차|은|는)|이어서|(\d+)\s*부/;
const teaserLines = lastShot.lines.filter(l => teaserRe.test(l.say ?? l.text));
const teaserDur = teaserLines.reduce((a, l) =>
  a + (l.say ?? l.text.replace(/\n/g, ' ')).length / CPS_REF + (l.pauseAfter ?? 0) / 1000, 0);
add(teaserLines.length > 0, '다음 편 예고 있음',
  teaserLines.length ? `${lastShot.id} 자막 ${teaserLines.length}줄` : `마지막 샷(${lastShot.id})에 예고가 없다`);
add(teaserLines.length === 0 || teaserDur <= TEASER_MAX, `예고 ${TEASER_MAX}초 이하`,
  teaserLines.length ? `${teaserDur.toFixed(1)}초` : '예고 없음 — 위 항목 참조', 'warn');

if (timed) {
  const flat = timed.shots.flatMap((s, si) => s.lines.map((l, li) => ({ ...l, shot: scene.shots[si].id })));
  const tooShort = flat.filter(l => (l.dur + (l.pause || 0)) < 2000);
  add(tooShort.length === 0, '자막 2초 미만 없음',
    tooShort.length ? `${tooShort.length}줄 (${tooShort.map(l => `${l.shot}:${((l.dur + l.pause) / 1000).toFixed(1)}s`).join(', ')})` : '전부 2초 이상',
    'warn');

  const cps = timed.summary?.charsPerSec;
  add(cps >= 6.0 && cps <= 7.2, '말 속도 6.0~7.2자/초', `${cps}자/초 (참고 영상 6.93)`, 'warn');

  /* ── 총 길이 ────────────────────────────────────────
     실측 절대 시청 시간이 16~27초인데 영상이 80~122초였다
     (Docs/영상_이탈률_개선_실행명세서.md §관측①). 70~80% 를 아무도 안 본다.
     🔴 30~45 는 제안치다. 이 파이프라인에 길이 상한이 있었던 적이 없어 기존 값이 없다.

     상한(50)과 권장대역(45)을 나눈 이유: 상한은 페널티의 천장이고 권장은 목표다.
     둘을 같은 값으로 합치면 46초짜리가 반려되어 무인 되돌리기 루프가 헛돈다. */
  const totalSec = timed.shots.flatMap(s => s.lines)
    .reduce((a, l) => a + l.dur + (l.pause || 0), 0) / 1000;
  add(totalSec <= 50, '총 길이 50초 이하', `${totalSec.toFixed(1)}초 (목표 30~45초)`);
  add(totalSec >= 30 && totalSec <= 45, '총 길이 30~45초 권장대역',
    `${totalSec.toFixed(1)}초`, 'warn');
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

  /* 🔴 하단 안전영역 — 유튜브가 영상 위에 자기 UI 를 덮는 자리.
     이 점검이 없어서 회차 둘이 그대로 나갔다. 업로드된 ep00s 를 쇼츠 앱에서 찍어 보고서야
     자막 둘째 줄이 채널 아이콘·제목 줄에 통째로 묻혀 있다는 걸 알았다(2026-07-29).
     기존 점검이 전부 캔버스 픽셀만 보는데 자막은 DOM 이라 처음부터 시야 밖이었다.

     1477 의 출처 = 실측. 화면 좌표 역산으로 영상 상단 = 기기 y 108, 배율 1.3333(전체 폭),
     영상 하단 = 2668 → 채널 아이콘 상단 기기 2078 = (2078-108)/1.3333 = 1477.
     ⚠️ 그 화면은 크리에이터 본인 화면이라 `분석`·`공개`·`동영상 공유` 가 오버레이를 위로
     밀어 올린 최악 조건이다. 일반 시청자는 덜 가리지만 최악에 맞춘다.

     상단(0~130)은 안 본다 — 그 값은 실측한 적이 없다. 잰 것만 판정한다.
     오른쪽 세로 버튼 열(x≥930·y≥1090)도 아직 안 본다 — 비주얼이 x 1025 까지 그려서
     지금도 모서리가 겹치는데, 폭을 줄이면 안 가리는 위쪽까지 손해라 미뤘다. 관찰 항목. */
  const SAFE_BOTTOM = 1477;
  const low = await cdp.evaluate(`(()=>{
    const st = document.querySelector('.stage') || document.body;
    const S = st.getBoundingClientRect();
    const y = el => (el.getBoundingClientRect().bottom - S.top) / S.height * 1920;
    const worst = {};
    // 자막은 페이드 중 translateY 로 움직인다. 한 프레임만 보면 안 되고 전 구간에서 최저점을 본다.
    const N = 24;
    for (let i = 0; i < N; i++) {
      window.seek(window.TOTAL * i / (N - 1));
      for (const sel of ['.cap', '.vis']) {
        const el = document.querySelector(sel); if (!el) continue;
        const b = y(el); if (!(worst[sel] >= b)) worst[sel] = b;
      }
    }
    return worst;
  })()`);
  const over = Object.entries(low).filter(([, v]) => v > SAFE_BOTTOM);
  add(over.length === 0, `하단 안전영역 (바닥 ${1920 - SAFE_BOTTOM}px 비움)`,
    over.length
      ? over.map(([s, v]) => `${s} 바닥 ${v.toFixed(0)} > 한계 ${SAFE_BOTTOM}`).join(', ')
      : `자막 바닥 ${low['.cap']?.toFixed(0)} · 비주얼 바닥 ${low['.vis']?.toFixed(0)} (한계 ${SAFE_BOTTOM})`);

  frame = await cdp.evaluate(`(async () => {
    const FPS = ${FPS}, N = Math.max(2, Math.floor(window.TOTAL/1000*FPS));
    const per = {};
    let prev = null, prevId = null;
    for (let i = 0; i < N; i++) {
      window.seek(i / FPS * 1000);
      const el = document.querySelector('.shot.on');
      const id = el?.dataset.id; if (!id) continue;
      const cv = el.querySelector('canvas');
      if (!per[id]) per[id] = { frames:0, bad:0, total:0, staticRun:0, maxStatic:0, edgeBot:0, edgeSide:0, edgeSideFrames:0 };
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

      /* 가장자리에 칠이 닿는가 = 밖으로 나간 것이 잘린 흔적.
         🔴 오래 아래쪽만 봤다. 그래서 ep00s 는 도장이 오른쪽으로 잘린 채 나갔고(사람이 잡음),
         ep02s 첫 무인 실행은 "GOAP 목표 : HungerLevel" 이 "HungerLe" 로 잘린 채
         **점검 13종을 전부 통과했다.** 무인이면 눈이 없으니 기계가 봐야 한다.
         ⚠️ 이 블록은 템플릿 문자열 안이다 — 주석에 백틱을 쓰면 문자열이 끊긴다(실제로 겪었다).
         좌우도 같은 방법으로 잴 수 있었는데 안 재고 있었을 뿐이다. */
      const W=cv.width, H=cv.height;
      let e=0; const row=g.getImageData(0,H-1,W,1).data;
      for (let x=0;x<W;x++) if (row[x*4+3] > 10) e++;
      P.edgeBot = Math.max(P.edgeBot, e);

      /* 좌우는 아래와 다르게 재야 한다. 두 번 틀리고 나서 얻은 규칙이다.
         ① **맨 끝 한 열만 보면 놓친다.** ep02s S4 의 잘린 라벨은 969열이 0 이고 968열부터
            잉크가 있었다(안티에일리어싱이 마지막 열에서 죽는다). → 바깥 4열을 띠로 본다.
         ② **한 프레임이라도 닿으면 실패로 하면 오탐한다.** 화면을 쓸고 지나가는 그림
            (ep01s 의 erasure)은 가장자리를 지나는 게 설계다. → "몇 프레임이나 닿아 있었나"를
            센다. 스쳐 가는 연출은 잠깐이고, **잘린 글자는 몇 초씩 그 자리에 서 있다.** */
      /* 띠 폭 2 는 실측으로 정했다. 4 로 잡았더니 ep01s 의 shelf 가 100% 로 걸렸는데
         눈으로 보니 안 잘렸다 — 가장자리에서 **3px 안쪽**에 정상적으로 그려진 요소였다.
         진짜 잘린 글자(ep02s S4)는 **1px 앞**에 잉크가 선다(맨 끝 열은 안티에일리어싱으로 비어도).
         2 로 좁히니 승인된 두 회차는 통과하고 잘린 것만 남았다. */
      const BAND = 2;
      let sx = 0;
      for (let i = 0; i < BAND; i++) {
        const l = g.getImageData(i, 0, 1, H).data, r = g.getImageData(W - 1 - i, 0, 1, H).data;
        for (let y = 0; y < H; y++) { if (l[y*4+3] > 10) sx++; if (r[y*4+3] > 10) sx++; }
      }
      if (sx >= 3) { P.edgeSideFrames++; P.edgeSide = Math.max(P.edgeSide, sx); }

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

  /* 잘린 글자는 몇 초씩 서 있고, 쓸고 지나가는 그림은 스친다. 그 차이로 가른다.
     샷 프레임의 4분의 1 넘게 가장자리에 붙어 있으면 잘린 것으로 본다. */
  const side = rows.filter(([id, v]) => !edgeSkip[id] && v.frames && v.edgeSideFrames / v.frames > 0.25);
  add(side.length === 0, '좌우 가장자리 잘림 없음',
    side.length
      ? side.map(([id, v]) => `${id} ${Math.round(v.edgeSideFrames / v.frames * 100)}% 구간 · 최대 ${v.edgeSide}px`).join(', ')
      : '없음' + (Object.keys(edgeSkip).length ? ` (선언 예외: ${Object.keys(edgeSkip).join(', ')})` : ''));

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
