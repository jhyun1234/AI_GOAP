import { ease, clamp, disp, mono, tone, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* written — 사건. 회차 축(「한 칸의 폭」)을 **한 줄로 확대**한 배치다.

   위: 「간호」 한 줄이 화면 폭을 다 쓰는 카드로 커져 있고, 오른쪽 값 칸에
       강조색 `1` 이 **위에서 아래로 그어지며** 적힌다(적는 행위 자체가 사건이다).
   아래: 그 카드와 **아무 선도 안 이어진** 자리에서 「돌 캐기 · 계획」 로그가
       `NoSolutionFound` 를 찍기 시작하고, 같은 줄이 계속 위로 밀려 쌓인다.

   🔴 두 영역 사이에 연결선을 한 획도 안 그렸다. 이 편의 사건이 정확히
      「아무 상관 없어 보이는 두 곳」이기 때문이다 — 선을 그으면 그림이 답을
      먼저 말해 버린다. 잇는 것은 S2 의 자다.

   🔴 로그가 한 줄이 아니라 **계속 쌓이는** 이유는 원문이 "예상하지 못한 에러가
      콘솔에 찍히기 시작했습니다" 라고 적었기 때문이다(한 번이 아니다).
      이 스크롤이 이 샷의 정적 구간 대책이기도 하다 — 폭 130px 짜리 글줄 넷이
      매 프레임 세로로 움직이므로 갱신 면적이 넉넉하다.

   🔴 `NoSolutionFound` 만 mono 다(ASCII 인용). 한국어 라벨은 전부 disp —
      SceneMono 에 한글이 없어 한 줄 안에서 폰트가 갈리고 폴백이 머신마다 다르다.

   세로 예산(캔버스 307) — 카드 62~130 · 값 칸 74~118 · 구획 이름 168 ·
   로그 클립 176~272. 최저 272. */

const M = 18;
const CARD_Y = 62, CARD_H = 68;
const BOX_W = 58, BOX_H = 44, BOX_Y = 74;
const LOG_TOP = 176, LOG_BOT = 272;
const LINE_H = 27;
const PER = 1.05;          // 로그 한 줄이 새로 찍히는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const k0 = ease(cue(spec.costCue ?? 0, 0.15, 0.68));

    /* ── 위 : 「간호」 한 줄 ────────────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, M, CARD_Y, w - M * 2, CARD_H, 6); ctx.stroke();

    ctx.font = disp(800, 20); ctx.fillStyle = tone('ink');
    ctx.fillText(spec.actionLabel ?? '', M + 20, 104);

    ctx.font = disp(700, 12.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.fieldLabel ?? '', M + 92, 104);

    /* 값 칸 — 카드 테두리에서 24px 안쪽이라 강조색이 흰 테두리에 안 닿는다 */
    {
      const bx = w - M - 24 - BOX_W;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bx, BOX_Y, BOX_W, BOX_H, 4); ctx.stroke();

      /* 숫자는 위에서 아래로 드러난다 = 「적는다」.
         클립 상자를 값 칸 안쪽(테두리 반폭 밖)으로 잡아 획이 테두리를 안 먹는다. */
      const s = String(spec.cost ?? '');
      if (s && k0 > 0.02) {
        ctx.save();
        ctx.beginPath();
        ctx.rect(bx + 3, BOX_Y + 3, BOX_W - 6, (BOX_H - 6) * clamp(k0 * 1.15));
        ctx.clip();
        ctx.font = disp(900, 32);
        const tw = ctx.measureText(s).width;
        ctx.fillStyle = tone('accent');
        ctx.fillText(s, bx + (BOX_W - tw) / 2, BOX_Y + 34);
        ctx.restore();
      }
    }

    /* ── 아래 : 콘솔 — 잇는 선은 한 획도 없다 ───────── */
    ctx.font = disp(700, 12.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.logLabel ?? '', M, 168);

    const s1 = since(spec.logCue ?? 1) + 0.15;   // 자막보다 0.15초 앞선다
    if (s1 <= 0) return;

    const line = spec.logLine ?? '';
    const off = s1 / PER;
    const idx = Math.floor(off);
    const fr = off - idx;

    ctx.save();
    ctx.beginPath();
    ctx.rect(M, LOG_TOP, w - M * 2, LOG_BOT - LOG_TOP);
    ctx.clip();
    ctx.font = mono(700, 15);

    for (let j = 0; j <= 3; j++) {
      if (idx - j < 0) continue;
      const y = LOG_BOT - (j + fr) * LINE_H;
      /* 맨 아래 줄만 한 글자씩 찍힌다 */
      const txt = j === 0
        ? line.slice(0, Math.max(1, Math.round(line.length * clamp(fr * 1.6))))
        : line;
      ctx.save();
      ctx.globalAlpha = clamp(1 - j * 0.24);
      ctx.fillStyle = tone('ink');
      ctx.fillText(txt, M + 6, y);
      ctx.restore();
    }
    ctx.restore();
  }
};
