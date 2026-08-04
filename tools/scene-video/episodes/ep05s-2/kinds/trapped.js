import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* trapped — 길 위에 집이 앉는다. 그래서 길이 끊기고, 안에 있던 사람이 못 나온다.

   이 샷의 사건은 셋이고 자막 셋에 하나씩 물려 있다.
   ① buildCue — 이미 흐르고 있던 통행선 **위로** 집이 앉고, 길을 걷던 주민 하나가
      집 안에 들어간 채로 벽 사이를 좌우로만 오간다(= 갇힌다).
   ② blockCue — 통행선이 집 테두리에서 벌어져 끊기고 그 자리에 ✕ 둘이 박힌다.
      `BlocksMovement` 는 여기서 이름이 붙는다.
   ③ standCue — 집 아래에 **서는 타일** 한 칸이 켜지고, 갇혀 있던 주민이 그 칸으로
      내려선다. 짓는 자리와 서는 자리가 갈리는 것이 이 그림의 결말이다.

   🔴 통행선은 처음부터 이어져 있고 처음부터 흐른다. 그래야 "가로지르던 길이 끊겼다"가
   화면에서 참이 된다 — 끊긴 두 토막으로 태어나게 해 놓고 주석에만 "가로지른다"고 적었던
   앞 회차의 결함(ep05s2 검수 1·2차)을 되풀이하지 않으려고 `cut` 하나로 벌린다.
   `cut = 0` 이면 두 토막이 집 한가운데(CX)에서 만나 한 줄이고, `cut = 1` 이면 집 폭만큼
   벌어진다.

   🔴 앞 회차의 같은 소재(집·통행 차단)와 다른 점: 저쪽은 **선과 ✕ 만** 있었고 갇힌 사람이
   화면에 없었다. 여기는 갇힌 주민이 실제로 벽에 막혀 좌우로만 움직이고, 그 사람이
   마지막에 서는 타일로 나오는 것이 해결이다. 문제와 해결이 같은 표식 하나에 걸려 있다.

   계속 도는 것 = ① 통행선 위를 흐르는 파선(자막 0 부터 끝까지) ② 주민 표식 —
   집이 앉기 전에는 길 위를 왕복하고, 갇힌 뒤에는 벽 사이를 오가고, 나온 뒤에는 타일 위에서
   숨 쉰다. 셋 다 t 의 함수라 어느 자막 구간에도 멈추는 창이 없다. */

const PATH_Y = 130;
const HX = 134, HW = 92, HTOP = 76, HBOT = 160;
const CX = HX + HW / 2;                     // 180
const APEX_Y = 46, EAVE_L = 122, EAVE_R = 238;
const TILE = { x: 180, cy: 216, h: 17 };    // x 163~197 · y 199~233

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const bk = ease(cue(spec.buildCue ?? 0, 0.15, 0.62));
    const mk = ease(cue(spec.blockCue ?? 1, 0.15, 0.62));
    const sk = ease(cue(spec.standCue ?? 2, 0.15, 0.62));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 통행선 — 이어진 채로 흐르다가 집이 앉으면서 벌어진다 ── */
    const cut = clamp(mk * 1.8);
    const gL = lerp(CX, HX, cut), gR = lerp(CX, HX + HW, cut);
    ctx.globalAlpha = 0.55 + 0.45 * bk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.setLineDash([13, 9]);
    ctx.lineDashOffset = -frac(t / 1.7) * 22;    // 계속 도는 것 — 길 위의 통행
    ctx.beginPath();
    ctx.moveTo(16, PATH_Y); ctx.lineTo(gL, PATH_Y);
    ctx.moveTo(gR, PATH_Y); ctx.lineTo(w - 16, PATH_Y);
    ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    /* ── 집 — 길 위에 앉는다 ─────────────────────── */
    if (bk > 0.02) {
      const k = clamp(bk * 2.2);
      const s = Math.min(1, spring(clamp(bk * 1.4)));
      ctx.globalAlpha = k;
      ctx.save();
      ctx.translate(CX, (HTOP + HBOT) / 2);
      ctx.scale(s, s);
      ctx.translate(-CX, -(HTOP + HBOT) / 2);

      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 12, 0);
      roundRect(ctx, HX, HTOP, HW, HBOT - HTOP, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(EAVE_L, HTOP); ctx.lineTo(CX, APEX_Y); ctx.lineTo(EAVE_R, HTOP);
      ctx.stroke();
      clearShadow(ctx);

      // 통과 못 하는 면 — 어두운 fill 대신 빗금으로만 채운다(3색 규칙)
      ctx.save();
      ctx.beginPath(); ctx.rect(HX + 3, HTOP + 3, HW - 6, HBOT - HTOP - 6); ctx.clip();
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (let i = -3; i < 9; i++) {
        const x = HX + i * 15;
        ctx.beginPath();
        ctx.moveTo(x, HBOT); ctx.lineTo(x + (HBOT - HTOP), HTOP);
        ctx.stroke();
      }
      ctx.restore();
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    /* ── ✕ — 끊긴 자리 ───────────────────────────── */
    const xk = clamp((mk - 0.3) / 0.4);
    if (xk > 0.02) {
      ctx.globalAlpha = xk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      [HX, HX + HW].forEach(mx => {
        ctx.beginPath();
        ctx.moveTo(mx - 7, PATH_Y - 7); ctx.lineTo(mx + 7, PATH_Y + 7);
        ctx.moveTo(mx + 7, PATH_Y - 7); ctx.lineTo(mx - 7, PATH_Y + 7);
        ctx.stroke();
      });
      ctx.globalAlpha = 1;
    }

    /* ── 서는 타일 — 짓는 자리와 갈린다 ──────────── */
    if (sk > 0.02) {
      const k = clamp(sk * 2);
      const s = Math.min(1, spring(clamp(sk * 1.3)));
      const hh = TILE.h * s;
      ctx.globalAlpha = k * 0.8;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([6, 5]);
      ctx.lineDashOffset = -(t * 9) % 11;        // 계속 도는 것 — 갈라 놓은 자리를 잇는 점선
      ctx.beginPath();
      ctx.moveTo(TILE.x, HBOT + 5); ctx.lineTo(TILE.x, TILE.cy - TILE.h - 5);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, TILE.x - hh, TILE.cy - hh, hh * 2, hh * 2, 3); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 주민 하나 ────────────────────────────────
       길을 걷는다 → 집이 앉으면 안에서 좌우로만 오간다 → 서는 타일로 내려선다.
       셋 다 t 로 계속 움직이므로 이 샷에는 표식이 멈추는 구간이 없다. */
    const inside = ease(clamp((bk - 0.32) / 0.5));
    const out = ease(clamp((sk - 0.22) / 0.62));
    const roam = 0.5 - 0.5 * Math.cos(frac(t / 5.4) * Math.PI * 2);
    const bump = 0.5 - 0.5 * Math.cos(frac(t / 1.9) * Math.PI * 2);
    const roadX = lerp(30, 148, roam);
    const capX = lerp(HX + 18, HX + HW - 18, bump);
    const vx = lerp(lerp(roadX, capX, inside), TILE.x, out);
    const vy = lerp(PATH_Y + 12, TILE.cy + 10, out);

    // 벽에 닿는 자리 — 갇혔다는 표시. bump 가 양 끝에 붙을 때만 밝아진다.
    if (inside > 0.45 && out < 0.5) {
      const on = (1 - out * 2 > 0 ? 1 - out * 2 : 0) * clamp((inside - 0.45) / 0.3);
      [[HX + 6, Math.max(0, 1 - bump * 7)], [HX + HW - 6, Math.max(0, 1 - (1 - bump) * 7)]]
        .forEach(([bx, near]) => {
          if (near <= 0.02) return;
          ctx.globalAlpha = near * on * 0.9;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          ctx.beginPath();
          ctx.moveTo(bx, PATH_Y - 2); ctx.lineTo(bx, PATH_Y + 16);
          ctx.stroke();
          ctx.globalAlpha = 1;
        });
    }

    ctx.fillStyle = out > 0.5 ? tone('accent') : tone('ink');
    roundRect(ctx, vx - 6, vy - 20, 12, 20, 3); ctx.fill();
    ctx.beginPath(); ctx.arc(vx, vy - 26, 5, 0, Math.PI * 2); ctx.fill();

    /* ── 이름표 ──────────────────────────────────── */
    if (mk > 0.35 && spec.blocks) {
      ctx.globalAlpha = clamp((mk - 0.35) / 0.4);
      ctx.textAlign = 'left';
      ctx.font = mono(700, 10); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.blocks, 16, PATH_Y - 14);
      ctx.globalAlpha = 1;
    }

    if (sk > 0.25) {
      const k = clamp((sk - 0.25) / 0.45);
      ctx.globalAlpha = k;
      if (spec.buildLabel) {
        ctx.textAlign = 'center';
        const fs = fit(spec.buildLabel, 700, 11, 120, 8);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.buildLabel, CX, 34);
      }
      if (spec.standLabel) {
        ctx.textAlign = 'left';
        const fs = fit(spec.standLabel, 900, 14, 118, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.standLabel, 208, TILE.cy + 6);
      }
      ctx.globalAlpha = 1;
    }

    if (sk > 0.5 && spec.standNote) {
      ctx.globalAlpha = clamp((sk - 0.5) / 0.45);
      ctx.textAlign = 'center';
      const fs = fit(spec.standNote, 700, 11.5, w - 28, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.standNote, w / 2, 288);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
