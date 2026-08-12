import {
  ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* blankstone — 죽은 자리마다 동그라미가 하나씩 놓이는데, 그 아래 이름이 들어갈 자리는
   일곱 개 전부 끝까지 빈 채로 남는다.

   원문 근거:
   > "죽은 자리에는 회색 원이 하나씩 남습니다. 무덤입니다. 그런데 그 원에는 아무것도 안
   >  적혀 있습니다."
   > "화면을 가득 채운 회색 원 일곱 개를 보면서, 저는 그중 누가 제가 아침에 지켜보던
   >  그 주민인지 알 수 없었습니다."

   🔴 **이 샷에는 글자가 하나도 없다.** 처음에는 코드 주석 인용(「"영구 흔적"」)을 위에
   올리려 했는데, 같은 순간의 자막이 「무덤인데 아무것도 안 적혀 있었죠」라서 **화면의 그
   글자가 비석에 적힌 것으로 읽힐 위험**이 있었다. 인용이 오독을 만들면 인용을 버린다.

   🔴 색 규약(namefade 에서 세운 것 그대로): 강조색 = 이름이 들어갈 자리 / 흰색 = 무덤.
   그래서 여기서 강조색은 **점선으로만** 있고 한 칸도 안 채워진다. 그 점선이 계속 흐르는
   것이 이 샷의 「아직도 비어 있다」이고, 동시에 정적 구간을 막는 움직임이다.

   ⏱ 동그라미는 `cue(0)`, 빈 자리는 `cue(1)` — 둘 다 왼쪽부터 하나씩 어긋나게 뜬다.
   효과음 없음(이 편의 소리는 onecount·carvename 두 마디뿐이다).

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 동그라미 바깥 바닥 150 + (17+1.5 숨) + 1.5(선 반폭) = 170 /
            빈 자리 바깥 꼭대기 192−1.5 = 190.5, 글로우 8 을 빼면 182.5 → **12.5px** 뜬다.
            좌우로는 같은 x 를 쓰지만 세로가 갈려 있어 한 픽셀도 안 겹친다.
     축 B · 흰 글자가 0개다(위 참조). 흰 도형끼리는 동그라미 사이 간격이
            44 − 2×20 = **4px** 인데, 이건 「일곱 개가 늘어서 있다」를 만드는 의도한 쌍이다.

   세로 예산(캔버스 307): 최저 = 빈 자리 바깥 바닥 213.5 + 글로우 8 = **221.5**. 85px 남는다.
   가로: 빈 자리 왼쪽 44−20−1.5−8 = **14.5** / 오른쪽 308+20+1.5+8 = **337.5**.
   바깥 2px 띠에서 좌 12.5 · 우 12.5 뜬다.

   계속 도는 것(30fps 기준): 강조색 점선이 초당 34px 로 흐른다 → **1.1px/프레임**,
   테두리 길이 (40+20)×2 = 120px × 일곱 칸. 동그라미 반지름 숨 진폭 1.5px · 4.6rad/s. */

const XS = [44, 88, 132, 176, 220, 264, 308];
const CY = 150, R = 17;
const SLOT_Y = 192, SLOT_HW = 20, SLOT_H = 20;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));

    const c0 = cue(spec.stoneCue ?? 0, 0.15, 0.60);
    if (c0 <= 0.005) return;
    const c1 = cue(spec.slotCue ?? 1, 0.15, 0.55);

    for (let i = 0; i < XS.length; i++) {
      const x = XS[i];

      /* 무덤 — 흰색. 위에서 살짝 내려앉는다 */
      const a = ease(clamp((c0 - i * 0.085) / 0.28));
      if (a > 0.01) {
        const r = R + 1.5 * (0.5 + 0.5 * Math.sin(t * 4.6 + i * 0.8));
        ctx.save();
        ctx.globalAlpha = a;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(x, lerp(CY - 10, CY, a), r, 0, Math.PI * 2);
        ctx.stroke();
        ctx.restore();
      }

      /* 이름이 들어갈 자리 — 점선. 끝까지 비어 있다.
         🔄 2026-08-12: accent(해결) → fail(결함). 이 빈 칸이 이 회차의 결함 그 자체다.
         옛 팔레트는 강조색이 하나여서 「고쳤다」와 「고장났다」를 같은 색으로 그렸다. */
      const s = ease(clamp((c1 - i * 0.06) / 0.26));
      if (s > 0.01) {
        ctx.save();
        ctx.globalAlpha = s * 0.92;
        setShadow(ctx, FAIL_GLOW, 8);
        ctx.strokeStyle = tone('fail');
        ctx.lineWidth = 3;
        ctx.setLineDash([9, 7]);
        ctx.lineDashOffset = -((t * 34) % 16);
        ctx.strokeRect(x - SLOT_HW, SLOT_Y, SLOT_HW * 2, SLOT_H);
        ctx.setLineDash([]);
        clearShadow(ctx);
        ctx.restore();
      }
    }
  }
};
