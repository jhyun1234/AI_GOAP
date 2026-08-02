import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* threeway — 수치 하나가 세 군데에 따로 있다.

   위의 값 하나에서 세 갈래가 내려가 세 저장소(코드 · 에셋 파일 · 레지스트리 테이블)에
   같은 값이 각각 따로 앉는다. driftCue 에서 왼쪽 하나만 손을 대면 — 그 갈래만 강조색이
   되고 값에 취소선이 그어진다 — 나머지 두 갈래가 끊어지고 두 값은 그 자리에 그대로 남는다.

   🔴 바뀐 값을 숫자로 쓰지 않는다. 원문 15행은 '어느 하나를 바꾸면'이라고만 하고 바꾼
   값이 얼마인지는 말하지 않는다. 취소선과 라벨로만 표시한다.

   회차의 기본 도형 안에서 이 그림의 몫은 '한 값이 세 벌로 존재한다'.
   계속 도는 것 = 갈래를 타고 내려가는 값 표식. 끊긴 갈래에서는 끊긴 자리에서 사라진다. */

const FLOW = 2.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const stores = spec.stores || ['코드', '에셋 파일', '레지스트리 테이블'];
    const n = stores.length;
    const edit = spec.editAt ?? 0;

    const sk = ease(cue(spec.splitCue ?? 0, 0.15, 0.7));
    const dk = ease(cue(spec.driftCue ?? 1, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';

    /* ── 값 카드 ───────────────────────────────────── */
    const vW = 208, vH = 40, vX = w / 2 - vW / 2, vY = 22;
    {
      const s = Math.min(1, spring(clamp(sk * 2.2)));
      ctx.globalAlpha = clamp(sk * 3);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, vX + (vW - vW * s) / 2, vY + (vH - vH * s) / 2, vW * s, vH * s, 4);
      ctx.stroke();
      ctx.textAlign = 'center'; ctx.fillStyle = tone('ink'); ctx.font = disp(800, 13);
      ctx.fillText(spec.value || '', w / 2, vY + 25);
      ctx.globalAlpha = 1;
    }

    /* ── 세 갈래와 세 저장소 ───────────────────────── */
    const boxW = 100, boxH = 76, boxY = 156;
    const gap = (w - 24 - boxW * n) / (n - 1);
    const forkY = vY + vH + 18;
    const breakY = forkY + 26;         // 끊기는 자리

    for (let i = 0; i < n; i++) {
      const bx = 12 + i * (boxW + gap);
      const cx = bx + boxW / 2;
      const vis = clamp(sk * 3.4 - i * 0.9);
      if (vis < 0.02) continue;

      const isEdit = i === edit;
      const cut = !isEdit ? dk : 0;
      const col = isEdit && dk > 0.15 ? tone('accent') : tone('sub');

      // 갈래
      ctx.strokeStyle = col; ctx.lineWidth = 3;
      ctx.globalAlpha = clamp(vis * 1.5);
      ctx.beginPath();
      ctx.moveTo(w / 2, vY + vH);
      ctx.lineTo(w / 2, forkY);
      ctx.lineTo(cx, forkY);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(cx, forkY);
      ctx.lineTo(cx, cut > 0.4 ? breakY : boxY);
      ctx.stroke();
      if (cut > 0.4) {
        // 끊긴 자리 — 아래쪽 토막만 남는다
        ctx.globalAlpha = clamp(vis * 1.5) * 0.35;
        ctx.beginPath();
        ctx.moveTo(cx, lerp(breakY, boxY - 14, clamp((cut - 0.4) / 0.6)));
        ctx.lineTo(cx, boxY);
        ctx.stroke();
        ctx.globalAlpha = clamp(vis * 1.5);
      }

      /* 계속 도는 것 — 갈래를 내려가는 값 표식 */
      {
        const u = frac(t / FLOW + i * 0.31);
        const endY = cut > 0.4 ? breakY : boxY;
        const my = lerp(forkY, endY, u);
        ctx.globalAlpha = 0.75 * (1 - u * u);
        ctx.fillStyle = col;
        ctx.fillRect(cx - 3, my - 3, 6, 6);
        ctx.globalAlpha = clamp(vis * 1.5);
      }

      // 흔들림 — 안 따라온 두 상자만
      const shake = cut > 0.4 ? Math.sin(frac(t / 0.62) * Math.PI * 2 + i) * 2.2 * clamp((cut - 0.4) / 0.6) : 0;

      // 상자
      const s = Math.min(1, spring(vis));
      const sw = boxW * s, sh = boxH * s;
      const sx = bx + (boxW - sw) / 2 + shake, sy = boxY + (boxH - sh) / 2;
      if (isEdit && dk > 0.15) setShadow(ctx, GLOW, 12, 0);
      ctx.lineWidth = isEdit && dk > 0.15 ? 4 : 3;
      ctx.strokeStyle = isEdit && dk > 0.15 ? tone('accent') : tone('ink');
      roundRect(ctx, sx, sy, sw, sh, 4); ctx.stroke();
      clearShadow(ctx);

      // 저장소 이름
      ctx.textAlign = 'center';
      let fs = 11;
      ctx.font = disp(800, fs);
      while (fs > 8 && ctx.measureText(stores[i]).width > boxW - 12) { fs -= 0.5; ctx.font = disp(800, fs); }
      ctx.fillStyle = tone('sub');
      ctx.fillText(stores[i], sx + sw / 2, sy + 20);

      // 값
      ctx.fillStyle = isEdit && dk > 0.15 ? tone('accent') : tone('ink');
      ctx.font = disp(900, 26);
      const vt = String(spec.num ?? '2');
      ctx.fillText(vt, sx + sw / 2, sy + 52);
      if (isEdit && dk > 0.2) {
        // 취소선 — 이 값은 이제 다르다
        const tw = ctx.measureText(vt).width;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(sx + sw / 2 - tw / 2 - 5, sy + 44);
        ctx.lineTo(sx + sw / 2 - tw / 2 - 5 + (tw + 10) * clamp((dk - 0.2) / 0.4), sy + 44);
        ctx.stroke();
      }

      // 라벨
      if (dk > 0.3) {
        const lk = clamp((dk - 0.3) / 0.4);
        ctx.globalAlpha = lk;
        const lab = isEdit ? (spec.editLabel || '여기만 바꿈') : (spec.staleLabel || '안 따라옴');
        ctx.fillStyle = isEdit ? tone('accent') : tone('sub');
        ctx.font = disp(800, 10.5);
        ctx.fillText(lab, sx + sw / 2, sy + boxH + 20);
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
