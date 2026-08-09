import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* handraise — 구현 칸을 채워 가던 손이 한 칸 앞에서 멈춘다. 그 자리에서 아래로
   내려가는 대신 위로 선택지 칸 셋과 추천 표식 하나가 올라온다.
   이 회차의 표기대로 선택지 칸은 전부 AI 색이고, 그 위에 앉을 결정 표식만 사람 색이다
   — 이 샷에서는 아직 앉지 않는다(S11 이 그걸 그린다).
   연속 모션 = 멈춘 칸 위에서 계속 오르내리는 질문 화살. 손은 멈췄지만 질문은 계속
   올라간다는 뜻이고, 그게 이 조항의 내용이다.
   문자열 출처: Docs/CLAUDE.md 작업 프로토콜 · Docs/개발_방법론_명세서.md 211~215행. */

const OPTS = ['A', 'B', 'C'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kRule = ease(cue(0));    // 규칙 문서에 이런 한 줄
    const kStop = ease(cue(1));    // 구현하지 말고
    const kOpt = ease(cue(2));     // 선택지를 늘어놓고
    const kOwn = ease(cue(3));     // 방향은 사용자의 몫

    // ── 아래 : 구현 칸 줄 ──────────────────────────
    const ry = h - 62, cw = 56, gap = 9;
    const n = 6;
    const x0 = M;

    /* 🔴 2차 개정 (검수 C6): 연속 모션이 3px 화살 흔들림뿐이라 문턱 아래였다.
       구현 칸 줄을 훑어 가는 48px 폭 띠를 세운다 — 「채워 가던 손」이 이 샷의 주어다.
       칸보다 **먼저** 그린다(강조색 위에 흰색 금지). */
    {
      const BR = 2.9, bk = (t % BR) / BR;
      const bx0 = lerp(x0 - 20, x0 + n * (cw + gap) + 20, bk);
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx0 - 24, ry - 34, 48, 82);
    }
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('IMPLEMENT', M, ry - 12);

    const stopIdx = 3;
    for (let i = 0; i < n; i++) {
      const cx = x0 + i * (cw + gap);
      const done = i < stopIdx;
      const k = clamp((kRule - i * 0.09) / 0.4);
      ctx.strokeStyle = done && k > 0.1 ? tone('accent') : tone('track');
      ctx.lineWidth = i === stopIdx ? 4 : 3;
      if (i === stopIdx) ctx.strokeStyle = kStop > 0.15 ? tone('ink') : tone('track');
      roundRect(ctx, cx, ry, cw, 34, 3); ctx.stroke();
      if (done && k > 0.05) {
        ctx.fillStyle = tone('accent');
        ctx.fillRect(cx + 7, ry + 15, (cw - 14) * ease(k), 5);
      }
      if (i === stopIdx && kStop > 0.3) {
        ctx.font = mono(700, 10); ctx.fillStyle = tone('ink');
        const s = 'STOP';
        const sw = ctx.measureText(s).width;
        ctx.fillText(s, cx + (cw - sw) / 2, ry + 22);
      }
    }

    // ── 연속 모션 : 멈춘 칸 위 질문 화살 ────────────
    {
      const cx = x0 + stopIdx * (cw + gap) + cw / 2;
      const bob = 6 * Math.sin(t * 2.6);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(cx, ry - 6 + bob); ctx.lineTo(cx, ry - 26 + bob);
      ctx.moveTo(cx - 7, ry - 18 + bob); ctx.lineTo(cx, ry - 26 + bob);
      ctx.lineTo(cx + 7, ry - 18 + bob);
      ctx.stroke();
    }

    // ── 위 : 선택지 칸 셋 + 추천 ────────────────────
    const ox = w - M - 300, oy = 44, ow = 92, oh = 52, og = 12;
    /* 🔴 3차 개정 (검수 N5): 라벨 baseline 이 `oy - 12`(= 32) 이고 추천 표식 ▼ 가
       `oy + rise - 6` ~ `oy + rise - 18`(= 26~38) 에 섰다 — 라벨과 표식이 **같은
       가로 띠를 나눠 쓰도록 설계돼 있어** 구조적으로 끝 두 글자를 덮었다
       (증거 `build/stills/294s.png` — `RECOMMENDATI◤N`).
       라벨을 위로 올릴 수는 없다: 엔진 HUD 진행 막대가 캔버스 y 13~17 · x 328~591 을
       실제로 덮고 있다(같은 스틸에서 실측). 아래로 내릴 수도 없다 — 칸이 26px 아래에서
       올라오므로 상승 구간에 라벨을 지나간다.
       → **표식을 칸 안으로 들였다**(아래 참조). 라벨 자리는 그대로 둔다. */
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('OPTIONS + 1 RECOMMENDATION', ox, oy - 12);

    for (let i = 0; i < OPTS.length; i++) {
      const cx = ox + i * (ow + og);
      const k = clamp((kOpt - i * 0.16) / 0.45);
      if (k <= 0.02) continue;
      const rise = lerp(26, 0, ease(k));
      ctx.save(); ctx.globalAlpha = clamp(k);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, cx, oy + rise, ow, oh, 3); ctx.stroke();
      ctx.font = disp(900, 20); ctx.fillStyle = tone('accent');
      const sw = ctx.measureText(OPTS[i]).width;
      ctx.fillText(OPTS[i], cx + (ow - sw) / 2, oy + rise + 34);
      // 트레이드오프 두 줄
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(cx + 10, oy + rise + oh - 12); ctx.lineTo(cx + ow - 10, oy + rise + oh - 12);
      ctx.stroke();
      ctx.restore();
      /* 추천 표식 — 두 번째 칸 **안**, 글자 왼쪽 (3차 개정 N5).
         칸 안 왼쪽 여백(x cx+11 ~ cx+33)은 비어 있다 — 'B' 는 가운데 정렬이라
         cx+39 부터 시작하고, 트레이드오프 선은 y = oy+rise+oh-12 에 있다.
         표식 상자는 x cx+11~cx+25 · y oy+rise+22~34 라 칸 테두리(±1.5px)·글자·
         트레이드오프 선 어디와도 4px 이상 떨어진다. */
      if (i === 1 && kOpt > 0.6) {
        ctx.save(); ctx.globalAlpha = clamp((kOpt - 0.6) / 0.4);
        ctx.fillStyle = tone('accent');
        ctx.beginPath();
        ctx.moveTo(cx + 18, oy + rise + 34);
        ctx.lineTo(cx + 11, oy + rise + 22);
        ctx.lineTo(cx + 25, oy + rise + 22);
        ctx.closePath(); ctx.fill();
        ctx.restore();
      }
    }

    // ── 결정 자리 : 사람 칸 (아직 비어 있다) ─────────
    if (kOwn > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kOwn);
      const dx = M, dy = 44, dw = 150, dh = 52;
      ctx.setLineDash([8, 6]);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, dx, dy, dw, dh, 3); ctx.stroke();
      ctx.setLineDash([]);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('DECISION', dx, dy - 12);
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      const s = '사람 몫';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, dx + (dw - sw) / 2, dy + 33);
      ctx.restore();
    }
  },
};
