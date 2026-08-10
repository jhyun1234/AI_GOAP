/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다.
   🔑 `ep14s-4` 마스터 판정(2026-08-10) — 여유가 다섯 곳에서 1.5~3px 넉넉하게 적혀 있었고
   원인은 부주의가 아니라 **어느 기준으로 적는지가 어디에도 없었던 것**이다. */
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
     ⓐ 반지름 : 불(accent) 글립은 r ≤ 17.5, 글로우(blur 최대 12)까지 **r ≤ 31.0**.
        집(ink)의 중심은 **r ≥ 48**(아래 하한)이라 안쪽 가장자리가 34 — 4.5px 뜬다.
        당김줄(accent)은 r 26 에서 시작해 **집 중심에서 18px 앞**(= 🔴 **닿는다** — 317° 에서 불투명끼리 **0.12px** 다(마스터 실측 · 렌더에서 혼합 픽셀 4~7개). 「4px 앞」은 틀렸다. 🔑 **이건 사고가 아니라 의도다**: 당김줄은 불에 «닿아서» 끌어당기는 선이므로 끝이 글립에 물리는 것이 뜻에 맞다. 다만 **여유가 있다고 적으면 다음 사람이 그 여유를 쓴다** — 닿는다고 쓴다)에서 끊는다.
     ⓑ 시간 : 왕복 경로·걷는 표식(ink)과 당김줄(accent)은 **같은 반지름 선**을 쓰므로 위상으로
        갈랐다 — 경로는 `since(1)` 0.25초에 알파 0 이 되고 당김줄은 0.30초에야 시작한다.
        둘이 동시에 0 보다 큰 프레임이 없다.
        🔑 `ㅠ_ㅠ` 표식만 이 분리에서 빼도 된다 — 317° 집 **위쪽**(x 232~260 · y 41~58)에 있고
        그 각도의 당김줄은 r 26~78(x 195~233 · y 97~132)이라 좌표가 아예 안 만난다.
     ⓒ 통로 : 「밥은 여기서만」과 유도선(accent)은 90° 세로 통로에만 있다. 여섯 각도가
        전부 90°에서 ±35° 밖이고, 안쪽으로 끌리는 폭(최대 8px)을 다 넣어도 통로에 가장
        가까운 집(125°)의 상자 오른쪽 끝이 **x 162.75** 를 못 넘는다(통로는 x 174.5~177.5 · 획 바깥 기준).
        **11.75px** 뜬다(옛 표기 13px 은 경로 기준이었다).

   세로 예산(캔버스 307): 최저 = 「밥은 여기서만」 글립 바닥 270(기준선 266). 37px 남는다.
   가로: 5° 집의 오른쪽 끝 261. 캔버스 352, 여유 91px.

   🔴 **1차 반려를 고친 자리 (B)** — 30fps 한 프레임 기준으로 움직임이 없어 3.2초가 정적으로
   잡혔다(자막 0 구간). 왕복 표식 하나(반지름 5.5)만 움직이고 있었고 프레임당 1.8px 이라
   바뀌는 면적이 40px² 뿐이었다. 셋을 키웠다: ①집 여섯에 **상시 숨**(`idle` 2px · 7.4rad/s,
   프레임당 0.25px · 여섯 개의 외곽선이 함께) ②가운데 불꽃 맥동 4→**7**·3.1→**5.6rad/s**
   (프레임당 0.65px) ③자막 1 의 큰 숨을 4.2→**9.0rad/s**(프레임당 0.9px).
   🔑 ①은 **사건이 아니라 상시 운동**이라 `t` 로 돌린다 — 사건(당김)은 여전히 `since(1)` 에
   물리고, 진폭이 3배 갈려서 둘이 안 헷갈린다.

   계속 도는 것 = 자막 0·1 공통으로 여섯 집의 상시 숨 + 불꽃 맥동 · 자막 0 은 왕복 주기(1.6초)
   · 자막 1 은 여섯이 함께 크게 끌리는 숨. */

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

    /* ── 숨 두 겹 ──────────────────────────────────
       ① `idle` = 상시 운동(2px · 7.4rad/s). **사건이 아니라 살아 있다는 표시**라 `t` 로 돈다.
          30fps 한 프레임에 0.25px 이 바뀌고 집 여섯의 외곽선이 함께 움직인다 — 1차 반려
          사유 B(자막 0 구간이 3.2초 정적)를 여는 자리가 여기다.
       ② `breathe` = 당김줄이 걸린 뒤의 큰 숨(0~6px · 9.0rad/s). 이쪽은 **사건**이라
          `since(1)` 에 물린다. 진폭이 idle 의 3배라 「끌린다」가 「살아 있다」와 안 헷갈린다. */
    const idle = 2.0 * (0.5 + 0.5 * Math.sin(t * 7.4));
    const breathe = pullK * 3 * (1 + Math.sin(s1 >= 0 ? s1 * 9.0 : 0));   // 0~6px 안쪽으로

    /* ── 가운데 불 ────────────────────────────────── */
    const puff = 0.5 + 0.5 * Math.sin(t * 5.6);
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 6 + 3 * puff + 3 * pullK);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    const fh = 22 + 7 * puff;      // 22~29 — 맥동을 키워 30fps 한 프레임에 0.65px 이 바뀐다
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
      /* 🔴 하한 48 은 장식이 아니라 **색 분리선**이다. 불(강조색)의 글립 반경 17.5 +
         글로우 12 = 29.5 이고 집(흰색) 글립의 안쪽 가장자리가 r − 14 이므로,
         r ≥ 48 이어야 34 ≥ 29.5 로 4.5px 뜬다. 숨(idle 2 + breathe 6)을 다 먹어도 안 뚫린다. */
      const r = Math.max(48, rOf(s.ratio) - idle - breathe);
      const a = s.deg * Math.PI / 180;
      const x = CX + r * Math.cos(a), y = CY + r * Math.sin(a);
      pos[s.deg] = { x, y, r, a };
    }

    /* ── 자막 0 : 가장 먼 집이 불까지 왕복한다 ────── */
    if (pathA > 0.01) {
      const far = pos[FAR_DEG];
      /* 🔴 안쪽 끝은 34 가 아니라 **40** 이다 — 걷는 표식(흰색)이 반지름 5.5 라 34 에 서면
         안쪽 가장자리가 28.5 로 불의 글로우(29.5) 안에 들어간다. 40 이면 34.5 로 5px 뜬다.
         (걷는 것은 「불 앞까지」 가는 것이지 불 속으로 들어가는 게 아니다.) */
      const rA = 40, rB = far.r - 18;
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
    }

    /* 왕복이 한 번 돌고 나면 그 집이 무너진다.
       🔴 이 표식만 `pathA` 에서 떼어냈다. 붙여 두면 왕복 경로가 꺼질 때(`since(1)` 0.25초)
       같이 꺼지는데, 「멀리 살면 굶어요」의 말 온셋이 **0.47초**라 **굶는다는 뜻을 지는 그림이
       그 말보다 0.22초 먼저 사라진다.** 그래서 `since(1)` 0.90초까지 살려 말과 만나게 했다.
       ⓑ 시간 분리(흰 경로 ↔ 강조색 당김줄)는 안 깨진다 — 이 표식은 317° 집 **위쪽**
       (x 232~260 · y 41~58)에 있고, 그 각도의 당김줄은 r 26~78 구간(x 195~233 · y 97~132)이라
       좌표가 아예 안 만난다. */
    if (spec.face) {
      const sf = since(spec.mealCue ?? 0);
      const fadeOut = s1 >= 0 ? ease(clamp((s1 - 0.90) / 0.35)) : 0;
      ctext(spec.face, pos[FAR_DEG].x, pos[FAR_DEG].y - 30, 800, 15, tone('ink'), 62,
        st * ease(clamp(((sf >= 0 ? sf : 0) - 1.5) / 0.4)) * (1 - fadeOut));
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
        ctx.moveTo(CX + 26 * Math.cos(p.a), CY + 26 * Math.sin(p.a));
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
