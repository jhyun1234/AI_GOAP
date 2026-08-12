import {
  ease, easeOut, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, DEPTH,
  project, cam3, invertFlash, speedLines
} from '../../../engine/lib.js';

/* blankstone — **묘지 안을 카메라가 걸어 들어간다.** 비석 일곱이 앞뒤로 흩어져 서 있고,
   가장 가까운 비석에 파인 이름 자리가 끝까지 비어 있다.

   원문 근거:
   > "죽은 자리에는 회색 원이 하나씩 남습니다. 무덤입니다. 그런데 그 원에는 아무것도 안
   >  적혀 있습니다."
   > "화면을 가득 채운 회색 원 일곱 개를 보면서, 저는 그중 누가 제가 아침에 지켜보던
   >  그 주민인지 알 수 없었습니다."

   ── 2026-08-12 개정 v8 (사용자 판정 「2D 에서 움직이고 있다. 3D 로 보이게」) ──────
   🔴 **화면 좌표를 버리고 월드 좌표(x, y, z)로 놓는다.** 비석 일곱을 지면 위 서로 다른
      깊이에 세우고, 카메라가 **앞으로 들어가면서 살짝 돈다**(dolly + orbit).
      전까지의 `camAt` 확대는 그림 전체를 똑같이 키우는 것이라 「가까워졌다」가 아니라
      「커졌다」로 읽혔다. 깊이는 배율이 아니라 **시차**에서 온다 —
      카메라가 전진하면 앞 비석은 화면 밖으로 밀려나고 뒤 비석은 천천히 커진다.
      물체마다 흐르는 속도가 다른 것, 그 하나가 3D 신호의 전부다.

   🔑 원근이 붙자 손으로 정하던 것들이 저절로 맞는다:
      · 크기 위계 — 뒤 비석이 작은 것은 배열에 알파를 적어서가 아니라 **멀어서**다.
      · 가림 — 깊이 정렬만 하면 앞 비석이 뒤를 덮는다.
      · 옆면 두께 — 앞면과 뒷면을 각자의 깊이로 던지면 **중앙 왼쪽 비석은 오른쪽 면이,
        오른쪽 비석은 왼쪽 면이** 저절로 보인다. 손으로 그리면 절대 안 맞던 부분이다.
      · 그림자 — 지면(y=0) 위의 타원을 같이 던지면 원근이 맞는다.

   🔴 **촬영 박자(on 2s) 는 걷어냈다.** 레퍼런스를 보고 넣었는데 사용자 실물 판정이
      「렉 걸린 것처럼 버벅거린다」였고, 맞는 말이다. 손그림의 on 2s 는 **한 스텝마다
      다른 그림**이라 성립한다. 우리는 연속 파라미터를 양자화한 것이라 같은 도형이
      뚝뚝 끊기고, 그건 프레임 드랍과 구분이 안 된다. **인과를 잘못 짚었다.**

   🔴 색 규약(namefade 에서 세운 것 그대로): 강조색 = 이름이 들어갈 자리 / 흰색 = 무덤.
      이름 자리는 비석을 **파낸 홈**(destination-out)이고 그 안의 커서만 강조색이다.
      한 획도 안 채워진다 — 그것이 이 편의 진단이다.
   🔴 무덤은 **일곱**이다(원문 수치). 깊이로 흩었을 뿐 수는 그대로다.

   phase (샷 분할):
     phase 0 (S1a) = 카메라가 묘지로 들어간다. 자막 「죽은 자리엔 동그라미가 하나씩 남아요」
     phase 1 (S1b) = 앞 비석의 빈 이름 자리로 더 파고든다.
                     자막 「무덤인데 아무것도 안 적혀 있었죠」

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색은 앞 비석 이름 자리 안의 커서뿐이고, 그 자리는 비석을 **파낸 홈** 한가운데다.
            홈의 벽(흰 비석)까지 좌우로 홈 폭의 30%(원근 배율과 무관하게 비율 고정)가 뜬다.
            글로우가 홈 벽에 닿는데, **겹치는 상대가 자기 홈의 벽이라 의도한 쌍**이다.
     축 B · 흰 글자가 0개다. 흰 비석끼리는 **깊이로 갈린다** — 겹치면 앞이 뒤를 덮는 것이
            원근의 정의이고, 이것이 이 그림의 뜻(구별이 안 되는 무리)이다.
   화면 예산: 카메라가 움직이므로 좌표가 아니라 **카메라 트랙**으로 관리한다.
     지평선은 언제나 화면 세로 한가운데(z→∞ 에서 배율 0)이고, 자막 안전영역(아래 34%)에는
     지면만 온다. 앞 비석은 전진 중 화면 좌우로 나간다 → `checks.edge` 선언 대상이다.

   계속 도는 것: 카메라가 내내 움직인다(정적 구간이 원리적으로 안 생긴다).
   그 위에 커서가 1.1초 주기로 깜빡인다. */

/* 월드 — [x, z]. 지면은 y=0, 비석은 위로 자란다(y 음수).
   z 0 이 가장 가깝고, 일곱 번째가 가장 멀다. 좌우를 번갈아 흩어 카메라가 전진할 때
   양쪽으로 갈라져 흐르게 했다 — 시차가 가장 잘 보이는 배치다. */
const STONES = [
  [0, 0], [-132, 96], [142, 108], [-258, 250], [268, 268], [-104, 430], [120, 452],
];
const SW = 68, SH = 104, SD = 26;      // 비석 폭·높이·두께 (월드 단위)
const SLOT = { w: 0.62, h: 0.20, dy: 0.42 };   // 비석 대비 비율 — 원근이 알아서 줄인다

/** 둥근 윗변 슬래브 (화면 좌표) */
function slab(ctx, x, yBase, w, h) {
  const r = w / 2;
  ctx.beginPath();
  ctx.moveTo(x - r, yBase); ctx.lineTo(x - r, yBase - h + r);
  ctx.arc(x, yBase - h + r, r, Math.PI, 0);
  ctx.lineTo(x + r, yBase); ctx.closePath();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const ph = spec.phase ?? 0;
    const c0 = ph === 0 ? cue(0, 0.15, 0.55) : 1;
    if (c0 <= 0.005) return;
    const cam = cam3(spec.cam3, t);

    /* ── 지면 ───────────────────────────────────────
       지평선은 원근상 언제나 화면 한가운데다. 그 아래를 아주 옅게 깔아 「바닥이 있다」만
       말한다 — 격자를 깔면 배경이 인포그래픽과 경쟁한다(가이드 금지). */
    /* 🔴 처음엔 지평선에 선을 긋고 아래를 알파 0.16 으로 깔았다. 실물에서 **바닥이 아니라
       회색 벽**으로 읽혔다 — 위쪽 경계가 너무 또렷하고 면이 평평해서다. 원근이 이미 깊이를
       말하고 있으므로 지면은 「있다」만 하면 된다. 지평선 쪽을 0 으로 두고 발밑으로 갈수록
       아주 옅게 밝히면 면이 눕는다. */
    const hz = h / 2;
    const gg = ctx.createLinearGradient(0, hz, 0, h);
    gg.addColorStop(0, 'rgba(255,255,255,0.00)');
    gg.addColorStop(0.55, 'rgba(255,255,255,0.05)');
    gg.addColorStop(1, 'rgba(255,255,255,0.10)');
    ctx.fillStyle = gg;
    ctx.fillRect(0, hz, w, h - hz);

    /* ── 비석 일곱 — 먼 것부터(painter) ─────────────
       각 비석의 앞면·뒷면을 **각자의 깊이로** 던진다. 그래서 옆면 두께가 위치에 따라
       저절로 달라진다(중앙 왼쪽은 오른쪽 면, 오른쪽은 왼쪽 면). */
    const drawn = STONES
      .map(([x, z], i) => ({ x, z, i, F: project(x, 0, z, cam, w, h) }))
      .filter(o => o.F.d > 14)
      .sort((a, b) => b.F.d - a.F.d);

    let heroSlot = null;
    for (const o of drawn) {
      /* 등장 — 먼 것부터 순서대로 솟는다. phase 1 은 이미 다 서 있다. */
      const rise = ph === 0
        ? clamp((c0 - (STONES.length - 1 - o.i) * 0.055) / 0.22)
        : 1;
      if (rise <= 0.01) continue;
      const grow = easeOut(rise);

      const B = project(o.x, 0, o.z + SD, cam, w, h);      // 뒷면 바닥
      const sw = SW * o.F.s, sh = SH * o.F.s * grow;
      const bw = SW * B.s, bh = SH * B.s * grow;

      ctx.save();
      /* 바닥 그림자 — 지면 위 타원도 같은 원근으로 */
      ctx.globalAlpha = 0.42;
      ctx.fillStyle = '#000';
      ctx.beginPath();
      ctx.ellipse(o.F.x + (B.x - o.F.x) * 0.5, o.F.y, sw * 0.62, sw * 0.16, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();

      /* 뒷면 — 어둡게. 앞면이 덮고 남는 띠가 곧 두께다 */
      ctx.fillStyle = DEPTH[3];
      slab(ctx, B.x, B.y, bw, bh); ctx.fill();

      /* 앞면 — 가까울수록 밝다. 깊이를 색으로도 한 번 말한다 */
      const near = clamp((520 - o.F.d) / 420);
      const g = ctx.createLinearGradient(o.F.x - sw / 2, o.F.y - sh, o.F.x + sw / 2, o.F.y);
      g.addColorStop(0, near > 0.5 ? '#FFFFFF' : '#D6D6D6');
      g.addColorStop(1, near > 0.5 ? '#C4C4C4' : '#9A9A9A');
      ctx.fillStyle = g;
      slab(ctx, o.F.x, o.F.y, sw, sh); ctx.fill();

      /* 이름 자리 — 파낸 홈. 비율로 잡아 두면 원근이 알아서 줄인다 */
      const slotW = sw * SLOT.w, slotH = sh * SLOT.h;
      const slotY = o.F.y - sh + sh * SLOT.dy;
      ctx.save();
      ctx.globalCompositeOperation = 'destination-out';
      ctx.fillStyle = '#000';
      ctx.fillRect(o.F.x - slotW / 2, slotY, slotW, slotH);
      ctx.restore();

      if (o.i === 0) heroSlot = { x: o.F.x, y: slotY, w: slotW, h: slotH, rise };
    }

    /* ── 앞 비석의 커서 — 쓸 자리는 있는데 아무도 안 쓴다 ── */
    if (heroSlot && heroSlot.rise > 0.9 && frac(t / 1.1) < 0.55) {
      ctx.save();
      setShadow(ctx, GLOW, 10);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(heroSlot.x - heroSlot.w * 0.04, heroSlot.y + heroSlot.h * 0.16,
        heroSlot.w * 0.08, heroSlot.h * 0.68);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 앞 비석이 자리를 잡는 순간의 임팩트 (phase 0 에서 한 번) ── */
    if (ph === 0) {
      const hit = clamp((c0 - 0.72) / 0.08) * clamp((0.96 - c0) / 0.12);
      if (hit > 0.01 && heroSlot) {
        speedLines(ctx, w, h, heroSlot.x, heroSlot.y, hit, { n: 22, seed: 31, alpha: 0.5 });
        invertFlash(ctx, w, h, hit > 0.62);
      }
    }
  }
};
