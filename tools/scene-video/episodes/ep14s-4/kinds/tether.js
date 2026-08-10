import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* tether — 진짜 원인. 밥을 모닥불 앞에서만 먹을 수 있었다 = 모닥불이 중력이었다.

   원문 근거:
   > "다른 하나가 진짜 문제였습니다 — 식사가 모닥불 앞에서만 가능했습니다.
   >  멀리 살면 끼니마다 왕복하다 굶어 죽습니다."
   > "공용 모닥불이 마을을 물리적으로 붙잡아두는 중력이었던 겁니다."
   🔑 「중력」은 비유가 아니라 **원문 자신의 낱말**이다(ADR-V25-14 의 비유 목록에 안 들어간다).

   자막 0(밥은 모닥불 앞에서만) : 불 아래 세로 통로로 「밥은 여기서만」이 켜지고,
     가장 먼 집에서 불까지 **흰 표식이 왕복**한다. 오갈수록 그 집 위에 `ㅠ_ㅠ` 가 뜬다.
   자막 1(멀리 살면 굶어요 · 중력) : 왕복 경로가 꺼지고 **같은 자리에** 불에서 집들로
     강조색 줄이 팽팽하게 뻗는다. 여섯이 그 줄에 맞춰 안팎으로 끌려 숨 쉰다.
     🔑 줄은 **다섯**이다 — 가장 안쪽 집(비율 0.10)은 불에 이미 붙어 있어 줄을 놓을 자리가
     없다. 빼먹은 게 아니라 원문 그대로다(「순둥이는 이웃 곁에 붙어 산다」).

   라벨을 다 가려도 「한 점과 집들 사이를 오간다 → 그 점이 전부를 붙들고 있다」가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세 겹으로 막았다.**
     ⓐ 반지름 : 불(accent) 글립은 r ≤ 18(글로우까지 r ≤ 29), 집(ink)은 r ≥ 36. 당김줄(accent)은 r 30 에서
        시작해 **집 중심에서 18px 앞**(= 글립 가장자리에서 4px 앞)에서 끊는다.
     ⓑ 시간 : 왕복 경로·표식(ink)과 당김줄(accent)은 **같은 반지름 선**을 쓰므로 위상으로
        갈랐다 — 경로는 `since(1)` 0.25초에 알파 0 이 되고 당김줄은 0.30초에야 시작한다.
        둘이 동시에 0 보다 큰 프레임이 없다.
     ⓒ 통로 : 「밥은 여기서만」과 유도선(accent)은 90° 세로 통로에만 있다. 여섯 각도가
        전부 90°에서 ±35° 밖이고, 안쪽으로 끌리는 폭(최대 8px)을 다 넣어도 통로에 가장
        가까운 집(125°)의 상자 오른쪽 끝이 **x 161.2** 를 못 넘는다(통로는 x 174.5~177.5).
        13px 뜬다.

   세로 예산(캔버스 307): 최저 = 「밥은 여기서만」 글립 바닥 270(기준선 266). 37px 남는다.
   가로: 5° 집의 오른쪽 끝 261. 캔버스 352, 여유 91px.

   계속 도는 것 = 자막 0 은 왕복 주기(1.6초 · 경로 전체) · 자막 1 은 여섯 집이 8px 폭으로
   함께 숨 쉬는 움직임(집 상자 여섯의 면적) + 불꽃 맥동. */

const CY = 150;
const R_IN = 50, R_OUT = 96;
const FAR_DEG = 317;                  // 가장 먼 집 — 왕복을 여기서 보여 준다

const SEATS = [
  { deg: 125, ratio: 0.25 },
  { deg: 173, ratio: 0.80 },
  { deg: 221, ratio: 0.10 },
  { deg: 269, ratio: 0.50 },
  { deg: 317, ratio: 0.95 },
  { deg: 5, ratio: 0.50 }
];
const rOf = ratio => R_IN + (ratio - 0.10) / 0.85 * (R_OUT - R_IN);
const tri = q => (q < 0.5 ? q * 2 : 2 - q * 2);      // 0→1→0 왕복

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const CX = w / 2;

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const house = (x, y, alpha, sc) => {
      if (alpha <= 0.01) return;
      const bw = 22 * sc, bh = 18 * sc;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, x - bw / 2, y - bh / 2 + 3, bw, bh, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(x - bw / 2 - 3, y - bh / 2 + 3);
      ctx.lineTo(x, y - bh / 2 - 9 * sc);
      ctx.lineTo(x + bw / 2 + 3, y - bh / 2 + 3);
      ctx.stroke();
      ctx.restore();
    };

    const c0 = cue(spec.mealCue ?? 0, 0.15, 0.90);
    const st = ease(clamp(c0 / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 두 국면의 무게 : 겹치는 프레임이 없게 위상으로 갈랐다 ── */
    const s1 = since(spec.pullCue ?? 1);
    const pathA = s1 >= 0 ? 1 - ease(clamp(s1 / 0.25)) : 1;
    const pullK = s1 >= 0 ? ease(clamp((s1 - 0.30) / 0.40)) : 0;

    /* ── 숨 : 당김줄이 걸리면 여섯이 함께 안팎으로 끌린다 ── */
    const breathe = pullK * 4 * (1 + Math.sin(s1 >= 0 ? s1 * 4.2 : 0));   // 0~8px 안쪽으로

    /* ── 가운데 불 ────────────────────────────────── */
    const puff = 0.5 + 0.5 * Math.sin(t * 3.1);
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 8 + 3 * puff + 4 * pullK);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    const fh = 24 + 4 * puff;
    ctx.beginPath();
    ctx.moveTo(CX, CY - fh / 2 - 3);
    ctx.lineTo(CX + 10, CY + fh / 2 - 3);
    ctx.lineTo(CX - 10, CY + fh / 2 - 3);
    ctx.closePath(); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();

    /* ── 90° 세로 통로 : 밥은 여기서만 ────────────── */
    ctx.save();
    ctx.globalAlpha = st * (0.55 + 0.45 * ease(clamp(c0 / 0.4)));
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(CX, CY + 20); ctx.lineTo(CX, CY + 96); ctx.stroke();
    ctx.restore();
    if (spec.mealLabel) {
      ctext(spec.mealLabel, CX, CY + R_OUT + 20, 900, 14, tone('accent'), 150,
        st * ease(clamp((c0 - 0.10) / 0.22)));
    }

    /* ── 집 여섯 ──────────────────────────────────── */
    const pos = {};
    for (const s of SEATS) {
      const r = Math.max(36, rOf(s.ratio) - breathe);
      const a = s.deg * Math.PI / 180;
      const x = CX + r * Math.cos(a), y = CY + r * Math.sin(a);
      pos[s.deg] = { x, y, r, a };
    }

    /* ── 자막 0 : 가장 먼 집이 불까지 왕복한다 ────── */
    if (pathA > 0.01) {
      const far = pos[FAR_DEG];
      const rA = 34, rB = far.r - 18;
      ctx.save();
      ctx.globalAlpha = st * pathA * 0.75;
      ctx.fillStyle = tone('ink');
      for (let r = rA; r <= rB; r += 9) {
        ctx.beginPath();
        ctx.arc(CX + r * Math.cos(far.a), CY + r * Math.sin(far.a), 2, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();

      const s0 = since(spec.mealCue ?? 0);
      const q = s0 >= 0 ? frac(s0 / (spec.tripSec ?? 1.6)) : 0;
      const rw = lerp(rB, rA, ease(tri(q)));
      ctx.save();
      ctx.globalAlpha = st * pathA;
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.arc(CX + rw * Math.cos(far.a), CY + rw * Math.sin(far.a), 5.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();

      /* 왕복이 한 번 돌고 나면 그 집이 무너진다 */
      if (spec.face) {
        const s0b = since(spec.mealCue ?? 0);
        ctext(spec.face, far.x, far.y - 30, 800, 15, tone('ink'), 62,
          st * pathA * ease(clamp(((s0b >= 0 ? s0b : 0) - 1.5) / 0.4)));
      }
    }

    /* ── 자막 1 : 불에서 여섯으로 팽팽한 줄 ────────── */
    if (pullK > 0.01) {
      ctx.save();
      ctx.globalAlpha = st * pullK;
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'round';
      for (const s of SEATS) {
        /* 🔴 줄을 그릴지 말지는 **숨 쉬는 반지름이 아니라 제자리 반지름**으로 정한다.
           breathe 로 판정하면 가장 가까운 집의 줄이 프레임마다 켜졌다 꺼진다.
           그리고 가장 안쪽 집(비율 0.10)은 애초에 불에 붙어 있어 줄을 놓을 자리가 없다 —
           그게 원문의 사실이다(「순둥이는 이웃 곁에 붙어 산다」). */
        if (rOf(s.ratio) - 18 < 36) continue;
        const p = pos[s.deg];
        const rEnd = p.r - 18;
        ctx.beginPath();
        ctx.moveTo(CX + 30 * Math.cos(p.a), CY + 30 * Math.sin(p.a));
        ctx.lineTo(CX + rEnd * Math.cos(p.a), CY + rEnd * Math.sin(p.a));
        ctx.stroke();
      }
      clearShadow(ctx);
      ctx.restore();
    }

    for (const s of SEATS) house(pos[s.deg].x, pos[s.deg].y, st, 1);

    ctx.textAlign = 'left';
  }
};
