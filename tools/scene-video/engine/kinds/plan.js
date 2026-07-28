import { span, ease, tone } from '../lib.js';

/* plan — 목표 하나와 행동 후보들.
   mode:"thrash" = 엉뚱한 카드만 계속 밝아지고 정답 카드는 끝내 안 켜진다.
   mode:"gate"   = 조건 못 맞춘 카드가 꺼지고, 남은 것 중 하나가 뽑힌다. */
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

  draw(root, { spec, p }) {
    const cards = spec.cards || [];
    const els = root.querySelectorAll('.card');
    const goal = root.querySelector('.goalchip');
    if (goal) goal.style.opacity = ease(span(p, 0.04, 0.16));

    if (spec.mode === 'thrash') {
      // 미끼 카드들만 빠르게 순회 — 정답 카드에는 끝까지 안 닿는다
      const decoys = cards.map((c, i) => c.decoy ? i : -1).filter(i => i >= 0);
      const run = span(p, 0.14, 0.80);
      const idx = decoys[Math.floor(run * decoys.length * 5.2) % decoys.length];
      els.forEach((el, i) => {
        const c = cards[i];
        const hit = run > 0 && run < 1 && i === idx;
        el.style.background = hit ? 'rgba(255,255,255,.16)' : 'transparent';
        el.style.color = hit ? tone('ink') : tone('sub');
        el.style.outline = c.target ? `3px dashed ${tone('track')}` : 'none';
        el.style.outlineOffset = '2px';
        el.style.opacity = c.target ? 0.42 : 1;
      });
      const v = root.querySelector('.verdict');
      if (v) { v.style.opacity = ease(span(p, 0.82, 0.95)); v.style.color = tone('ink'); }
      return;
    }

    // gate
    els.forEach((el, i) => {
      const c = cards[i];
      const off = ease(span(p, 0.20 + i * 0.05, 0.40 + i * 0.05));
      const pick = ease(span(p, 0.62, 0.82));
      if (c.state === 'off') {
        el.style.background = 'transparent';
        el.style.color = `rgba(255,255,255,${0.7 - 0.4 * off})`;
        el.style.textDecoration = off > 0.5 ? 'line-through' : 'none';
      } else if (c.state === 'pick') {
        el.style.background = pick > 0.1 ? tone('accent') : 'transparent';
        el.style.color = pick > 0.1 ? '#000000' : tone('sub');
        el.style.fontWeight = pick > 0.1 ? 700 : 400;
      } else {
        el.style.background = 'transparent'; el.style.color = tone('ink');
      }
      const why = el.querySelector('.why');
      if (why) { why.style.opacity = off; why.style.color = tone('sub'); }
    });
  }
};
