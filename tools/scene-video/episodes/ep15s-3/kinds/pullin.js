/* 📐 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이고, 강조색은 글로우(blur 8)까지 센다.
   여기서는 집이 부푸는 순간(도착 맥동 1.14배 · 등장 스프링 1.05배)까지 다 먹여서 쟀다.
   상시 숨(±2px)도 포함이다. */
import {
  disp, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* pullin — 뒤집힘과 착지. 거리를 판정이 아니라 배치로 풀었다.

   원문 근거:
   > "그래서 거리는 판정 반경이 아니라 배치로 풀었습니다. 서비스 직업(목수·요리사·치료사)에
   >  택지 가산 음수를 줘서 마을 안쪽으로 당겼습니다. 떠돌이 목수는 … 기지에서 8타일입니다.
   >  주민들이 자재를 나르러 오가는 구역이라 마주침이 성립합니다."
   > "그러면서도 정주형 목수는 여전히 최근접이라 성격 차이는 살아 있습니다."

   자막 0(8타일 안으로 당겼다) : 오른쪽에 떨어져 있던 강조색 집이 마을 쪽으로 미끄러져 들어와
     앉고, 마을의 흰 표식에서 점들이 흘러 그 집에 닿는다. 닿는 순간 집이 한 번 부푼다.
   자막 1(성격 차이는 살아 있다) : 훨씬 가까운 자리에 집이 하나 더 앉고 거기로도 점이 닿는다.
     **둘 다 닿는데 앉은 거리가 서로 다르다** — 그게 이 편의 남는 한 줄이다.

   🔑 집이 출발하는 자리(x 306)는 **S1 이 목수를 두고 온 바로 그 자리**다. 회차 안에서
   같은 좌표를 되받아 「저기 있던 그 집」으로 읽히게 했다.
   🔴 **화면 밖에서 밀려 들어오지 않는다.** 이 캔버스(352px)에서 화면 밖 translate 등장은
   거의 항상 가장자리를 문다 — 출발점이 처음부터 캔버스 안이고, 뜻도 이쪽이 맞는다.
   🔴 **당기는 줄을 안 그렸다.** 「당겼다」는 원문의 낱말이지만 물리적으로 끌어당기는 줄은
   게임에 없다 — 줄을 그리는 순간 원문에 없는 장치를 내가 지어내는 것이 된다. 집이 옮겨
   앉는 것만 그렸다.
   🔴 **가까운 집에는 수를 안 붙였다.** 원문이 「최근접」이라고만 쓰고 수를 안 준다.

   라벨을 다 가려도 「밖에 있던 것이 안으로 들어와 닿고, 훨씬 가까운 자리에 하나가 더 앉아
   거기로도 닿는다. 둘이 앉은 거리가 서로 다르다」가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세 겹으로 막았다.**
     ⓐ 점의 끝을 잘라서. 흰 점(반지름 3.5)은 먼 집 중심에서 40px 앞에서 멈춘다 — 그 집이
        도착 맥동으로 가장 부푼 순간에도 글로우·숨까지 **6.2px** 뜬다. 가까운 집 쪽은
        40px 앞에서 멈춰 가장 가까운 획까지 **4.9px** 뜬다.
     ⓑ 각도로 통로를 비워서. 두 갈래 점 흐름의 각도가 −16.9° 와 +40.1° 라 서로의 집을
        스치지 않는다.
     ⓒ 왼쪽 라벨을 좁혀서. 「마을」(sub, 중심 40)의 오른쪽 끝이 53 이고 가까운 집의 글로우
        왼쪽 끝이 100.75 라 **47.7px** 뜬다. 흰 말풍선도 x 67.5 에서 끝난다.

   세로 예산(캔버스 307): 최저 = 「눌러사는 목수」 글립 바닥 282. 25px 남는다.
   가로: 오른쪽 = 먼 집이 **출발 자리**에 있을 때의 글로우 + 숨 332.5(여유 17.5px) ·
   왼쪽 = 흰 말풍선 36.5.

   계속 도는 것 = 집 이동(81.6px / 1.00초 = **프레임당 2.7px**) · 점 흐름
   (90px/s = **프레임당 3.0px**, 자막 1 에서 두 갈래로 늘어난다) ·
   표식·집의 상시 숨(**프레임당 0.49px**). */

const VX = 52, VY = 164;
const FAR_X = 236, FAR_Y = 108;        // 당겨 온 뒤 떠돌이 목수의 집
const START_X = 306, START_Y = 150;    // S1 이 목수를 두고 온 자리
const NEAR_X = 128, NEAR_Y = 228;      // 눌러사는 목수의 집
const PITCH = 12;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

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

    const house = (x, y, sc, alpha) => {
      if (alpha <= 0.01) return;
      const hw = 15 * sc, hh = 18 * sc;
      ctx.save();
      ctx.globalAlpha = alpha;
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(x - hw, y + hh);
      ctx.lineTo(x - hw, y - hh * 0.2);
      ctx.lineTo(x, y - hh);
      ctx.lineTo(x + hw, y - hh * 0.2);
      ctx.lineTo(x + hw, y + hh);
      ctx.closePath(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    };

    /* 마을 사람에게서 목표로 흐르는 흰 점. 시작·끝을 거리로 잘라 강조색 글립을 안 문다. */
    const ray = (tx, ty, from, to, alpha) => {
      if (alpha <= 0.01) return;
      const dx = tx - VX, dy = ty - VY, len = Math.hypot(dx, dy);
      const ux = dx / len, uy = dy / len;
      const room = Math.max(0, to - from);
      const NP = Math.ceil(room / PITCH) + 1;
      const SPAN = NP * PITCH;
      ctx.save();
      ctx.fillStyle = tone('ink');
      for (let i = 0; i < NP; i++) {
        const s = (((i * PITCH + t * 90) % SPAN) + SPAN) % SPAN;
        if (s > room) continue;
        const a = clamp(s / 9) * clamp((room - s) / 11);
        if (a <= 0.02) continue;
        ctx.globalAlpha = alpha * a;
        ctx.beginPath();
        ctx.arc(VX + (from + s) * ux, VY + (from + s) * uy, 3.5, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();
    };

    const st = ease(clamp(t / 0.28));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    const bx = i => 2 * Math.sin(t * 7.4 + i * 1.7);
    const by = i => 2 * Math.cos(t * 6.1 + i * 2.3);

    /* ── 자막 0 : 집이 마을 쪽으로 옮겨 앉는다 ────── */
    const s0 = Math.max(0, since(spec.pullCue ?? 0));
    const slide = ease(clamp((s0 - 0.55) / 1.00));   // 81.6px / 1.00초 = 프레임당 2.7px
    const fx = lerp(START_X, FAR_X, slide) + bx(0);
    const fy = lerp(START_Y, FAR_Y, slide) + by(0);
    /* 앉는 순간의 맥동 — 「닿았다」를 크기로 말한다(밝기만 흔드는 것은 금지). */
    const land = 1 + 0.14 * Math.sin(Math.PI * clamp((s0 - 1.50) / 0.45));

    const farLen = Math.hypot(FAR_X - VX, FAR_Y - VY);
    const linked = st * ease(clamp((s0 - 1.45) / 0.35));
    ray(FAR_X, FAR_Y, 26, farLen - 40, linked);

    house(fx, fy, land, st);

    /* ── 자막 1 : 훨씬 가까운 자리에 하나가 더 앉는다 ── */
    const s1 = since(spec.keepCue ?? 1);
    const nearA = s1 >= 0 ? ease(clamp(s1 / 0.30)) : 0;
    if (nearA > 0.01) {
      const nearLen = Math.hypot(NEAR_X - VX, NEAR_Y - VY);
      house(NEAR_X + bx(1), NEAR_Y + by(1), spring(clamp(s1 / 0.45)), st * nearA);
      ray(NEAR_X, NEAR_Y, 22, nearLen - 40, st * ease(clamp((s1 - 0.45) / 0.35)));
    }

    /* ── 마을 사람과 그 말풍선 ────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    ctx.beginPath(); ctx.arc(VX + bx(2), VY + by(2), 9, 0, Math.PI * 2); ctx.stroke();
    if (linked > 0.01) {
      ctx.globalAlpha = linked;
      const hw = 14, hh = 10;
      ctx.beginPath();
      ctx.moveTo(VX - hw, 124 - hh);
      ctx.lineTo(VX + hw, 124 - hh);
      ctx.lineTo(VX + hw, 124 + hh);
      ctx.lineTo(VX + hw * 0.25, 124 + hh);
      ctx.lineTo(VX + hw * 0.55, 124 + hh + 7);
      ctx.lineTo(VX + hw * 0.55, 124 + hh);
      ctx.lineTo(VX - hw, 124 + hh);
      ctx.closePath(); ctx.stroke();
    }
    ctx.restore();

    ctext(spec.villageLabel ?? '', 40, 206, 800, 13, tone('sub'), 80, st);
    ctext(spec.pulledLabel ?? '', FAR_X, 66, 900, 13.5, tone('accent'), 100,
      st * ease(clamp((s0 - 1.60) / 0.40)));
    ctext(spec.farName ?? '', FAR_X, 158, 900, 13, tone('accent'), 110,
      s1 >= 0 ? st * ease(clamp((s1 - 0.20) / 0.35)) : 0);
    ctext(spec.nearName ?? '', NEAR_X, 278, 900, 13, tone('accent'), 120,
      s1 >= 0 ? st * ease(clamp((s1 - 0.55) / 0.35)) : 0);

    ctx.textAlign = 'left';
  }
};
