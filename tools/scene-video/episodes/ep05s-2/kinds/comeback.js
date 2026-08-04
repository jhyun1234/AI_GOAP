import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* comeback — 집 한 채에 규칙 셋이 붙고, 그제야 주민에게 갈 데가 생긴다.

   집은 이 샷의 배경이다(앞의 세 샷에서 이미 지었다 — 여기서 처음 등장하는 것이 아니라서
   cue 에 안 물렸다). 주민 셋도 처음부터 서 있고, 집 앞을 왔다 갔다 한다.

   ① ruleCue — 왼쪽에 규칙 카드 셋이 아래에서부터 하나씩 꽂힌다.
      '서는 타일 분리' · '정원제' · '단일 창구' — 앞의 세 샷이 하나씩 세운 것들이다.
      집 한 채를 놓는 데 이만큼이 붙었다는 회수다.
   ② homeCue — 흩어져 서성이던 주민 셋의 머리 위에서 귀가선이 집 문으로 붙고,
      셋이 집 앞으로 모인다. 그 아래에 '돌아갈 곳'이 선다.
   ③ hookCue — 마지막 자막(다음 편 예고)에 훅 한 줄이 아래에서 올라온다.
      🔴 예고 자막에 cue 를 안 물리면 그 3초 동안 화면이 통째로 멈춘다(ep05s 가 그렇게
      정적 6.6초를 냈다). 훅을 예고에 물려 두면 읽는 동안 화면이 산다.

   🔴 이 그림은 고리를 그리지 않는다. 앞 구성의 결말 샷은 '밭 → 부엌 → 집'이 한 바퀴 도는
   고리였는데 그건 경제 루프 이야기(이 글의 다른 편이 맡은 사건)다. 이 편의 결말은 순환이
   아니라 **한 점으로 모이는 것**이다 — 흩어져 있던 사람이 같은 곳으로 온다.

   계속 도는 것 = ① 주민 셋의 서성임과 숨(자막 0 부터 끝까지, 모인 뒤에도 멈추지 않는다)
   ② 귀가선 위를 흐르는 점선. */

const CHIP = { x: 8, w: 140, h: 30, ys: [176, 134, 92] };   // 아래에서 위로 꽂힌다
const HX = 214, HW = 84, HTOP = 104, HBOT = 172;
const CX = HX + HW / 2;                                      // 256
const APEX_Y = 70, EAVE_L = 204, EAVE_R = 308;
const VIL = [208, 256, 304], DOOR = [222, 256, 290], VY = 232;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const rk = ease(cue(spec.ruleCue ?? 0, 0.15, 0.62));
    const hk = ease(cue(spec.homeCue ?? 1, 0.15, 0.7));
    const kk = ease(cue(spec.hookCue ?? 2, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const home = ease(clamp((hk - 0.35) / 0.5));

    /* ── 집 — 이 샷의 배경 ───────────────────────── */
    ctx.globalAlpha = 1;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
    setShadow(ctx, GLOW, 12, 0);
    roundRect(ctx, HX, HTOP, HW, HBOT - HTOP, 3); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(EAVE_L, HTOP); ctx.lineTo(CX, APEX_Y); ctx.lineTo(EAVE_R, HTOP);
    ctx.stroke();
    clearShadow(ctx);

    // 문 — 모이는 자리
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(CX - 12, HBOT); ctx.lineTo(CX - 12, HBOT - 26);
    ctx.lineTo(CX + 12, HBOT - 26); ctx.lineTo(CX + 12, HBOT);
    ctx.stroke();

    /* ── 주민 셋 — 서성이다가 집 앞으로 모인다 ───── */
    const pos = VIL.map((x0, i) => {
      const drift = 16 * Math.sin(frac(t / 3.8) * Math.PI * 2 + i * 2.1);
      const bob = 2.2 * Math.sin(frac(t / 2.4) * Math.PI * 2 + i * 1.3);
      return { x: lerp(x0 + drift, DOOR[i], home), y: VY + bob * (1 - 0.5 * home) };
    });

    if (hk > 0.05) {
      const k = clamp(hk * 1.8);
      ctx.globalAlpha = k * 0.85;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([7, 6]);
      ctx.lineDashOffset = -(t * 12) % 13;       // 계속 도는 것 — 집으로 향하는 길
      pos.forEach(p => {
        ctx.beginPath();
        ctx.moveTo(p.x, p.y - 32);
        ctx.lineTo(CX, HBOT);
        ctx.stroke();
      });
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    pos.forEach(p => {
      ctx.globalAlpha = 1;
      ctx.fillStyle = home > 0.6 ? tone('accent') : tone('ink');
      roundRect(ctx, p.x - 6, p.y - 20, 12, 20, 3); ctx.fill();
      ctx.beginPath(); ctx.arc(p.x, p.y - 26, 5, 0, Math.PI * 2); ctx.fill();
    });

    /* ── 규칙 카드 셋 ─────────────────────────────── */
    (spec.rules || []).slice(0, 3).forEach((label, i) => {
      const born = clamp(rk * 3.2 - i * 0.62);
      if (born < 0.02) return;
      const s = Math.min(1, spring(clamp(born)));
      const y = CHIP.ys[i];
      ctx.globalAlpha = clamp(born * 1.8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, CHIP.x + (CHIP.w - CHIP.w * s) / 2, y + (CHIP.h - CHIP.h * s) / 2,
        CHIP.w * s, CHIP.h * s, 3);
      ctx.stroke();
      ctx.textAlign = 'center';
      const fs = fit(label, 800, 13, CHIP.w - 16, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(label, CHIP.x + CHIP.w / 2, y + CHIP.h / 2 + 5);
      ctx.globalAlpha = 1;
    });

    if (rk > 0.6 && spec.rulesNote) {
      ctx.globalAlpha = clamp((rk - 0.6) / 0.4);
      ctx.textAlign = 'left';
      const fs = fit(spec.rulesNote, 700, 11, CHIP.w + 20, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.rulesNote, CHIP.x, 74);
      ctx.globalAlpha = 1;
    }

    /* ── '돌아갈 곳' ─────────────────────────────── */
    if (home > 0.15 && spec.returnLabel) {
      const k = clamp((home - 0.15) / 0.4);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.returnLabel, 900, 18, 168, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.returnLabel, CX, 268);
      ctx.globalAlpha = 1;
    }

    /* 🔴 훅 카드는 여기서 그리지 않는다(2026-08-04 사용자 판정).
       .outrocard 가 .vis 와 좌표가 한 픽셀도 다르지 않고 배경이 불투명이라, 마지막
       OUTRO_MS(2.6초) 동안 이 캔버스는 통째로 덮인다 — 검수 실측으로 ep04s-3 은 훅이
       단 한 프레임도 보인 적이 없었고(카드 등장 35,765ms vs 예고 자막 35,776ms),
       가장 오래 보인 ep05s-1 도 100ms 였다. 훅은 engine/index.html 의 .oc-hook 이 맡아
       카드 안에서 2.6초 내내 서 있다. kind 는 그림에만 집중한다.
       🔑 "두 곳에 적으면 갈린다"가 원래 원칙이었는데 카드와 캔버스 사이에서 깨져 있었다. */
ctx.textAlign = 'left';
  }
};
