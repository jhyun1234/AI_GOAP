/* 회차 대본 → 유튜브 업로드용 영어 자막(.srt)
   사용: node tools/scene-video/srt.mjs ep06s

   만드는 것
     episodes/<ep>/build/<ep>.en.srt   스튜디오 → 자막/언어 → 영어(미국) → 파일 업로드

   🔴 여기서 번역하지 않는다. `lines[].en` 을 그대로 조립할 뿐이다.
      번역은 대본을 쓸 때 원문 옆에 함께 쓰인다(명세 ADR-V-16) — 그래야 검수팀이 한 줄씩
      대조해서 지어낸 내용과 딱딱한 표현을 잡는다. 조립기가 번역을 알면 그 자리가 사라진다.

   🔴 타이밍을 여기서 계산하지 않는다. **엔진이 만든 타임라인을 그대로 읽는다.**
      engine.js 의 buildTimeline 은 ①줄 길이에 pauseAfter 를 더하고 ②샷마다 끝에
      SHOT_TAIL(0.35s)을 붙인다. 이걸 여기서 다시 구현했다가 실제로 틀렸다 —
      1차본이 둘 다 빠뜨려서 회차 끝에서 자막이 1.4초 빨랐다(ep05s-3 실측).
      render.mjs 도 같은 `window.__timeline()` 을 읽으므로, 이렇게 하면 영상과 자막이
      **정의상** 어긋날 수 없다.

   🔑 자막이 사라지는 시각도 엔진을 따른다. 엔진은 침묵(pauseAfter) 동안 자막을 그대로 둔다
      — "사람이 말을 멈춘 것이지 자막이 끝난 게 아니다"(engine.js). 영어 자막만 먼저 사라지면
      한국어 번인 자막과 깜빡임이 어긋난다.
*/
import fs from 'fs';
import path from 'path';
import { ROOT, epScene, epBuild, openEngine } from './lib-node.mjs';

const argv = process.argv.slice(2);
const EP = argv.find(a => !a.startsWith('--')) || 'ep01s';

const scene = JSON.parse(fs.readFileSync(epScene(EP), 'utf8'));
const flat = scene.shots.flatMap(s => s.lines.map(l => ({ ...l, shot: s.id })));

/* 번역 누락은 조용히 넘어가지 않는다 — 빠진 줄만큼 자막이 통째로 비어 나간다.
   몇 번째 줄인지, 원문이 무엇인지까지 말한다. 그래야 바로 고칠 수 있다. */
const missing = flat
  .map((l, i) => ({ i, l }))
  .filter(({ l }) => !l.en?.trim());
if (missing.length) {
  console.error(`${EP}: 영어 번역(en)이 없는 줄 ${missing.length}개`);
  for (const { i, l } of missing)
    console.error(`  ${i + 1}번째 (${l.shot}) ${JSON.stringify(l.text.replace(/\n/g, ' '))}`);
  process.exit(1);
}

/* 타임라인이 **실측인지** 확인한다.
   🔴 여기가 이 스크립트에서 가장 조용히 틀릴 수 있는 자리다. engine.js 의 buildTimeline 은
   실측(timed.json)이 없는 줄을 글자 수 추정치(provisionalDur)로 **말없이** 채운다.
   그래서 "타임라인 줄 수 == 대본 줄 수"는 언제나 참이라 아무것도 못 잡는다(1차본의 검사가
   그랬다). 대본만 고치고 tts.mjs 를 안 돌리면 그 줄만 추정치로 나가고, 자막이 음성과
   어긋난 채 조용히 발행된다.
   → 줄 수가 아니라 **줄 내용**을 대조한다. tts.mjs 가 timed.json 에 각 줄의 `say` 를
     적어 두므로, 그것이 지금 대본과 같은지 보면 그 줄의 실측이 최신인지 알 수 있다. */
const timedPath = epBuild(EP, 'timed.json');
if (!fs.existsSync(timedPath)) {
  console.error(`episodes/${EP}/build/timed.json 이 없다 — tts.mjs 를 먼저 돌려라.\n` +
    `  (없으면 엔진이 글자 수로 어림한 타임라인을 쓰고, 자막이 음성과 어긋난다)`);
  process.exit(1);
}
const timed = JSON.parse(fs.readFileSync(timedPath, 'utf8'));
const timedFlat = timed.shots.flatMap(s => s.lines);
const stale = [];
if (timedFlat.length !== flat.length)
  stale.push(`줄 수: 대본 ${flat.length} ≠ 실측 ${timedFlat.length}`);
else flat.forEach((l, i) => {
  const want = l.say || l.text;
  if (timedFlat[i].say !== want) stale.push(`${i + 1}번째 (${l.shot}) 대본과 실측의 대사가 다르다`);
});
if (stale.length) {
  console.error(`${EP}: 실측 타임라인이 지금 대본과 안 맞는다 — tts.mjs 를 다시 돌려라`);
  for (const s of stale.slice(0, 5)) console.error(`  ${s}`);
  process.exit(1);
}

const { cdp, close } = await openEngine(EP, { quiet: true });
let timeline;
try { timeline = await cdp.evaluate('window.__timeline()'); } finally { close(); }

// 위에서 실측과의 일치를 이미 확인했다. 여기는 엔진이 예상 밖의 것을 준 경우의 마지막 그물.
if (timeline.lines.length !== flat.length)
  throw new Error(`대본 ${flat.length}줄 ≠ 엔진 타임라인 ${timeline.lines.length}줄`);

/* SRT 시각 형식: 00:00:04,175 (쉼표다. 마침표가 아니다) */
const ts = ms => {
  const p = (n, w = 2) => String(Math.floor(n)).padStart(w, '0');
  return `${p(ms / 3600000)}:${p(ms / 60000 % 60)}:${p(ms / 1000 % 60)},${p(ms % 1000, 3)}`;
};

const cues = flat.map((l, i) => {
  const t = timeline.lines[i];
  return `${i + 1}\n${ts(t.t)} --> ${ts(t.t + t.dur)}\n${l.en}\n`;
});

const out = epBuild(EP, `${EP}.en.srt`);
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, cues.join('\n'), 'utf8');   // 🔴 UTF-8 — 유튜브가 요구한다

const last = timeline.lines.at(-1);
console.log(`${EP} · 영어 자막 ${cues.length}줄`);
console.log(`  ${path.relative(process.cwd(), out)}`);
console.log(`  마지막 자막 ${(last.t / 1000).toFixed(1)}s ~ ${((last.t + last.dur) / 1000).toFixed(1)}s` +
  ` · 영상 ${(timeline.totalMs / 1000).toFixed(1)}s`);
