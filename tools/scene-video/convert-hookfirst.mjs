/* 선(先)사건 후(後)설명 전환 — ADR-V25-17. 구 구조 회차를 신 구조로 기계 변환한다.
   사용: node convert-hookfirst.mjs <ep> [<ep>...]

   하는 일 셋뿐이다(그 밖은 한 글자도 안 건드린다):
     ① shots[0](인트로) 와 shots[1](훅) 자리를 맞바꾼다
     ② 훅 첫 줄에서 보일러플레이트(「오늘 개발 일지 내용은,」/「Today's devlog:」)를 뗀다
     ③ 인트로 문안을 3.0초 상한 압축본으로 바꾼다
   🔴 그림(kind)·효과음·본문·아웃트로·제목은 그대로다 — 전환이 실패해도 되돌릴 것이 셋뿐이게. */
import fs from 'fs';
import path from 'path';
import { ROOT } from './lib-node.mjs';

/* 압축 인트로. 🔴 「로만」을 지킨다 — §4 반과장 포지셔닝의 핵심어라 압축에서 잃으면 안 된다.
   say 의 `AI` 는 TTS 가 못 읽으므로 「에이아이」로 적는다(자막은 AI 그대로). */
const INTRO = {
  text: 'Claude Code로만 만든\nAI 마을 개발일지',
  say: '클로드 코드로만 만든 에이아이 마을이에요.',
  en: 'An AI village built only with Claude Code',
};

const BOILER_KO = /^오늘\s*개발\s*일지\s*내용은[,，]?\s*/;
const BOILER_EN = /^Today['’]s\s+devlog[:,]?\s*/i;

for (const ep of process.argv.slice(2)) {
  const p = path.join(ROOT, 'episodes', ep, 'scene.json');
  const s = JSON.parse(fs.readFileSync(p, 'utf8'));

  if (s.shots[0]?.kind !== 'intro') { console.log(`${ep}: 이미 신 구조 — 건너뜀`); continue; }

  const intro = s.shots[0], hook = s.shots[1];

  // ② 보일러플레이트 제거 — 첫 글자가 소문자로 남지 않게 en 은 첫 자를 올린다
  const h = hook.lines[0];
  h.text = h.text.replace(BOILER_KO, '').replace(/^\n/, '');
  h.say = h.say.replace(BOILER_KO, '');
  if (h.en) h.en = h.en.replace(BOILER_EN, '').replace(/^./, c => c.toUpperCase());

  // ③ 인트로 압축
  Object.assign(intro.lines[0], INTRO);

  // ① 자리 교체
  s.shots[0] = hook; s.shots[1] = intro;

  s.notes = s.notes || {};
  s.notes['구조'] = `🔄 선(先)사건 후(後)설명 전환 (ADR-V25-17 · 2026-08-12 · convert-hookfirst.mjs). `
    + `구 구조는 사건이 6.8~7.0초에 시작했다(인트로 4.6 + 보일러플레이트 ~2.3). `
    + `자리 교체 + 보일러플레이트 제거 + 인트로 압축 셋만 했고 그림·효과음·본문·제목은 불변이다. `
    + `측정용 전환분(3편) — 이미 나간 ep14s-1·2·3 과 「계속 시청함 %」를 비교한다.`;

  fs.writeFileSync(p, JSON.stringify(s, null, 2));
  console.log(`${ep}: 전환 · 첫 줄 「${h.say.slice(0, 28)}…」`);
}
