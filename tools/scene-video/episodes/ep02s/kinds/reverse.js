import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* reverse — 하나만 방향이 반대인 게이지.

   세 게이지(체력·기분·충성도)는 위로 차오르고 얼굴이 밝아진다. 네 번째(허기)는
   차오를수록 얼굴이 시들어 간다. GOAP 목표는 아래 방향 화살표(↓)로 붙는다. 잘 어울리는
   셋 옆에 튀는 하나가 있어야 눈에 든다.

   회차의 기본 도형 안에서 이 그림의 몫은 '방향이 반대'.
   계속 도는 것 = 허기 게이지 옆의 시들었다 되살아나는 굶주림 아이콘. */

const PULSE = 2.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const good = spec.goodBars || [];
    const odd = spec.oddBar || {};

    const fk = ease(cue(spec.fillCue ?? 1, 0.15, 0.55));
    const gk = ease(cue(spec.flagCue ?? 2, 0.15, 0.45));
    const goalK = ease(cue(spec.goalCue ?? 3, 0.15, 0.5));

    const cols = good.length + 1;
    const gap = 10;
    const totalPad = 24;
    const bw = (w - totalPad - gap * (cols - 1)) / cols;
    const barTop = 70;
    const barBot = h - 108;   // GOAP 목표 라벨(hgt=22)이 barBot+32 에 들어가므로 h-108 로 잡아 아래 46px 여유 확보

    ctx.textBaseline = 'alphabetic';

    // 사인
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.fillText('높을수록 →', w / 2, 14);

    /* ── 정상 게이지 셋 ────────────────────────── */
    good.forEach((b, i) => {
      const x = totalPad / 2 + i * (bw + gap);
      drawBar(ctx, x, barTop, bw, barBot - barTop, b.fill * fk, b.label, tone('ink'), '↑', 'good', w);
    });

    /* ── 튀는 하나 : 허기 (거꾸로) ─────────────── */
    {
      const x = totalPad / 2 + good.length * (bw + gap);
      // 배경 하이라이트 (gk 로 강조)
      if (gk > 0.02) {
        ctx.globalAlpha = clamp(gk * 1.4) * 0.14;
        ctx.fillStyle = tone('accent');
        roundRect(ctx, x - 4, barTop - 12, bw + 8, (barBot - barTop) + 62, 6);
        ctx.fill();
        ctx.globalAlpha = 1;
      }
      // 게이지 자체 — 채워진 값 fill, 하지만 채워질수록 나쁘다
      const useCol = gk > 0.4 ? tone('accent') : tone('ink');
      drawBar(ctx, x, barTop, bw, barBot - barTop, (odd.fill || 0) * fk, odd.label, useCol, '↓', 'bad', w);

      // 굶주림 아이콘 — 계속 도는 것. 게이지가 찰수록 더 시들어 보인다
      const pulse = (Math.sin(t * Math.PI * 2 / PULSE) + 1) / 2;
      const wilt = clamp((odd.fill || 0) * fk);
      const cx = x + bw / 2;
      const cy = barTop - 20;
      ctx.strokeStyle = useCol; ctx.lineWidth = 3;
      // 얼굴 : 원
      ctx.beginPath(); ctx.arc(cx, cy, 10, 0, Math.PI * 2); ctx.stroke();
      // 눈 두 개 — wilt 가 커지면 눈이 감기듯이 짧아짐
      const eyeLen = 3 - 1.5 * wilt;
      ctx.beginPath();
      ctx.moveTo(cx - 5, cy - 2); ctx.lineTo(cx - 5 + eyeLen, cy - 2);
      ctx.moveTo(cx + 5 - eyeLen, cy - 2); ctx.lineTo(cx + 5, cy - 2);
      ctx.stroke();
      // 입 — wilt 낮으면 웃음, 높으면 반대 곡선(찡그림). pulse 로 미세 흔들림
      const smile = lerp(2, -3, wilt) + pulse * 0.4;
      ctx.beginPath();
      ctx.moveTo(cx - 4, cy + 4);
      ctx.quadraticCurveTo(cx, cy + 4 - smile, cx + 4, cy + 4);
      ctx.stroke();

      // GOAP 목표 라벨 — goalCue 에서
      if (goalK > 0.02) {
        const gy = barBot + 32;
        const gwText = odd.goal || '';
        /* 🔴 한글이 섞인 문자열을 mono 로 찍으면 안 된다(lib.js 폰트 주석).
           SceneMono 에 한글이 없어 '목표' 만 시스템 폴백으로 떨어진다 — 실측으로
           '목표' 폭이 mono 지정본과 폰트 없는 폴백본이 둘 다 26.00px 로 같았다.
           한 줄 안에서 폰트가 갈리고, 그 폴백은 머신마다 다르다. */
        ctx.font = disp(700, 11);
        const tw = ctx.measureText(gwText).width;        // 실측 138.72
        /* 🔴 폭 제한을 박스에만 걸고 글자에는 안 걸었던 자리다. 박스는 82.50 인데
           글자는 164.60 이라 글자가 박스를 뚫고 나가 캔버스에서 잘렸다
           (오른쪽 33.05px 초과 · 'GOAP 목표 : HungerLe' 까지만 보였다).
           박스는 글자를 감싸고, 가운데 정렬 기준점을 캔버스 안으로 당긴다. */
        const gw = tw + 18;
        const PAD = 16;                                   // 글로우(blur 10)까지 포함한 여백
        const gcx = gw < w - PAD * 2
          ? clamp(x + bw / 2, PAD + gw / 2, w - PAD - gw / 2)
          : w / 2;
        const gx = gcx - gw / 2;
        ctx.globalAlpha = clamp(goalK * 1.4);
        ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
        setShadow(ctx, GLOW, 10, 0);
        roundRect(ctx, gx, gy, gw, 22, 3); ctx.stroke();
        clearShadow(ctx);
        ctx.textAlign = 'center';
        ctx.fillStyle = tone('accent');
        ctx.fillText(gwText, gcx, gy + 15);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};

function drawBar(ctx, x, yTop, w, hgt, fill, label, col, arrow, kind, cw) {
  // 트랙
  ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
  roundRect(ctx, x, yTop, w, hgt, 4); ctx.stroke();
  // 채우기
  const f = clamp(fill);
  const fh = hgt * f;
  ctx.fillStyle = col;
  if (col === tone('accent')) setShadow(ctx, GLOW, 10, 0);
  roundRect(ctx, x + 3, yTop + hgt - fh + 1.5, w - 6, Math.max(0, fh - 3), 3);
  ctx.fill();
  clearShadow(ctx);
  // 라벨 — 막대 중심에 두되 캔버스 밖으로 넘치면 안쪽으로 당긴다.
  // '허기 · HungerLevel'(84.40px)은 지금 오른쪽 여백이 7.05px 뿐이다(실측).
  ctx.textAlign = 'center';
  ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
  const lw = ctx.measureText(label || '').width;
  const lcx = (cw && lw < cw - 16) ? clamp(x + w / 2, 8 + lw / 2, cw - 8 - lw / 2) : x + w / 2;
  ctx.fillText(label || '', lcx, yTop + hgt + 14);
  // 방향 화살표 : 게이지 안쪽 위/아래
  ctx.fillStyle = col; ctx.font = disp(900, 14);
  const ay = kind === 'good' ? yTop - 4 : yTop + hgt + 30;
  ctx.fillText(arrow, x + w / 2, ay);
}
