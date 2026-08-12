import {
  ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, disp, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* fogrule — 왜 그렇게 보였나. 원인 두 겹을 한 화면에 세운다.

   근거(원문 = `devlog/sessions/2026-08-11.md` 14:38 커밋 본문):
   > "지형 기억(탐험됨) 위에 개체까지 그려져"
   > "FoW 는 3단계(미탐험·기억·시야)인데 그 단계를 읽는 것이 바닥 텍스처뿐이었다."
   → 화면에서 움직이는 것 = ①**사람이 지나간 만큼 땅 무늬가 남는다**(= 기억)
     ②그 남은 땅 **위에** 강조색 표식이 켜진다(= 개체까지 그려졌다).

   🔴 코드 이름은 한 글자도 안 올렸다 — `FoW`·`ThreatAgent`·`fow=2` 는 화면 한국어
   규칙(ADR-V-10)에 걸린다. 화면에 뜨는 낱말은 원문 산문에서 그대로 온 「다녀간 땅」뿐이다.

   🔴 라벨을 다 가려도 읽힌다(ADR-V25-15): 표식이 지나간 만큼 아래 띠에 무늬가
   남고, 그 무늬 위에 다른 표식들이 뒤늦게 켜진다.

   ⏱ `since(0)`(무늬가 남는다) · `since(1)`(그 위에 켜진다). 소리는 안 달았다 —
      이 샷은 사건이 아니라 **까닭**이고, 이 편의 소리는 두 개뿐이다(S1·S3).

   ── 겹침 감사 (선 반폭 1.5 · 숨 진폭 1.2 · GLOW blur 8 포함) ──
   축 A(강조색↔흰색):
     · 강조색 삼각형 위 끝 188 − 1.5 = 186.5 / 띠 위 획 안쪽 150 + 1.5 + 1.2 = 152.7
       → **33.8px**, GLOW 8 을 빼도 **25.8px**.
     · 삼각형 아래 끝 214 + 1.5 = 215.5 / 띠 아래 획 안쪽 236 − 1.5 − 1.2 = 233.3
       → **17.8px**, GLOW 를 빼면 **9.8px**.
     · 삼각형 위 끝 186.5 / 흰 사람 표식 아래 끝 116 + 15 + 1.5 + 1.5(흔들림) = 134
       → **52.5px**.
     ⓘ 띠 **안쪽 빗금**(알파 0.12)만은 강조색 아래를 지난다 — 그것이 이 샷의 사건이다.
   축 B(흰색↔흰색):
     · 사람 표식(y 100.5~134) ↔ 띠 테두리(y 148.5~) → **14.5px**. **의도한 쌍**이다
       (사람이 그 띠 위를 지나가며 무늬를 남긴다).
     · 라벨 「다녀간 땅」 글자 위 262 − 12.5 = 249.5 ↔ 띠 아래 획 바깥 237.5
       → **12px**. 이것도 라벨과 그 대상이라 **의도한 쌍**이다.
     · 서로 다른 것을 가리키는 흰 글자 쌍은 없다(라벨이 하나뿐).

   세로 예산(캔버스 307): 최저 = 라벨 밑선 약 266. 41px 남는다.
   가로(캔버스 352): 최좌 = 출발점 46 − 7.5 = 38.5 · 최우 = 도착점 306 + 7.5 = 313.5.

   계속 도는 것: 표식이 260px 를 1.60초에 → 5.4px/프레임(30fps). 도착 뒤에도 띠
   둘레 2×(272+86) = 716px 가 `bre = 1.2·(1+sin(8.8t))` 로 숨 쉰다 → 약 250px². */

const BAND = { x: 40, y: 150, w: 272, h: 86 };
const ENEMY = [150, 246];
const BASE_Y = 214, TRI_H = 26, TRI_HW = 15;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    ctx.lineJoin = 'round';

    const s0 = Math.max(0, since(spec.trailCue ?? 0));
    const s1 = Math.max(0, since(spec.drawCue ?? 1));
    const w = ease(clamp((s0 - 0.30) / 1.60));
    const vx = lerp(46, 306, w);
    const bre = 1.2 * (1 + Math.sin(t * 8.8));
    const bob = 1.5 * Math.sin(t * 7.6);
    const B = BAND;

    /* ── 지나간 만큼 남는 땅 ── */
    const ba = ease(clamp((s0 - 0.15) / 0.50));
    if (ba > 0.02) {
      ctx.save();
      ctx.globalAlpha = ba;
      ctx.strokeStyle = tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, B.x + bre, B.y + bre, B.w - bre * 2, B.h - bre * 2, 8);
      ctx.stroke();
      ctx.clip();
      ctx.beginPath();
      ctx.rect(B.x, B.y, B.w * w, B.h);
      ctx.clip();
      for (let i = -5; i < 15; i++) {
        const x0 = B.x + i * 24;
        ctx.beginPath();
        ctx.moveTo(x0, B.y);
        ctx.lineTo(x0 + B.h, B.y + B.h);
        ctx.stroke();
      }
      ctx.restore();
    }

    /* ── 그 위에 개체까지 ── */
    ctx.save();
    ctx.strokeStyle = tone('fail');
    ctx.lineWidth = 3;
    ENEMY.forEach((cx, i) => {
      const k = ease(clamp((s1 - 0.35 - i * 0.28) / 0.40));
      if (k <= 0.02) return;
      ctx.globalAlpha = k;
      setShadow(ctx, FAIL_GLOW, 8);
      ctx.beginPath();
      ctx.moveTo(cx, BASE_Y - TRI_H);
      ctx.lineTo(cx + TRI_HW, BASE_Y);
      ctx.lineTo(cx - TRI_HW, BASE_Y);
      ctx.closePath();
      ctx.stroke();
      clearShadow(ctx);
    });
    ctx.restore();

    /* ── 지나가는 사람 ── */
    ctx.save();
    ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3;
    roundRect(ctx, vx - 6, 101 + bob, 12, 30, 6);
    ctx.stroke();
    ctx.restore();

    if (spec.bandLabel && ba > 0.02) {
      ctx.save();
      ctx.globalAlpha = ba;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.bandLabel, B.x, 262);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
