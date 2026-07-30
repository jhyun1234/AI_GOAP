import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* nextnote — 이 편 표식이 옆으로 밀려나고, 다음 편 표식이 들어와 앉는다.

   이번 편에서 세운 두 표식('빈 말풍선', '거꾸로 게이지')이 왼쪽으로 밀려나고, 그 자리에
   다음 편의 표식이 오른쪽에서 들어와 앉는다. 아래에는 scene.hook 이 한 줄로
   남는다 — ep01s erasure 가 만들어 둔 자리를 이 편이 이어서 쓴다.

   새 표식 카드는 세 줄이다 — 막 이름(label) · 소재(topic) · 숫자(note).
   🔴 세 줄인 이유: 다음 편 예고는 '막이 바뀐다'와 '무슨 이야기인가'를 둘 다 말해야 한다.
   막 이름과 숫자만 있으면 무엇이 줄어드는지는 알려도 무슨 이야기인지는 안 알려 준다.
   막 이름은 accent · 소재는 ink · 숫자는 sub — 색이 아니라 위계로 셋을 가른다.
   세 줄 다 measureText 로 카드 안폭에 맞춰 줄인다. 폭 제한은 박스가 아니라 글자에 걸고,
   문자열을 잘라 내지 않는다(자르면 정본 표기가 아니게 된다).

   회차의 기본 도형 안에서 이 그림의 몫은 '표식이 교체된다'.
   계속 도는 것 = 밀려나는 옛 표식과 들어오는 새 표식이 한 방향으로 계속 흐른다. */

const CARRY = 3.6;

/** 카드 안폭을 넘지 않게 글자 크기를 줄인다. 문자열은 건드리지 않는다. */
function fit(ctx, text, maxW, make, start, min) {
  let s = start;
  ctx.font = make(s);
  while (s > min && ctx.measureText(text).width > maxW) { s -= 0.5; ctx.font = make(s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const prev = spec.prev || [];
    const next = spec.next || {};
    const shk = ease(cue(spec.shiftCue ?? 0, 0.1, 0.55));
    const ak = ease(cue(spec.arriveCue ?? 1, 0.15, 0.55));
    /* 숫자 한 줄은 카드가 앉는 순간이 아니라 그 숫자를 부르는 자막에서 들어온다.
       noteCue 를 안 준 회차는 arriveCue 로 떨어져 예전과 똑같이 동작한다. */
    const ntk = ease(cue(spec.noteCue ?? spec.arriveCue ?? 1, 0.15, 0.55));

    const laneY = h / 2 - 24;

    ctx.textBaseline = 'alphabetic';

    // 가이드 레인 (매우 옅게)
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([4, 6]);
    ctx.beginPath(); ctx.moveTo(12, laneY); ctx.lineTo(w - 12, laneY); ctx.stroke();
    ctx.setLineDash([]);

    /* ── 옛 표식 : 왼쪽으로 밀려난다 ─────────────── */
    const tagW = 96, tagH = 32;
    /* 🔴 두 장이 서로 다른 거리를 가면 뒤엣것이 앞엣것을 들이받는다.
       옛 코드는 출발 간격이 106 인데 도착 간격이 6 이라 i=1 이 100px 더 달렸다.
       간격 = 106 − 100·e 라 e=0.10(44.87s)부터 상자가 겹치고,
       e=0.56(45.32s)부터 '빈 말풍선' 과 '거꾸로 게이지' 가 글자째 포개졌다(실측).
       같은 거리를 가면 간격 106 이 끝까지 유지된다 — 두 장이 끝까지 두 장으로 읽힌다. */
    const SLIDE = 170;
    prev.forEach((label, i) => {
      const startX = w / 2 - 60 + i * (tagW + 10);
      const cx = startX - SLIDE * ease(shk);
      const y = laneY - tagH / 2;

      /* 🔴 옛 투명도 1-0.6*shk 는 최솟값이 0.4 라 끝까지 안 사라졌다. 표식이 사라진 게
         아니라 캔버스 경계가 잘라 준 것이고, 잘린 '게이지' 세 글자가 그 자리에 섰다.
         가장자리에 닿기 전에 0 이 되게 한다: shk=0.526 에서 alpha=0 이고
         그때 왼쪽 끝이 x=24.4 라 띠(바깥 2열)에 닿지 않는다. */
      const alpha = clamp(1 - 1.9 * shk);
      if (alpha < 0.02) return;
      ctx.globalAlpha = alpha;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, cx, y, tagW, tagH, 3); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 11);
      let ls = 11;
      while (ls > 8 && ctx.measureText(label).width > tagW - 10) { ls -= 0.5; ctx.font = disp(800, ls); }
      ctx.fillText(label, cx + tagW / 2, y + tagH / 2 + 4);
      ctx.globalAlpha = 1;
    });

    /* ── 새 표식 : 오른쪽에서 들어와 가운데에 앉는다 ─ */
    {
      /* 카드가 60 → 78 로 자랐다. 세 줄(막 이름 · 소재 · 숫자)이 들어오면서
         기준선이 26 / 48 / 66 이 되고, 바닥은 laneY + 39 = 168.5 다. */
      const nextW = 152, nextH = 78;
      const startX = w + 20;
      const endX = w / 2 - nextW / 2;
      const cx = lerp(startX, endX, ease(ak));
      const y = laneY - nextH / 2;

      const s = spring(ak);
      const sw = nextW * s, sh = nextH * s;
      const sx = cx + (nextW - sw) / 2, sy = y + (nextH - sh) / 2;

      ctx.globalAlpha = clamp(ak * 1.4);
      setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 5; ctx.strokeStyle = tone('accent');
      roundRect(ctx, sx, sy, sw, sh, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      /* 🔴 카드가 오른쪽 가장자리에 걸쳐 있는 동안 글자를 찍으면 글자가 화면 밖에서
         반토막 난다. 상자가 들어오는 것은 연출이지만 글자는 아니다. */
      const inK = clamp((w - 6 - (cx + nextW)) / 20);
      const maxW = nextW - 16;                    // 카드 안폭 136 — 좌우 8px 씩 물린다
      const sc = Math.min(1, s);                  // 스프링은 줄이기만 한다(넘칠 수 없다)
      const tx = cx + nextW / 2;

      ctx.globalAlpha = clamp(ak * 1.4) * inK;
      const fL = fit(ctx, next.label || '', maxW, x => disp(900, x), 18, 11);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, fL * sc);
      ctx.fillText(next.label || '', tx, y + 26);

      // 소재 이름 — 막 이름과 같은 순간(카드가 앉을 때)에 함께 온다
      if (next.topic) {
        const fT = fit(ctx, next.topic, maxW, x => disp(800, x), 12, 8);
        ctx.fillStyle = tone('ink'); ctx.font = disp(800, fT * sc);
        ctx.fillText(next.topic, tx, y + 48);
      }

      /* 🔴 숫자는 그 숫자를 부르는 자막에서 들어온다. 카드가 앉는 순간(ak)에 같이 띄우면
         나레이션보다 몇 초 앞서 뜬다 — 가이드의 ±20프레임을 넘는다. */
      ctx.globalAlpha = clamp(ak * 1.4) * inK * clamp(ntk * 1.4);
      const fN = fit(ctx, next.note || '', maxW, x => disp(700, x), 11, 8);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, fN * sc);
      ctx.fillText(next.note || '', tx, y + 66);
      ctx.globalAlpha = 1;
    }

    /* ── 계속 도는 것 : 한 방향으로 흐르는 흔적 ─────
       🔴 3×3 점 8개로는 프레임간 변화가 0.000233 이라 check.mjs 의 정지 문턱(0.0008)에
       못 미쳤다. 샷이 3.5초일 때는 정지 구간이 1.6초여서 통과했을 뿐이고, 예고가 세 줄로
       늘면 샷이 12초 가까이 된다. 면적 ×2.67 · 개수 ×1.75 · 알파 ×1.4 = 약 ×6.5 → 0.0015.
       카드가 60 → 78 로 자랐으므로 흔적도 laneY+46 → laneY+54 로 내린다(카드 바닥 168.5,
       흔적 183.5~187.5 → 15px 아래. hook 첫 줄 기준선 247 과도 겹치지 않는다). */
    const MARKS = 14;
    for (let i = 0; i < MARKS; i++) {
      const u = frac(t / CARRY + i / MARKS);
      const dx = lerp(20, w - 26, u);            // 오른쪽 끝 326 + 폭 6 = 332 < 가장자리 띠
      const dy = laneY + 54;
      ctx.globalAlpha = 0.7 * (1 - Math.abs(u - 0.5) * 2);
      ctx.fillStyle = tone('sub');
      ctx.fillRect(dx, dy, 6, 4);
    }
    ctx.globalAlpha = 1;

    /* ── 남는 한 줄 : scene.hook ───────────────── */
    const hook = spec.hook || scene?.hook;
    if (hook && ak > 0.2) {
      const hk = clamp((ak - 0.2) * 1.4);
      const lines = String(hook).split('\n').slice(0, 2);
      let base = 20;
      ctx.font = disp(900, base);
      while (base > 12 && lines.some(l => ctx.measureText(l).width > w - 36)) {
        base -= 1; ctx.font = disp(900, base);
      }
      ctx.globalAlpha = hk;
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink');
      const s = spring(hk);
      ctx.font = disp(900, base * s);
      const ty = h - 60;
      lines.forEach((l, i) => ctx.fillText(l, w / 2, ty + i * (base * 1.34)));
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
