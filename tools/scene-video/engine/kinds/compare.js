import { span, ease, easeOut, tone } from '../lib.js';

/* compare — 두 값의 대비. layout:"choice" 면 값 비교가 아니라 선택지 채택/기각. */
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

  draw(root, { spec, p }) {
    if (spec.layout === 'choice') {
      root.querySelectorAll('.opt').forEach(el => {
        const s = spec[el.dataset.k];
        const k = ease(span(p, el.dataset.k === 'a' ? 0.12 : 0.34, el.dataset.k === 'a' ? 0.32 : 0.54));
        el.style.opacity = 0.12 + 0.88 * k;
        const settle = ease(span(p, 0.62, 0.86));
        const chosen = s.tone !== 'dim';
        el.style.borderColor = settle > 0.3
          ? (chosen ? tone('cool') : '#23262F') : '#1B1E28';
        el.style.background = settle > 0.3 && chosen ? 'rgba(79,182,168,.09)' : 'transparent';
        el.querySelector('.vd').style.color = settle > 0.3
          ? (chosen ? tone('cool') : tone('dim')) : tone('dim');
        el.querySelector('.nm').style.color = settle > 0.3 && !chosen ? tone('dim') : tone('ink');
        const tag = el.querySelector('.tag');
        if (tag) tag.style.opacity = ease(span(p, 0.5, 0.66));
      });
      return;
    }

    const max = Math.max(spec.a.value, spec.b.value) || 1;
    root.querySelectorAll('.cmpcol').forEach(el => {
      const s = spec[el.dataset.k];
      const k = easeOut(span(p, el.dataset.k === 'a' ? 0.10 : 0.30, 0.78));
      const bar = el.querySelector('.bar2');
      bar.style.height = (s.value / max * 100 * k) + '%';
      bar.style.background = tone(s.tone);
      const num = el.querySelector('b');
      num.textContent = Math.round(s.value * k).toLocaleString();
      num.style.color = tone(s.tone);
      num.style.opacity = k > 0.02 ? 1 : 0.15;
    });
    const note = root.querySelector('.cmpnote');
    if (note) {
      note.textContent = spec.note;
      note.style.opacity = ease(span(p, 0.72, 0.9));
    }
  }
};
