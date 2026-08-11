import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편에서 다룰 것의 0.5초 테스트 장면을 돌린다.

   🔴 재료는 이 편이 아니라 **다음 편(`ep16s-4`, 관계 보존)이 맡은 대목**에서 뽑았다.
   같은 원문의 문장이다 — `2026-08-04-unity-goap-m13-events-and-traces.html`:
   > "주민이 사라질 때 호출되는 정리 함수가 관계 기록을 양방향으로 삭제하고 있었던 겁니다.
   >  단짝이 죽으면 남은 사람의 기억에서도 그 관계가 통째로 없어졌습니다.
   >  상실이 생존자에게 전달될 통로 자체가 없었던 거죠."

   🔴 예고 음성 · `outro.next` 카드 · 이 미리보기 셋이 **같은 것 하나**를 가리킨다:
      단짝이 죽으면 기억까지 지워지던 이야기.
   🔴 다음 편이 **자기 몫으로 가진 수치는 한 개도 안 적었다**(브리프 §6 기준 16-4 소유 =
      「한 글자도 안 고침」). 미리보기는 도형만 쓴다.
   🔴 무덤·비석은 안 그린다 — 그건 `ep16s-1` 의 무대다(브리프 §3-C). 여기 무대는
      **산 사람의 기억**이라 남는 쪽이 화면에 계속 서 있다.
   🔴 본문에서 쓴 도형(이름 칸·기록 줄·펼침 상자)을 하나도 안 끌고 왔다 — 다음 편의
      사건은 「목록이 덮인다」가 아니라 「둘을 잇던 것이 같이 사라진다」라 축이 다르다.

   0.5초 루프 : 흰 표식 둘이 강조색 줄로 이어져 있다가 → 오른쪽이 사라지고 → 이어 주던
   줄까지 함께 사라져 왼쪽 하나만 남는다 → 되감긴다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표로 갈랐다.**
     표식은 (110,150)·(242,150) 반지름 21 에 선 두께 3 이라 안쪽 끝이 132.5 와 219.5 다.
     잇는 줄은 x 146~206 이라 양쪽으로 **13.5px** 뜬다.
     🔑 이 줄에는 글로우를 안 걸었다 — blur 를 걸면 그 여백을 먹는다.

   ⏱ 등장은 예고 자막 `cue(0)` 에, 되풀이는 `t` 에 물렸다.
   🔴 소개 자막(`cue(1)`)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
      이 회차에서 카드가 뜨는 시각은 마지막 샷 기준 3.85초이고, 예고 자막은 4.19초짜리라
      **미리보기는 예고가 나오는 동안 7바퀴 넘게 돈 뒤에 덮인다.**

   세로 예산(캔버스 307): 표식 꼭대기 127.5, 바닥 172.5. 아래로 134px 남는다.
   가로: 87.5~264.5. 좌 87 · 우 87 남는다.

   계속 도는 것(30fps 기준): 0.5초 루프라 15프레임마다 한 바퀴다. 사라짐·되살아남 구간에
   반지름 21 짜리 원 하나(면적 1,385px²)와 60×8 짜리 줄의 알파가 프레임당 0.13~0.15 씩
   바뀐다. */

const CX = [110, 242], CY = 150, R = 21;
const LX0 = 146, LX1 = 206, LY = 146, LH = 8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    ctx.textAlign = 'left';
    if (spec.label) {
      ctx.save();
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) return;

    const u = frac(t / (spec.loopSec ?? 0.5));
    const revive = ease(clamp((u - 0.86) / 0.14));
    const rightA = Math.max(1 - ease(clamp((u - 0.30) / 0.22)), revive);
    const linkA = Math.max(1 - ease(clamp((u - 0.44) / 0.22)), revive);

    for (let i = 0; i < 2; i++) {
      const a = pk * (i === 0 ? 1 : rightA);
      if (a <= 0.02) continue;
      ctx.save();
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(CX[i], CY, R, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();
    }

    if (pk * linkA > 0.02) {
      ctx.save();
      ctx.globalAlpha = pk * linkA;
      ctx.fillStyle = tone('accent');
      roundRect(ctx, LX0, LY, LX1 - LX0, LH, 4); ctx.fill();
      ctx.restore();
    }
  }
};
