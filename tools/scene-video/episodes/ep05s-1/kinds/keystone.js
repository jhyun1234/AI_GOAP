import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* keystone — 첫 샷이 세워 둔 기둥이, 마지막에 무엇을 받고 있었는지 드러난다.

   이 편(5-1)의 **마지막 샷**이자 착지다. 첫 샷 hairline 은 「차이 1」 한 칸에서
   가느다란 기둥을 솟게 해 놓고 꼭대기를 비워 뒀다 — 그 빚을 여기서 갚는다.
   화면 폭을 꽉 채우는 넓은 판이 위에서 내려와 그 실 같은 기둥 하나에 얹힌다.
   판에 적힌 것은 원문 3절 그대로다: "왜 요리를 해야 하는가" / "게임 루프 전체의 동기가
   이 숫자에 걸려 있다".

   🔴 첫 샷과 같은 그림이 아니다. hairline 은 **재는 그림**이라 탑 둘·눈금·괄호가 있고,
   여기에는 탑도 눈금도 괄호도 없다. 남은 것은 한 칸과 기둥 하나뿐이고, 새로 오는 것은
   위에서 내려오는 무게다. 눈이 읽어야 하는 것도 다르다 — 저기서는 '차이가 몇 칸인가',
   여기서는 '저 큰 것을 저 가는 것이 혼자 받고 있다'는 불균형이다.
   그래서 판의 폭(320px)과 기둥의 굵기(5px)를 64:1 로 벌려 놨다.

   🔴 게이지·막대·퍼센트를 쓰지 않는다. 이 편은 값이 오르내리는 이야기가 아니라
   작은 것 하나에 큰 것이 걸려 있다는 이야기라, 크기 대비 말고는 아무것도 안 센다.

   계속 도는 것 = 기둥을 타고 위로 흐르는 표식 넷(자막 0부터, 아래의 한 칸에서 위의
   판으로 힘이 올라가는 것), 한 칸 안을 위로 흐르는 빗금, 판이 앉은 뒤의 미세한 눌림.
   🔑 예고 자막 구간(마지막 자막)에 캔버스에서 도는 것은 **기둥의 표식 넷 · 칸 안 빗금 ·
   판이 앉은 뒤의 눌림** 이 셋이다. 훅은 여기서 안 그린다 — 아웃트로 카드 안의 .oc-hook
   이 맡는다(아래 draw 끝의 주석 참조, 2026-08-04). */

const RISE = 1.9;
const FLOW = 2.3;
const PRESS = 2.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const sk = ease(cue(spec.loadCue ?? 0, 0.15, 0.55));   // 판이 내려와 앉는다

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const SLAB_TOP = 42, SLAB_H = 58;
    const PILLAR_TOP = SLAB_TOP + SLAB_H;          // 100 — 판이 앉는 높이
    const NW = 62, NH = 36, NY = 192;
    const nx = (w - NW) / 2, cx = w / 2;

    const base = clamp(sk * 5);                    // 한 칸과 기둥은 이미 서 있다(첫 샷의 것)

    /* ── 한 칸 — 첫 샷의 「차이 1」이 그대로 남아 있다 ── */
    if (base > 0.02) {
      ctx.globalAlpha = base;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, nx, NY, NW, NH, 3); ctx.stroke();

      /* 계속 도는 것 — 한 칸 안을 위로 흐르는 빗금 */
      ctx.save();
      ctx.beginPath(); ctx.rect(nx + 2, NY + 2, NW - 4, NH - 4); ctx.clip();
      ctx.globalAlpha = base * 0.3;
      const off = frac(t / FLOW) * 18;
      for (let hy = NY + NH + 18 - off; hy > NY - 20; hy -= 18) {
        ctx.beginPath();
        ctx.moveTo(nx - 6, hy); ctx.lineTo(nx + NW + 6, hy - 11);
        ctx.stroke();
      }
      ctx.restore();
      ctx.globalAlpha = base;

      if (spec.notchLabel) {
        ctx.textAlign = 'left';
        const fs = fit(spec.notchLabel, 900, 15, w - (nx + NW) - 18, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.notchLabel, nx + NW + 10, NY + NH / 2 + 5);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 기둥 — 5px. 위의 판은 320px 다 ───────────── */
    if (base > 0.02) {
      ctx.globalAlpha = base;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      setShadow(ctx, GLOW, 10, 0);
      ctx.beginPath();
      ctx.moveTo(cx, NY); ctx.lineTo(cx, PILLAR_TOP);
      ctx.stroke();
      clearShadow(ctx);

      /* 계속 도는 것 — 기둥을 타고 위로 올라가는 표식 넷 */
      for (let i = 0; i < 4; i++) {
        const u = frac(t / RISE + i / 4);
        const y = lerp(NY, PILLAR_TOP, u);
        ctx.globalAlpha = base * 0.75 * Math.sin(Math.PI * u);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx - 6, y); ctx.lineTo(cx + 6, y);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 판이 내려와 그 위에 앉는다 ───────────────── */
    if (sk > 0.02) {
      const drop = ease(clamp(sk / 0.55));                 // 0.55 에서 착지
      const landed = clamp((sk - 0.55) / 0.25);
      const bob = 1.6 * Math.sin(frac(t / PRESS) * Math.PI * 2) * landed;
      const y = lerp(SLAB_TOP - 34, SLAB_TOP, drop) + bob;
      const bx = 16, bw = w - 32;

      ctx.globalAlpha = clamp(sk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink');
      roundRect(ctx, bx, y, bw, SLAB_H, 4); ctx.stroke();

      const tk = clamp((sk - 0.2) / 0.3);
      if (tk > 0.02 && spec.slabLabel) {
        ctx.globalAlpha = clamp(sk * 2) * tk;
        ctx.textAlign = 'center';
        const fs = fit(spec.slabLabel, 900, 18, bw - 26, 12);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.slabLabel, w / 2, y + 30);

        if (spec.slabNote) {
          const nfs = fit(spec.slabNote, 700, 11, bw - 26, 8);
          ctx.font = disp(700, nfs); ctx.fillStyle = tone('sub');
          ctx.fillText(spec.slabNote, w / 2, y + 48);
        }
      }
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
