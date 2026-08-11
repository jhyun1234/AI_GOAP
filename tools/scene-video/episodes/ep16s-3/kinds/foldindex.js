import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* foldindex — 덮고 있던 아홉이 한 칸으로 접히고, 명부는 이름만 남는다.
   그리고 이름을 누르면 그 사람의 기록이 아래에 펼쳐진다. 마지막에 그중 하나가 도드라진다.

   원문 근거:
   > "그래서 네 가지를 바꿨습니다. 1) 명부는 목차로 되돌리고 연대기 줄을 뺐습니다.
   >  2) 명부의 줄을 클릭하면 아래쪽에 그 사람의 연대기가 펼쳐집니다.
   >  3) 같은 사건은 '×9' 식으로 압축합니다."
   > "추억은 다 쌓아 놓을 때가 아니라 골라 놓을 때 생깁니다."
   화면 글자 「×9」는 원문 문자열 그대로다.

   🔴 색 규약(훅에서 세운 것 그대로): **흰색 = 사람·그릇(이름 칸·펼침 상자·손 커서) /
   강조색 = 기록.** S2 와 같은 좌표(이름 칸 x 24~96, 줄 y 92·156·220)에서 시작해
   **거꾸로 감는다** — S2 가 아홉으로 불어나는 그림이면 여기는 하나로 접히는 그림이다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세 자리 다 좌표로 갈랐다.**
     ① 접히는 기록 칸은 x 110 부터(글로우 blur 6 까지 104), 이름 칸은 x 24~96 → **8px**.
     ② 펼침 상자 테두리는 x 106·330 / y 182·266 이고, 안의 기록 줄은 x 126~280
        (글로우 120~286) · y 200~208, 222~230, 244~252(글로우 194~258)다 →
        위 12px · 아래 8px · 왼 14px · 오른 44px 뜬다.
     ③ 「×9」(강조색, x 292~311)는 상자 오른쪽 테두리(330)에서 19px 뜬다.
     ④ 손 커서(흰색)는 x 104~117 · y 150~171 에 선다. 상자 꼭대기 182 에서 11px,
        가운데 이름 칸(x 24~96)에서 8px 뜬다. 커서가 서 있는 동안 그 자리에 강조색은 없다
        (접힌 칸은 s0 1.35 에 이미 사라졌고, 상자는 2.00 부터 열린다).

   ⏱ 전부 `since(0)` 위에 세웠다 — 첫 줄이라 `since(0) === t` 이고, 소리(sweep)의
     `delayMs` 와 원점이 같아 자막 길이가 바뀌어도 다시 안 푼다.
     0.30~0.95 접힘 / 0.95~1.35 명부는 이름만 / 1.40~1.85 커서 이동 / 1.85~2.05 눌림 /
     2.00~2.40 펼침 / 2.10~2.56 기록 줄 셋 / 2.45~2.65 「×9」.
   🔴 **착지(고르기)는 마지막 자막의 앞쪽에서 끝난다** — `cue(1, 0.15, 0.28)` 이라
     그 줄이 시작하고 **0.87초** 안에 완성된다(줄 길이의 28%). 아웃트로 카드가 덮는
     구간과는 무관한 위치다(그건 마지막 샷이다).

   세로 예산(캔버스 307): 맨 위 이름 칸 82, 펼침 상자 바닥 266. 아래로 41px 남는다.
   가로: 24~330. 좌 24 · 우 22 남는다.

   계속 도는 것(30fps 기준): 접히는 구간에는 칸 여덟이 각각 최대 **11.8px/프레임**으로
   왼쪽으로 미끄러진다. 🔴 **착지가 끝난 뒤(마지막 자막의 뒤쪽 약 2.4초)에 남는 움직임은
   기록 줄 셋의 밝기뿐**이라 거기서 정적 판정선을 손으로 계산했다 —
   면적 약 2,320px² × 프레임당 알파 변화 0.0557(진폭 0.38 · 4.4rad/s) × 255 ÷ 캔버스
   108,064px² = **평균 0.31**(문턱 0.0008 을 255 스케일로 옮기면 0.204)로 약 1.5배 여유다.
   이름 칸 셋의 숨(0.04)과 골라진 줄의 두께 변화가 그 위에 더 얹힌다. */

const ROW_Y = [92, 156, 220];
const CHIP_X = 24, CHIP_W = 72, CHIP_H = 20;
const REC_X0 = 110, REC_P = 23, REC_S = 14, PILE_H = 76;
const PN_X = 106, PN_Y = 182, PN_W = 224, PN_H = 84;
const BAR_X = 126, BAR_W = 154, BAR_Y = [200, 222, 244], BAR_H = 8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const s0 = Math.max(0, since(spec.foldCue ?? 0));
    const en = ease(clamp(s0 / 0.25));
    const n = spec.foldN ?? 9;

    const fold = ease(clamp((s0 - 0.30) / 0.65));
    const leave = ease(clamp((s0 - 1.00) / 0.35));
    const chip = ease(clamp((s0 - 0.95) / 0.40));
    const ptr = ease(clamp((s0 - 1.40) / 0.45));
    const flash = ease(clamp((s0 - 1.85) / 0.10)) * (1 - ease(clamp((s0 - 1.95) / 0.10)));
    const ptrOut = ease(clamp((s0 - 2.00) / 0.25));
    const panel = ease(clamp((s0 - 2.00) / 0.40));
    const x9 = ease(clamp((s0 - 2.45) / 0.20));
    const pick = ease(cue(spec.pickCue ?? 1, 0.15, 0.28));

    ctx.textAlign = 'left';
    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = en;
      ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, CHIP_X, 34);
      ctx.restore();
    }

    /* ── 덮고 있던 아홉이 한 칸으로 접힌다 ── */
    const stackA = en * (1 - leave);
    if (stackA > 0.02) {
      for (let k = 0; k < n; k++) {
        const ka = k === 0 ? 1 : 1 - ease(clamp((fold - 0.55) / 0.35));
        if (ka <= 0.02) continue;
        const x = lerp(REC_X0 + k * REC_P, REC_X0, fold);
        const h = lerp(PILE_H, REC_S, fold);
        ctx.save();
        ctx.globalAlpha = stackA * ka * (0.74 + 0.26 * (0.5 + 0.5 * Math.sin(t * 3.6 - k * 0.55)));
        setShadow(ctx, GLOW, 6);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, x, 156 - h / 2 + leave * 10, REC_S, h, 3); ctx.fill();
        clearShadow(ctx);
        ctx.restore();
      }
    }

    /* ── 명부는 이름만 남는다 (목차) ── */
    for (let r = 0; r < 3; r++) {
      const cy = ROW_Y[r];
      const hit = r === 1 ? flash : 0;
      ctx.save();
      ctx.globalAlpha = chip * (0.88 + 0.12 * (0.5 + 0.5 * Math.sin(t * 2.4 - r * 1.1))) ;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3 + 2 * hit; ctx.lineJoin = 'round';
      roundRect(ctx, CHIP_X, cy - CHIP_H / 2, CHIP_W, CHIP_H, 6); ctx.stroke();
      ctx.restore();
    }

    /* ── 손 커서가 가운데 이름을 누른다 ── */
    const pa = ptr * (1 - ptrOut) * en;
    if (pa > 0.02) {
      const tx = lerp(250, 104, ptr), ty = lerp(268, 150, ptr);
      ctx.save();
      ctx.globalAlpha = pa;
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.moveTo(tx, ty);
      ctx.lineTo(tx + 13, ty + 9);
      ctx.lineTo(tx + 7.5, ty + 10.5);
      ctx.lineTo(tx + 11, ty + 19);
      ctx.lineTo(tx + 7, ty + 21);
      ctx.lineTo(tx + 3.5, ty + 12.5);
      ctx.lineTo(tx, ty + 16);
      ctx.closePath(); ctx.fill();
      ctx.restore();
    }

    /* ── 누르면 그 사람의 기록이 펼쳐진다 ── */
    if (panel > 0.02) {
      const h = PN_H * panel;
      ctx.save();
      ctx.globalAlpha = panel;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, PN_X, PN_Y, PN_W, Math.max(6, h), 8); ctx.stroke();
      ctx.restore();

      for (let i = 0; i < 3; i++) {
        const bk = ease(clamp((s0 - (2.10 + i * 0.14)) / 0.18));
        if (bk <= 0.02) continue;
        const chosen = i === 1;
        const w = BAR_W * bk * (chosen ? 1 : lerp(1, 0.44, pick));
        const bh = BAR_H + (chosen ? 8 * pick : 0);
        /* 🔴 이 진폭·박자는 정적 판정선에서 나왔다. 착지가 끝난 뒤 이 샷에 남는 움직임이
           이 셋뿐이라, 3.2rad/s 로는 프레임당 평균 변화가 문턱(0.0008)에 아슬아슬했다.
           4.4rad/s 로 올려 여유를 1.5배로 뒀다(계산은 writer.md §1). */
        const a = (chosen ? 1 : lerp(1, 0.42, pick))
                * (0.62 + 0.38 * (0.5 + 0.5 * Math.sin(t * 4.4 - i * 1.3)));
        ctx.save();
        ctx.globalAlpha = bk * a;
        setShadow(ctx, GLOW, 6);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, BAR_X, BAR_Y[i] + (BAR_H - bh) / 2, w, bh, 4); ctx.fill();
        clearShadow(ctx);
        ctx.restore();
      }

      if (spec.foldLabel && x9 > 0.02) {
        ctx.save();
        ctx.globalAlpha = x9;
        ctx.font = disp(800, 14); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.foldLabel, 292, BAR_Y[1] + 8);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
