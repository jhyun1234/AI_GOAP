import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, camAt
} from '../../../engine/lib.js';

/* namefade — 훅. 내가 이름을 아는 주민 하나가 무덤 무리로 들어가면, 이름표가 꺼지고
   나머지와 똑같은 동그라미가 된다.

   원문 근거(`2026-08-04-unity-goap-m13-events-and-traces.html`):
   > "Day 45에 주민 일곱 명이 죽었습니다."
   > "죽은 자리에는 회색 원이 하나씩 남습니다. 무덤입니다. 그런데 그 원에는 아무것도 안
   >  적혀 있습니다. 화면을 가득 채운 회색 원 일곱 개를 보면서, 저는 그중 누가 제가
   >  아침에 지켜보던 그 주민인지 알 수 없었습니다."

   🔴 이 회차의 색 규약(다섯 샷 전부에서 지킨다):
      **강조색 = 이름, 또는 이름이 들어갈 자리 / 흰색 = 그 밖의 전부(주민·무덤·수·경로).**
   그래서 이 샷은 강조색이 **켜져 있다가 꺼지는** 유일한 샷이고, 본문 두 샷(blankstone·
   onecount)에서는 이름이 채워지는 일이 없으며, carvename 에서 처음으로 채워진다.

   🔴 원문 무덤은 **일곱 개**다. `ep15s-5` 의 예고 미리보기는 동그라미가 셋이었는데
   그건 그 편의 그림이고, 이 편은 원문 수치를 따른다(브리프 §2 이행 4항).

   🔴 화면에 올린 글자는 「Day 45」 하나뿐이다. 「Day」는 게임이 무덤에 실제로 찍는 표기
   (`{shortName} · Day {day}`)의 인용이라 영어로 둔다(ADR-V-10 예외 ①). 45 는 원문 문장
   「Day 45에 주민 일곱 명이 죽었습니다」의 값이고 이 편 소유다(브리프 §6).

   ⏱ 등장은 훅 자막 `cue(0)` 에, 걸어와 무덤이 되는 되풀이는 `t`(2.6초 루프)에 물렸다.
   효과음은 안 달았다 — 훅에서 소리를 쓰면 이 편의 두 마디(모임·새김)가 흐려진다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 이름표(강조)는 y 166~174, 글로우 8 까지 158~182.
            위 = 윗줄 동그라미 바깥 바닥 118 + (20+1.5 숨) + 1.5(선 반폭) = 141 → **17px**.
            아래 = 걸어오는 표식 바깥 머리 222 − 26 − 1.5 = 194.5 → **12.5px**.
            이름표 x 는 19~125(글로우까지 11~133)를 지나가고, 그 구간의 다른 강조색은 없다.
     축 B · 글자는 「Day 45」 하나뿐이라 흰 글자끼리 붙을 짝이 없다.
            흰 도형끼리는 아랫줄 동그라미 왼쪽 끝 182−23 = 159 와
            표식 오른쪽 끝 110+11.5 = 121.5 사이가 **37.5px** 이다(의도한 쌍 아님).

   세로 예산(캔버스 307): 최저 = 아랫줄 동그라미 바깥 바닥 222+21.5+1.5 = **245**. 62px 남는다.
   가로: 왼쪽 = 이름표 글로우 34−15−8 = **11** / 오른쪽 = 290+21.5+1.5 = **313**.
   바깥 2px 띠에서 좌 9 · 우 37 뜬다.

   계속 도는 것(30fps 기준): 2.6초 루프 + 동그라미 일곱의 반지름 숨(진폭 1.5px · 5rad/s)
   → 표식 이동이 도는 구간에서 76px/1.17초 ≈ **2.2px/프레임**. */

const ROW1 = [74, 146, 218, 290], Y1 = 118;
const ROW2 = [182, 254], Y2 = 222;
const SLOT_X = 110, START_X = 34, R = 20;
const TAG_Y = 166, TAG_W = 30, TAG_H = 8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);   // 카메라 무브 (lib.js) — cam 이 없으면 아무 일도 안 한다
    ctx.textBaseline = 'alphabetic';

    const st = ease(cue(spec.fieldCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.dayLabel) {
      ctx.save();
      ctx.globalAlpha = st;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.dayLabel, 20, 26);
      ctx.restore();
    }

    const breath = i => 1.5 * (0.5 + 0.5 * Math.sin(t * 5 + i * 0.9));
    const ring = (x, y, r, alpha) => {
      if (alpha <= 0.01 || r <= 0.5) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();
    };

    /* 이미 이름을 잃은 여섯 */
    let k = 0;
    for (const x of ROW1) { ring(x, Y1, R + breath(k), st); k++; }
    for (const x of ROW2) { ring(x, Y2, R + breath(k), st); k++; }

    /* 일곱 번째 — 걸어와서 무덤이 된다 */
    const u = frac(t / (spec.loopSec ?? 2.6));
    const walk = ease(clamp(u / 0.45));
    const gone = ease(clamp((u - 0.45) / 0.15));
    const rise = ease(clamp((u - 0.47) / 0.20));
    const tagOff = ease(clamp((u - 0.55) / 0.17));
    const fx = lerp(START_X, SLOT_X, walk);

    if (1 - gone > 0.01) {
      ctx.save();
      ctx.globalAlpha = st * (1 - gone);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, fx - 10, Y2 - 26, 20, 52, 10); ctx.stroke();
      ctx.restore();
    }
    if (rise > 0.01) ring(SLOT_X, Y2, (R + breath(6)) * rise, st * rise);

    /* 이름표 — 이 편에서 강조색이 뜻하는 것. 무덤이 되면 꺼진다 */
    const ta = st * (1 - tagOff);
    if (ta > 0.01) {
      ctx.save();
      ctx.globalAlpha = ta;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(fx - TAG_W / 2, TAG_Y, TAG_W, TAG_H);
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
