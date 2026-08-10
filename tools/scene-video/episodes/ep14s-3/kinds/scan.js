import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* scan — 추적. 「근데 부탁은 자기한테 못 걸어요 / 게다가 상대가 목수여야 하고요 /
   목수가 한 명이면 부탁할 상대가 세상에 없어요.」

   원문 근거(8단계 절):
   > "부탁 스캔은 자기 자신을 대상에서 제외하고 상대가 목수 직업이기를 요구합니다.
   >  마을에 목수가 한 명이면 그가 부탁을 걸 상대가 세상에 존재하지 않습니다."

   🔑 기획 브리프 §7 이 이 두 문장을 **「원문이 스스로 준 완벽한 풀이」**로 지목했다.
   그래서 「스캔」이라는 낱말은 화면·자막 어디에도 안 쓰고, 조건 두 개를 **원문 어절
   그대로** 칩에 올렸다(「자기 자신 제외」·「상대가 목수여야」). 비유는 하나도 안 만들었다.

   🖼️ 라벨을 전부 가려도 남는 사건:
       **칸들이 하나씩 안이 비고, 마지막에 내려앉은 상자도 비어 있다.**

   🔴 본문 다른 샷과 도형 축이 다르다(세로 층이 아니라 가로 명단). 이 샷만 「자리 이야기」가
      아니라 **「목록 이야기」**이기 때문이고, 한눈에 갈리라는 뜻도 있다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **좌우 + 위아래 + 선 사이 간격**으로 갈랐다.
     조건 칩(accent)   y  29~59
     빈 테두리(track)  y  95.5~152.5   (72×54)
     후보 칸(ink/acc)  y 105.5~142.5   (54×34)  ← 테두리 선과 **6px** 뜬다
     훑는 막대(accent) y 165~171
     결과 상자(accent) y 189~231 (내려앉은 뒤)
     · 목수 자신 칸(index 3)만 accent 인데 이웃 ink 칸과 x 로 26px 떨어져 있고,
       **후보 칸에는 `setShadow` 를 안 건다** — 글로우 10px 이면 자기 테두리(track)를 먹는다.
     · 훑는 막대의 x 이동 범위를 [34, w−34] 로 잘라 가장자리에서 19px 을 남긴다.

   🔴 화면에 수를 한 개도 안 올렸다. 후보 넷은 `spec.cands` 로만 있고 「0」 같은 결과 수를
      적지 않았다 — **없다는 것은 빈 상자가 진다.**

   세로 예산(캔버스 307): 최저 = 결과 라벨 글립 바닥 254. 53px 남는다.
   가로: 빈 테두리가 20~332(캔버스 352).

   ⏱ 조건 둘은 `cue(0)`·`cue(1)` 에, **결과 상자는 `since(2)`** 에 물렸다.
      결과만 `since` 인 이유는 그 자리에 효과음(thud)이 붙기 때문이다 —
      `delayMs` 와 원점이 같아지므로 자막 자수가 바뀌어도 소리가 안 밀린다.
   계속 도는 것 = 훑는 막대(주기 spec.sweepSec) + 빈 테두리 점선 + 칸 안 빗금 +
      결과 상자 점선과 폭의 느린 숨. **후보가 다 사라진 뒤에도 계속 훑는 것이 곧 뜻이다.** */

const CHIP_CY = 44, CHIP_H = 30;
const ROW_CY = 124;
const SLOT_W = 72, SLOT_H = 54;
const BOX_W = 54, BOX_H = 34;
const BAR_CY = 168, BAR_W = 30, BAR_H = 6;
const RES_W = 112, RES_H = 42, RES_FROM = 196, RES_TO = 210;
const RES_LABEL_Y = 250;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01 || !txt) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    const n = Math.max(2, spec.cands ?? 4);
    const self = Math.min(n - 1, spec.selfIndex ?? n - 1);
    const gap = Math.min(80, (w - 110) / (n - 1));
    const mx0 = w / 2 - gap * (n - 1) / 2;

    const c0 = cue(spec.selfCue ?? 0, 0.15, 0.70);
    const c1 = cue(spec.jobCue ?? 1, 0.15, 0.72);
    const sc = since(spec.resultLine ?? 2);

    const st = ease(clamp(c0 / 0.10));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 조건 두 개 ────────────────────────────────── */
    const chipW = Math.min(126, w * 0.40);
    const chips = [
      { txt: spec.cond1, cx: w * 0.28, k: ease(clamp(c0 / 0.32)) },
      { txt: spec.cond2, cx: w * 0.72, k: ease(clamp(c1 / 0.30)) }
    ];
    for (const c of chips) {
      if (c.k <= 0.02 || !c.txt) continue;
      ctx.save();
      ctx.globalAlpha = c.k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, c.cx - chipW / 2, CHIP_CY - CHIP_H / 2, chipW, CHIP_H, 8);
      ctx.stroke();
      ctx.restore();
      ctext(c.txt, c.cx, CHIP_CY + 5, 800, 13, tone('accent'), chipW - 14, c.k);
    }

    /* ── 빈 테두리 넷 : 칸 자체는 사라지지 않는다 ───── */
    ctx.save();
    ctx.globalAlpha = st * 0.95;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([10, 8]);
    ctx.lineDashOffset = (18 - ((t * 20) % 18)) % 18;    // 양수 정규화(결정성)
    for (let i = 0; i < n; i++) {
      roundRect(ctx, mx0 + i * gap - SLOT_W / 2, ROW_CY - SLOT_H / 2, SLOT_W, SLOT_H, 9);
      ctx.stroke();
    }
    ctx.restore();

    /* ── 후보 : 조건이 켜질 때마다 하나씩 오그라들어 사라진다 ──
       자기 자신(index self)은 첫 조건에, 나머지는 둘째 조건에 차례로 빠진다. */
    for (let i = 0; i < n; i++) {
      const cx = mx0 + i * gap;
      const gone = i === self
        ? ease(clamp((c0 - 0.30) / 0.40))
        : ease(clamp((c1 - 0.24 - (i > self ? i - 1 : i) * 0.14) / 0.22));
      const k = 1 - gone;
      if (k <= 0.02) continue;

      const s = 0.18 + 0.82 * k;
      const isSelf = i === self;
      const col = isSelf ? tone('accent') : tone('ink');
      ctx.save();
      ctx.globalAlpha = st * k;
      ctx.translate(cx, ROW_CY); ctx.scale(s, s); ctx.translate(-cx, -ROW_CY);
      ctx.strokeStyle = col; ctx.lineWidth = 3;
      roundRect(ctx, cx - BOX_W / 2, ROW_CY - BOX_H / 2, BOX_W, BOX_H, 7);
      ctx.stroke();

      /* 칸 안 빗금 — 계속 흐른다(면적 있는 움직임) */
      ctx.beginPath();
      ctx.rect(cx - BOX_W / 2 + 3, ROW_CY - BOX_H / 2 + 3, BOX_W - 6, BOX_H - 6);
      ctx.clip();
      ctx.lineWidth = 4; ctx.globalAlpha = st * k * 0.20;
      const S = 14, off = (t * 18) % S;
      for (let x = cx - BOX_W / 2 - BOX_H - S + off; x < cx + BOX_W / 2 + S; x += S) {
        ctx.beginPath();
        ctx.moveTo(x, ROW_CY + BOX_H / 2);
        ctx.lineTo(x + BOX_H, ROW_CY - BOX_H / 2);
        ctx.stroke();
      }
      ctx.restore();

      const face = isSelf ? spec.selfFace : spec.wellFace;
      ctext(face, cx, ROW_CY + 5, 800, 13 * s, col, (BOX_W - 10) * s, st * k);
    }

    /* ── 훑는 막대 : 다 사라진 뒤에도 계속 훑는다 ───── */
    {
      const u = frac(t / Math.max(0.6, spec.sweepSec ?? 1.8));
      const pos = u < 0.5 ? u * 2 : 2 - u * 2;
      const bx = lerp(34, w - 34, pos);
      ctx.save();
      ctx.globalAlpha = st * 0.9;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = BAR_H; ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(bx - BAR_W / 2, BAR_CY); ctx.lineTo(bx + BAR_W / 2, BAR_CY);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 결과 : 빈 채로 툭 내려앉는다 (since(2) 위) ──
       thud 의 delayMs 980 이 이 착지(sc = 0.98)와 같은 원점을 쓴다. */
    const ra = ease(clamp((sc - 0.30) / 0.20));
    if (ra > 0.004) {
      const drop = ease(clamp((sc - 0.38) / 0.60));
      const ry = lerp(RES_FROM, RES_TO, drop);
      const bw = RES_W + 3 * Math.sin(t * 1.9);        // 느린 숨
      ctx.save();
      ctx.globalAlpha = ra;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([11, 9]);
      ctx.lineDashOffset = (20 - ((t * 24) % 20)) % 20;
      roundRect(ctx, w / 2 - bw / 2, ry - RES_H / 2, bw, RES_H, 9);
      ctx.stroke();
      ctx.restore();
      ctext(spec.resultLabel, w / 2, RES_LABEL_Y, 900, 14, tone('accent'), w - 60,
        ease(clamp((sc - 0.95) / 0.40)));
    }

    ctx.textAlign = 'left';
  }
};
