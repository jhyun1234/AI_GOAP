import {
  disp, mono, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* chase — S3. 임계값을 두 번 내리고, 늑대의 경로를 사람 쪽으로 꺾었다. 그래서 물렸다.

   원문: "그래서 임계값을 두 차례에 걸쳐 낮췄습니다. 12에서 10으로, 다시 10에서 8로요." /
   "그래서 사람을 노리는 위협에게만 추격을 넣었습니다… 그러고 나서 다시 관찰했더니,
   드디어 늑대 무리가 도망치는 주민을 따라붙어 부상이 착탄했습니다." /
   "죽은 자리에는 무덤이 하나 남습니다."

   🔴 S2 와 **같은 띠, 같은 쐐기, 같은 도망**인데 경로만 다르다. S2 의 경로는 곧고
   도착점이 비어 있었고, 여기서는 경로가 **꺾여** 도망치는 표식을 따라간다. 회차 안에서
   같은 도형을 되받는 것은 이 한 번뿐이고, 되받은 이유는 **뒤집기 위해서**다.

   ⏱ cue 0(임계값·추격 자막) = 12 → 10 → 8 이 차례로 서고, 이어서 경로가 꺾인다.
      cue 1(무덤 자막) = 표식이 꺼지고(물렸다) 늑대가 물러난 자리에 무덤이 앉는다.
      `seat`(c1 0.72)이 이 회차 두 번째 효과음(latch)이 걸린 자리다.
      `t` 로만 도는 것 = 구경하는 표식들의 바운스 · 띠 점선 · 자취 점선 · 무덤 밑 점선 호.

   🔴 겹침 감사 (두 축)
   · **임계값 화살표에는 후광을 안 준다.** 흰 숫자와 같은 행에 있어 후광을 주면 글리프를
     덮는다. 후광 없이 x 로만 갈랐다 — 「12」 오른끝 117 → 화살표1 128(11px),
     화살표1 오른끝 166 → 「10」 왼끝 173(7px), 「10」 오른끝 199 → 화살표2 210(11px),
     화살표2 오른끝 248 → 「8」 왼끝 261.5(13.5px).
   · 늑대와 자취는 표식이 **꺼진 뒤에야** 그 자리에 접근한다. 늑대가 가장 오른쪽으로
     가는 순간(c1 0.26)의 후광 앞끝은 (254, 200) 이고, 그때 도망 표식은 이미 알파 0 이다.
     무덤 후광(x 246~314 · y 138~213)이 서는 것은 c1 0.42 부터이고, 그때 늑대·자취는
     알파가 0.4 아래로 내려간 채 x 220 왼쪽에 있다(자취 끝 ↔ 무덤 후광 왼끝 26px).
   · 부상 자국(흰 점선 원, x 267~293 · y 181~207)은 c1 0.40 에 완전히 꺼지고 무덤은
     0.42 에 시작한다 — 두 그림이 같은 프레임에 없다.
   · 흰↔흰: 숫자 행 바닥 59 ↔ 구분선 84 = 25 · 구분선 84 ↔ 「추격」 글리프 위 96 = 12 ·
     「추격」 바닥 112 ↔ 위 점선 126 = 14 · 위 점선 126 ↔ 구경 표식 위 145 = 19 ·
     구경 표식 바닥 181 ↔ 늑대 후광 위(y 213) = 32 · 「무덤」 바닥 252 ↔ 아래 점선 276 = 24.
   · 「임계값」(x 16~68)과 「12」(x 91~) 은 같은 행이지만 x 로 23px 떨어져 있다. */

const ROW_Y = 56, SPLIT_Y = 84, CHASE_Y = 108;
const TOP_RULE = 126, BOT_RULE = 276;
const NUM_X = [104, 186, 268];
const ARROW = [[128, 166], [210, 248]];
const BYS = [[62, 160], [106, 168], [150, 158]];
const LANE_Y = 236;
const RUN_FROM = [206, LANE_Y], RUN_TO = [280, 194];
const STONE_H = 42, STONE_W = 18;
const GRAVE_LBL_Y = 248;

function dashRule(ctx, y, x0, x1, t, speed) {
  ctx.save();
  ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
  ctx.setLineDash([7, 7]);
  ctx.lineDashOffset = -(t * speed) % 14;
  ctx.beginPath(); ctx.moveTo(x0, y); ctx.lineTo(x1, y); ctx.stroke();
  ctx.restore();
}

function wolfWedge(ctx, x, y, ang) {
  ctx.save();
  ctx.translate(x, y); ctx.rotate(ang);
  ctx.beginPath();
  ctx.moveTo(13, 0); ctx.lineTo(-9, -9); ctx.lineTo(-3, 0); ctx.lineTo(-9, 9);
  ctx.closePath(); ctx.fill();
  ctx.beginPath();
  ctx.moveTo(-9, -9); ctx.lineTo(-15, -14); ctx.lineTo(-12, -5);
  ctx.closePath(); ctx.fill();
  ctx.beginPath();
  ctx.moveTo(-9, 9); ctx.lineTo(-15, 14); ctx.lineTo(-12, 5);
  ctx.closePath(); ctx.fill();
  ctx.restore();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fitFont = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.fixCue ?? 0, 0.15, 0.80);
    const c1 = cue(spec.graveCue ?? 1, 0.15, 0.75);

    /* ── 임계값 : 12 → 10 → 8 ─────────────────────── */
    if (spec.thrLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.thrLabel, 800, 13, 62));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.thrLabel, 16, ROW_Y);
    }
    const steps = spec.thresholds ?? ['12', '10', '8'];
    const s1 = ease(clamp((c0 - 0.06) / 0.16));
    const s2 = ease(clamp((c0 - 0.24) / 0.16));
    const shown = [1, s1, s2];
    const dim = [1 - 0.55 * s1, 1 - 0.55 * s2, 1];

    ctx.textAlign = 'center';
    ctx.font = mono(900, 22);
    for (let i = 0; i < steps.length; i++) {
      if (shown[i] <= 0.02) continue;
      ctx.globalAlpha = clamp(shown[i] * 1.6) * dim[i];
      ctx.fillStyle = tone('ink');
      ctx.fillText(String(steps[i]), NUM_X[i], ROW_Y);
    }
    ctx.globalAlpha = 1;

    /* 화살표 — 후광 없음(같은 행의 흰 숫자를 덮지 않으려고 일부러 뺐다) */
    ctx.strokeStyle = tone('accent'); ctx.fillStyle = tone('accent'); ctx.lineWidth = 3.5;
    for (let i = 0; i < ARROW.length; i++) {
      const k = i === 0 ? s1 : s2;
      if (k <= 0.02) continue;
      const [ax, bx] = ARROW[i];
      const ex = lerp(ax, bx, ease(k));
      ctx.globalAlpha = clamp(k * 1.6);
      ctx.beginPath(); ctx.moveTo(ax, 47); ctx.lineTo(Math.max(ax + 1, ex - 7), 47); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(ex, 47); ctx.lineTo(ex - 9, 41.5); ctx.lineTo(ex - 9, 52.5);
      ctx.closePath(); ctx.fill();
      ctx.globalAlpha = 1;
    }

    dashRule(ctx, SPLIT_Y, 16, w - 16, t, 9);

    /* ── 추격 ────────────────────────────────────── */
    const chaseLab = ease(clamp((c0 - 0.44) / 0.16));
    if (spec.chaseLabel && chaseLab > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(chaseLab * 1.6);
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.chaseLabel, 800, 13, 90));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.chaseLabel, 16, CHASE_Y);
      ctx.restore();
    }

    dashRule(ctx, TOP_RULE, 16, w - 16, t, 12);
    dashRule(ctx, BOT_RULE, 16, w - 16, t, -10);

    /* ── 구경하는 사람들 (계속 흔들린다) ─────────── */
    ctx.fillStyle = tone('ink');
    for (let i = 0; i < BYS.length; i++) {
      const [bx, by] = BYS[i];
      ctx.beginPath();
      ctx.arc(bx, by + Math.sin(t * 4.4 + i * 1.6) * 4, 9, 0, Math.PI * 2);
      ctx.fill();
    }

    /* ── 도망치는 사람과 그를 따라 꺾이는 경로 ───── */
    const run = ease(clamp((c0 - 0.46) / 0.54));
    const bite = ease(clamp(c1 / 0.26));
    const away = ease(clamp((c1 - 0.26) / 0.26));

    const mx = lerp(RUN_FROM[0], RUN_TO[0], run);
    const my = lerp(RUN_FROM[1], RUN_TO[1], run);

    let wx, wy;
    if (run < 0.42) { wx = lerp(40, 150, run / 0.42); wy = LANE_Y; }
    else {
      const u = ease((run - 0.42) / 0.58);
      wx = lerp(150, 196, u); wy = lerp(LANE_Y, 222, u);
    }
    wx = lerp(wx, 232, bite); wy = lerp(wy, 208, bite);
    wx -= 54 * away; wy += 16 * away;
    const walpha = (1 - away) * clamp(run * 6);

    /* 경로 자취 — 꺾이는 것이 이 샷의 사건이라 자취를 남긴다 (후광 없음) */
    if (walpha > 0.02) {
      ctx.save();
      ctx.globalAlpha = walpha * 0.75;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([6, 8]);
      ctx.lineDashOffset = -(t * 22) % 14;
      ctx.beginPath();
      ctx.moveTo(40, LANE_Y);
      ctx.lineTo(Math.min(150, wx), LANE_Y);
      if (wx > 150) ctx.lineTo(wx, wy);
      ctx.stroke();
      ctx.restore();
    }

    /* 물린 사람 — 흔들리다 꺼지고, 잠깐 자국만 남는다 */
    if (bite < 0.99) {
      ctx.save();
      ctx.globalAlpha = 1 - bite;
      ctx.fillStyle = tone('ink');
      const jitter = bite > 0.25 ? Math.sin(t * 34) * 3 * bite : 0;
      ctx.beginPath(); ctx.arc(mx + jitter, my, 9, 0, Math.PI * 2); ctx.fill();
      ctx.restore();
    }
    const scarK = clamp((c1 - 0.10) / 0.16) * (1 - clamp((c1 - 0.28) / 0.12));
    if (scarK > 0.02) {
      ctx.save();
      ctx.globalAlpha = scarK * 0.9;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([5, 6]);
      ctx.lineDashOffset = -(t * 18) % 11;
      ctx.beginPath(); ctx.arc(RUN_TO[0], RUN_TO[1], 13, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();
    }

    /* 늑대 */
    if (walpha > 0.02) {
      ctx.save();
      ctx.globalAlpha = walpha;
      setShadow(ctx, GLOW, 14);
      ctx.fillStyle = tone('accent');
      wolfWedge(ctx, wx, wy, run < 0.42 ? 0 : -0.42);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 무덤 : 그 자리에 앉고, 안 지워진다 ───────── */
    const gr = ease(clamp((c1 - 0.42) / 0.28));
    const seat = ease(clamp((c1 - 0.72) / 0.16));
    if (gr > 0.01) {
      const h = STONE_H * gr;
      ctx.save();
      ctx.globalAlpha = clamp(gr * 1.8);
      setShadow(ctx, GLOW, 14);
      ctx.fillStyle = tone('accent');
      roundRect(ctx, RUN_TO[0] - STONE_W / 2, RUN_TO[1] - h, STONE_W, h, STONE_W / 2);
      ctx.fill();
      roundRect(ctx, RUN_TO[0] - 20 * gr, RUN_TO[1] - 2, 40 * gr, 7, 3);
      ctx.fill();
      clearShadow(ctx);
      if (gr > 0.62) {
        ctx.globalAlpha = clamp((gr - 0.62) / 0.30);
        roundRect(ctx, RUN_TO[0] - 16, RUN_TO[1] - h + 9, 32, 7, 3);
        ctx.fill();
      }
      ctx.restore();
    }
    if (seat > 0.02) {
      ctx.save();
      ctx.globalAlpha = seat * 0.85;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([6, 8]);
      ctx.lineDashOffset = -(t * 12) % 14;
      ctx.beginPath();
      ctx.ellipse(RUN_TO[0], 210, 32 * seat, 8 * seat, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }

    const lab = clamp((c1 - 0.74) / 0.20);
    if (spec.graveLabel && lab > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(lab * 1.5);
      const fs = fitFont(spec.graveLabel, 900, 19, w - 40);
      ctx.font = disp(900, fs);
      const tw = ctx.measureText(spec.graveLabel).width;
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.graveLabel, clamp(RUN_TO[0], 8 + tw / 2, w - 8 - tw / 2), GRAVE_LBL_Y);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
