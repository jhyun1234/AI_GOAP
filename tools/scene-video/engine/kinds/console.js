import { span, clamp } from '../lib.js';

/* console — 실제 로그가 한 줄씩 찍힌다.
   개발자 시청자에게는 이 화면 자체가 증거다. */
export default {
  build(root, { spec }) {
    root.innerHTML = `<div class="rows">${
      (spec.rows || []).map(r => `<div class="row ${r.tone || ''}"></div>`).join('')}</div>`;
  },

  draw(root, { spec, cue, nLines }) {
    const rows = spec.rows || [];
    const els = root.querySelectorAll('.row');
    rows.forEach((r, i) => {
      // 로그 줄을 자막에 나눠 붙인다. rows[i].cue 로 직접 지정할 수 있고,
      // 없으면 자막 수에 맞춰 고르게 분배한다 — 마지막 줄(결정적 로그)은 마지막 자막에.
      const ci = r.cue ?? Math.min(nLines - 1, Math.floor(i * nLines / rows.length));
      const stagger = (i - (r.cue ?? 0)) * 0.12;
      const k = Math.max(0, Math.min(1, cue(ci, 0.25 - stagger, 0.45)));
      const el = els[i];
      el.style.opacity = k > 0 ? 1 : 0.06;

      // 타자 치듯 — 글자 수를 시간의 함수로 자른다
      const n = Math.round(clamp(k) * r.text.length);
      const shown = r.text.slice(0, n);
      if (r.mark && n >= r.text.length) {
        el.innerHTML = r.text.replace(r.mark, `<em>${r.mark}</em>`);
      } else {
        el.textContent = shown + (k > 0 && k < 1 ? '▌' : '');
      }
    });
  }
};
