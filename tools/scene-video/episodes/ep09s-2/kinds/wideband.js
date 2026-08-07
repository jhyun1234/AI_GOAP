import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* wideband — 성립하는 자리가 너무 넓다. 원 하나가 화면을 거의 다 먹는다.

   원문: "대화가 성립하는 반경도 4타일에서 2타일로 줄였습니다."
   → 줄이기 **전** 값이 4타일이고, 이 샷은 그 4타일이 얼마나 넓었는지만 그린다.

   🔴 기본 도형(대화가 성립하는 자리)이 여기서 **원**으로 나온다. 앞 샷에서 「자리가
   정해져 있지 않다」였던 것이, 사실은 「자리가 너무 넓다」였음이 이 샷에서 드러난다.

   🔴 지나가는 표식이 원 안에 머무는 구간(IN RANGE)을 굵은 선분으로 따로 그린다.
   경로의 절반 가까이가 그 구간이라 **스쳐 지나가는 동안 내내 성립한다**가 읽힌다.
   그 선분의 길이는 원의 반지름에서 기하로 나온 값이지 원문에 없는 수치가 아니다 —
   그래서 눈금도 숫자도 안 붙였다.

   🔴 화면에 적는 수는 **4 하나뿐**이고 반지름 선 끝에 붙는다. 반지름 88px 은
   다음 샷에서 44px(= 정확히 절반)로 줄어든다. 원문이 4 → 2 라고 못박았으므로 비를
   정확히 2:1 로 그렸다.

   🔴 등장은 전부 cue 에 물렸다(이 샷은 자막이 한 줄이라 **그 한 줄의 앞뒤 구간**으로
   가른다). t 로 도는 것은 원 테두리 점선·경로 점선·지나가는 표식뿐이고 문턱이 없다. */

const CXF = 0.44, CY = 148, R4 = 88;
const PATH_Y = 202, PER = 4.6;

/* 🔴 **위상 상수 — 이 샷의 사건이 언제 일어나는지를 정하는 값이다.**
   1차본은 `pp(t, PER)` 이라 첫 사정권 통과가 ts 0.43~1.54초였는데, 표식 등장 게이트가
   열리는 것은 ts 1.71초였다. 즉 **사건이 화면에 나오기 전에 지나가 버렸고**, 다음 통과는
   ts 4.42초라 샷(약 3.9초)이 이미 끝난 뒤였다 — 결과적으로 「말이 튀어나왔어요」를 말하는
   내내 말풍선 프레임이 0 이고, 말풍선은 문장이 끝나고 0.36초 뒤에야 떴다(검수 R1,
   60fps 스윕 + wav 실측). 「말이」 기준 지연 1.33초 = 40프레임으로 가이드 한도(±20)의 두 배다.
   🔑 ep08s-3 의 `weightknob` 과 같은 종류의 결함이다. 저건 문턱을 **0번** 넘었고 이건
   **엉뚱한 자리에서** 넘었다 — 둘 다 「주기를 샷 길이와 게이트 위상까지 함께 보고 고르지
   않은 것」이 원인이다. 진폭이 아무리 커도 게이트 밖에서 일어나면 화면에는 안 나온다.
   PHASE 3.24 를 더하면 사정권이 **ts 1.79~2.90초**가 되어 「말이」(1.72~1.96) ~
   「튀어나왔어요」(1.97~2.72) 구간과 거의 정확히 겹친다. */
/* 🔴 이 상수는 `rel.dur` 3.563 초(= 말하는 시간 3.313 + pauseAfter 0.250)와 **짝**이다.
   `rel` 이 바뀌면 사정권과 발화 구간의 맞물림이 밀리므로 **재사용 시 반드시 다시 푼다.**
   여유: rel ±12%(3.20~4.00)에서 43~52프레임 유지 — 위상 항에 rel 이 안 들어가서 강하다.
   대신 PHASE 자체에는 예민하다: ±0.2 까지 ≥40프레임, −0.4 에서 28, +0.8 에서 14.
   ⚠️ `rel.dur` 은 wav 길이가 아니다(engine.js:163). 이 회차에서 그 척도를 네 번 혼동했다. */
const PHASE = 3.24;

const pp = (t, p) => { const u = frac(t / p); return u < 0.5 ? u * 2 : 2 - u * 2; };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const X0 = 20, X1 = w - 20;
    const cx = w * CXF;

    /* 🔴 자막 한 줄을 앞뒤로 가른다.
       앞 절(「네 타일 안에 들어오면」) → 원과 반지름, 뒤 절(「말이 튀어나왔어요」) → 지나가는 표식. */
    const c0 = cue(spec.rangeCue ?? 0, 0.15, 0.85);
    const split = spec.split ?? 0.45;
    const rk = ease(clamp(c0 / split));
    const bk = ease(clamp((c0 - split) / (1 - split)));

    /* ── 위 라벨 ──────────────────────────────────── */
    if (spec.label) {
      const a = clamp(rk * 3.5);
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'left';
        ctx.font = disp(800, fit(spec.label, 800, 12.5, 150, 9));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.label, X0, 34);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 성립 반경 ────────────────────────────────── */
    const r = R4 * ease(clamp(rk * 1.1));
    if (rk > 0.02 && r > 6) {
      ctx.globalAlpha = clamp(rk * 2.6) * 0.95;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([12, 9]);
      ctx.lineDashOffset = -(t * 17) % 21;
      ctx.beginPath(); ctx.arc(cx, CY, r, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;

      /* 반지름 하나 — 끝에 값 */
      const ang = -0.91;                       // 오른쪽 위
      const ex = cx + Math.cos(ang) * r, ey = CY + Math.sin(ang) * r;
      ctx.globalAlpha = clamp(rk * 2.6);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cx, CY); ctx.lineTo(ex, ey); ctx.stroke();
      ctx.globalAlpha = 1;

      if (spec.radiusValue && rk > 0.55) {
        const a = clamp((rk - 0.55) / 0.3);
        ctx.globalAlpha = a;
        ctx.textAlign = 'left';
        ctx.font = disp(900, 19); ctx.fillStyle = tone('ink');
        ctx.fillText(String(spec.radiusValue), Math.min(ex + 8, w - 26), ey - 7);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 가운데 표식 ──────────────────────────────── */
    if (rk > 0.05) {
      ctx.globalAlpha = clamp(rk * 3);
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(cx, CY, 6.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 지나가는 경로 ────────────────────────────── */
    const half = Math.sqrt(Math.max(0, R4 * R4 - (PATH_Y - CY) * (PATH_Y - CY)));
    if (bk > 0.02) {
      const ab = clamp(bk * 2.6);
      ctx.globalAlpha = ab * 0.95;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([9, 8]);
      ctx.lineDashOffset = -(t * 14) % 17;
      ctx.beginPath(); ctx.moveTo(X0, PATH_Y); ctx.lineTo(X1, PATH_Y); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;

      /* 원 안에 머무는 구간 — 굵게 */
      const sx = cx - half, exx = cx - half + 2 * half * ease(clamp(bk * 1.4));
      ctx.globalAlpha = ab;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath(); ctx.moveTo(sx, PATH_Y); ctx.lineTo(exx, PATH_Y); ctx.stroke();
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(sx, PATH_Y - 9); ctx.lineTo(sx, PATH_Y + 9); ctx.stroke();
      if (exx > cx + half - 2) {
        ctx.beginPath();
        ctx.moveTo(cx + half, PATH_Y - 9); ctx.lineTo(cx + half, PATH_Y + 9); ctx.stroke();
      }
      ctx.globalAlpha = 1;

      if (spec.rangeLabel) {
        const a = clamp((bk - 0.45) / 0.35);
        if (a > 0.02) {
          ctx.globalAlpha = a;
          ctx.textAlign = 'center';
          ctx.font = disp(800, fit(spec.rangeLabel, 800, 12.5, 170, 9));
          ctx.fillStyle = tone('accent');
          ctx.fillText(spec.rangeLabel, cx, 266);
          ctx.globalAlpha = 1;
        }
      }
    }

    /* ── 지나가는 표식 — 원 안에 있는 동안 말이 튄다 ──
       🔴 등장 램프도 함께 조였다(0.15~0.45 → **0.10~0.30**). 위상만 맞추면 표식이
       사정권에 드는 순간의 알파가 0.14 라 말풍선이 「말이」를 말하는 동안 거의 안 보인다.
       조인 뒤에는 ts 1.62 에 표식이 서고 **ts 1.95 에 말풍선이 알파 1.0** 이 된다 —
       「말이」가 끝나고 「튀어나왔어요」가 시작되는 바로 그 자리다. */
    const a = clamp((bk - 0.10) / 0.20);
    if (a > 0.02) {
      const bx = lerp(X0 + 12, X1 - 12, pp(t + PHASE, PER));
      const inr = clamp((half - Math.abs(bx - cx)) / 20);

      if (inr > 0.02) {
        ctx.globalAlpha = a * inr * 0.9;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(cx, CY); ctx.lineTo(bx, PATH_Y); ctx.stroke();
        ctx.globalAlpha = 1;

        const mx = (cx + bx) / 2, my = (CY + PATH_Y) / 2 - 4;
        ctx.globalAlpha = a * inr;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, mx - 19, my - 12, 38, 24, 6); ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(mx - 6, my + 12); ctx.lineTo(mx + 1, my + 20); ctx.lineTo(mx + 7, my + 12);
        ctx.stroke();
        ctx.fillStyle = tone('accent');
        for (let i = -1; i <= 1; i++) {
          ctx.beginPath(); ctx.arc(mx + i * 8, my, 2.4, 0, Math.PI * 2); ctx.fill();
        }
        ctx.globalAlpha = 1;
      }

      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(bx, PATH_Y, 6.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
