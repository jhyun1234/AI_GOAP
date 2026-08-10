import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* firstgrave — 훅(SH). 마을 띠 안에서 무덤 하나가 솟는다.

   원문 첫 문장: "제가 만드는 마을에 이번 회차에서 처음으로 무덤이 생겼습니다."
   / "죽은 자리에는 무덤이 하나 남습니다."

   🔴 이 회차의 기본 도형은 **「위아래 점선 두 줄 사이의 띠 = 마을」** 하나다.
   SH · S1 · S2 · S3 넷이 전부 그 띠를 쓰고, 색은 한 문장으로 갈랐다 —
   **흰색은 마을이 이미 가진 것(주민 · 밭 · 눈금), 강조색은 늑대와 내가 늑대를 위해
   바꾼 것(추격 경로 · 임계값 화살표), 그리고 그 결과인 무덤.**
   그래서 이 첫 화면에서 강조색은 무덤 하나뿐이다 — 흰 사람들 사이에 딱 하나.

   ⏱ 무덤이 솟는 것만 자막 cue 에 물린다. 사람 표식의 바운스·표류와 띠 점선의 흐름은
      `t` 로만 도는 것이라 문턱이 없다(정적 방지).

   🔴 겹침 감사 (두 축 다 잰다)
   · 강조색: 표석 y 150~196 · 받침 194~202 · 가로 홈 y 160~167, x 180~212.
     후광 14 를 더한 최대 범위 = **x 160~232 · y 136~216.**
   · 흰색: 라벨 「마을」 글리프 y 14~24 · 위 점선 44 · 표식 5개(반지름 11, 바운스 ±4,
     표류 ±8) · 아래 점선 236 · 라벨 「처음 생긴 무덤」 글리프 y 249~266.
   · 표식은 **표류 최댓값을 넣어도** 강조색 x 띠(160~232) 안으로 못 들어온다:
     x 118 짜리는 최대 118+8+11 = **137**(< 160), x 262 짜리는 최소 262−8−11 = **243**(> 232).
   · 강조색 후광 아래 216 ↔ 아래 점선 236 = 20px · ↔ 라벨 글리프 위 249 = 33px.
   · 흰↔흰: 라벨 「마을」(24) ↔ 위 점선(44) 20px · 아래 점선(236) ↔ 라벨 글리프 위(249) 13px.
     표식끼리는 가장 가까운 쌍이 (54,96)–(86,200) 으로 세로 104px 떨어져 있다. */

const TOP_RULE = 44, BOT_RULE = 236;
const GX = 196, BASE_Y = 196, STONE_MAX = 46, STONE_W = 18;
const LABEL_Y = 262;

/* 사람 표식 — 강조색 띠(x 160~232) 밖에만 둔다. 표류 ±8 · 바운스 ±4 를 넣은 값으로 골랐다. */
const HEADS = [
  { x: 54, y: 96 }, { x: 118, y: 150 }, { x: 262, y: 100 },
  { x: 308, y: 168 }, { x: 86, y: 200 }
];

/* 🔴 마을 띠의 가로 점선은 `setLineDash` 로 그리지 않는다 — 조각을 직접 채운다.
   1차 검수에서 `chase.js` 의 같은 함수가 30fps 결정성 44/1062 를 냈고, 어긋난 픽셀이
   전부 이 가로 점선 세 줄의 안티에일리어싱(알파 21↔19)이었다. 긴 축정렬 점선은 Skia 의
   dash 경로 효과를 타는데 그 경로가 패스 순서에 따라 다르게 래스터화됐다.
   `fillRect` 는 순수 산술이라 같은 `t` 면 같은 픽셀이 나온다.
   위상은 `((v % P) + P) % P` 로 **양수 정규화**한다(JS 의 `%` 는 음수를 남긴다).
   모양은 이전과 같다 — 7px 조각 · 7px 틈 · 두께 3 · y 중심. */
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

    const g = ease(cue(spec.riseCue ?? 0, 0.15, 0.60));

    /* ── 마을 띠 ─────────────────────────────────── */
    if (spec.planeLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.planeLabel, 800, 12.5, 150));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.planeLabel, 14, 24);
    }
    dashRule(ctx, TOP_RULE, 16, w - 16, t, 13);
    dashRule(ctx, BOT_RULE, 16, w - 16, t, -11);

    /* ── 사람들 — 계속 흔들리고 조금씩 표류한다 ──── */
    ctx.fillStyle = tone('ink');
    for (let i = 0; i < HEADS.length; i++) {
      const hd = HEADS[i];
      const dx = Math.sin(t * 0.7 + i * 1.9) * 8;
      const dy = Math.sin(t * 4.4 + i * 1.7) * 4;
      ctx.beginPath();
      ctx.arc(hd.x + dx, hd.y + dy, 11, 0, Math.PI * 2);
      ctx.fill();
    }

    /* ── 무덤 — 이 화면에서 유일한 강조색 ─────────── */
    if (g > 0.01) {
      const stoneH = STONE_MAX * g;
      ctx.save();
      ctx.globalAlpha = clamp(g * 1.8);
      setShadow(ctx, GLOW, 14);
      ctx.fillStyle = tone('accent');
      roundRect(ctx, GX - STONE_W / 2, BASE_Y - stoneH, STONE_W, stoneH, STONE_W / 2);
      ctx.fill();
      roundRect(ctx, GX - 20 * g, BASE_Y - 2, 40 * g, 7, 3);
      ctx.fill();
      clearShadow(ctx);
      /* 표석 가로 홈 — 봉분이 아니라 「표석」으로 읽히게 하는 한 획 */
      if (g > 0.62) {
        ctx.globalAlpha = clamp((g - 0.62) / 0.30);
        roundRect(ctx, GX - 16, BASE_Y - stoneH + 10, 32, 7, 3);
        ctx.fill();
      }
      ctx.restore();
    }

    /* ── 「처음 생긴 무덤」 ───────────────────────── */
    const lab = clamp((g - 0.55) / 0.45);
    if (spec.graveLabel && lab > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(lab * 1.5);
      ctx.textAlign = 'center';
      const fs = fitFont(spec.graveLabel, 800, 16, w - 56);
      ctx.font = disp(800, fs);
      const tw = ctx.measureText(spec.graveLabel).width;
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.graveLabel, clamp(GX, 12 + tw / 2, w - 12 - tw / 2), LABEL_Y);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
