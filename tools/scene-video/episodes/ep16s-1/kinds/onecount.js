import {
  disp, mono, ease, easeOut, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, PALETTE, depthGrad, camAt
} from '../../../engine/lib.js';

/* onecount — 무덤 일곱 위로 거대한 「+1」 도장이 떨어져 **전부 납작하게 눌러 버리고**,
   남는 것은 그 도장 하나뿐이다.

   원문 근거:
   > "누가 죽었냐고? 사망 카운터 +1. 내가 가진 건 그게 전부인데."
   ← 원문이 옛 코드의 속마음을 **사람 말로 옮겨 준** 대목이다(ADR-V25-13 ①풀어쓰기).
      화면 라벨 「내가 가진 것」과 화면 문자열 「+1」은 둘 다 이 인용에 글자 그대로 있다.
   🔴 누적값(일곱까지 올라간 수 같은 것)은 원문에 없으므로 **숫자로 안 적는다.**

   ── 2026-08-12 개정 v4 (그림 어휘 교체) ────────────────────────────
   🔴 **비유를 바꿨다.** v3 까지는 「동그라미 일곱에서 점선 줄기가 흘러 칸으로 모인다」였다.
      흐름도다. 흐름도는 읽어야 알고, 읽으려면 자막이 필요했다.
      지금은 **도장이 쿵 찍혀 사람들이 숫자가 된다** — 한 장면으로 뜻이 선다.
      🔑 이 편에서 가장 나쁜 자리가 여기다(사람이 수로 접힌다). 그러면 그림도 가장 폭력적인
         동작이어야 한다. 「흘러 모인다」는 그 자리에 너무 얌전했다.
   🔴 **무한 루프를 걷어냈다.** 점선 흐름·숨쉬기가 사라지고 도장의 낙하 한 번이 대신한다.
   🔴 **크다.** 도장 폭 250 = 캔버스 352 의 **71%**. 옛 칸은 120(34%)이었다.

   ⏱ 낙하는 `since(0)` 위에 세웠다 — **`cue` 가 아니라 `since` 인 것이 효과음의 전제다.**
      `since` 는 자막이 시작된 뒤 흐른 초라 `delayMs` 와 원점이 같다.
   🔴 **`sfx.delayMs 285` 를 한 자도 안 고쳤다.** sweep 은 `lp × sin(π·k)` 라 에너지 정점이
      dur 의 절반(0.27초) 뒤이므로 정점이 **0.555초**에 온다. 그래서 도장이 바닥에 닿는
      순간을 **0.555초**에 맞췄다 — 소리의 정점과 그림의 충돌이 같은 프레임이다.
      v3 까지는 이 시각이 「일곱 줄기가 흘러내리는 구간의 한가운데」였고, 소리는 같은 자리에
      있었지만 화면에서는 아무것도 부딪히지 않았다.

   phase (샷 분할):
     phase 0 (S2a) = 비석 일곱 → 도장이 떨어져 납작하게 누른다.
                     자막 「누가 죽었냐고 물으면 답이 하나였어요」
     phase 1 (S2b) = 카메라가 도장으로 파고든다. 비석은 이미 없다.
                     자막 「죽은 사람 수만 1 올라가고, 끝이었죠」
   🔑 phase 1 에 비석을 안 그리는 것이 요점이다 — 「내가 가진 건 그게 전부」라면
      화면에 남는 것도 그것 하나여야 한다.

   🔴 색 규약: 이 샷에는 **강조색이 한 점도 없다.** 이 편에서 강조색은 「이름」인데
      여기서 남는 것은 이름이 아니라 수뿐이다. 도장은 흰 판이고 「+1」은 배경색으로 **파낸다**
      (blankstone 의 이름 자리와 같은 수법 — 파인 것은 새겨진 것이다).

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색 0개 — 잴 것이 없다.
     축 B · 흰 글자가 0개다(「+1」은 도장을 파낸 홈이라 글자가 아니라 배경이다).
            라벨 「내가 가진 것」만 흰 계열(sub)이고, 도장 바깥 아래변 236 과 라벨 글립
            꼭대기 262−11 = 251 사이가 **15px** 뜬다.
            흰 실루엣끼리는 비석 일곱이 서로 6px 간격인데 「늘어서 있다」를 만드는 의도한 쌍이다.
   세로 예산(캔버스 307): 최저 = 라벨 내림선 약 **266**(41px 남음). 최고 = 도장 꼭대기 **136**,
     낙하 시작 시각에는 화면 위(−80)에서 들어온다.
   가로: 도장 51~301. 비석 줄 46~306(눌리면 폭 42.5 까지 벌어져 24.75~327.25).
     바깥 2px 띠에서 좌우 22.75 뜬다 — 처음엔 22~330 이었는데 눌린 폭이 캔버스를 넘어
     가장자리 검사에 75% 구간으로 걸렸다(실측). 벌어지는 폭까지 예산에 넣어야 한다.
   phase 1(1.5 → 2.8 확대 · focus [0.5, 0.62]): 도장의 좌우가 화면 밖으로 나간다
     → `checks.edge` 선언 대상.

   계속 도는 것: 낙하가 끝난 뒤 남는 것은 「+1」의 깜빡임(0.62초 주기) 하나다.
   정적 3초 검사를 넘기려면 무언가는 살아 있어야 하는데, 이 편에서 깜빡여도 되는 것은
   「계속 올라가는 수」뿐이다 — 뜻과 맞는 유일한 루프라 이것만 남겼다. */

const GROUND = 236;
const STAMP = { x: 176, w: 250, h: 100, y: 136 };   // 도장 — 캔버스 폭의 71%
const STONE_Y = 232, STONE_W = 34, STONE_H = 52;
const XS = [46, 89, 132, 176, 219, 262, 306];

function stone(ctx, x, y, w, h) {
  const r = w / 2;
  ctx.beginPath();
  ctx.moveTo(x - r, y); ctx.lineTo(x - r, y - h + r);
  ctx.arc(x, y - h + r, r, Math.PI, 0);
  ctx.lineTo(x + r, y); ctx.closePath();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);
    ctx.textBaseline = 'alphabetic';

    const ph = spec.phase ?? 0;
    const st = ph === 0 ? ease(cue(0, 0.15, 0.20)) : 1;
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* 낙하 — 0.20 에 화면 위에서 들어와 **0.555초에 닿는다**(sweep 정점과 같은 프레임).
       0.20~0.32 는 살짝 위로 뺀다(anticipation). */
    const s0 = ph === 0 ? since(0) : 99;
    const drop = clamp((s0 - 0.20) / 0.355);
    const land = clamp((s0 - 0.555) / 0.12) * clamp((0.90 - s0) / 0.25);   // 임팩트

    /* ── 비석 일곱 — 눌리면 납작해진다 ────────────── */
    if (ph === 0) {
      const crush = ease(clamp((s0 - 0.555) / 0.22));
      ctx.save();
      ctx.globalAlpha = st * (1 - crush * 0.85);
      ctx.fillStyle = tone('ink');
      for (const x of XS) {
        const hh = STONE_H * (1 - crush * 0.92);
        stone(ctx, x, STONE_Y, STONE_W * (1 + crush * 0.25), Math.max(2, hh));
        ctx.fill();
      }
      ctx.restore();
    }

    /* ── 「+1」 도장 ───────────────────────────────── */
    const sy = ph === 0
      ? lerp(-150, 0, drop < 0.34 ? -0.10 * Math.sin(drop / 0.34 * Math.PI) : easeOut((drop - 0.34) / 0.66))
      : 0;
    if (ph === 1 || drop > 0.001) {
      ctx.save();
      ctx.globalAlpha = st;
      /* 닿는 순간 도장이 한 번 눌렸다 펴진다 — 눌리지 않으면 '내려놓았다'로 읽힌다 */
      const squash = land > 0.01 ? 1 - 0.10 * land : 1;
      ctx.translate(STAMP.x, STAMP.y + STAMP.h / 2 + sy);
      ctx.scale(1 + (1 - squash) * 0.8, squash);
      ctx.fillStyle = depthGrad(ctx, -STAMP.w / 2, -STAMP.h / 2, STAMP.w / 2, STAMP.h / 2, 'ink');
      roundRect(ctx, -STAMP.w / 2, -STAMP.h / 2, STAMP.w, STAMP.h, 12);
      ctx.fill();

      /* 「+1」 — 도장을 **파낸** 자리다. 글자를 얹지 않고 판을 파낸다. */
      ctx.globalCompositeOperation = 'destination-out';
      ctx.font = mono(700, 68);
      ctx.textAlign = 'center';
      ctx.fillStyle = '#000';
      const blink = ph === 1 ? (frac(t / 0.62) < 0.62 ? 1 : 0.28) : 1;
      ctx.globalAlpha = blink;
      ctx.fillText(spec.markLabel ?? '+1', 0, 24);
      ctx.restore();
    }

    /* ── 착지 임팩트 ─────────────────────────────── */
    if (land > 0.01) {
      ctx.save();
      ctx.globalAlpha = land * 0.8;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5; ctx.lineCap = 'round';
      for (let i = 0; i < 9; i++) {
        const a = Math.PI + (i + 0.5) / 9 * Math.PI;
        const r0 = 30 + 30 * (1 - land), r1 = r0 + 40;
        const cy = STAMP.y + STAMP.h;
        ctx.beginPath();
        ctx.moveTo(STAMP.x + Math.cos(a) * r0 * 1.5, cy - Math.sin(a) * r0 * 0.18);
        ctx.lineTo(STAMP.x + Math.cos(a) * r1 * 1.5, cy - Math.sin(a) * r1 * 0.18);
        ctx.stroke();
      }
      ctx.restore();
    }

    /* ── 라벨 ─────────────────────────────────────── */
    if (spec.haveLabel && ph === 0 && s0 > 1.05) {
      ctx.save();
      ctx.globalAlpha = st * clamp((s0 - 1.05) / 0.25);
      ctx.textAlign = 'center'; ctx.fillStyle = tone('sub');
      ctx.font = disp(900, 15);
      ctx.fillText(spec.haveLabel, STAMP.x, 262);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
