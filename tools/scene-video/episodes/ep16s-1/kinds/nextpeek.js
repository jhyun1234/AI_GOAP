import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, camAt
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편(`ep16s-2`)에서 화면에 생기는 것을
   0.5초 루프로 미리 보여 준다.

   🔴 재료는 다음 편의 사건에서 뽑았고, 근거는 같은 원문의 2·4단계 문단이다:
   > "그래서 상태형 줄 하나를 새로 얹었습니다. 굶는 주민은 \"■ 굶는 주민 {이름} — 식량
   >  {N}일치\" …"
   > "\"굶는 주민 — 식량 0일치\"라는 줄은 누구인지 모르는 정보입니다. 누군지 모르면
   >  개입을 못 합니다."

   🔴 **다음 편 소유의 수치를 한 개도 안 썼다**(열네 곳 · 상태형 0개 · 0일치 · 두 번).
   그 수들은 `ep16s-2` 가 쓰는 것이고, 여기서 먼저 쓰면 그 편의 훅을 이 편이 태워 버린다.
   그래서 미리보기는 **수 없이 형태로만** 그린다.

   0.5초 루프 : 위쪽 알림 자리에 흰 줄이 켜지고(왼쪽 작은 네모는 원문의 「■」 표기) →
   그 아래 이름이 들어갈 강조색 칸이 뜨는데 **끝내 안 채워지고** → 아래 지도를 훑는
   흰 점선이 좌우로 오가지만 다섯 표식 중 누구에게도 안 닿는다.

   🔑 이 편이 무덤에 이름을 새겼는데, 다음 편은 **살아 있는 사람**의 이름이 없어서 못
   구한 이야기다. 그래서 이 편의 색 규약(강조색 = 이름 자리)이 미리보기에서도 그대로 산다 —
   같은 강조색 빈 칸이 다시 나온다.

   🔴 예고 음성 · `outro.next` 카드 · 이 미리보기 셋이 같은 것을 가리킨다:
      **위험이 화면에 떴는데도 아무도 못 구한 이야기.**

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색 빈 칸 y 110~134, 선 반폭까지 108.5~135.5, 글로우 8 까지 100.5~143.5.
            위 = 흰 알림 줄 바깥 바닥 84+1.5 = 85.5 → **15px**.
            아래 = 훑는 선 바깥 꼭대기 168−1.5 = 166.5 → **23px**.
     축 B · 흰 글자는 「다음 편」 하나뿐이다(y 26, 왼쪽 정렬). 흰 도형끼리는
            훑는 세로 선의 바깥 아래 끝 191.5 와 표식 바깥 꼭대기 236−21.2−1.5 = 213.3
            → **21.8px** 뜬다(닿지 않는 것이 요점이다).

   세로 예산(캔버스 307): 최저 = 표식 바깥 바닥 236+21.2+1.5 = **258.7**. 48px 남는다.
   가로: 알림 줄 바깥 44.5~307.5, 표식 51.5~302.5. 바깥 2px 띠에서 좌 42.5 · 우 42.5 뜬다.

   계속 도는 것(30fps 기준): 훑는 선이 0.25초에 232px 를 지난다 → **31px/프레임**,
   강조색 칸의 점선 흐름 32px/s, 알림 줄의 등장/소멸이 반 초마다 되풀이된다. */

const BAR = { x: 46, y: 62, w: 260, h: 22 };
const SLOT = { x: 120, y: 110, w: 112, h: 24 };
const SCAN_Y = 168, SCAN_X0 = 60, SCAN_X1 = 292;
const MARKS = [62, 118, 176, 234, 292], MK_CY = 236;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);   // 카메라 무브 (lib.js) — cam 이 없으면 아무 일도 안 한다
    ctx.textBaseline = 'alphabetic';

    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    const u = frac(t / (spec.loopSec ?? 0.5));

    /* 알림 줄 — 위험은 화면에 뜬다 (흰색) */
    const on = ease(clamp((u - 0.04) / 0.12));
    ctx.save();
    ctx.globalAlpha = pk * (0.3 + 0.7 * on);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, BAR.x, BAR.y, BAR.w, BAR.h, 5); ctx.stroke();
    ctx.fillStyle = tone('ink');
    ctx.fillRect(BAR.x + 8, BAR.y + 5, 12, 12);
    ctx.restore();

    /* 이름이 들어갈 자리 — 강조색. 끝내 안 채워진다 */
    const slotOn = ease(clamp((u - 0.22) / 0.16));
    ctx.save();
    ctx.globalAlpha = pk * (0.25 + 0.7 * slotOn);
    setShadow(ctx, GLOW, 8);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.setLineDash([9, 7]);
    ctx.lineDashOffset = -((t * 32) % 16);
    ctx.strokeRect(SLOT.x, SLOT.y, SLOT.w, SLOT.h);
    ctx.setLineDash([]);
    clearShadow(ctx);
    ctx.restore();

    /* 지도를 훑지만 누구에게도 안 닿는다 (흰 점선) */
    const tri = u < 0.5 ? u * 2 : 2 - u * 2;
    const sx = lerp(SCAN_X0, SCAN_X1, tri);
    ctx.save();
    ctx.globalAlpha = pk * 0.7;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.setLineDash([10, 8]);
    ctx.lineDashOffset = -((t * 50) % 18);
    ctx.beginPath(); ctx.moveTo(SCAN_X0, SCAN_Y); ctx.lineTo(SCAN_X1, SCAN_Y); ctx.stroke();
    ctx.setLineDash([]);
    ctx.beginPath(); ctx.moveTo(sx, SCAN_Y); ctx.lineTo(sx, SCAN_Y + 22); ctx.stroke();
    ctx.restore();

    /* 지도의 주민들 — 아무도 골라지지 않는다 (흰색) */
    for (let i = 0; i < MARKS.length; i++) {
      ctx.save();
      ctx.globalAlpha = pk * 0.9;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      const br = 1.2 * (0.5 + 0.5 * Math.sin(t * 5.2 + i * 1.1));
      roundRect(ctx, MARKS[i] - 9, MK_CY - 20 - br, 18, 40 + br * 2, 9);
      ctx.stroke();
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
