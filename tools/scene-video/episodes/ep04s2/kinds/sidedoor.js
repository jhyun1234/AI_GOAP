import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* sidedoor — 달성이 없는 goal 하나만 플래너를 지나쳐 간다.

   위쪽 goal 들에는 '달성'이 있다. 목표선이 있고, 그 선까지 가면 충족된다. 그래서 이들은
   가운데 플래너 상자(A*)를 통과해 액션을 받는다. 아래의 Goal_Leisure 만 목표선 자리가
   비어 있다 — 언제까지 쉬어야 여가 완료인지 정의할 수가 없기 때문이다(noEndCue).
   그래서 이 goal 은 상자로 들어가지 않고, 상자 **아래를 지나가는** 옆문으로 배회 액션에
   바로 닿는다(doorCue). 경로가 상자를 물리적으로 비껴가는 것이 이 문단의 요지다.

   🔴 사다리를 다시 그리지 않았다. 이 편에는 사다리 그림이 이미 둘 있고(S1 의 검사, S8 의
   우선순위 축), 여기서 또 칸을 쌓으면 세 샷이 같은 모양이 된다. 이 문단이 실제로 말하는
   것은 '맨 아래'가 아니라 '달성이 없다 → 플래너를 못 쓴다'이므로, 사다리 위치는 칩
   한 줄로 적고 그림은 경로에 건다.

   계속 도는 것 = 위 goal 셋의 진행 막대(목표선까지 차올랐다가 달성 순간 칸이 밝아지고
   0 부터 다시 시작한다) + 위 경로를 흐르는 표식(goal → 플래너 → 액션),
   그리고 샷 후반에만 뜨는 배회 노드 안의 주민.

   🔴 1차 검수 반려를 여기서 고쳤다. 원래는 상시 움직이는 것이 7×5px 표식 하나뿐이라
   `check.mjs` 정적 구간이 9.2초였다(한도 3초). 배회 점은 `dk > 0.25` 라 정적 구간
   (자막 0·1번)에는 존재조차 안 했다. 그래서 **'달성이 있다'는 이 샷의 논지 자체를
   움직임으로** 바꿨다 — 달성이 있다는 말은 곧 목표선까지 차오르고 충족되고 다시
   시작한다는 뜻이므로, 진행 막대가 멈춰 있던 것이 오히려 그림의 오류였다.
   세 줄의 주기를 갈라 두어(2.4 / 2.9 / 3.5초) 한 줄이 리셋으로 비어도 나머지가 자란다.
   전부 `t` 만의 순수 함수다 — 난수·시계·이전 프레임 기억 없음. */

const FLOW = 2.8;
const WANDER = 4.2;
const BAR_PERIOD = [2.4, 2.9, 3.5];
const BAR_OFFSET = [0, 0.9, 1.8];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const sk = ease(cue(spec.showCue ?? 0, 0.15, 0.6));
    const ek = ease(cue(spec.noEndCue ?? 1, 0.15, 0.6));
    const dk = ease(cue(spec.doorCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const gx = 8, gw = 132;
    const px = 158, pw = 92, pyT = 26, pyB = 106;
    const ax = 266, aw = w - 8 - 266;

    /* ── 달성이 있는 goal 셋 ──────────────────────── */
    ctx.font = disp(700, 10); ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub');
    ctx.fillText(spec.doneLabel || '달성이 있다', gx, 20);

    for (let i = 0; i < 3; i++) {
      const y = 30 + i * 26;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, gx, y, gw, 20, 3); ctx.stroke();

      // 목표선 — 달성 지점
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.globalAlpha = 0.85;
      const tx = gx + gw - 22 - i * 6;
      ctx.beginPath(); ctx.moveTo(tx, y + 2); ctx.lineTo(tx, y + 18); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 진행 — 목표선까지 차오르다 달성하고 0 에서 다시 시작한다.
         '달성이 있다'가 정지한 그림이면 아래 Goal_Leisure 의 '끝이 없다'와 대비가 안 선다. */
      const u = frac((t + BAR_OFFSET[i]) / BAR_PERIOD[i]);
      const full = tx - gx - 3;

      // 달성 순간 — 칸 전체가 한 번 밝아졌다 꺼진다
      const fl = u < 0.18 ? 1 - u / 0.18 : 0;
      if (fl > 0) {
        ctx.fillStyle = tone('sub'); ctx.globalAlpha = 0.32 * fl;
        roundRect(ctx, gx + 1.5, y + 1.5, gw - 3, 17, 2); ctx.fill();
        ctx.globalAlpha = 1;
      }

      ctx.fillStyle = tone('sub'); ctx.globalAlpha = 0.5;
      roundRect(ctx, gx + 3, y + 3, Math.max(2.5, full * u), 14, 2); ctx.fill();
      ctx.globalAlpha = 1;

      if (i === 0 && spec.example) {
        ctx.font = disp(700, 9.5); ctx.fillStyle = tone('sub');
        ctx.textAlign = 'left';
        ctx.fillText(spec.example, gx + 6, y + 14);
      }
    }

    /* ── 플래너 상자 ──────────────────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, px, pyT, pw, pyB - pyT, 4); ctx.stroke();
    ctx.textAlign = 'center';
    {
      const s = spec.planner || 'GOAP 플래너 (A*)';
      const parts = s.split(' (');
      const a = parts[0], b = parts[1] ? '(' + parts[1] : '';
      let fs = fit(a, 800, 12, pw - 12);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(a, px + pw / 2, pyT + 38);
      if (b) {
        ctx.font = disp(700, 10.5); ctx.fillStyle = tone('sub');
        ctx.fillText(b, px + pw / 2, pyT + 55);
      }
    }

    // goal → 플래너 → 액션
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    for (let i = 0; i < 3; i++) {
      const y = 40 + i * 26;
      ctx.beginPath(); ctx.moveTo(gx + gw, y); ctx.lineTo(px, y); ctx.stroke();
    }
    ctx.beginPath(); ctx.moveTo(px + pw, 66); ctx.lineTo(ax, 66); ctx.stroke();

    // 액션 상자
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, ax, 46, aw, 40, 4); ctx.stroke();
    ctx.textAlign = 'center';
    ctx.font = disp(800, 12); ctx.fillStyle = tone('ink');
    ctx.fillText(spec.actionLabel || '액션', ax + aw / 2, 71);

    /* 계속 도는 것 — 위 경로를 흐르는 표식 */
    {
      const u = frac(t / FLOW);
      const legA = px - (gx + gw), legB = ax - (px + pw);
      const total = legA + legB;
      const d = u * total;
      let mx, my;
      if (d < legA) { mx = gx + gw + d; my = 40 + 26; }
      else { mx = px + pw + (d - legA); my = 66; }
      ctx.globalAlpha = 0.55 + 0.4 * Math.sin(Math.PI * u);
      ctx.fillStyle = tone('sub');
      ctx.fillRect(mx - 3, my - 2.5, 7, 5);
      ctx.globalAlpha = 1;
    }

    /* ── 우선순위 칩 ──────────────────────────────── */
    if (sk > 0.02) {
      ctx.globalAlpha = clamp(sk * 1.8);
      ctx.textAlign = 'left';
      const s = spec.leisureNote || '';
      const fs = fit(s, 800, 10.5, 176);
      ctx.font = disp(800, fs);
      const tw = ctx.measureText(s).width;
      // 오른쪽에 붙인다 — 왼쪽 위는 '즉시 이탈' 화살표가 지나가는 자리다
      const cxx = w - 8 - (tw + 16);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, cxx, 118, tw + 16, 21, 3); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.fillText(s, cxx + 8, 133);
      ctx.globalAlpha = 1;
    }

    /* ── Goal_Leisure — 목표선이 없다 ─────────────── */
    const ly = 150, lh = 46;
    if (sk > 0.02) {
      const s = Math.min(1, spring(sk));
      ctx.globalAlpha = clamp(sk * 1.8);
      setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, gx + (gw - gw * s) / 2, ly + (lh - lh * s) / 2, gw * s, lh * s, 4);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'left';
      ctx.font = mono(700, 13); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.leisure || 'Goal_Leisure', gx + 9, ly + 21);

      // 목표선이 있어야 할 자리 — 비어 있다
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([4, 5]);
      ctx.beginPath(); ctx.moveTo(gx + 9, ly + 33); ctx.lineTo(gx + gw - 9, ly + 33); ctx.stroke();
      ctx.setLineDash([]);
      ctx.globalAlpha = 1;
    }
    if (ek > 0.05) {
      ctx.globalAlpha = clamp(ek * 1.6);
      ctx.textAlign = 'left';
      const s = spec.noEnd || '';
      const fs = fit(s, 800, 11.5, gw + 30);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(s, gx, ly + lh + 20);
      ctx.globalAlpha = 1;
    }

    /* ── 옆문 — 플래너 아래를 지나 배회로 ─────────── */
    const wy = ly + lh / 2;
    if (dk > 0.02) {
      const k = ease(clamp(dk / 0.8));
      ctx.globalAlpha = clamp(dk * 2);
      setShadow(ctx, GLOW, 10, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(gx + gw, wy);
      ctx.lineTo(lerp(gx + gw, ax, k), wy);
      ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;

      if (spec.door) {
        ctx.globalAlpha = clamp(dk * 1.6);
        ctx.textAlign = 'center';
        ctx.font = mono(700, 10.5); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.door, (gx + gw + ax) / 2, wy - 9);
        ctx.globalAlpha = 1;
      }
    }

    // 배회 노드
    if (dk > 0.25) {
      const k = clamp((dk - 0.25) / 0.5);
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, ax, ly, aw, lh, 4); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = disp(800, 12); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.action || '배회', ax + aw / 2, ly + 19);

      /* 계속 도는 것 — 배회 노드 안을 도는 주민 */
      const u = frac(t / WANDER) * Math.PI * 2;
      const cx = ax + aw / 2, cy = ly + 32, r = 9;
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.arc(cx + Math.cos(u) * r, cy + Math.sin(u) * r * 0.55, 3.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 상위 goal 이 켜지면 즉시 이탈 ────────────── */
    if (dk > 0.55 && spec.exitLabel) {
      const k = clamp((dk - 0.55) / 0.45);
      ctx.globalAlpha = k;
      // 위로 향하는 화살표
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      const axx = gx + 16, ay0 = ly - 4, ay1 = lerp(ly - 4, 112, k);
      ctx.beginPath();
      ctx.moveTo(axx, ay0); ctx.lineTo(axx, ay1);
      ctx.moveTo(axx, ay1); ctx.lineTo(axx - 4, ay1 + 6);
      ctx.moveTo(axx, ay1); ctx.lineTo(axx + 4, ay1 + 6);
      ctx.stroke();

      ctx.textAlign = 'left';
      const fs = fit(spec.exitLabel, 700, 10.5, w - 16);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.exitLabel, gx, 240);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
