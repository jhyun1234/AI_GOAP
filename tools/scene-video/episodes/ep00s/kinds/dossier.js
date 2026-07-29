import {
  disp, mono, ease, easeOut, clamp, spring, depthGrad,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* dossier — 이름을 붙이는 샷. 이 편이 시청자에게 들려 보내는 물건 하나.

   원문 85행이 "그냥 버그라고 부르지 않고 결함 C 라는 이름을 따로 붙여 두고 있었습니다"
   라고 말한다. 그래서 이름표(네 귀의 꺾쇠)가 밖에서 날아와 물리는 그림으로 짰다 —
   '이름이 붙는다'가 동작으로 보여야 한다.

   🔴 원문을 다시 읽고 얻은 것: **원문은 결함 C 의 몸통을 코드 한 줄로 지목해 뒀다.**
   83행("Brain.TileX, Brain.TileY 를 목적지 값으로 덮어써버리는 코드")과
   113행("`Brain.TileX = targetX` 라는 정확한 코드 패턴")이 같은 물건이다.
   1차 씬은 이 한 줄을 S8 검색 장면에서 딱 한 번 썼는데, 그러면 "죽은 버그의 유전자 서열"이
   처음 보는 문자열이 되어 지문 대조라는 말이 성립하지 않는다.
   그래서 이 편에서 이 한 줄은 세 번 나온다 — 여기(살아 있음) · S5(잘려 나감) · S8(지문으로 등록됨).
   같은 문자열의 상태가 세 번 바뀌는 것이 이 회차의 서사 그 자체다. */

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

/** 네 귀의 꺾쇠. off 만큼 바깥에서 들어온다. */
function brackets(ctx, x, y, bw, bh, off, alpha) {
  const L = 26;
  ctx.globalAlpha = alpha;
  ctx.lineWidth = 4; ctx.lineCap = 'butt';
  ctx.strokeStyle = tone('accent');
  const corners = [
    [x - off, y - off, 1, 1], [x + bw + off, y - off, -1, 1],
    [x - off, y + bh + off, 1, -1], [x + bw + off, y + bh + off, -1, -1]
  ];
  for (const [px, py, sx, sy] of corners) {
    ctx.beginPath();
    ctx.moveTo(px + sx * L, py); ctx.lineTo(px, py); ctx.lineTo(px, py + sy * L);
    ctx.stroke();
  }
  ctx.globalAlpha = 1;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;

    const nameK = ease(cue(spec.nameCue ?? 0, 0.25, 0.55));
    const bodyK = ease(cue(spec.bodyCue ?? 1, 0.2, 0.55));

    /* ── 이름표 ─────────────────────────────────── */
    const px = 26, py = 34, pw = w - 52, ph = 94;
    if (nameK > 0.01) {
      /* 꺾쇠는 다 물린 뒤에도 자리에서 미세하게 벌어졌다 좁혀진다.
         🔴 밝기가 아니라 위치를 흔든다 — 밝기만 흔들면 프레임 차이가 거의 0 이라
         정적 구간 판정에 그대로 걸린다(ep01s metrics 의 숨쉬는 빛이 그 자리였다). */
      const off = (1 - easeOut(nameK)) * 22 + 2.5 + Math.sin(t * 1.9) * 2.2;
      brackets(ctx, px, py, pw, ph, off, clamp(nameK * 1.6));

      // 훑는 빛 — 이름표 안을 일정한 속도로 계속 지나간다(수배 전단을 대조하듯)
      if (nameK > 0.3) {
        ctx.save();
        ctx.beginPath(); ctx.rect(px, py, pw, ph); ctx.clip();
        const bx = px + ((t * 96) % (pw + 90)) - 45;
        ctx.globalAlpha = 0.10;
        ctx.fillStyle = tone('accent');
        ctx.fillRect(bx, py, 45, ph);
        ctx.restore();
        ctx.globalAlpha = 1;
      }

      const s = spring(nameK);
      ctx.save();
      ctx.translate(cx, py + ph / 2); ctx.scale(s, s); ctx.translate(-cx, -(py + ph / 2));
      ctx.globalAlpha = clamp(nameK * 1.6);
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      fitFont(ctx, spec.name || '', pw - 60, 900, 52);
      setShadow(ctx, GLOW, 26, 4);
      ctx.fillStyle = depthGrad(ctx, cx, py + 20, cx, py + ph - 10, 'accent');
      ctx.fillText(spec.name || '', cx, py + ph / 2 + 2);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
      ctx.restore();
    }

    /* ── 뜻풀이 ─────────────────────────────────
       자막은 이 문장을 읽지 않는다("그냥 버그가 아니라, 구조적 결함이거든요").
       뜻은 화면이, 성격은 소리가 맡는다. */
    if (nameK > 0.4 && spec.gloss) {
      ctx.globalAlpha = clamp(nameK * 1.8 - 0.7);
      ctx.textAlign = 'center'; ctx.textBaseline = 'alphabetic';
      fitFont(ctx, spec.gloss, w - 40, 800, 15);
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.gloss, cx, 160);
      ctx.globalAlpha = 1;
    }

    /* ── 몸통 ───────────────────────────────────
       원문이 지목한 코드 한 줄. 여기서는 아직 살아 있다. */
    if (bodyK > 0.02) {
      const by = 196, bh = 52, bx = 20, bw = w - 40;
      ctx.globalAlpha = clamp(bodyK * 1.6);
      ctx.fillStyle = '#000';
      roundRect(ctx, bx, by, bw, bh, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, bx, by, bw, bh, 5); ctx.stroke();

      // 왼쪽 딱지
      ctx.font = disp(900, 10.5);
      const tw = ctx.measureText(spec.bodyTag || '몸통').width + 14;
      ctx.fillStyle = tone('accent');
      roundRect(ctx, bx + 10, by - 9, tw, 18, 3); ctx.fill();
      ctx.fillStyle = '#000';
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.fillText(spec.bodyTag || '몸통', bx + 10 + tw / 2, by);

      // 코드 한 줄 — ASCII 라 mono 가 맞는 자리다
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      let cs = 14;
      ctx.font = mono(700, cs);
      while (cs > 8 && ctx.measureText(spec.body || '').width > bw - 30) {
        cs -= 1; ctx.font = mono(700, cs);
      }
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.body || '', cx, by + bh / 2 + 1);

      /* 훑는 빛 — 이 줄이 앞으로 계속 '찾아지는 대상'이 된다는 예고이자,
         자막이 흐르는 동안 화면이 멎지 않게 하는 장치. */
      ctx.save();
      ctx.beginPath(); ctx.rect(bx + 3, by + 3, bw - 6, bh - 6); ctx.clip();
      const sx = bx + ((t * 90) % (bw + 80)) - 40;
      ctx.globalAlpha = clamp(bodyK) * 0.16;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(sx, by, 40, bh);
      ctx.restore();
      ctx.globalAlpha = 1;

      if (spec.bodyNote) {
        ctx.globalAlpha = clamp(bodyK * 1.6 - 0.5) * 0.9;
        ctx.textAlign = 'center'; ctx.textBaseline = 'alphabetic';
        fitFont(ctx, spec.bodyNote, w - 30, 700, 11);
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.bodyNote, cx, 274);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
