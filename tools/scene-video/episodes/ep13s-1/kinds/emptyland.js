import {
  disp, mono, ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* emptyland — S2. 늑대는 출발할 때 정한 자리로만 걸어가고, 도착하면 거기 아무도 없다.

   원문: "늑대가 스폰될 때 정해진 좌표로만 걸어가고 있었습니다… 그런데 주민은 늑대를
   감지하면 도망칩니다. 결과적으로 늑대는 항상 방금 전까지 사람이 있던 빈 땅에 도착해서,
   아무도 없는 곳을 습격하고 퇴장했습니다. 도망이 100% 승리하는 구조라…"
   그리고 도망의 시각적 재료 — "대신 위협을 감지하는 반경을 성격마다 다르게 줬습니다.
   고집쟁이 0.6 … 농사꾼 0.8 … 순둥이·떠돌이 1.0 … 새침이 1.2 — 가장 먼저, 가장
   예민하게 도망칩니다."

   🔴 도형이 사실을 그대로 진다. 다섯 사람을 **반경이 큰 순서로 왼쪽부터** 세웠기 때문에,
   왼쪽에서 오는 늑대가 원에 닿는 순서가 곧 **새침이 → … → 고집쟁이** 다. 원문의 도망
   순서가 배치에서 자동으로 나온다 — 순서를 손으로 정해 준 데가 한 군데도 없다.
   그리고 늑대가 겨냥한 좌표는 **가장 늦게 뛴 사람의 자리**라, 도착했을 때 거기가 비어 있다.

   🔴 반경 값은 spec 으로 받는다(kind 상수 아님). 화면 눈금 = 1.0 → 18px.
      라벨은 **양 끝 둘만** 단다 — 가운데 셋(1.0 · 1.0 · 0.8)은 원 크기로만 말한다.

   ⏱ 늑대의 걸음만 자막 cue 에 물린다. 원 점선의 흐름 · 사람 바운스 · 도착한 늑대의
      미세 흔들림은 `t` 로만 돈다.

   🔴 겹침 감사 (두 축)
   · **원이 사라지는 시점이 곧 늑대가 닿는 시점**이라 강조색이 흰 점선을 절대 안 덮는다.
     도망 문턱을 `x − r − 50` 에서 열어 `x − r − 30` 에 닫아 두었고, 늑대의 후광 앞끝은
     중심 +25 다. 원이 완전히 꺼지는 순간 후광 앞끝은 원 왼쪽 끝에서 **5px 못 미친다**
     (가장 빠듯한 고집쟁이 기준: 닫힘 275.2 → 앞끝 300.2 · 원 왼쪽 305.2).
   · 사람이 위로 뛰는 동안에도 안 닿는다. 표식이 늑대 후광 띠(y 137~183)를 벗어나는 데
     드는 것은 비행의 41%(= 늑대 8px)이고, 그때 후광 앞끝은 `x − r − 22` 라 표식 왼쪽
     끝(`x − 7`)보다 **항상 왼쪽**이다(r ≥ 0 이면 부등식이 성립한다 — 좌표가 아니라
     부등식으로 막았다).
   · 도착 후: 늑대 후광 x 291~341 · y 137~183. 위로 도망 행 표식 바닥(바운스 최대 115)까지
     **22px**, 아래로 라벨 글리프 위(192)까지 **9px**, 오른쪽으로 캔버스 끝까지 11px.
   · 흰↔흰: 「감지 반경」 글리프 바닥 37 ↔ 위 점선 52 = 15 · 도망 행 바닥 115 ↔ 원 위
     끝(가장 큰 것 138.4) = 23 · 원 바닥 181.6 ↔ 라벨 글리프 위 192 = 10.4 ·
     라벨 바닥 208 ↔ 「빈 땅」 글리프 위 226 = 18 · 「빈 땅」 바닥 242 ↔ 아래 점선 264 = 22.
     두 라벨은 x 로 77~143 과 272~344 라 겹치지 않는다. */

const TOP_RULE = 52, BOT_RULE = 264;
const ROW_Y = 160, SAFE_Y = 104, MARK_R = 7;
const UNIT = 18;                       // 감지 반경 1.0 = 18px
const LBL_Y = 204, EMPTY_Y = 238;
const XS = [110, 170, 226, 272, 316];  // 반경 큰 순서 = 도망 순서
const LEAD = 50, RAMP = 20;            // 후광이 원에 닿기 전에 원이 꺼지도록 앞당긴 문턱

/* 🔴 가로 점선은 `setLineDash` 를 안 쓴다 — 조각을 직접 채운다(결정성).
   사유는 `firstgrave.js` 의 같은 함수 주석에 적어 뒀다. 모양은 이전과 같다. */
function dashRule(ctx, y, x0, x1, t, speed) {
  const P = 14, DASH = 7;
  const off = (((t * speed) % P) + P) % P;
  ctx.save();
  ctx.fillStyle = tone('track');
  for (let x = x0 - P + off; x < x1; x += P) {
    const a = Math.max(x0, x), b = Math.min(x1, x + DASH);
    if (b > a) ctx.fillRect(a, y - 1.5, b - a, 3);
  }
  ctx.restore();
}

function wolfWedge(ctx, x, y, s = 1) {
  ctx.beginPath();
  ctx.moveTo(x + 13 * s, y);
  ctx.lineTo(x - 9 * s, y - 9 * s);
  ctx.lineTo(x - 3 * s, y);
  ctx.lineTo(x - 9 * s, y + 9 * s);
  ctx.closePath(); ctx.fill();
  ctx.beginPath();
  ctx.moveTo(x - 9 * s, y - 9 * s);
  ctx.lineTo(x - 15 * s, y - 14 * s);
  ctx.lineTo(x - 12 * s, y - 5 * s);
  ctx.closePath(); ctx.fill();
  ctx.beginPath();
  ctx.moveTo(x - 9 * s, y + 9 * s);
  ctx.lineTo(x - 15 * s, y + 14 * s);
  ctx.lineTo(x - 12 * s, y + 5 * s);
  ctx.closePath(); ctx.fill();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fitFont = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };
    const put = (txt, weight, size, cx, y, color) => {
      const fs = fitFont(txt, weight, size, w - 40);
      ctx.font = disp(weight, fs);
      const tw = ctx.measureText(txt).width;
      ctx.textAlign = 'center';
      ctx.fillStyle = color;
      ctx.fillText(txt, clamp(cx, 8 + tw / 2, w - 8 - tw / 2), y);
    };

    const radii = spec.radii ?? [1.2, 1.0, 1.0, 0.8, 0.6];
    const c = cue(spec.walkCue ?? 0, 0.15, 0.85);
    const walk = ease(clamp(c / 0.74));
    const px = lerp(26, XS[XS.length - 1], walk);

    /* ── 띠와 라벨 ───────────────────────────────── */
    if (spec.radiusLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.radiusLabel, 800, 12.5, 150));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.radiusLabel, 16, 34);
    }
    dashRule(ctx, TOP_RULE, 16, w - 16, t, 12);
    dashRule(ctx, BOT_RULE, 16, w - 16, t, -10);

    /* ── 사람들과 감지 반경 ──────────────────────── */
    for (let i = 0; i < XS.length; i++) {
      const x = XS[i], r = radii[i] * UNIT;
      const fl = ease(clamp((px - (x - r - LEAD)) / RAMP));
      const my = lerp(ROW_Y, SAFE_Y, fl) + Math.sin(t * 4.6 + i * 1.3) * 4;

      if (fl < 0.98) {
        ctx.save();
        ctx.globalAlpha = 1 - fl;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.setLineDash([6, 7]);
        ctx.lineDashOffset = (((-(t * 16 + i * 5)) % 13) + 13) % 13;   // 양수 정규화(값은 동일)
        ctx.beginPath(); ctx.arc(x, ROW_Y, r, 0, Math.PI * 2); ctx.stroke();
        ctx.restore();
      }
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(x, my, MARK_R, 0, Math.PI * 2); ctx.fill();
    }

    /* ── 늑대 : 출발할 때 정한 자리로만 ──────────── */
    ctx.save();
    ctx.globalAlpha = clamp((px - 18) / 18);
    setShadow(ctx, GLOW, 14);
    ctx.fillStyle = tone('accent');
    wolfWedge(ctx, px, ROW_Y + Math.sin(t * 6.2) * 2);
    clearShadow(ctx);
    ctx.restore();

    /* ── 양 끝 두 사람만 이름을 단다 ─────────────── */
    if (spec.farLabel) put(spec.farLabel, 800, 12.5, XS[0], LBL_Y, tone('sub'));
    if (spec.nearLabel) put(spec.nearLabel, 800, 12.5, XS[XS.length - 1], LBL_Y, tone('sub'));

    /* ── 도착한 자리는 비어 있다 ─────────────────── */
    const empty = ease(clamp((c - 0.74) / 0.18));
    if (spec.emptyLabel && empty > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(empty * 1.5);
      put(spec.emptyLabel, 900, 19, XS[XS.length - 1], EMPTY_Y, tone('ink'));
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
