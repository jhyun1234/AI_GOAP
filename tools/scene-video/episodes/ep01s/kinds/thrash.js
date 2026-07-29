import {
  disp, ease, easeOut, clamp, lerp, rnd, fitCanvas, mkCanvas, tone, roundRect } from '../../../engine/lib.js';

/* thrash — 계획을 못 짜고 헤매는 구간. 이 회차에서 "격동"이 있어야 할 자리다.

   ep01 정본의 plan(mode:"thrash") 는 카드 하나가 조용히 밝아지는 정도였다.
   여기서 바뀐 것 둘:
     ① 탐색 머리가 t 의 함수로 계속 튄다 — 자막이 흐르는 동안 화면이 멈추지 않는다.
     ② 예산 막대가 같이 줄어든다. 헤맴에 대가가 붙어야 답답함이 성립한다.

   튀는 순서는 rnd(정수) 로 뽑는다. Math.random 을 쓰면 seek 이 순수 함수가 아니게 되어
   같은 시각을 두 번 그렸을 때 그림이 달라진다(= mp4 추출 불가).

   호명 규칙은 정본에서 그대로 가져온다 — 나레이션이 카드 이름을 부르는 순간
   그 카드가 켜져야 한다. named: 0,1,2 가 부르는 순서다. */

const HOP = 7.5;   // 초당 탐색 머리가 옮겨가는 횟수

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, dur, cue, since, lines, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cards = spec.cards || [];
    const budget = spec.budget || 4096;

    const goalH = spec.goal ? 30 : 0;
    const meterH = 54;
    const gridTop = goalH;
    const gridH = h - goalH - meterH;

    const cols = 2, rows = Math.ceil(cards.length / cols);
    const gapX = 8, gapY = 7;
    const cw = (w - gapX * (cols - 1)) / cols;
    const ch = (gridH - gapY * (rows - 1)) / rows;

    /* ── 목표 ────────────────────────────────────── */
    if (spec.goal) {
      const gk = ease(cue(0, 0.3, 0.25));
      ctx.globalAlpha = 0.25 + 0.75 * gk;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.font = disp(700, 11); ctx.textAlign = 'left';
      const tw = ctx.measureText(spec.goal).width;
      roundRect(ctx, 1.5, 1.5, tw + 18, 21, 3); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.goal, 11, 16);
      ctx.globalAlpha = 1;
    }

    /* ── 호명 ─────────────────────────────────────
       카드 이름을 부르는 자막 안에서 순서대로 켜지고, 켜진 채 남는다. */
    const nameLine = spec.nameCue ?? 1;
    const named = cards.map((c, i) => ({ i, o: c.named }))
      .filter(x => x.o != null).sort((a, b) => a.o - b.o);
    const nameT = since(nameLine), nameDur = lines[nameLine]?.dur || 1;
    const spoken = nameT < -0.1 ? -1
      : Math.floor(clamp((nameT + 0.1) / (nameDur * 0.88)) * named.length);

    /* ── 탐색 머리 ────────────────────────────────
       엉뚱한 후보(decoy) 사이만 튄다. 정답 카드에는 끝내 안 앉는다 — 그게 이 샷의 내용이다. */
    const decoys = cards.map((c, i) => c.decoy ? i : -1).filter(i => i >= 0);
    const hop = Math.floor(t * HOP);
    const headIdx = decoys[Math.floor(rnd(hop * 31 + 7) * decoys.length) % decoys.length];
    const hopK = clamp((t * HOP - hop) * 2.6);     // 앉자마자 밝고 다음 칸으로 갈수록 옅어진다

    /* ── 카드 ────────────────────────────────────── */
    cards.forEach((c, i) => {
      const cx0 = (i % cols) * (cw + gapX);
      const cy0 = gridTop + Math.floor(i / cols) * (ch + gapY);
      const order = named.find(x => x.i === i)?.o;
      const lit = order != null && spoken >= order;
      const head = i === headIdx && !c.target;

      ctx.lineWidth = 3;
      let filled = true;
      if (lit) {
        ctx.fillStyle = tone('ink'); ctx.strokeStyle = tone('ink');
      } else if (head) {
        ctx.fillStyle = `rgba(255,255,255,${(0.30 - 0.16 * hopK).toFixed(3)})`;
        ctx.strokeStyle = tone('ink');
      } else {
        filled = false; ctx.strokeStyle = tone('track');
      }
      roundRect(ctx, cx0 + 1.5, cy0 + 1.5, cw - 3, ch - 3, 3);
      if (filled) ctx.fill();
      ctx.stroke();

      // 정답 카드 — 내내 거기 있는데 한 번도 열리지 않는다
      if (c.target) {
        ctx.setLineDash([5, 5]);
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, cx0 + 1.5, cy0 + 1.5, cw - 3, ch - 3, 3); ctx.stroke();
        ctx.setLineDash([]);
      }

      ctx.textAlign = 'left';
      ctx.font = disp(800, 13);
      ctx.fillStyle = lit ? '#000000' : c.target ? 'rgba(255,255,255,.35)' : tone('sub');
      ctx.fillText(c.label, cx0 + 11, cy0 + ch / 2 + 5);
    });

    /* ── 예산 막대 ────────────────────────────────
       두 겹이다. 바닥에 깔린 건 샷 시작부터 t 로 계속 새는 몫(85%까지) —
       자막 사이에도 대가가 붙고 있어야 헤맴이 답답해진다.
       나머지는 마지막 자막에 물려 바닥을 치고 도장이 찍힌다. */
    const my = h - meterH + 14;
    const steady = clamp(t / (dur * 0.95)) * 0.85;
    const drain = clamp(Math.max(steady, easeOut(clamp(cue(spec.drainCue ?? nLines - 1, 0.2, 0.55)))));
    const left = Math.max(0, Math.round(budget * (1 - drain)));

    ctx.fillStyle = 'rgba(255,255,255,.12)';
    ctx.fillRect(0, my, w, 12);
    ctx.fillStyle = tone('ink');
    ctx.fillRect(0, my, w * (1 - drain), 12);

    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText('남은 예산', 0, my - 8);
    ctx.textAlign = 'right';
    ctx.fillStyle = drain > 0.98 ? tone('sub') : tone('ink');
    ctx.font = disp(900, 22);
    ctx.fillText(left.toLocaleString(), w, my - 4);

    if (drain > 0.98 && spec.verdict) {
      ctx.fillStyle = tone('ink');
      ctx.font = disp(900, 15);
      ctx.fillText(spec.verdict, w, my + 34);
    }
    ctx.textAlign = 'left';
  }
};
