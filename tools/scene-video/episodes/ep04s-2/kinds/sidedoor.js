import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* sidedoor — 달성이 없는 goal 하나만 플래너를 지나쳐 간다.

   위쪽 goal 들에는 '달성'이 있다. 목표선이 있고, 그 선까지 가면 충족된다. 그래서 이들은
   가운데 플래너 상자(A*)를 통과해 액션을 받는다(doneCue). 아래의 Goal_Leisure 만 목표선
   자리가 비어 있다 — 언제까지 쉬어야 여가 완료인지 정의할 수가 없기 때문이다(noEndCue).
   그래서 이 goal 은 상자로 들어가지 않고, 상자 **아래를 지나가는** 옆문으로 배회 액션에
   바로 닿는다(doorCue). 경로가 상자를 물리적으로 비껴가는 것이 이 문단의 요지다.

   🔴 사다리를 다시 그리지 않았다. 바로 앞 샷(S1 standstill)이 세로 사다리를 그리고 그
   맨 아래에 여가 칸이 붙는 장면까지 이미 보여 줬다. 여기서 칸을 또 쌓으면 두 샷이 같은
   모양이 된다. 이 문단이 실제로 말하는 것은 '맨 아래'가 아니라 '달성이 없다 → 플래너를
   못 쓴다'이므로, 위치는 앞 샷에 맡기고 이 샷은 경로만 그린다.
   (재분할 전 이 주석은 근거로 'S1 의 검사, S8 의 우선순위 축'을 들었는데 S8 은 4-1편으로
   갔다. 판단은 그대로 유효하고 근거만 이 편의 실제 샷으로 갱신했다 — 2026-08-04.)

   🔴 재분할로 cue 배치를 다시 짰다. 원본은 showCue(0)에서 Goal_Leisure 상자를 켰는데,
   그 자막이 이 편에서는 '원래 goal은 달성이 있어요'로 바뀌었다. 말이 위쪽을 부르는데
   화면이 아래쪽을 켜면 안 된다. 그래서 doneCue(0) = 목표선과 예시, noEndCue(1) =
   Goal_Leisure 상자와 빈 목표선, doorCue(2) = 옆문·배회다.
   원본의 '우선순위 1 · 사다리 맨 아래' 칩도 뺐다 — S1 이 그 사실을 장면으로 보여 준다.

   계속 도는 것 = 위 goal 셋의 진행 막대(목표선까지 차올랐다가 달성 순간 칸이 밝아지고
   0 부터 다시 시작한다) + 위 경로를 흐르는 표식(goal → 플래너 → 액션),
   그리고 샷 후반에만 뜨는 배회 노드 안의 주민. 전부 t·cue 만의 순수 함수다. */

const FLOW = 2.8;
const WANDER = 4.2;
const BAR_PERIOD = [2.4, 2.9, 3.5];
const BAR_OFFSET = [0, 0.9, 1.8];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const dn = ease(cue(spec.doneCue ?? 0, 0.15, 0.55));
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
    ctx.textAlign = 'left';
    if (dn > 0.03) {
      ctx.globalAlpha = clamp(dn * 1.6);
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.doneLabel || '달성이 있다', gx, 20);
      ctx.globalAlpha = 1;
    }

    for (let i = 0; i < 3; i++) {
      const y = 30 + i * 26;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, gx, y, gw, 20, 3); ctx.stroke();

      // 목표선 — 달성 지점. doneCue 에서 세로로 자란다.
      const tx = gx + gw - 22 - i * 6;
      const tk = clamp(dn * (3 + 1) - i);
      if (tk > 0.04) {
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.globalAlpha = 0.85 * clamp(tk * 1.5);
        const half = 8 * clamp(tk * 1.5);
        ctx.beginPath();
        ctx.moveTo(tx, y + 10 - half); ctx.lineTo(tx, y + 10 + half);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

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

      if (i === 0 && spec.example && dn > 0.35) {
        const k = clamp((dn - 0.35) / 0.35);
        ctx.globalAlpha = k;
        ctx.font = disp(800, 9.5); ctx.fillStyle = tone('ink');
        ctx.textAlign = 'left';
        ctx.fillText(spec.example, gx + 6, y + 14);
        ctx.globalAlpha = 1;
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
      const fs = fit(a, 800, 12, pw - 12);
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

    /* ── Goal_Leisure — 목표선이 없다 ─────────────── */
    const ly = 140, lh = 46;
    if (ek > 0.02) {
      const s = Math.min(1, spring(clamp(ek * 1.6)));
      ctx.globalAlpha = clamp(ek * 1.8);
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
    if (ek > 0.45 && spec.noEnd) {
      const k = clamp((ek - 0.45) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.noEnd, 800, 11.5, gw + 30);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.noEnd, gx, ly + lh + 20);
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
    if (dk > 0.45) {
      const k = clamp((dk - 0.45) / 0.35);
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
    if (dk > 0.62 && spec.exitLabel) {
      const k = clamp((dk - 0.62) / 0.38);
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
