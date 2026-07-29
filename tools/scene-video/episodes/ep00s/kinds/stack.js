import {
  disp, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* stack — 거짓 전제 위에 판단이 쌓이는 그림.

   원문 85행: "실제 도착 여부와 상관없이 '성공'이라는 거짓 신호가 GOAP 에 전달되면,
   그다음 판단들은 전부 잘못된 전제 위에서 이루어집니다. 자원 노드를 발견했다는 플래그가
   갱신되지 않고, 똑같은 실패한 계획이 다시 스케줄되는 식으로요."

   🔴 이 두 항목(플래그 미갱신 / 재스케줄)은 1차 씬에서 "그림이 서지 않는다"는 사유로
   버려졌던 것이다. 버린 게 아니라 그릇이 없었던 것이다 — 쌓는 그림을 만들자
   둘은 그냥 **위층 블록 두 개의 라벨**이 됐다. 가이드가 금지하는 '라벨 없는 막대 stack'과
   갈리는 지점이 정확히 여기다: 블록마다 원문에서 나온 이름이 붙어 있다.

   기울기가 이 샷의 논지다. 맨 아래 '화면'은 끝까지 평평하다(원문: 순간이동은 안 한다).
   기우는 건 그 위에 얹힌 판단들뿐이다. 그래서 겉으로는 멀쩡해 보인다. */

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;

    const floorY = h - 28, floorH = 8;
    const baseH = 46, baseY = floorY - 20 - baseH;      // 거짓 전제
    const layerH = 44, gapY = 8;
    const blkX = 30, blkW = w - 60;

    const floorK = ease(cue(spec.floorCue ?? 0, 0.2, 0.5));
    const baseK = ease(cue(spec.baseCue ?? 1, 0.2, 0.55));
    const stackK = ease(cue(spec.stackCue ?? 2, 0.2, 0.6));

    /* ── 바닥 : 화면에 보이는 것 ─────────────────
       끝까지 수평이고 끝까지 밝다. 그 위를 점 하나가 계속 지나간다 —
       "게임은 멀쩡히 돌아가고 있다"가 샷 내내 눈에 보여야 대비가 산다. */
    if (floorK > 0.02) {
      ctx.globalAlpha = floorK;
      ctx.fillStyle = 'rgba(255,255,255,0.55)';
      ctx.fillRect(12, floorY, w - 24, floorH);

      const px = 12 + ((t * 64) % (w - 24));
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(px, floorY + floorH / 2, 7, 0, Math.PI * 2); ctx.fill();

      ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText(spec.floor?.label || '화면에 보이는 것', 12, floorY - 10);
      ctx.textAlign = 'right';
      ctx.fillText(spec.floor?.note || '이상 없음', w - 12, floorY - 10);
      ctx.globalAlpha = 1;
    }

    if (baseK <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 기울기 ─────────────────────────────────
       바닥 중앙을 축으로 시계 방향. 각도를 3.5도로 묶어 둔 이유는 두 가지다 —
       프레임 밖으로 나가지 않게, 그리고 "무너졌다"가 아니라 "어긋났다"로 읽히게.
       흔들림은 t 로 계속 돈다(정적 프레임 방지). */
    const tilt = stackK * 0.062 + Math.sin(t * 1.5) * 0.011 * stackK;
    ctx.save();
    ctx.translate(cx, baseY + baseH);
    ctx.rotate(tilt);
    ctx.translate(-cx, -(baseY + baseH));

    /* ── 거짓 전제 ─────────────────────────────── */
    const bs = spring(baseK);
    ctx.save();
    ctx.translate(cx, baseY + baseH / 2); ctx.scale(bs, bs); ctx.translate(-cx, -(baseY + baseH / 2));
    ctx.globalAlpha = clamp(baseK * 1.5);
    ctx.fillStyle = '#000';
    roundRect(ctx, blkX, baseY, blkW, baseH, 5); ctx.fill();
    setShadow(ctx, GLOW, 14, 0);
    ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
    roundRect(ctx, blkX, baseY, blkW, baseH, 5); ctx.stroke();
    clearShadow(ctx);

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10.5);
    ctx.fillText(spec.base?.label || '두뇌가 받은 값', blkX + 14, baseY + 18);
    ctx.fillStyle = tone('ink'); ctx.font = disp(900, 19);
    ctx.fillText(spec.base?.value || '도착', blkX + 14, baseY + 39);

    // '거짓' 딱지 — 전제가 틀렸다는 표시. 이게 없으면 그냥 튼튼한 바닥으로 읽힌다
    if (spec.base?.tag) {
      ctx.font = disp(900, 11);
      const tw = ctx.measureText(spec.base.tag).width + 16;
      ctx.fillStyle = tone('accent');
      roundRect(ctx, blkX + blkW - tw - 12, baseY + 13, tw, 20, 3); ctx.fill();
      ctx.fillStyle = '#000';
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.fillText(spec.base.tag, blkX + blkW - tw / 2 - 12, baseY + 23.5);
      ctx.textBaseline = 'alphabetic';
    }
    ctx.globalAlpha = 1;
    ctx.restore();

    /* ── 그 위에 쌓이는 판단들 ─────────────────── */
    const layers = spec.layers || [];
    layers.forEach((L, i) => {
      const k = clamp(stackK * layers.length - i * 0.55);
      if (k <= 0.02) return;
      const ly = baseY - gapY - (i + 1) * layerH - i * gapY;
      const drop = (1 - ease(k)) * 26;           // 위에서 내려와 얹힌다
      ctx.globalAlpha = clamp(k * 1.6);
      ctx.fillStyle = '#000';
      roundRect(ctx, blkX, ly - drop, blkW, layerH, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, blkX, ly - drop, blkW, layerH, 5); ctx.stroke();

      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      fitFont(ctx, L.label, blkW - 120, 800, 13);
      ctx.fillStyle = tone('ink');
      ctx.fillText(L.label, blkX + 14, ly - drop + layerH / 2);
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText(L.state || '', blkX + blkW - 14, ly - drop + layerH / 2);
      ctx.globalAlpha = 1;
    });

    ctx.restore();
    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
