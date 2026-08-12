import {
  ease, easeOut, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, PALETTE, depthGrad, castShadow, DEPTH, camAt, invertFlash, speedLines
} from '../../../engine/lib.js';

/* blankstone — 무덤 일곱이 땅에서 솟는데, 앞의 비석에 파인 **이름 자리가 비어 있다.**

   원문 근거:
   > "죽은 자리에는 회색 원이 하나씩 남습니다. 무덤입니다. 그런데 그 원에는 아무것도 안
   >  적혀 있습니다."
   > "화면을 가득 채운 회색 원 일곱 개를 보면서, 저는 그중 누가 제가 아침에 지켜보던
   >  그 주민인지 알 수 없었습니다."

   ── 2026-08-12 개정 v4 (그림 어휘 교체 · 사용자 판정 「뭘 의미하는지 대사를 봐야 안다」)
   앞선 v1~v3 은 **카메라와 컷만 고쳤고 그림은 그대로 뒀다.** 자막을 가린 프레임을 뽑아
   보니 화면에 남는 것이 동그라미와 점선 네모뿐이라 아무것도 해독되지 않았다. 셋을 고친다.

   ① **형상이 그 물건처럼 생겼다.** 동그라미 → **비석**(둥근 윗변 슬래브). 원이 무덤인 것은
      약속이라 배워야 알지만, 비석은 보면 안다. `DESIGN_GUIDE.md` 1행이 「그림이 곧 설명
      이다」인데 지금까지 그림은 자막의 각주였다.
   ② **크다.** 주인공 비석 폭 150 = 캔버스 352 의 **43%**. 가이드가 원형 차트에 요구하는
      「화면의 40%+」를 처음으로 지킨다. 옛 동그라미는 지름 56 = **16%** 였다.
      그리고 **채운다** — 윤곽선 3px 은 검정 화면에서 무게가 없다. 실루엣이라야 눈에 걸린다.
   ③ **사건을 그린다.** 상태를 늘어놓지 않는다. 비석이 **땅에서 솟아 오르며 오버슛하고,
      착지에서 먼지가 튀고, 마지막 비석에서 방사선이 터진다.** 그 다음 정지한다.
      🔴 **무한 루프를 걷어냈다.** 숨쉬기(반지름 sin)와 점선 흐름은 `MINIMAL_DESIGN_
      LANGUAGE:433`(「루프 금지 — 어지러움」) 위반이면서, 동시에 정적 3초 게이트를 넘기려는
      타협이었다. 그래서 화면이 늘 떨리는데 아무 일도 안 일어났다. 지금은 **사건**이 게이트를
      넘기고, 사건이 끝난 뒤 남는 것은 이름 자리의 커서 깜빡임 하나뿐이다(1.1초 주기).
      커서만 남긴 이유: 정적 방지가 필요하고, 그 깜빡임 자체가 「쓸 자리는 있는데 안 쓴다」다.

   🔴 **일곱 개를 다 그린다**(원문 수치). 다만 주인공 하나만 크게 앞에 두고 나머지 여섯은
      뒤로 작게 겹쳐 세운다 — 일곱을 나란히 그리면 다시 전부 작아진다. 이게 v3 까지의
      함정이었다: 개수를 지키느라 크기를 잃었다.
   🔴 색 규약(namefade 에서 세운 것 그대로): 강조색 = 이름, 또는 이름이 들어갈 자리 /
      흰색 = 무덤. 그래서 이름 자리는 비석을 **파낸 홈**(배경색)이고 그 안의 커서만 강조색이다.
      한 획도 안 채워진다.

   phase (샷 분할):
     phase 0 (S1a) = 비석 일곱이 솟는다. 자막 「죽은 자리엔 동그라미가 하나씩 남아요」
     phase 1 (S1b) = 카메라가 이름 자리로 들어간다. 자막 「무덤인데 아무것도 안 적혀 있었죠」
     phase 1 에서 비석은 애니메이션 없이 이미 서 있다 — 컷 뒤에 되감으면 앞 샷을 못 본 셈이다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색은 이름 자리 안의 커서(x 176±3 · y 154~180)뿐이고, 그 자리는 비석을
            **파낸 홈**(배경색 121~231 × 150~184) 한가운데다. 홈의 벽(흰 비석)까지
            좌우 52 · 위아래 4px 이 뜬다. 글로우 8 을 얹으면 위아래가 −4 로 겹치는데,
            **겹치는 상대가 자기 홈의 벽이라 의도한 쌍**이다(파인 자리 안에서 빛난다).
     축 B · 흰 글자가 0개다. 흰 실루엣끼리는 뒤 비석이 주인공에 **일부러 가려진다**(원근).
   세로 예산(캔버스 307): 최저 = 지평선 272 + 먼지 4 = **276**(31px 남음).
     최고 = 주인공 비석 꼭대기 **87**. 임팩트 방사선은 착지 0.16초 동안만 지평선 둘레
     70px 까지 뻗는다(202~342 × 202~307 안).
   가로: 뒤 비석 왼쪽 끝 26−0.30×75 = **3.5** / 오른쪽 끝 **348.5**.
     🔴 바깥 2px 띠까지 1.5px 이라 가장자리 검사에 걸린다 — 화면 밖으로 이어지는 무리라는
     뜻이므로 scene.json 에 `checks.edge` 로 선언한다.

   계속 도는 것: **없다**(사건이 끝나면 정지). 커서만 1.1초 주기로 깜빡인다. */

const GROUND = 272;                       // 지평선
const MAIN = { x: 176, w: 128, h: 208 };  // 주인공 비석 — 캔버스 폭의 36%, 높이 68%
const SLOT = { w: 88, h: 30, dy: 92 };    // 이름 자리(파낸 홈) — 비석 꼭대기에서 dy 아래

/* 뒤 여섯 — [x, 크기배율, 알파]. 바닥을 살짝 올려(258) 뒤에 선 것으로 읽히게 한다.
   순서는 먼 것부터다(솟는 순서도 이 순서 = 뒤에서 앞으로). */
const BACK = [
  [26, 0.30, 0.18], [326, 0.30, 0.18],
  [62, 0.42, 0.28], [290, 0.42, 0.28],
  [108, 0.62, 0.42], [244, 0.62, 0.42],
];
const BACK_GROUND = 258;

/** 비석 — 둥근 윗변 슬래브. (x, 바닥 y, 폭, 높이) */
function stone(ctx, x, y, w, h) {
  const r = w / 2;
  ctx.beginPath();
  ctx.moveTo(x - r, y);
  ctx.lineTo(x - r, y - h + r);
  ctx.arc(x, y - h + r, r, Math.PI, 0);
  ctx.lineTo(x + r, y);
  ctx.closePath();
}

/** 솟아오름 — 착지에서 한 번 넘겼다가 앉는다. 밋밋한 ease 는 '놓였다'로 읽히고,
 *  넘겼다 앉으면 '떨어졌다'로 읽힌다. k 0~1 → 0~1(중간에 1 을 넘는다). */
function pop(k) {
  k = clamp(k);
  return easeOut(k) + 0.18 * Math.sin(Math.PI * k) * (1 - k) * 2;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);

    const ph = spec.phase ?? 0;
    const c0 = ph === 0 ? cue(0, 0.15, 0.55) : 1;
    if (c0 <= 0.005) return;

    /* ── 지평선 ─────────────────────────────────────
       비석이 '떠 있는 도형'이 아니라 '땅에 박힌 것'으로 읽히게 하는 유일한 선이다. */
    ctx.save();
    ctx.globalAlpha = ease(clamp(c0 / 0.15)) * 0.28;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(8, GROUND); ctx.lineTo(344, GROUND); ctx.stroke();
    ctx.restore();

    /* ── 뒤 여섯 — 먼 것부터 솟는다 ────────────────── */
    BACK.forEach(([x, sc, alpha], i) => {
      const k = pop((c0 - i * 0.055) / 0.20);
      if (k <= 0.01) return;
      const bw = MAIN.w * sc, bh = MAIN.h * sc;
      ctx.save();
      ctx.beginPath(); ctx.rect(0, 0, 352, GROUND);   // 땅 밑은 안 보인다
      ctx.clip();
      ctx.globalAlpha = alpha;
      ctx.fillStyle = tone('ink');
      stone(ctx, x, BACK_GROUND + bh * (1 - k), bw, bh);
      ctx.fill();
      ctx.restore();
    });

    /* ── 주인공 비석 ───────────────────────────────── */
    const km = pop((c0 - 0.34) / 0.22);
    if (km > 0.01) {
      const yb = GROUND + MAIN.h * (1 - km);
      ctx.save();
      ctx.beginPath(); ctx.rect(0, 0, 352, GROUND);
      ctx.clip();

      castShadow(ctx, MAIN.x + 6, yb - 1, MAIN.w * 0.62, 7, 0.42);
      ctx.fillStyle = DEPTH[3];                       // 옆면 = 두께. 앞면 그라데이션만으로는 종잇조각이다
      stone(ctx, MAIN.x + 11, yb, MAIN.w, MAIN.h); ctx.fill();
      ctx.fillStyle = depthGrad(ctx, MAIN.x - MAIN.w / 2, yb - MAIN.h, MAIN.x + MAIN.w / 2, yb, "ink");
      stone(ctx, MAIN.x, yb, MAIN.w, MAIN.h);
      ctx.fill();

      /* 이름 자리 — 비석을 **파낸 홈**. 배경색으로 파야 '새길 자리'로 읽힌다.
         🔴 여기에 글자를 한 획도 안 그린다. 그것이 이 편의 진단이다. */
      const sy = yb - MAIN.h + SLOT.dy;
      ctx.globalCompositeOperation = 'destination-out';
      ctx.fillStyle = '#000';
      ctx.fillRect(MAIN.x - SLOT.w / 2, sy, SLOT.w, SLOT.h);
      ctx.restore();

      /* 커서 — 쓸 자리는 있는데 아무도 안 쓴다. 사건이 끝난 뒤 남는 유일한 움직임이다. */
      const cur = ease(clamp((c0 - 0.62) / 0.18));
      if (cur > 0.01) {
        const on = frac(t / 1.1) < 0.55 ? 1 : 0;
        if (on) {
          ctx.save();
          ctx.globalAlpha = cur * 0.95;
          setShadow(ctx, GLOW, 10);
          ctx.fillStyle = tone('accent');
          ctx.fillRect(MAIN.x - 3, sy + 4, 6, SLOT.h - 8);
          clearShadow(ctx);
          ctx.restore();
        }
      }

      /* 착지 임팩트 — 집중선이 화면 끝까지 뻗고, 그 한가운데 2프레임을 **흑백 반전**한다.
         (레퍼런스 실측: simDdang 검격이 정확히 이 순서다 — 선 → 반전 → 복귀) */
      const hit = clamp((km - 0.86) / 0.14) * clamp((1.16 - km) / 0.14);
      if (hit > 0.01) speedLines(ctx, w, h, MAIN.x, GROUND - 40, hit, { n: 22, seed: 31, alpha: 0.55 });
      invertFlash(ctx, w, h, hit > 0.55);
      if (hit > 0.01) {
        ctx.save();
        ctx.globalAlpha = hit * 0.75;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4; ctx.lineCap = 'round';
        for (let i = 0; i < 10; i++) {
          const a = Math.PI + (i + 0.5) / 10 * Math.PI;      // 위쪽 반원으로만
          const r0 = 30 + 26 * (1 - hit), r1 = r0 + 34;
          ctx.beginPath();
          ctx.moveTo(MAIN.x + Math.cos(a) * r0, GROUND + Math.sin(a) * r0 * 0.55);
          ctx.lineTo(MAIN.x + Math.cos(a) * r1, GROUND + Math.sin(a) * r1 * 0.55);
          ctx.stroke();
        }
        ctx.restore();
      }

      /* 먼지 — 착지 순간 바닥에서 좌우로 퍼진다 */
      const dust = clamp((km - 0.78) / 0.22) * clamp((1.30 - km) / 0.30);
      if (dust > 0.01) {
        ctx.save();
        ctx.globalAlpha = dust * 0.5;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'round';
        for (const s of [-1, 1]) {
          const x0 = MAIN.x + s * (MAIN.w / 2 + lerp(6, 30, 1 - dust));
          ctx.beginPath();
          ctx.moveTo(x0, GROUND - 3);
          ctx.lineTo(x0 + s * lerp(14, 40, 1 - dust), GROUND - 8);
          ctx.stroke();
        }
        ctx.restore();
      }
    }
  }
};
