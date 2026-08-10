import { ease, clamp, lerp, frac, disp, tone, fitCanvas, mkCanvas }
  from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 구조 ④). **다음 편(13-3)** 의 사건을 0.5초 루프로 보여 준다.

   근거는 이 글 「의외의 디테일: 간호는 사망 시계를 되감지 않고 잠깐 멈춘다」 절이다.
   > "여기서 자연스러워 보이는 구현은 '누가 간호를 시작하면 방치 시간을 0으로
   >  되돌린다' 입니다. 실제로 처음엔 저도 그게 맞는 줄 알았습니다."
   > "지나가던 주민이 0.1초만 곁에 스쳐도 죽음의 시계가 처음으로 되감기니까요."

   그리는 것 : 다친 주민(흰 테두리 원 + `ㅠ_ㅠ`) 둘레로 **죽음의 시계**(강조색 호)가
   차오르는데, 아래 길로 지나가던 흰 표식이 **스치는 순간** 호가 통째로 0 으로
   되감긴다. 그리고 처음부터 다시 차오른다 — 그래서 아무도 안 죽는다.
   🔴 이 그림은 「그렇게 만들면 벌어지는 일」이다(원문이 조건절로 쓴 장면).
      음성 예고도 「되감을 뻔한 이야기」로 맞춰 두었다 — 안 난 사고를 난 것으로
      만들지 않기 위해서다.

   🔴 흰 것과 강조색은 반지름으로 갈랐다.
     · 시계 호 : 중심에서 r 34, 획 6 → 31~37
     · 다친 표식 : r 17, 획 3 → 바깥 18.5 (호 안쪽 31 까지 **12.5px** 뜬다)
     · 지나가는 표식 : 레인 y 236, 바깥 반지름 10.5 → 윗변 225.5
       (호 바깥 아랫변 213 에서 **12.5px** 뜬다. 좌우로 어디를 지나든 같다)
   🔴 이 회차 어디에도 글로우를 안 썼다 — 강조색 번짐이 흰 획에 얹히는 경로를 없앴다.
   🔴 화면에 올린 수는 `0.1초` 하나뿐이고 원문 문장에 그대로 있다.
      주민 수·시계의 절대 시간은 이 편의 재료가 아니라 그리지 않았다.

   ⏱ 등장은 예고 자막(cue 0)에 물리고 루프만 t 로 돈다.
      🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가
      `.vis` 를 픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   세로 예산(캔버스 307) — 구획 이름 30 · 모서리 괄호 56~286 · 라벨 126 ·
   시계 139~213 · 표식 레인 225.5~246.5 · 아래 라벨 268. 최저 287(괄호 획 포함). */

const CY = 176;
const R = 34;
const LANE_Y = 236;
const PH_HIT = 0.68;      // 지나가는 표식이 다친 주민을 스치는 위상

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const put = (txt, cx, by, weight, size, color, maxW) => {
      if (!txt) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.fillStyle = color;
      ctx.textAlign = 'center';
      ctx.fillText(txt, clamp(cx, 20 + tw / 2, w - 20 - tw / 2), by);
      ctx.textAlign = 'left';
    };

    /* ── 구획 이름 (괄호 밖) ───────────────────────── */
    if (spec.label) {
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 14, 30);
    }

    const ak = ease(cue(spec.peekCue ?? 0, 0.15, 0.28));
    if (ak <= 0.02) return;

    /* ── 모서리 괄호 넷 — 미리보기 틀 ───────────────── */
    {
      const x0 = 14, x1 = w - 14, y0 = 56, y1 = 286;
      const armX = lerp(10, 40, ak), armY = lerp(10, 34, ak);
      ctx.save();
      ctx.globalAlpha = clamp(ak * 1.6);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3; ctx.lineJoin = 'miter';
      const corner = (x, y, sx, sy) => {
        ctx.beginPath();
        ctx.moveTo(x + sx * armX, y);
        ctx.lineTo(x, y);
        ctx.lineTo(x, y + sy * armY);
        ctx.stroke();
      };
      corner(x0, y0, 1, 1); corner(x1, y0, -1, 1);
      corner(x0, y1, 1, -1); corner(x1, y1, -1, -1);
      ctx.restore();
    }

    if (ak < 0.55) return;
    const inner = clamp((ak - 0.55) / 0.45);
    const cx = w / 2;

    /* 🔴 괄호 안쪽으로 클립한다 — 지나가는 표식은 설계상 화면 밖에서 들어오는데,
       클립이 없으면 좌우 바깥 2px 띠를 몇 프레임씩 칠해 「가장자리 잘림」이 센다.
       잘린 것처럼 보이지 않게 만드는 쪽이 예외를 선언하는 쪽보다 낫다. */
    ctx.save();
    ctx.beginPath();
    ctx.rect(17, 58, w - 34, 226);
    ctx.clip();

    /* ── 0.5초 루프 ────────────────────────────────── */
    const ph = frac(t / (spec.loopSec ?? 0.5));
    const fill = ph < PH_HIT
      ? ph / PH_HIT
      : (ph < PH_HIT + 0.08 ? 1 - ease((ph - PH_HIT) / 0.08) : 0);
    const px = ph < PH_HIT
      ? lerp(w + 30, cx, ph / PH_HIT)
      : lerp(cx, -30, (ph - PH_HIT) / (1 - PH_HIT));

    /* 죽음의 시계 — 강조색 호 */
    if (fill > 0.004) {
      ctx.save();
      ctx.globalAlpha = inner;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 6; ctx.lineCap = 'butt';
      ctx.beginPath();
      ctx.arc(cx, CY, R, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * fill);
      ctx.stroke();
      ctx.restore();
    }
    /* 빈 궤도 — 흰색 12% 라 강조색과 같은 자리에 겹쳐도 팔레트가 한 색이다 */
    ctx.save();
    ctx.globalAlpha = inner;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(cx, CY, R + 7, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    /* 다친 주민 */
    ctx.save();
    ctx.globalAlpha = inner;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(cx, CY, 17, 0, Math.PI * 2); ctx.stroke();
    ctx.font = disp(700, 10.5);
    const f = spec.face ?? '';
    const fw = ctx.measureText(f).width;
    ctx.fillStyle = tone('ink');
    ctx.fillText(f, cx - fw / 2, CY + 4);
    ctx.restore();

    /* 지나가는 주민 */
    ctx.save();
    ctx.globalAlpha = inner;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(px, LANE_Y, 9, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    ctx.restore();          // 클립 해제

    put(spec.clockLabel, cx, 126, 800, 12.5, tone('sub'), w - 60);
    put(spec.graceLabel, cx, 268, 700, 12.5, tone('sub'), w - 60);
  }
};
