import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone
} from '../../../engine/lib.js';

/* tangle — 나중에 고치려면 실타래가 훨씬 두꺼워진다.

   원문 75행의 마지막 두 문장. 이 편의 결론이다.
   두 어긋남에서 나온 두 가닥이 아래로 내려가며 서로 감긴다. 위쪽은 감김이 성기고 가늘어
   두 가닥이 두 가닥으로 보이지만, 아래로 갈수록 감김이 촘촘해지고 굵어져 한 다발이 된다.
   화살표 하나가 위(지금 = 성긴 곳)를 가리키다가 아래(나중 = 다발)로 내려간다.
   글자는 두지 않는다.

   🔴 3차 재작업(2026-07-30)에서 라벨 넷을 전부 뺐다. 원문 75행의 마지막 두 문장이
   그대로 이 샷의 자막 두 줄이라, 화면이 같은 문장을 적으면 두 채널이 같은 일을 한다
   (ep00s S4 가 같은 사유로 반려됐다). 자막이 말하지 않는 것을 라벨이 말하게 하는 안도
   검토했지만 이 샷이 쓸 수 있는 원문 문자열이 그 두 문장뿐이라, 어떻게 잘라도 자막과
   겹친다. 그래서 화면의 몫은 '그 두 시점이 이 실타래의 어디인가' 하나만 남겼다.

   🔴 쌓임이 아니다(ep00s stack 은 거짓 전제 위에 판단이 층으로 쌓인다).
   🔴 퍼짐도 아니다(ep01s sprawl 은 넓게 번지다 만 점들이다).
   🔴 지우는 획도 아니다(ep01s erasure). 여기서 일어나는 일은 **얽힘** 하나뿐이라
   두 가닥이 서로를 감는 것 말고는 화면에 아무 사건도 두지 않았다.
   숫자·시간(몇 배·몇 달 뒤)은 원문에 없으므로 화면에 없다.

   계속 도는 것 = 감김 위상이 t 로 계속 아래로 흐른다. 실은 지금도 감기고 있다. */

const SPIN = 4.2;     // 감김이 한 마디 흘러내리는 초

/* 배치 (캔버스 352×307 기준)
     64~232   두 가닥이 감긴다 (위 성기고 가늘게 → 아래 촘촘하고 굵게)
     84 → 224 화살표가 훑고 내려가는 구간. 가닥 폭(AMP0→AMP1)을 따라 안쪽으로 좁혀 온다 */
const Y0 = 64, Y1 = 232;
const AMP0 = 62, AMP1 = 9;
const LW0 = 3.5, LW1 = 10;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const nk = ease(cue(spec.nowCue ?? 0, 0.15, 0.55));
    const lk = ease(cue(spec.laterCue ?? 1, 0.15, 0.5));

    const PAD = 14;
    const cx = w / 2;
    const phase = frac(t / SPIN) * Math.PI * 2;

    ctx.lineCap = 'round';

    /* ── 두 가닥이 감긴다 ─────────────────────────
       y 를 잘게 훑으며 두 짧은 선분을 그린다. 그 자리에서 뒤에 있는 가닥(코사인 부호가
       음수인 쪽)을 먼저 그려야 앞뒤가 실제로 교차해 보인다 — 겹침 순서가 곧 매듭이다. */
    const STEP = 3;
    const posOf = (y, side) => {
      const u = clamp((y - Y0) / (Y1 - Y0));
      const e = ease(u);
      const amp = lerp(AMP0, AMP1, e);
      // 아래로 갈수록 감김이 촘촘해진다 — u 의 이차항이 마디 수를 늘린다
      const ang = (u * 1.15 + u * u * 1.85) * Math.PI * 2 + phase;
      const s = side ? 1 : -1;
      return { x: cx + Math.cos(ang) * amp * s, depth: Math.sin(ang) * s, u, e };
    };

    for (let y = Y0; y < Y1; y += STEP) {
      const a0 = posOf(y, false), a1 = posOf(y + STEP, false);
      const b0 = posOf(y, true), b1 = posOf(y + STEP, true);
      const lw = lerp(LW0, LW1, a0.e);
      const order = a0.depth < b0.depth ? [[a0, a1], [b0, b1]] : [[b0, b1], [a0, a1]];

      order.forEach(([p, q], idx) => {
        // 뒤에 오는(나중에 그리는) 가닥은 검정 테두리를 깔아 겹침이 읽히게 한다
        if (idx === 1) {
          ctx.strokeStyle = '#000'; ctx.lineWidth = lw + 5;
          ctx.beginPath(); ctx.moveTo(p.x, y); ctx.lineTo(q.x, y + STEP); ctx.stroke();
        }
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = lw;
        ctx.beginPath(); ctx.moveTo(p.x, y); ctx.lineTo(q.x, y + STEP); ctx.stroke();
      });
    }

    // 다발의 끝 — 굵어진 채로 멎는다
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = LW1 + 3;
    ctx.beginPath(); ctx.moveTo(cx - 6, Y1 + 2); ctx.lineTo(cx + 6, Y1 + 2); ctx.stroke();

    /* ── 가리키는 표식 : 지금(위, 성긴 곳) → 나중(아래, 다발) ──
       🔴 글자를 두지 않는다. 원문 75행 마지막 두 문장은 자막 두 줄이 통째로 말하고,
       화면이 같은 문장을 옮겨 적으면 두 채널이 같은 일을 한다.
       화살표가 어디를 가리키느냐만으로 '지금'과 '나중'이 실타래 위의 두 지점이 된다.
       ⚠️ lk 는 위에서 이미 ease() 를 통과했다 — 여기서 다시 ease(lk) 하지 않는다. */
    if (nk > 0.02) {
      const ay = lerp(84, 224, lk);                  // lk = 0 위(성긴 곳) → 1 아래(다발)
      const ax = cx + lerp(AMP0, AMP1, lk) + 22;     // 가닥 폭을 따라 안쪽으로 좁혀 온다
      const tail = Math.min(ax + 34, w - PAD);
      ctx.globalAlpha = clamp(nk * 1.4);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(tail, ay); ctx.lineTo(ax, ay); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(ax - 9, ay); ctx.lineTo(ax + 3, ay - 6); ctx.lineTo(ax + 3, ay + 6);
      ctx.closePath(); ctx.fill();
      ctx.globalAlpha = 1;
    }

    ctx.lineCap = 'butt';
  }
};
