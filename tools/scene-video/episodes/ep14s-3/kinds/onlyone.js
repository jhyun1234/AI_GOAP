import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* onlyone — 훅. 「마을에서 집 없는 건 목수뿐이었어요.」

   원문 근거(8단계 절):
   > "같은 작업에서 오래된 구멍 하나를 발견했습니다.
   >  목수는 마을에서 유일하게 집이 없는 주민이었습니다. 항상 그랬습니다."

   이 회차의 기본 도형이 여기서 선다 —
   **한 줄로 늘어선 자리들 위에 집이 하나씩 서는데, 딱 한 자리만 끝까지 비어 있다.**
   자막의 「목수뿐이었어요」는 원문의 「유일하게」를, 집이 한 번도 안 서는 그 자리는
   원문의 「항상 그랬습니다」를 진다.

   🖼️ 라벨을 전부 가려도 남는 사건: **넷 중 셋 위에만 무언가가 서고 하나는 빈 채로 남는다.**

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **층으로 갈랐다.**
     집(ink) 132~172 · 빈 집자리(track) 132~172 · 표식(ink) 188~216 ·
     강조색 밑줄 226~230 · 강조색 라벨 글립 238~254.
     밑줄에는 `setShadow` 를 **안 걸었다** — 글로우 12px 이면 표식 바닥(216)을 침범한다.
     맥동은 글로우 대신 알파로 준다.

   🔴 화면에 수를 한 개도 안 올렸다. 자리 넷은 `spec.marks` 로만 있고 세는 라벨이 없다
      (원문 머리말의 「주민 넷」이 근거지만, 숫자를 글자로 적으면 이 편의 수치가 된다).

   세로 예산(캔버스 307): 최저 = 라벨 글립 바닥 254. 53px 남는다.
   가로: 자리 넷이 31~321(캔버스 352). ctext 가 폭을 재서 안쪽에 가둔다.

   ⏱ 등장은 훅 자막 `cue(0)` 에, 계속 도는 것은 `t` 에 물렸다.
   계속 도는 것 = 집 몸통 안 빗금 스크롤(면적) + 빈 자리 점선 흐름 + 밑줄 알파 맥동. */

const MARK_CY = 202, MARK_W = 46, MARK_H = 28;
const HOUSE_BASE = 172, HOUSE_H = 40, HOUSE_W = 52;
const RULE_Y = 228, LABEL_Y = 250;

/** 집 외곽선 하나. base 에서 위로 h 만큼 자란다(h 가 0 이면 아무것도 안 그린다). */
function housePath(ctx, cx, base, w, h) {
  const bodyW = w * 0.78, bodyH = h * 0.60, eave = base - bodyH;
  ctx.beginPath();
  ctx.moveTo(cx - bodyW / 2, base);
  ctx.lineTo(cx - bodyW / 2, eave);
  ctx.lineTo(cx - w / 2, eave);
  ctx.lineTo(cx, base - h);
  ctx.lineTo(cx + w / 2, eave);
  ctx.lineTo(cx + bodyW / 2, eave);
  ctx.lineTo(cx + bodyW / 2, base);
  ctx.closePath();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
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

    const n = Math.max(2, spec.marks ?? 4);
    const self = Math.min(n - 1, spec.selfIndex ?? n - 1);
    const gap = Math.min(84, (w - 108) / (n - 1));
    const mx0 = w / 2 - gap * (n - 1) / 2;

    const c0 = cue(spec.riseCue ?? 0, 0.15, 0.72);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const empty = ease(clamp((c0 - 0.58) / 0.16));
    const hi = ease(clamp((c0 - 0.72) / 0.20));

    /* ── 집 : 왼쪽부터 하나씩 땅에서 솟는다 ─────────── */
    for (let i = 0; i < n; i++) {
      const cx = mx0 + i * gap;
      if (i === self) continue;
      const k = ease(clamp((c0 - 0.16 - i * 0.13) / 0.20));
      if (k <= 0.02) continue;
      const h = HOUSE_H * k;

      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      housePath(ctx, cx, HOUSE_BASE, HOUSE_W, h);
      ctx.stroke();

      /* 몸통 안 빗금 — 계속 오른쪽으로 흐른다(면적 있는 움직임) */
      const bodyW = HOUSE_W * 0.78, bodyH = h * 0.60;
      if (bodyH > 5) {
        ctx.beginPath();
        ctx.rect(cx - bodyW / 2, HOUSE_BASE - bodyH, bodyW, bodyH);
        ctx.clip();
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        ctx.globalAlpha = st * 0.34;
        const S = 15, off = (t * 22) % S;
        for (let x = cx - bodyW / 2 - bodyH - S + off; x < cx + bodyW / 2 + S; x += S) {
          ctx.beginPath();
          ctx.moveTo(x, HOUSE_BASE);
          ctx.lineTo(x + bodyH, HOUSE_BASE - bodyH);
          ctx.stroke();
        }
      }
      ctx.restore();
    }

    /* ── 빈 집자리 : 여기만 끝까지 안 찬다 ───────────── */
    if (empty > 0.02) {
      const cx = mx0 + self * gap;
      ctx.save();
      ctx.globalAlpha = st * empty * 0.95;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.setLineDash([9, 8]);
      ctx.lineDashOffset = (17 - ((t * 19) % 17)) % 17;   // 양수 정규화(결정성)
      roundRect(ctx, cx - HOUSE_W / 2, HOUSE_BASE - HOUSE_H, HOUSE_W, HOUSE_H, 6);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 자리 넷 ───────────────────────────────────── */
    for (let i = 0; i < n; i++) {
      const cx = mx0 + i * gap;
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - MARK_W / 2, MARK_CY - MARK_H / 2, MARK_W, MARK_H, 7);
      ctx.stroke();
      ctx.restore();
      const face = i === self ? spec.selfFace : spec.wellFace;
      ctext(face, cx, MARK_CY + 5, 800, 13.5, tone('ink'), MARK_W - 10, st);
    }

    /* ── 빈 자리의 주인 ────────────────────────────── */
    if (hi > 0.02) {
      const cx = mx0 + self * gap;
      const pulse = 0.72 + 0.28 * (0.5 + 0.5 * Math.sin(t * 2.6));
      ctx.save();
      ctx.globalAlpha = hi * pulse;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4; ctx.lineCap = 'round';
      const half = lerp(6, MARK_W / 2 - 1, hi);
      ctx.beginPath();
      ctx.moveTo(cx - half, RULE_Y); ctx.lineTo(cx + half, RULE_Y);
      ctx.stroke();
      ctx.restore();
      ctext(spec.selfLabel, cx, LABEL_Y, 900, 14, tone('accent'), 96, hi);
    }

    ctx.textAlign = 'left';
  }
};
