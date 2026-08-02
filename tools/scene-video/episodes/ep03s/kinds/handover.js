import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* handover — 막의 띠가 끊기고, 다음 막이 오른쪽으로 열린다.

   맨 위는 막 하나짜리 가로 띠다. 1막은 지나간 구간이라 흐리고, 2막은 짧다 — 이 편 하나로
   끝나므로 오른쪽에 마감 캡이 찍힌다(closeCue). openCue 에서 3막 구간이 오른쪽으로 자라며
   화살표가 화면 끝을 향한다. 가장 긴 구간이 이제 시작이라는 뜻이다.
   그 아래 다음 편 카드가 앉고(topicCue), 마지막에 촌장의 명령이 주민에게 다가왔다가
   주민 앞에서 밀려난다(refuseCue). 다음 편에서 실제로 벌어질 일을 미리 한 번 보여 준다.

   🔴 앞 회차의 예고는 '카드가 옆에서 들어와 앉는' 그림이었다. 같은 장치를 반복하지 않으려고
   여기서는 카드가 아니라 띠가 주인공이다 — 이 편의 예고가 넘겨야 하는 것이 '다음 편'이
   아니라 '다음 막'이라, 시간 축 자체가 화면에 있어야 한다.

   계속 도는 것 = 띠 아래 안내선을 계속 오른쪽으로 흐르는 표식과, 주민의 호흡. */

const CARRY = 3.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const ck = ease(cue(spec.closeCue ?? 0, 0.15, 0.6));
    const ok = ease(cue(spec.openCue ?? 1, 0.15, 0.62));
    const tk = ease(cue(spec.topicCue ?? 2, 0.15, 0.5));
    const fk = ease(cue(spec.refuseCue ?? 3, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';

    /* ── 막의 띠 ──────────────────────────────────── */
    const barY = 24, barH = 26;
    const a1x = 12, a1w = 84;
    const a2x = 102, a2w = 48;
    const a3x = 156, a3wFull = 174;

    // 1막 — 지나간 구간
    ctx.globalAlpha = clamp(ck * 2) * 0.5;
    ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
    ctx.setLineDash([6, 5]);
    roundRect(ctx, a1x, barY, a1w, barH, 3); ctx.stroke();
    ctx.setLineDash([]);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(800, 11);
    ctx.fillText(spec.act1 || '1막', a1x + a1w / 2, barY + 18);
    ctx.globalAlpha = 1;

    // 2막 — 이 편 하나로 끝난다
    {
      const k = clamp(ck * 1.8);
      ctx.globalAlpha = clamp(k * 1.4);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, a2x, barY, a2w, barH, 3); ctx.stroke();
      ctx.fillStyle = tone('ink'); ctx.font = disp(900, 12);
      ctx.fillText(spec.act2 || '2막', a2x + a2w / 2, barY + 18);
      if (ck > 0.4) {
        const kk = clamp((ck - 0.4) / 0.4);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(a2x + a2w + 3, barY + barH / 2 - (barH / 2 + 5) * kk);
        ctx.lineTo(a2x + a2w + 3, barY + barH / 2 + (barH / 2 + 5) * kk);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    // 3막 — 오른쪽으로 열린다
    if (ok > 0.02) {
      const gw = a3wFull * ease(ok);
      ctx.globalAlpha = clamp(ok * 2);
      setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, a3x, barY, Math.max(6, gw), barH, 3); ctx.stroke();
      clearShadow(ctx);
      if (ok > 0.75) {
        const ak = clamp((ok - 0.75) / 0.25);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        const ax = a3x + a3wFull + 4;
        ctx.beginPath();
        ctx.moveTo(ax, barY + barH / 2); ctx.lineTo(ax + 10 * ak, barY + barH / 2);
        ctx.moveTo(ax + 10 * ak, barY + barH / 2); ctx.lineTo(ax + 10 * ak - 5, barY + barH / 2 - 4);
        ctx.moveTo(ax + 10 * ak, barY + barH / 2); ctx.lineTo(ax + 10 * ak - 5, barY + barH / 2 + 4);
        ctx.stroke();
      }
      // 글자는 띠가 거의 다 자란 뒤에만 — 자라는 중에 찍으면 반토막 난다
      const inK = clamp((gw - a3wFull * 0.82) / (a3wFull * 0.12));
      if (inK > 0.02) {
        ctx.globalAlpha = clamp(ok * 2) * inK;
        ctx.textAlign = 'center';
        const txt = spec.act3 || '';
        let fs = 12.5; ctx.font = disp(900, fs);
        while (fs > 9 && ctx.measureText(txt).width > a3wFull - 14) { fs -= 0.5; ctx.font = disp(900, fs); }
        ctx.fillStyle = tone('accent');
        ctx.fillText(txt, a3x + a3wFull / 2, barY + 18);
      }
      ctx.globalAlpha = 1;
    }

    // 막 부제
    ctx.textAlign = 'center';
    ctx.font = disp(700, 9.5); ctx.fillStyle = tone('sub');
    ctx.globalAlpha = clamp(ck * 2) * 0.75;
    ctx.fillText(spec.act1sub || '', a1x + a1w / 2, barY + barH + 14);
    ctx.fillText(spec.act2sub || '', a2x + a2w / 2, barY + barH + 14);
    ctx.globalAlpha = 1;

    /* 계속 도는 것 — 띠 아래 안내선을 흐르는 표식 */
    {
      const gy = barY + barH + 24;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([4, 6]);
      ctx.beginPath(); ctx.moveTo(12, gy); ctx.lineTo(w - 12, gy); ctx.stroke();
      ctx.setLineDash([]);
      for (let i = 0; i < 6; i++) {
        const u = frac(t / CARRY + i / 6);
        const x = lerp(16, w - 24, u);
        ctx.globalAlpha = 0.7 * (1 - Math.abs(u - 0.5) * 2);
        ctx.fillStyle = tone('sub');
        ctx.fillRect(x, gy - 2.5, 7, 5);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 다음 편 ──────────────────────────────────── */
    if (tk > 0.02) {
      const cx = 22, cy = 100, cw = w - 44, chh = 52;
      const s = Math.min(1, spring(tk));
      ctx.globalAlpha = clamp(tk * 1.6);
      setShadow(ctx, GLOW, 16, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, cx + (cw - cw * s) / 2, cy + (chh - chh * s) / 2, cw * s, chh * s, 5);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 9.5);
      ctx.fillText(spec.nextLabel || '다음 편', cx + cw / 2, cy + 16);
      const txt = spec.topic || '';
      let fs = 17; ctx.font = disp(900, fs);
      while (fs > 11 && ctx.measureText(txt).width > cw - 26) { fs -= 0.5; ctx.font = disp(900, fs); }
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, fs * s);
      ctx.fillText(txt, cx + cw / 2, cy + 40);
      ctx.globalAlpha = 1;
    }

    /* ── 명령과 거부 ──────────────────────────────── */
    if (tk > 0.35) {
      const show = clamp((tk - 0.35) / 0.4);
      const breathe = Math.sin(frac(t / 2.2) * Math.PI * 2) * 3;
      const vx = 56, vy = 200 + breathe;

      ctx.globalAlpha = show;
      ctx.lineWidth = 3; ctx.strokeStyle = fk > 0.5 ? tone('accent') : tone('ink');
      ctx.beginPath(); ctx.arc(vx, vy, 17, 0, Math.PI * 2); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
      ctx.fillText(spec.villagerLabel || '주민', vx, vy + 34);
      ctx.globalAlpha = 1;

      // 명령이 다가온다 → 밀려난다
      const app = ease(clamp(fk / 0.5));
      const back = ease(clamp((fk - 0.5) / 0.5));
      const chipW = 92, chipH = 26;
      const px = lerp(246, 96, app) + back * 46;
      ctx.globalAlpha = show * clamp(fk * 3 + 0.15) * (1 - back * 0.55);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, px, vy - chipH / 2, chipW, chipH, 3); ctx.stroke();
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 11);
      ctx.fillText(spec.orderLabel || '촌장의 명령', px + chipW / 2, vy + 4);
      ctx.globalAlpha = 1;

      // 거부 — 주민 앞에 막이 선다
      if (back > 0.08) {
        const k = clamp((back - 0.08) / 0.45);
        ctx.globalAlpha = k;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        setShadow(ctx, GLOW, 12, 0);
        ctx.beginPath();
        ctx.arc(vx, vy, 27, -0.85 * k, 0.85 * k);
        ctx.stroke();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    const hook = spec.hook || scene?.hook;
    if (hook && fk > 0.25) {
      const k = clamp((fk - 0.25) / 0.45);
      const rows = String(hook).split('\n').slice(0, 2);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      let fs = 14.5; ctx.font = disp(900, fs);
      while (fs > 10 && rows.some(l => ctx.measureText(l).width > w - 34)) { fs -= 0.5; ctx.font = disp(900, fs); }
      const s = Math.min(1, spring(k));
      ctx.font = disp(900, fs * s);
      ctx.fillStyle = tone('ink');
      rows.forEach((l, i) => ctx.fillText(l, w / 2, 274 + i * (fs * 1.3)));
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
