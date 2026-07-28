import { span, clamp } from '../lib.js';

/* console — 실제 로그가 한 줄씩 찍힌다.
   개발자 시청자에게는 이 화면 자체가 증거다. */
export default {
  build(root, { spec }) {
    root.innerHTML = `<div class="rows">${
      (spec.rows || []).map(r => `<div class="row ${r.tone || ''}"></div>`).join('')}</div>`;
  },

  draw(root, { spec, p }) {
    const rows = spec.rows || [];
    const els = root.querySelectorAll('.row');
    rows.forEach((r, i) => {
      const a = 0.05 + i * 0.20;                    // 줄마다 순차 등장
      const k = span(p, a, a + 0.17);
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
