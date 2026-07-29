import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* wire — 이 회차의 척추.

   ep01s 는 "탐색이 4096번 폭발했다" = 양의 이야기였고 그래서 격자·카운터·막대가 맞았다.
   0편은 양이 아니라 **어긋남**이다. 아래(현실)에서 출발한 값이 위(두뇌)에 다른 값으로
   도착한다 — 그 사이 어딘가에서 바뀐다. 그래서 이 편의 기본 도형은 칸이 아니라
   **한 줄의 세로 선과 그 위를 올라가는 신호 하나**다.

   같은 그림을 두 번 쓴다. 그게 이 편의 구조다.
     mode:"lie"  (S1)  변조 지점을 지나며 '못 감' 이 '도착' 으로 바뀐다.
     mode:"gate" (S7)  같은 자리에 규칙 9 의 문이 서고, '못 감' 이 '못 감' 인 채로 올라간다.
   시청자는 40초쯤 뒤에 같은 화면으로 돌아와 **같은 자리가 달라진 것**을 본다.
   회차 안의 재사용은 돌려막기가 아니라 되받기다 — 두 모드의 그림이 서로 다른 말을 한다.

   초록 규칙(이 회차 한정): 초록은 '옳다'가 아니라 **화면이 지금 가리키는 지점**이다.
   그래서 값(패킷·받은 값)은 전부 흰색이고, 변조 지점과 게이트만 초록이다.
   거짓말에 초록을 칠하지 않기 위해서다.

   움직임은 t 로 계속 돈다(신호는 자막과 무관하게 계속 올라간다). cue 는 강조만 얹는다 —
   샷이 한 번도 멎지 않는 이유이자, 0초부터 이미 사건이 벌어져 있는 이유. */

const PERIOD = 2.45;   // 신호 한 번이 아래에서 위로 올라가는 데 걸리는 초
const LEAD = 2.10;     // 🔴 콜드 오픈. t=0 에 이미 첫 신호가 위에 도착해 있다 (제목 카드 없음)

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

/** 검정으로 채우고 회색 테두리. lit>0 이면 그 위에 초록 테두리를 겹친다. */
function frame(ctx, x, y, bw, bh, lit = 0, glow = false) {
  ctx.fillStyle = '#000';
  roundRect(ctx, x, y, bw, bh, 6); ctx.fill();
  ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
  roundRect(ctx, x, y, bw, bh, 6); ctx.stroke();
  if (lit > 0.01) {
    ctx.globalAlpha = clamp(lit);
    if (glow) setShadow(ctx, GLOW, 14, 0);
    ctx.strokeStyle = tone('accent');
    roundRect(ctx, x, y, bw, bh, 6); ctx.stroke();
    clearShadow(ctx); ctx.globalAlpha = 1;
  }
}

/** 신호 알약. 값은 언제나 흰색이다 — 거짓말도 흰색이다(위 초록 규칙). */
function packet(ctx, cx, cy, text) {
  ctx.font = disp(900, 16);
  const bw = Math.min(ctx.measureText(text).width + 26, 150), bh = 30;
  ctx.fillStyle = tone('ink');
  roundRect(ctx, cx - bw / 2, cy - bh / 2, bw, bh, 5); ctx.fill();
  ctx.fillStyle = '#000';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText(text, cx, cy + 1);
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;
    const gate = spec.mode === 'gate';

    const topY = 2, topH = 66, botH = 54;
    const botY = gate ? h - 24 - botH : h - 36 - botH;
    const wt = topY + topH, wb = botY;
    const midY = (wt + wb) / 2;

    /* ── 신호 ───────────────────────────────────────
       t 의 함수. 위쪽 18% 구간은 '도착해 머무는' 시간이라 도착이 눈에 걸린다. */
    const tt = t + LEAD;
    const ph = frac(tt / PERIOD);
    const u = clamp(ph / 0.82);
    // 위 칸 테두리보다 18px 아래에서 멈춘다 — 알약이 두뇌 칸에 물리지 않게
    const py = lerp(wb - 6, wt + 18, u);
    const passed = py <= midY;
    const truth = spec.truth || '못 감';
    const lie = spec.lie || '도착';
    const carrying = gate ? truth : (passed ? lie : truth);
    const landed = Math.floor((tt + PERIOD * 0.18) / PERIOD);

    /* ── 선 ─────────────────────────────────────────
       지나온 구간만 흰 선으로 남는다 — 신호가 실제로 지나갔다는 자국. */
    ctx.lineCap = 'round';
    ctx.lineWidth = 3;
    ctx.strokeStyle = tone('track');
    ctx.beginPath(); ctx.moveTo(cx, wb); ctx.lineTo(cx, wt); ctx.stroke();
    ctx.strokeStyle = 'rgba(255,255,255,0.55)';
    ctx.beginPath(); ctx.moveTo(cx, wb); ctx.lineTo(cx, py); ctx.stroke();

    /* 🔴 그리는 순서가 두 모드에서 갈린다.
       lie 모드에서는 신호가 맨 위에 있어야 한다 — 변조 지점을 지나며 글자가 바뀌는 게
       이 샷의 전부라 무엇에도 가려지면 안 된다.
       gate 모드에서는 신호를 먼저 그린다 — 문을 지날 때 문 뒤로 잠깐 사라졌다가
       같은 값으로 나온다. 조항 문구가 신호에 가려지지 않는 효과도 겸한다. */
    if (gate) packet(ctx, cx, py, carrying);

    /* ── 위 : 두뇌 ─────────────────────────────────
       받은 값이 여기 남는다. 이 편의 사건은 전부 이 칸의 내용이 틀렸다는 것이다. */
    frame(ctx, 6, topY, w - 12, topH, 0, false);
    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.topLabel || '두뇌', 18, topY + 21);

    if (landed >= 1) {
      // 받은 값 — 두뇌에 남은 것. 이 편의 사건은 전부 이 한 칸이 틀렸다는 것이다
      const got = gate ? truth : lie;
      ctx.textAlign = 'center';
      fitFont(ctx, got, w - 130, 900, 24);
      ctx.fillStyle = tone('ink');
      ctx.fillText(got, cx, topY + 52);

      /* 눈금 — 몇 번 받았는가. 🔴 숫자를 찍지 않는다.
         원문에 없는 수치(횟수)를 화면이 주장하지 않기 위해서다. 눈금은 애니메이션 시계가
         만드는 것이고 나레이션도 특정 숫자를 말하지 않는다. 쌓인다는 사실만 보이면 된다. */
      const ticks = Math.min(landed, 8);
      ctx.fillStyle = tone('ink');
      for (let i = 0; i < ticks; i++) {
        ctx.fillRect(w - 20 - i * 7, topY + 12, 3, 12);
      }
    }

    /* ── 아래 : 실제로 일어난 일 ────────────────── */
    const realK = gate ? 0 : ease(cue(spec.realCue ?? 0, 0.2, 0.5));
    const botW = w - 12;
    frame(ctx, 6, botY, botW, botH, realK, false);
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.botLabel || '실제로 일어난 일', 18, botY + 19);
    ctx.fillStyle = tone('ink');
    ctx.font = disp(900, 20);
    ctx.fillText(truth, 18, botY + 44);

    if (!gate) {
      /* ── 변조 지점 ───────────────────────────────
         마름모 하나. 신호가 여기에 닿는 순간 초록으로 터지고, 그 뒤로 알약의 글자가 바뀐다.
         '거짓말이 태어나는 좌표'를 화면에 못박는 것이 이 샷의 전부다. */
      const tam = ease(cue(spec.tamperCue ?? 1, 0.2, 0.5));
      const near = clamp(1 - Math.abs(py - midY) / 26);
      const r = 12;
      ctx.save();
      ctx.translate(cx, midY); ctx.rotate(Math.PI / 4);
      if (near > 0.02) {
        ctx.globalAlpha = near;
        setShadow(ctx, GLOW, 18, 0);
        ctx.fillStyle = tone('accent');
        ctx.fillRect(-r, -r, r * 2, r * 2);
        clearShadow(ctx); ctx.globalAlpha = 1;
      }
      ctx.lineWidth = 3;
      ctx.strokeStyle = tam > 0.5 ? tone('accent') : tone('ink');
      ctx.strokeRect(-r, -r, r * 2, r * 2);
      ctx.restore();

      if (tam > 0.02 && spec.tamperLabel) {
        ctx.globalAlpha = tam;
        ctx.textAlign = 'right';
        ctx.fillStyle = tone('accent'); ctx.font = disp(800, 11);
        ctx.fillText(spec.tamperLabel, w - 10, midY - 22);
        ctx.globalAlpha = 1;
      }

      /* ── 커밋 여섯 칸 ────────────────────────────
         이 편의 열린 고리를 화면에도 심는 자리다. 자막이 "커밋 여섯 개"라고 말할 때
         빈 칸 여섯이 뜨고, S6(ledger)이 그 여섯 칸을 채우며 고리를 닫는다. */
      const ck = ease(cue(spec.commitsCue ?? 2, 0.2, 0.5));
      if (ck > 0.02) {
        const n = spec.commits || 6, gapx = 24, y = h - 24;
        const x0 = cx - (n - 1) * gapx / 2;
        ctx.globalAlpha = ck;
        ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
        for (let i = 0; i < n; i++) {
          const k = clamp(ck * n - i);
          if (k <= 0) continue;
          ctx.globalAlpha = ck * k;
          ctx.beginPath(); ctx.arc(x0 + i * gapx, y, 5, 0, Math.PI * 2); ctx.stroke();
        }
        ctx.globalAlpha = ck;
        ctx.textAlign = 'right';
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
        ctx.fillText(spec.commitsLabel || '커밋', x0 - 12, y + 4);
        ctx.globalAlpha = 1;
      }
    } else {
      /* ── 게이트 ──────────────────────────────────
         S1 에서 값이 바뀌던 바로 그 y 좌표에 문이 선다. 되받기의 핵심이라 자리를 옮기지 않았다. */
      const gk = ease(cue(spec.gateCue ?? 0, 0.25, 0.55));
      const gh = 44, gy = midY - gh / 2;
      if (gk > 0.02) {
        ctx.globalAlpha = gk;
        ctx.fillStyle = '#000';
        roundRect(ctx, 6, gy, w - 12, gh, 6); ctx.fill();
        setShadow(ctx, GLOW, 16, 0);
        ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
        roundRect(ctx, 6, gy, w - 12, gh, 6); ctx.stroke();
        clearShadow(ctx);

        // 조항 번호 배지 — 문 위에 붙는 작은 명패
        if (spec.badge) {
          ctx.font = disp(900, 12);
          const bw = ctx.measureText(spec.badge).width + 18;
          ctx.fillStyle = tone('accent');
          roundRect(ctx, cx - bw / 2, gy - 26, bw, 22, 4); ctx.fill();
          ctx.fillStyle = '#000';
          ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
          ctx.fillText(spec.badge, cx, gy - 14);
          ctx.textBaseline = 'alphabetic';
        }
        ctx.globalAlpha = 1;
      }

      // 조항 문구 — 왼쪽에서 오른쪽으로 새겨진다
      const rk = ease(cue(spec.ruleCue ?? 1, 0.2, 0.6));
      if (rk > 0.02 && spec.rule) {
        ctx.save();
        ctx.beginPath(); ctx.rect(6, gy, (w - 12) * rk, gh); ctx.clip();
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        fitFont(ctx, spec.rule, w - 44, 800, 12);
        ctx.fillStyle = tone('ink');
        ctx.fillText(spec.rule, cx, midY + 1);
        ctx.restore();
        ctx.textBaseline = 'alphabetic';
      }

      /* ── 적용 범위 ───────────────────────────────
         원문 117행: "게임 속 AI든, 요즘 화제인 AI 에이전트든". 같은 문 아래로 두 갈래가 모인다.
         자막이 "AI 에이전트도 똑같더라고요"라고 말할 때 화면은 **둘이 같은 문을 지난다**를 보여준다. */
      const sk = ease(cue(spec.scopeCue ?? 2, 0.2, 0.55));
      const scope = spec.scope || [];
      if (sk > 0.02 && scope.length) {
        const fy = midY + 52;
        ctx.globalAlpha = sk;
        scope.forEach((s, i) => {
          const sx = i === 0 ? 58 : w - 58;
          ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(255,255,255,0.55)';
          ctx.beginPath();
          ctx.moveTo(sx, fy - 13);
          ctx.lineTo(cx + (i === 0 ? -10 : 10), midY + gh / 2 + 6);
          ctx.stroke();

          ctx.font = disp(800, 11);
          const bw = Math.min(ctx.measureText(s).width + 18, 108);
          ctx.fillStyle = '#000';
          roundRect(ctx, sx - bw / 2, fy - 13, bw, 26, 4); ctx.fill();
          ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
          roundRect(ctx, sx - bw / 2, fy - 13, bw, 26, 4); ctx.stroke();
          ctx.fillStyle = tone('ink');
          ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
          ctx.fillText(s, sx, fy + 1);
        });
        ctx.globalAlpha = 1;
        ctx.textBaseline = 'alphabetic';
      }
    }

    if (!gate) packet(ctx, cx, py, carrying);

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
