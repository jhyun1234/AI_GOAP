import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* orphan — 닿는 선이 하나도 없다.

   원문 39행이 말하는 '아키텍처의 틈'. 성격을 실어 나르는 경로(RecruitmentSystem)가
   위에서 한 줄로 계속 흐르고 있고, 아래에는 씬에 직접 배치한 주민 셋이 서 있다.
   둘 사이에는 막힌 문도, 갈라진 갈래도 없다 — **아무것도 없다.** 셋에게서 위로 뻗다 만
   점선 토막만 있고 그 끝에서 신호가 올라가려다 사라진다. 그 빈 세로 간격이 곧 '틈'이다.

   🔴 갈래로 그리지 않았다. ep00s channels 는 하나가 네 갈래로 갈리고 그중 하나에만 값이
   떨어지는 그림이고, ep00s fork 는 하나가 이름 셋으로 갈리는 그림이다. 갈래로 그리면
   '이 셋도 경로의 일부인데 값을 못 받았다'로 읽힌다. 원문은 그게 아니라 **경로 자체를
   지나지 않는다**고 말한다. 그래서 두 층을 아예 잇지 않았다.
   🔴 문턱으로도 그리지 않았다. 바로 다음 샷(S3 wardline)이 문턱이라 두 번 연속이 된다.

   아래 두 칸(대사·행동 비용)은 원문 33행이 '성격에 따라 달라진다'고 이름 붙인 두 항목이다.
   셋에게서 내려온 선이 두 칸에 닿지만 칸은 비어 있다 — S1 의 빈 말풍선이 왜 절반짜리
   증상인지가 여기서 드러난다. (칸이 비어 있다는 것까지만 그린다. '그래서 행동도 기본값으로
   돌았다'는 원문에 없는 추론이라 화면에도 자막에도 없다.)

   계속 도는 것 = ① 경로를 왼쪽에서 오른쪽으로 계속 흐르는 값 ② 끊긴 토막 끝에서 위로
   올라가려다 틈 앞에서 사라지는 신호. 둘 다 t 의 함수다. */

const FLOW = 1.9;     // 경로를 한 번 건너는 초
const REACH = 2.4;    // 끊긴 토막 끝에서 신호가 한 번 올라갔다 사라지는 초

/* 세로 배치 (캔버스 352×307 기준)
     48 경로 라벨 / 62 경로 / 82 경로가 하는 일
     90~110 틈(빈 간격) — 눈금 두 개와 라벨
     120~148 끊긴 토막 / 164 주민 셋 / 194 셋의 정체 / 202~228 None 딱지
     245~285 성격이 정하는 두 칸 — 바닥에서 22px 띄운다 */
const LANE_Y = 62;
const ROW_Y = 164;

/** 폭을 재서 글자가 상자·캔버스를 넘지 않게 크기를 줄인다 */
function fitFont(ctx, text, maxW, make, start, min) {
  let s = start;
  ctx.font = make(s);
  while (s > min && ctx.measureText(text).width > maxW) { s -= 0.5; ctx.font = make(s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const n = Math.max(1, spec.count || 3);
    const traits = spec.traits || [];

    const gk = ease(cue(spec.gapCue ?? 0, 0.15, 0.55));
    const trk = ease(cue(spec.traitCue ?? 1, 0.15, 0.5));
    const lk = ease(cue(spec.laneCue ?? 2, 0.15, 0.5));
    const nk = ease(cue(spec.noneCue ?? 3, 0.15, 0.5));

    const PAD = 14;
    ctx.textBaseline = 'alphabetic';

    /* ── 위층 : 성격을 실어 나르는 경로 ──────────────
       이 줄은 처음부터 그려져 있고 laneCue 에서 밝아진다. 값은 t 로 계속 흐른다. */
    {
      const x0 = PAD + 2, x1 = w - PAD - 14;
      const alpha = 0.45 + 0.55 * lk;

      // 라벨 (ASCII 라 mono)
      ctx.textAlign = 'left';
      ctx.globalAlpha = alpha;
      ctx.fillStyle = lk > 0.4 ? tone('accent') : tone('sub');
      const lab = spec.laneLabel || '';
      fitFont(ctx, lab, w - PAD * 2 - 10, s => mono(700, s), 11, 8);
      ctx.fillText(lab, x0, LANE_Y - 14);

      // 경로 자체
      ctx.lineWidth = 4;
      ctx.strokeStyle = lk > 0.4 ? tone('accent') : tone('sub');
      if (lk > 0.4) setShadow(ctx, GLOW, 10, 0);
      ctx.beginPath(); ctx.moveTo(x0, LANE_Y); ctx.lineTo(x1, LANE_Y); ctx.stroke();
      clearShadow(ctx);

      // 흐르는 값 — 계속 도는 것 ①
      for (let i = 0; i < 5; i++) {
        const u = frac(t / FLOW + i / 5);
        const px = lerp(x0, x1, u);
        ctx.globalAlpha = alpha * (0.35 + 0.65 * Math.sin(Math.PI * u));
        ctx.fillStyle = lk > 0.4 ? tone('accent') : tone('ink');
        ctx.beginPath(); ctx.arc(px, LANE_Y, 4.5, 0, Math.PI * 2); ctx.fill();
      }

      // 도착점 — 값이 실려 나간다
      ctx.globalAlpha = alpha;
      ctx.fillStyle = lk > 0.4 ? tone('accent') : tone('sub');
      if (lk > 0.4) setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath(); ctx.arc(x1 + 9, LANE_Y, 8, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);

      // 이 경로가 하는 일 (원문 39행)
      ctx.globalAlpha = alpha;
      ctx.fillStyle = tone('sub');
      const note = spec.laneNote || '';
      fitFont(ctx, note, w - PAD * 2 - 10, s => disp(700, s), 10, 8);
      ctx.fillText(note, x0, LANE_Y + 20);
      ctx.globalAlpha = 1;
    }

    /* ── 아래층 : 씬에 직접 배치한 주민 셋 ──────────── */
    const slotX = i => (w / (n + 1)) * (i + 1);

    for (let i = 0; i < n; i++) {
      const cx = slotX(i);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.arc(cx, ROW_Y, 12, 0, Math.PI * 2); ctx.stroke();

      /* 위로 뻗다 만 토막 — 여기서 끝난다. 위층까지 잇지 않는다. */
      ctx.setLineDash([4, 5]);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
      ctx.beginPath();
      ctx.moveTo(cx, ROW_Y - 16); ctx.lineTo(cx, ROW_Y - 44);
      ctx.stroke();
      ctx.setLineDash([]);

      /* 계속 도는 것 ② — 토막 끝에서 올라가려다 틈 앞에서 사라지는 신호 */
      const u = frac(t / REACH + i * 0.27);
      const py = lerp(ROW_Y - 16, ROW_Y - 52, u);
      ctx.globalAlpha = 0.75 * (1 - u) * (1 - u);
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(cx, py, 3.4, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    // 셋이 무엇인지 (원문 39행)
    {
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub');
      const rl = spec.rowLabel || '';
      fitFont(ctx, rl, w - PAD * 2, s => disp(700, s), 10, 8);
      ctx.fillText(rl, w / 2, ROW_Y + 30);
    }

    /* ── 틈 : 두 층 사이의 빈 세로 간격 ───────────── */
    {
      const midY = (LANE_Y + 26 + ROW_Y - 52) / 2;
      const label = spec.gapLabel || '';
      ctx.textAlign = 'center';
      const size = fitFont(ctx, label, w - PAD * 2 - 60, s => disp(900, s), 15, 10);
      const tw = ctx.measureText(label).width;

      // 간격을 세로로 재는 두 눈금 — 여기가 비어 있다는 표시
      ctx.globalAlpha = 0.35 + 0.65 * gk;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      const bx = clamp(w / 2 - tw / 2 - 16, PAD, w - PAD);
      const bx2 = clamp(w / 2 + tw / 2 + 16, PAD, w - PAD);
      const top = LANE_Y + 28, bot = ROW_Y - 54;
      [bx, bx2].forEach(x => {
        ctx.beginPath();
        ctx.moveTo(x - 6, top); ctx.lineTo(x + 6, top);
        ctx.moveTo(x, top); ctx.lineTo(x, bot);
        ctx.moveTo(x - 6, bot); ctx.lineTo(x + 6, bot);
        ctx.stroke();
      });

      // 라벨
      ctx.globalAlpha = clamp(gk * 1.4);
      setShadow(ctx, GLOW, 10, 0);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, size);
      ctx.fillText(label, w / 2, midY + size * 0.36);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── None 딱지 : 셋이 지금 무슨 값인가 ─────────── */
    if (nk > 0.02) {
      const label = spec.none || '';
      ctx.textAlign = 'center';
      const size = fitFont(ctx, label, w - PAD * 2 - 24, s => mono(700, s), 12, 8);
      const tw = ctx.measureText(label).width;
      const bw = tw + 20, bh = 26;
      const bx = clamp(w / 2, PAD + bw / 2, w - PAD - bw / 2) - bw / 2;
      const by = ROW_Y + 38;
      ctx.globalAlpha = clamp(nk * 1.4);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      ctx.setLineDash([5, 4]);
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();
      ctx.setLineDash([]);
      ctx.fillStyle = tone('ink'); ctx.font = mono(700, size);
      ctx.fillText(label, bx + bw / 2, by + bh / 2 + 4);
      ctx.globalAlpha = 1;
    }

    /* ── 성격이 정하는 두 칸 (원문 33행) ──────────────
       셋에게서 내려온 선이 두 칸에 닿아 있지만 칸 안은 비어 있다. */
    if (trk > 0.02 && traits.length) {
      const boxTop = h - 62;
      const boxH = 40;
      const gap = 10;
      const bw = (w - PAD * 2 - gap * (traits.length - 1)) / traits.length;

      ctx.globalAlpha = clamp(trk * 1.4);
      traits.forEach((name, i) => {
        const bx = PAD + i * (bw + gap);

        // 셋에서 내려오는 선
        ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
        ctx.beginPath();
        ctx.moveTo(bx + bw / 2, ROW_Y + 70);
        ctx.lineTo(bx + bw / 2, boxTop);
        ctx.stroke();

        // 빈 칸
        ctx.setLineDash([5, 4]);
        ctx.strokeStyle = tone('track');
        roundRect(ctx, bx, boxTop, bw, boxH, 4); ctx.stroke();
        ctx.setLineDash([]);

        // 칸 이름
        ctx.textAlign = 'center';
        ctx.fillStyle = tone('sub');
        const size = fitFont(ctx, name, bw - 12, s => disp(800, s), 11, 8);
        ctx.font = disp(800, size);
        ctx.fillText(name, bx + bw / 2, boxTop + 16);

        // 값 자리 — 비어 있다
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        for (let d = 0; d < 3; d++) {
          const dx = bx + bw / 2 - 15 + d * 11;
          ctx.beginPath();
          ctx.moveTo(dx, boxTop + 29); ctx.lineTo(dx + 7, boxTop + 29);
          ctx.stroke();
        }
      });
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
