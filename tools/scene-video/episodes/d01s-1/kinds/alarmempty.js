import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* alarmempty — 위에서는 경보가 켜지는데, 그 아래는 계속 비어 있다. (훅)

   원문 근거 (`devlog/sessions/2026-08-11.md` 12:06 커밋 본문 ①):
   > "'고블린 무리 14마리 -- 마을 습격 중' 배너. 상주가 태어나자마자 도착해 영원히
   >  Staying 이라 HUD 가 습격으로 셌다. **마을 근처에 오지도 않았는데.**"

   화면 라벨 「마을 습격 중」은 **그 배너 문자열의 뒷부분 그대로**다. 앞부분(수)은 S1 이
   진다 — 훅은 「켜졌다」만 말하고 「왜 켜졌나」는 다음 샷의 몫이라 여기 수를 안 올렸다.
   🔴 「HUD」·「배너」·「상주」·「Staying」은 화면에도 자막에도 안 쓴다
   (앞 셋은 비개발자 벽, 마지막은 화면 한국어 규칙 `ADR-V-10`).

   이 회차의 색 규약: **흰색 = 판 위에 실제로 있는 것 / 강조색 = 이번 사건에서 새로
   켜지거나 새로 그어진 것.** 여기서 경보 띠가 강조색인 이유가 그것이고, 아래 점선 원과
   마을 네모와 표식이 흰색인 이유도 그것이다. 이 편의 사건이 정확히 **그 둘의 어긋남**이다.

   2.6초 루프: 강조색 띠가 왼쪽부터 차올라 켜지고 → 숨을 쉬다 → 꺼진다. 그동안 아래
   점선 원 안에서는 표식 셋이 저희끼리 돌 뿐이고, **바깥에서 들어오는 것이 하나도 없다.**

   🔴 겹침 감사(캔버스 352×307 · 선 반폭 1.5 · 글로우 blur 8 포함):
     축 A(강조↔흰): 띠 채움 y 22~36 · 글로우 **14~44** ↔ 흰 라벨 글립 꼭대기 약 **57**
       = **13px**. 가로는 띠 글로우 오른쪽 **344** — 캔버스 끝 352 에서 8px 이고
       좌우 가장자리 띠(바깥 2열)에 안 닿는다.
     축 B(흰 글자끼리): 흰 글자가 「마을 습격 중」 하나뿐이라 성립하지 않는다.
     흰↔흰(의도한 쌍): 표식 궤도 62 + 표식 바깥 반지름 12.1(=√(6.5²+12.5²)) = **74.1**
       ↔ 점선 원 안쪽 면 **86.5** = **12.4px**. 표식 ↔ 마을 네모는 가장 좁은 자리가
       45° 쪽인데, 표식 바깥 상자(x 213.3~226.3 · y 227.3~252.3)와 네모 바깥 모서리
       (201.5, 221.5)가 x 로 11.8 · y 로 5.8 떨어져 **13.1px** 이다.

   세로 예산: 최저 = 점선 원 획 바깥 **285.5**. 21.5px 남는다(바닥 행 y=306 에 잉크 0).

   계속 도는 것(30fps 기준):
     · 점선 원(둘레 2π×88 = 553px)의 대시가 `-t × 26` 로 흐른다 → **0.87px/프레임**
     · 표식 셋이 반지름 62 궤도를 `t × 0.55` rad/s 로 돈다 → **1.14px/프레임**
     · 띠 채움 폭이 0.78초에 304px → 13px/프레임 (루프 앞구간) */

const RING = { cx: 176, cy: 196, r: 88 };
const BAR = { x: 24, y: 22, w: 304, h: 14 };
const TOWN = { x: 152, y: 172, w: 48, h: 48 };
const ORBIT = 62;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const g0 = ease(cue(spec.showCue ?? 0, 0.15, 0.30));
    if (g0 <= 0.02) { ctx.textAlign = 'left'; return; }

    const u = frac(t / (spec.loopSec ?? 2.6));
    /* 켜짐 = 0→0.30 에 차오르고, 0.80→0.98 에 꺼진다. u=0 과 u=1 에서 값이 같아 이어진다. */
    const lit = ease(clamp(u / 0.30)) * (1 - ease(clamp((u - 0.80) / 0.18)));

    /* ── 강조색 경보 띠 ─────────────────────────────
       빈 홈(흰 획)은 안 그린다 — 강조색과 흰색을 같은 자리에 겹치지 않으려고,
       띠는 「채움만」으로 지고 배경은 검정 그대로 둔다. */
    if (lit > 0.01) {
      const bw = BAR.w * ease(clamp(u / 0.30));
      ctx.save();
      ctx.globalAlpha = g0 * (0.55 + 0.45 * (0.5 + 0.5 * Math.sin(t * 7.4))) * lit;
      setShadow(ctx, FAIL_GLOW, 8);
      ctx.fillStyle = tone('fail');
      roundRect(ctx, BAR.x, BAR.y, Math.max(6, bw), BAR.h, 7);
      ctx.fill();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 흰 라벨 (배너 문구) ───────────────────────── */
    if (spec.bannerLabel) {
      let fs = 15; ctx.font = disp(800, fs);
      while (fs > 9 && ctx.measureText(spec.bannerLabel).width > 240) {
        fs -= 0.5; ctx.font = disp(800, fs);
      }
      const tw = ctx.measureText(spec.bannerLabel).width;
      ctx.save();
      ctx.globalAlpha = g0;
      ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
      ctx.fillText(spec.bannerLabel, clamp(176, 14 + tw / 2, w - 14 - tw / 2), 68);
      ctx.restore();
    }

    /* ── 흰 점선 원 = 경보가 가리킨 자리. 끝까지 빈다 ── */
    ctx.save();
    ctx.globalAlpha = g0 * 0.85;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.setLineDash([12, 10]); ctx.lineDashOffset = -t * 26;
    ctx.beginPath();
    ctx.arc(RING.cx, RING.cy, RING.r, 0, Math.PI * 2);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();

    /* ── 마을 네모 ─────────────────────────────────── */
    const bre = 1.2 * (1 + Math.sin(t * 6.2));
    ctx.save();
    ctx.globalAlpha = g0;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, TOWN.x + bre, TOWN.y + bre, TOWN.w - bre * 2, TOWN.h - bre * 2, 8);
    ctx.stroke();
    ctx.restore();

    /* ── 마을 사람 셋 — 저희끼리 돈다 ───────────────── */
    for (let i = 0; i < 3; i++) {
      const a = t * 0.55 + i * 2.0944;
      const x = RING.cx + Math.cos(a) * ORBIT;
      const y = RING.cy + Math.sin(a) * ORBIT;
      ctx.save();
      ctx.globalAlpha = g0;
      ctx.translate(x, y);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -5, -11, 10, 22, 5);
      ctx.stroke();
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
