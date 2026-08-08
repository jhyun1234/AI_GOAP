import {
  disp, mono, ease, clamp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* autoscale — 세운 축을 흔들어 본다.

   원문: "이 숫자의 힘은 자동 비례에 있습니다. 주민이 두 배가 되면 일수는 절반이 됩니다.
   겨울이 오면 같은 창고인데도 숫자가 계단처럼 뚝 떨어집니다(겨울엔 배가 더 빨리 고프니까요)." /
   "이제 제가 주민 수를 바꾸든 계절을 추가하든, 밸런스를 손으로 다시 계산할 일이 없습니다."

   🔴 S3 가 나눗셈을 세웠으니 이 샷은 그 나눗셈을 **실제로 흔들어 결과가 따라 움직이는지**
   보인다. 증명은 축을 하나 더 그리는 것이 아니라 이미 세운 축을 건드리는 것이다.
   ① halveCue 앞 — 주민 표식 넷 **사이사이로** 넷이 더 켜진다(끝에 덧붙이지 않는다.
      끼워 넣어야 「두 배」로 읽힌다).
   ② halveCue 뒤 — 그 순간 FoodDaysLeft 강조색 막대가 **정확히 절반** 길이로 내려앉는다.
      **이 편의 페이오프이고 자막 앞쪽 절반에서 끝난다**(halve 완성 = 자막 시작 +1.99초).
      🟢 4단 전환 뒤로는 아웃트로 카드가 이 샷을 덮지 않는다 — 카드는 마지막 샷(SO)에서
      뜨고, 이 페이오프는 그보다 9초 이상 앞이다.
   ③ 이어서 흰 MANUAL 배지가 오그라들어 **완전히 사라진 뒤에** 강조색 AUTO 배지가 자란다.
      크로스페이드가 아니라 순차 교체다 — 겹치는 프레임이 한 장도 없어야 한다.

   🔴 **2026-08-08 개정 — 계절 칸 넷(SPRING…WINTER)과 겨울 채움을 통째로 지웠다.**
      그 연출은 옛 **예고 자막**(이 샷의 둘째 줄)이 아웃트로 카드에 덮이기 전 0.3초 안에
      큰 변화를 만들어야 해서 넣은 것이었는데, 4단 전환으로 예고가 아웃트로 샷(SO)으로
      옮겨 가면서 목적이 사라졌다. 남겨 두면 자막이 부르지 않는 장식이 된다.
      ⚠️ 그리고 줄이 빠져 이 샷이 1줄이 됐으므로 `winterCue: 1` 은 `engine.js` 의
      `s.rel[Math.min(i, len-1)]` 때문에 **에러 없이 cue(0) 으로 접힌다** — 겨울 채움이
      자막 첫 프레임에 붙는 조용한 고장이라, 고치는 대신 지우는 쪽이 맞다.
      🔑 계절 축 자체는 S3 의 분모(SEASON)가 이미 지고 있다.

   🔴 **c0 계열(grow · halve · badge)의 창 인자와 문턱은 개정에서 한 글자도 안 건드렸다.**
      S4 의 drop `delayMs` 1,630 이 `cue(halveCue, 0.15, 0.55)` 와 halve 문턱 0.72 에서
      나온 값이다. 바꾼 것은 지운 계절 칸과 아래 y 좌표들뿐이다.

   계속 도는 것 = 표식 여덟의 바운스 · 막대 안 홈의 흐름(막대 전체 면적) · 바닥 점선의 흐름.
   🔴 막대 오른쪽 끝에 캐럿을 두려다 뺐다 — 트랙 흰 획을 가로지르는 자리라 강조색이 흰
   픽셀을 덮는다. 움직임은 막대 안 홈이 이미 더 큰 면적으로 지고 있다. */

const MARK_Y = 58, MARK_R = 11, SLOTS = 8;
const G_X = 20, G_Y = 118, G_H = 36, G_W = 312;
const B_W = 148, B_H = 36, B_Y = 198;
const BASE_Y = 282;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const cx = w / 2;

    const fitFont = (txt, weight, start, max, min = 8, fam = disp) => {
      let fs = start; ctx.font = fam(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = fam(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.halveCue ?? 0, 0.15, 0.55);

    const grow   = ease(clamp(c0 / 0.40));
    const halve  = ease(clamp((c0 - 0.72) / 0.14));
    const badge  = ease(clamp((c0 - 0.90) / 0.10));

    /* ── 바닥 점선 ─────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([7, 9]);
    ctx.lineDashOffset = -(t * 15) % 16;
    ctx.beginPath(); ctx.moveTo(16, BASE_Y); ctx.lineTo(w - 16, BASE_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;

    /* ── 주민 : 넷 사이사이로 넷이 더 켜진다 ───────── */
    if (spec.headLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.headLabel, 800, 12, 150));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.headLabel, 14, 24);
      /* 표식이 다 켜진 뒤에야 배수를 적는다 — 「주민이 두 배면」의 두 배가 이것이다 */
      if (spec.multLabel) {
        const mx = 14 + ctx.measureText(spec.headLabel).width + 10;
        ctx.globalAlpha = clamp(ease(clamp((grow - 0.72) / 0.28)) * 1.6);
        ctx.font = mono(700, 14);
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.multLabel, mx, 24);
        ctx.globalAlpha = 1;
      }
    }
    const step = (w - 80) / (SLOTS - 1);
    for (let i = 0; i < SLOTS; i++) {
      const mx = 40 + i * step;
      const my = MARK_Y + Math.sin(t * 4.6 + i * 0.9) * 3;
      if (i % 2 === 0) {
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(mx, my, MARK_R, 0, Math.PI * 2); ctx.fill();
      } else {
        const m = (i - 1) / 2;
        const k = ease(clamp((grow - m * 0.12) / 0.55));
        if (k > 0.02) {
          ctx.globalAlpha = clamp(k * 1.6);
          ctx.fillStyle = tone('ink');
          ctx.beginPath(); ctx.arc(mx, my, MARK_R * k, 0, Math.PI * 2); ctx.fill();
          ctx.globalAlpha = 1;
        }
      }
    }

    /* ── FoodDaysLeft 막대 ─────────────────────────── */
    if (spec.slotLabel) {
      ctx.textAlign = 'left';
      ctx.font = mono(700, fitFont(spec.slotLabel, 700, 13, 200, 8, mono));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.slotLabel, 14, 106);
    }

    /* 빈 트랙 — 얼마나 줄었는지 견주는 자리.
       🔴 강조색 막대는 이 흰 획 **안쪽으로 6px 물러난 자리**에 채운다. 획 위로 채우면
       강조색이 흰 픽셀을 덮게 되고, 그건 이 리포에서 반복해 걸린 결함이다
       (게이트는 불투명하게 덮인 경우 혼합 픽셀이 0이라 구조적으로 못 본다). */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, G_X, G_Y, G_W, G_H, 8); ctx.stroke();

    const ix = G_X + 6, iy = G_Y + 6, ih = G_H - 12;
    const gw = Math.max(18, (G_W - 12) * (1 - 0.5 * halve));
    setShadow(ctx, GLOW, 15);
    ctx.fillStyle = tone('accent');
    roundRect(ctx, ix, iy, gw, ih, 5); ctx.fill();
    clearShadow(ctx);

    /* 막대 안 홈이 흐른다 — 글자가 없는 도형이라 클립해도 안전하고,
       면적이 커서 자막과 무관한 움직임을 이 샷 내내 지고 있다. */
    ctx.save();
    roundRect(ctx, ix, iy, gw, ih, 5); ctx.clip();
    ctx.fillStyle = tone('bg');
    const off = (t * 19) % 24;
    for (let d = -24; d < gw + 24; d += 24) ctx.fillRect(ix + d + off, iy, 4, ih);
    ctx.restore();

    /* ── MANUAL 이 사라진 자리에 AUTO 가 앉는다 ────── */
    const bx = cx - B_W / 2;
    const mScale = 1 - ease(clamp(badge / 0.5));
    if (mScale > 0.03 && spec.manualLabel) {
      ctx.save();
      ctx.translate(cx, B_Y + B_H / 2);
      ctx.scale(mScale, mScale);
      ctx.translate(-cx, -(B_Y + B_H / 2));
      ctx.globalAlpha = clamp(mScale * 2);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx, B_Y, B_W, B_H, 8); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = mono(700, fitFont(spec.manualLabel, 700, 15, B_W - 24, 8, mono));
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.manualLabel, cx, B_Y + B_H / 2 + 5);
      ctx.globalAlpha = 1;
      ctx.restore();
    }

    const aK = clamp((badge - 0.5) / 0.5);
    if (aK > 0.02 && spec.autoLabel) {
      const sc = spring(aK);
      ctx.save();
      ctx.translate(cx, B_Y + B_H / 2);
      ctx.scale(sc, sc);
      ctx.translate(-cx, -(B_Y + B_H / 2));
      ctx.globalAlpha = clamp(aK * 2.2);
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bx, B_Y, B_W, B_H, 8); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      ctx.font = mono(700, fitFont(spec.autoLabel, 700, 15, B_W - 24, 8, mono));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.autoLabel, cx, B_Y + B_H / 2 + 5);
      ctx.globalAlpha = 1;
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
