import { ease, easeOut, clamp, tone } from '../lib.js';

/* compare — 두 값의 대비. layout:"choice" 면 값 비교가 아니라 선택지 채택/기각.

   🔴 나레이션이 말하는 순서대로 그려야 한다.
      전에는 작은 값(9)을 먼저 그렸는데 대본은 큰 값(150)을 먼저 말했다.
      말과 그림이 어긋나면 시청자가 둘을 따로 처리하게 된다.
      spec.a.cue / spec.b.cue 에 자막 인덱스를 적으면 그 순서로 자란다. */
export default {
  build(root, { spec }) {
    if (spec.layout === 'choice') {
      root.innerHTML = `<div class="choice">${['a', 'b'].map(k => `
        <div class="opt" data-k="${k}">
          <span class="nm">${spec[k].label}</span>
          <span style="display:flex;gap:7px;align-items:center">
            ${spec[k].tag ? `<span class="tag">${spec[k].tag}</span>` : ''}
            <span class="vd">${spec[k].value}</span>
          </span>
        </div>`).join('')}</div>`;
      return;
    }
    root.innerHTML = `<div class="cmp">${['a', 'b'].map(k => `
      <div class="cmpcol" data-k="${k}">
        <b></b>
        <div class="barwrap"><i class="bar2"></i></div>
        <small>${spec[k].label}</small>
      </div>`).join('')}</div>
      ${spec.note ? `<div class="cmpnote"></div>` : ''}`;
  },

  draw(root, { spec, cue, nLines }) {
    if (spec.layout === 'choice') {
      const showC = spec.showCue ?? 1;                 // 두 선택지가 언급되는 자막
      const tagC = spec.tagCue ?? nLines - 1;          // 기각 사유(GC 할당)를 말하는 자막
      const settleC = spec.settleCue ?? nLines - 1;    // 채택/기각이 확정되는 자막
      root.querySelectorAll('.opt').forEach(el => {
        const k = el.dataset.k, s = spec[k];
        const show = ease(cue(showC, 0.2, k === 'a' ? 0.45 : 0.8));
        el.style.opacity = 0.12 + 0.88 * show;
        const settle = ease(cue(settleC, 0.15, 0.6));
        const chosen = s.tone !== 'dim';
        el.style.borderColor = settle > 0.3 ? (chosen ? tone('accent') : tone('track')) : tone('track');
        el.style.background = settle > 0.3 && chosen ? 'rgba(0,255,136,.10)' : 'transparent';
        el.querySelector('.vd').style.color = settle > 0.3 ? (chosen ? tone('accent') : tone('sub')) : tone('sub');
        el.querySelector('.nm').style.color = settle > 0.3 && !chosen ? tone('sub') : tone('ink');
        const tag = el.querySelector('.tag');
        if (tag) tag.style.opacity = ease(cue(tagC, 0.2, 0.5));
      });
      return;
    }

    const max = Math.max(spec.a.value, spec.b.value) || 1;
    root.querySelectorAll('.cmpcol').forEach(el => {
      const key = el.dataset.key || el.dataset.k;
      const s = spec[key];
      const k = easeOut(cue(s.cue ?? (key === 'a' ? nLines - 1 : nLines - 2), 0.15, 0.62));
      const bar = el.querySelector('.bar2');
      bar.style.height = (s.value / max * 100 * k) + '%';
      bar.style.background = tone(s.tone);
      const num = el.querySelector('b');
      num.textContent = Math.round(s.value * k).toLocaleString();
      num.style.color = tone(s.tone);
      num.style.opacity = k > 0.02 ? 1 : 0.18;
      el.querySelector('small').style.color = tone('sub');
    });
    const note = root.querySelector('.cmpnote');
    if (note) {
      note.textContent = spec.note;
      // 배수 표기는 두 막대가 다 선 뒤에 — 비교 대상이 없으면 숫자가 뜻을 못 가진다
      note.style.opacity = ease(cue(spec.noteCue ?? nLines - 1, -0.1, 0.95));
    }
  }
};
