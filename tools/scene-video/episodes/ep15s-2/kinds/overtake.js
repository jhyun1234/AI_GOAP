/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* overtake — 뒤집힘과 착지. 도망을 105 로 올렸더니, 고집쟁이만 95 로 내려갔다.

   원문 근거:
   > "1차 수정은 간단했습니다. 피신 우선순위를 92에서 105로 올려 배고픔(100) 위에
   >  놓았습니다. 그러자 뜻밖에도 겁 축이 살아났습니다."
   > "그런데 겁이 가장 낮은 고집쟁이(겁 -60)는 95로 내려가 배고픔 아래에 놓입니다 —
   >  배가 고프면 늑대를 무시하고 밥부터 찾다가 물립니다."
   🔴 수치 슬롯 : **100 = 배고픔**(불변) · **92 = 도망(수정 전)** · **105 = 도망(수정 후 ·
     겁 0 기준)** · **95 = 도망(수정 후 · 고집쟁이)**. 110(새침이)은 이 편에 안 나온다.

   자막 0(도망을 105로 올렸고요) : 오른쪽 강조색 칸이 92 자리에서 105 자리까지 올라가
     왼쪽 흰 칸(100)보다 높아진다. **「105」는 다 올라가 앉은 뒤에 켜진다**(나는 도중에
     숫자를 갈아 끼우면 어느 쪽도 읽을 시간이 없다). 그러고 아래 장면에서 주민 하나가
     오른쪽으로 달아난다.
   자막 1(겁 없는 고집쟁이만 95, 밥부터 찾다 물려요) : 올라간 칸에서 **작은 칸 하나가
     갈라져 나와 도로 내려가** 기준선 아래에 앉는다. 그 아래에서는 남아 있던 주민에게
     쐐기가 닿고 `>_<` 가 뜬다.

   🖼 라벨을 다 가려도 읽힌다 — 「아래에 있던 것이 올라가 기준선을 넘는다. 그런데 그중
     조각 하나가 갈라져 도로 기준선 아래로 내려앉고, 그 아래에서 뾰족한 것이 남은
     동그라미에 닿는다.」

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표로 갈랐다**(twotracks 와 같은 규약).
     강조색은 오른쪽 기둥에만 있다 : 큰 칸 x 246.5~317.5 · 글로우(blur 5) 241.5~322.5,
     작은 칸 x 256.5~307.5 · 글로우 251.5~312.5, 「도망」 글립 x 257~307,
     「고집쟁이」 글립 x 250~314.
     · 오른쪽 기둥의 흰 난간 x 228.5~231.5 · 332.5~335.5 → 양쪽 **10px** 뜬다.
     · 🔴 흰 기준선은 **x 222 에서 끊는다.** 큰 칸이 92→105 로 **기준선 높이를 통과**하는데,
       끝에서 끝까지 그었다면 그 순간 흰 선이 강조색 칸을 관통한다. 19.5px 뜬다.
     · 흰 「배고픔」 칸·글립·난간은 전부 x 199.5 이하.
     · 아래 장면(쐐기·주민 둘·바닥선·`>_<`·「늑대」)은 x 206 이하 & y 229 이상 →
       가로로도 세로로도 안 만난다. 강조색의 가장 낮은 곳은 「고집쟁이」 글립 바닥 219.

   세로 예산(캔버스 307) : 최저 = 바닥선 획 바깥 281.5. 25.5px 남는다.
     최고 = 「배고픔」·「도망」 글립 top 73. 63px 남는다.
   가로(캔버스 352) : 오른쪽 최대 = 난간 335.5(16.5px 남음) · 왼쪽 최소 = 바닥선 16.
     달아나는 주민의 오른쪽 끝은 **가장 멀리 간 순간**으로 재서 207.5.
   흰 것끼리의 간격 : 남는 주민(중심 134)과 달아나는 주민(출발 166)은 **획 바깥 반지름이
     각 11.5**(r 10 + lineWidth/2) 라 중심 간격 32 − 23 = **9px 뜬다.** 🔑 흔들림(±3)이 이
     간격을 더 좁히지는 않는다 — 흔들림은 `bite`(since(1) ≥ 0.95 = since(0) ≥ 3.36)에서만
     켜지는데 달아나기는 since(0) 1.20~1.90 에 이미 끝나 그때 상대는 196 에 가 있다
     (중심 간격 62 · 흔들림이 가장 가까워지는 순간에도 59 → **획 바깥 36px**).

   ⏱ 계속 도는 것 = 기준선 점이 초당 46px 로 행진한다(30fps 한 프레임에 1.53px) +
     쐐기의 상시 흔들림(초당 24.8px). 사건은 전부 `since()` 위에 세워 **TTS 길이가
     바뀌어도 안 움직인다** — `sfx.delayMs` 를 실측 뒤 다시 풀 일이 없다.
     위상은 `frac()` 로 양수 정규화했고 `setLineDash` 는 한 군데도 안 썼다. */

const RAIL_Y0 = 100, RAIL_Y1 = 222;
const GUIDE_Y = 150, GUIDE_X0 = 118, GUIDE_X1 = 222;
const AX = [118, 210], BX = [230, 334];
const A_BLK = { x: 130, w: 68 }, B_BLK = { x: 248, w: 68 };
const A_CX = A_BLK.x + A_BLK.w / 2;      // 164
const B_CX = B_BLK.x + B_BLK.w / 2;      // 282
const BH = 24, CHIP_W = 50, CHIP_H = 22;
const lvl = v => GUIDE_Y - (v - 100) * 7; // 100→150 · 92→206 · 105→115 · 95→185

const GND_Y = 280, WOLF_Y = 262;
const STAY_X = 134;                       // 남는 주민(고집쟁이)
/* 🔴 `RUN_X0` 는 166 이다(1차는 160). 두 동그라미는 `arc(..., 10)` + `lineWidth 3` 이고
   글로우가 없으므로 **획 바깥 반지름이 각 11.5** 다(= 위 가로 예산의 「오른쪽 끝 207.5」가
   196 + 11.5 로 이미 쓰고 있는 값이다 — 같은 원에 두 값을 쓰면 안 된다).
   160 이면 중심 간격 26 − 23 = **3px** 밖에 안 떠서, 달아나기 시작하는 `since(0)=1.20`
   까지 1.2초 동안 「OO」 한 덩어리로 읽혔다(검수 스틸 `22.9s.png`). 166 이면 중심 간격 32
   − 23 = **9px 뜬다.** 오른쪽 끝은 `RUN_X1` 이 정하므로 가장자리 예산은 안 바뀐다.
   ⚠️ **2차 주석이 이 반지름을 13.5 로 적어(「1px 겹침」→「5px 뜸」) 같은 원에 두 값을
   쓰고 있었다** — 13.5 는 `frozen.js` 의 주민(r 12)에서 온 수다. 고친 방향과 결과는
   같고 **실제 여유가 적힌 것보다 크므로** 값은 안 건드리고 주석만 정정했다(4차). */
const RUN_X0 = 166, RUN_X1 = 196;         // 달아나는 주민

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const rail = (x, alpha) => {
      ctx.save();
      ctx.globalAlpha = alpha * 0.5;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
      ctx.beginPath(); ctx.moveTo(x, RAIL_Y0); ctx.lineTo(x, RAIL_Y1); ctx.stroke();
      ctx.restore();
    };

    const s0 = since(spec.raiseCue ?? 0);
    const s1 = since(spec.splitCue ?? 1);
    const st = ease(clamp(cue(spec.raiseCue ?? 0, 0.15, 0.7) / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    for (const x of AX) rail(x, st);
    for (const x of BX) rail(x, st);

    /* 기준선(흰) — 행진하는 점. x 222 에서 끊는다(위 색 분리 ⓑ). */
    ctx.save();
    ctx.globalAlpha = st * 0.45;
    ctx.fillStyle = tone('ink');
    const off = ((t * 46) % 14 + 14) % 14;
    for (let x = GUIDE_X0 - 14; x < GUIDE_X1; x += 14) {
      const x0 = Math.max(GUIDE_X0, x + off);
      const x1 = Math.min(GUIDE_X1, x + off + 8);
      if (x1 > x0) ctx.fillRect(x0, GUIDE_Y - 1.5, x1 - x0, 3);
    }
    ctx.restore();

    /* ── 배고픔 칸(흰) : 처음부터 끝까지 안 움직인다 ── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, A_BLK.x, lvl(100) - BH / 2, A_BLK.w, BH, 4); ctx.stroke();
    ctx.restore();
    ctext(spec.hungerValue ?? '100', A_CX, lvl(100) + 6, 900, 15, tone('ink'), 56, st);
    if (spec.hungerLabel) ctext(spec.hungerLabel, A_CX, 84, 800, 13, tone('ink'), 84, st);

    /* ── 도망 칸(강조색) : 92 자리에서 105 자리로 올라간다 ── */
    const rise = s0 >= 0 ? ease(clamp((s0 - 0.30) / 0.90)) : 0;
    const bigY = lerp(lvl(92), lvl(105), rise);
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 5);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, B_BLK.x, bigY - BH / 2, B_BLK.w, BH, 4); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();
    /* 🔴 숫자는 **올라가 앉은 뒤에** 켜지고 끝까지 안 꺼진다.
       초고는 나는 도중에 92 → 105 로 갈아 끼웠는데, 그러면 「92」가 이 샷에서
       0.5초도 안 떠 있게 된다(ADR-V25-15 ②의 반려 형태). 「92」는 앞 샷이 6.3초 동안
       이미 보여줬으므로 여기서 되풀이하지 않고, 105 하나만 착지 시점에 세운다. */
    ctext(spec.fleeAfter ?? '105', B_CX, bigY + 6, 900, 15, tone('accent'), 56,
      st * ease(clamp((rise - 0.75) / 0.25)));
    if (spec.fleeLabel) ctext(spec.fleeLabel, B_CX, 84, 800, 13, tone('accent'), 84, st);

    /* ── 갈라져 나온 작은 칸(강조색) : 기준선 아래로 내려앉는다 ── */
    const chipIn = s1 >= 0 ? ease(clamp((s1 - 0.30) / 0.25)) : 0;
    if (chipIn > 0.02) {
      const chipK = ease(clamp((s1 - 0.45) / 0.55));
      const chipY = lerp(lvl(105), lvl(95), chipK);
      ctx.save();
      ctx.globalAlpha = st * chipIn;
      setShadow(ctx, GLOW, 5);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, B_CX - CHIP_W / 2, chipY - CHIP_H / 2, CHIP_W, CHIP_H, 4); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctext(spec.stubbornValue ?? '95', B_CX, chipY + 5, 900, 14, tone('accent'), 42, st * chipIn);
      /* 🔑 이름은 칸 **아래**에 둔다 — 위에 두면 내려오는 칸이 글자를 타고 넘는다.
         한 번 켜지면 안 꺼진다(ADR-V25-15 ②). */
      if (spec.stubbornLabel) {
        ctext(spec.stubbornLabel, B_CX, 216, 900, 12, tone('accent'), 74,
          st * ease(clamp((s1 - 0.35) / 0.30)));
      }
    }

    /* ── 아래 장면 : 하나는 달아나고 하나는 밥부터 찾는다 ── */
    ctx.save();
    ctx.globalAlpha = st * 0.5;
    ctx.fillStyle = tone('ink');
    for (let x = 16; x < 206; x += 14) ctx.fillRect(x, GND_Y - 1.5, 8, 3);
    ctx.restore();

    const near = s1 >= 0 ? ease(clamp((s1 - 0.40) / 0.60)) : 0;
    const sway = 4 * Math.sin(t * 6.2) * (1 - near);
    const tip = lerp(54, 112, near) + sway;
    const bite = s1 >= 0 ? ease(clamp((s1 - 0.95) / 0.25)) : 0;
    const jit = bite * 3 * Math.sin(s1 >= 0 ? s1 * 32 : 0);
    const run = s0 >= 0 ? ease(clamp((s0 - 1.20) / 0.70)) : 0;

    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    ctx.beginPath();
    ctx.moveTo(tip, WOLF_Y);
    ctx.lineTo(tip - 30, WOLF_Y - 16);
    ctx.lineTo(tip - 30, WOLF_Y + 16);
    ctx.closePath(); ctx.stroke();

    ctx.beginPath();
    ctx.arc(STAY_X + jit, WOLF_Y, 10, 0, Math.PI * 2); ctx.stroke();
    ctx.beginPath();
    ctx.arc(lerp(RUN_X0, RUN_X1, run), WOLF_Y, 10, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    if (spec.hurtFace) ctext(spec.hurtFace, STAY_X + jit, 242, 800, 13, tone('ink'), 56, st * bite);
    /* 🔴 1차는 `st · 0.85 · (1 − near)` 라 `since(1) = 1.00` 에 알파가 **0** 이 됐다.
       가림 회피가 아니었다 — 이 글립(y 229~243)과 쐐기(y 246~278)는 끝까지 안 겹친다.
       ADR-V25-15 ② 는 「올린 글자가 읽을 시간이 있는가」를 묻는데, **꺼져야 할 이유가
       없는 라벨을 끄고 있었다.** `(1 − near)` 를 지웠다 — 이제 샷 끝까지 남는다. */
    if (spec.wolfLabel) ctext(spec.wolfLabel, 60, 240, 800, 11, tone('ink'), 60, st * 0.85);

    ctx.textAlign = 'left';
  }
};
