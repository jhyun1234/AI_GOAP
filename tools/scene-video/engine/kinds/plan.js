import { span, ease, easeOut, clamp, tone } from '../lib.js';

/* plan — 목표 하나와 행동 후보들.
   mode:"thrash" = 엉뚱한 카드만 켜지고 정답 카드는 끝내 안 켜진다.
   mode:"gate"   = 조건 못 맞춘 카드가 꺼지고, 남은 것 중 하나가 뽑힌다.

   🔴 나레이션이 카드 이름을 부르는 순간 그 카드가 켜져야 한다.
      전에는 무작위 순회라, "귀환하기"라고 말할 때 엉뚱한 카드가 켜져 있었다.
      cards[i].named = 0,1,2… 로 호명 순서를 적으면 그 순서대로 켠다. */
export default {
  build(root, { spec }) {
    root.innerHTML = `<div class="planwrap">
      ${spec.goal ? `<span class="goalchip">${spec.goal}</span>` : ''}
      <div class="cards">${(spec.cards || []).map(c =>
      `<span class="card">${c.label}${c.reason ? `<em class="why">${c.reason}</em>` : ''}</span>`
    ).join('')}</div>
      ${spec.verdict ? `<div class="verdict">${spec.verdict}</div>` : ''}
    </div>`;
  },

  draw(root, { spec, p, cue, since, lines, nLines }) {
    const cards = spec.cards || [];
    const els = root.querySelectorAll('.card');
    const goal = root.querySelector('.goalchip');
    if (goal) goal.style.opacity = ease(clamp(cue(0, 0.25, 0.25)));

    if (spec.mode === 'thrash') {
      const nameLine = spec.nameCue ?? 1;              // 카드 이름을 부르는 자막
      const named = cards.map((c, i) => ({ i, o: c.named })).filter(x => x.o != null)
        .sort((a, b) => a.o - b.o);
      const scanLine = spec.scanCue ?? 0;

      // ① 훑는 구간 — 아직 이름을 안 불렀다. 후보 위를 빠르게 지나간다
      const scan = cue(scanLine, 0.2, 0.95);
      const decoys = cards.map((c, i) => c.decoy ? i : -1).filter(i => i >= 0);
      const scanIdx = decoys[Math.floor(scan * decoys.length * 3.4) % decoys.length];

      // ② 호명 구간 — 부르는 순서대로 하나씩 켜지고, 켜진 채로 남는다
      // 첫 이름이 나오는 순간 첫 카드가 켜져야 한다. 한 칸 밀리면 말과 그림이 계속 어긋난다.
      const nameT = since(nameLine), nameDur = lines[nameLine]?.dur || 1;
      const spoken = nameT < -0.1 ? -1
        : Math.floor(clamp((nameT + 0.1) / (nameDur * 0.88)) * named.length);

      els.forEach((el, i) => {
        const c = cards[i];
        const order = named.find(x => x.i === i)?.o;
        const lit = order != null && spoken >= order;          // 호명됨 → 계속 켜짐
        const scanning = nameT < 0 && scan > 0 && scan < 1 && i === scanIdx;
        el.style.background = lit ? tone('ink') : scanning ? 'rgba(255,255,255,.16)' : 'transparent';
        el.style.color = lit ? '#000' : scanning ? tone('ink') : tone('sub');
        el.style.borderColor = lit ? tone('ink') : tone('track');
        el.style.outline = c.target ? `3px dashed ${tone('track')}` : 'none';
        el.style.outlineOffset = '2px';
        el.style.opacity = c.target ? 0.4 : 1;                 // 정답 카드는 끝까지 흐리다
      });

      const v = root.querySelector('.verdict');
      if (v) {
        v.style.opacity = ease(cue(spec.verdictCue ?? nLines - 1, 0.1, 0.6));
        v.style.color = tone('ink');
      }
      return;
    }

    // ── gate ──
    const offCue = spec.offCue ?? 1;                   // "후보에서 뺐다"고 말하는 자막
    const pickCue = spec.pickCue ?? nLines - 1;        // "탐험으로 넘어간다"고 말하는 자막
    let offSeen = 0;
    els.forEach((el, i) => {
      const c = cards[i];
      if (c.state === 'off') {
        // 꺼지는 카드끼리도 순서를 준다 — 동시에 꺼지면 무슨 일인지 안 보인다
        const k = ease(clamp(cue(offCue, 0.2, 0.85) * 1.6 - offSeen * 0.35));
        offSeen++;
        el.style.background = 'transparent';
        el.style.borderColor = tone('track');
        el.style.color = `rgba(255,255,255,${0.7 - 0.42 * k})`;
        el.style.textDecoration = k > 0.5 ? 'line-through' : 'none';
        const why = el.querySelector('.why');
        if (why) { why.style.opacity = k; why.style.color = tone('sub'); }
      } else if (c.state === 'pick') {
        const k = ease(cue(pickCue, 0.15, 0.55));
        el.style.background = k > 0.15 ? tone('accent') : 'transparent';
        el.style.color = k > 0.15 ? '#000' : tone('sub');
        el.style.borderColor = k > 0.15 ? tone('accent') : tone('track');
        el.style.fontWeight = 700;
      } else {
        el.style.background = 'transparent';
        el.style.borderColor = tone('track');
        el.style.color = tone('ink');
      }
    });
  }
};
