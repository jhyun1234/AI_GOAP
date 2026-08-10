import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* ownempty — 남는 한 줄. 갈라진 그릇 둘이 서로 반대로 간다.

   원문 근거(문장 그대로):
   > "공용 창고가 있는 마을에서는 게으름이 아무 대가를 치르지 않습니다."
   > "예전 게으름뱅이는 공용 창고 무임승차자였지만, 이제 게으르면 자기가 굶습니다."

   🔴 기본 도형의 **마지막 형태**다. SH 하나 → S1 그 하나에 이름이 없다 → S2 갈라진다 →
   여기서 갈라진 둘이 **서로 반대로** 간다.
   🔴 S2 의 좌표를 그대로 물려받았다(곳간 x 26~108 · 244~326 · y 118~190, 사람 y 234).
   시청자가 「아까 그 그릇」으로 읽어야 뒤집힘이 성립한다.

   🔴 **cue(0) 에서 훅(commonpot)의 움직임을 일부러 다시 돌린다** — 오른쪽 사람이 옛 그릇
   앞으로 걸어와 알갱이 하나를 받아 돌아간다. 돌려막기가 아니라 **되받기**다: 훅에서는
   그게 「지금」이었는데 여기서는 옛 그릇이 흐리게(획 알파 0.62) 서 있는 「예전」이고,
   cue(1) 이 시작되면 그 그릇과 함께 그 움직임 자체가 사라진다. 같은 동작을 보여 준 뒤
   지워야 「이제 안 된다」가 몸으로 읽힌다.

   🔴 「게으름뱅이」라는 성격 이름은 화면에도 자막에도 없다 — 그 성격은 원문에서 10단계에
   **새로 추가된 것**이라, 0초에 나온 「아무 일도 안 한 주민」과 같은 사람으로 세우면
   사실이 어긋난다. 라벨은 S1 과 같은 「농사꾼」·「아무 일도 안 한 주민」이다.

   🔴 흰색과 강조색이 같은 픽셀을 안 나눈다 (알갱이 한 변 7, 반폭 3.5):
     ⓐ 채움 셋(곳간 둘 · 옛 그릇 하나)은 전부 clip 안. 좌우 8~10px · 바닥 6px 들였다.
     ⓑ 왼쪽 곳간으로 계속 떨어지는 알갱이는 x 44 — 왼벽 획 바깥끝 28 에서 **12.5px.**
     ⓒ 옛 그릇에서 나오는 알갱이의 경로는 셋 다 흰 획을 피해 계산했다:
        올라감 x 176(옛 그릇의 **입** 한복판, 벽 안쪽 150~202 라 26px) →
        가로지름 y 76(옛 그릇 위끝 104 에서 **28px**, 곳간 위끝 116 에서 **36.5px**) →
        내려감 x 232(곳간 B 왼벽 획 바깥끝 242 에서 **6.5px**, 끝점 y 196 은 사람 캡슐
        위끝 205.5 에서 **6px**).
     ⓓ 🔴 `setShadow`/`GLOW` 를 안 썼다 — 번짐이 곧 혼합이다.

   ⚠️ 좌표 검산은 **움직이는 것의 가장 밖으로 나가는 순간**으로 했다. 오른쪽 사람이 옛
   그릇 앞(x 232)까지 걸어왔을 때 캡슐은 x 212~252 이고, 옛 그릇 오른벽 획 바깥끝 206 에서
   6px · 곳간 B 왼벽 획 바깥끝 242 에서는 y 가 안 겹친다(캡슐 205.5~262.5 / 곳간 ~192).

   세로 예산(캔버스 307): 최하단 = 오른쪽 이름 라벨 글립 바닥 약 288. 18px 남는다.
   가로: 오른쪽 이름 라벨이 가장 넓고 `measureText` + `clamp` 로 안쪽에 가둔다
   (10px 폰트에서 오른끝 약 335).

   ⏱ cue(0) = 「예전엔 게을러도 창고에서 꺼내 먹으면 됐죠」 — 옛 그릇이 서고, 오른쪽 사람의
      왕복이 `since(0)` 위의 1.6초 루프로 돈다.
      cue(1) = 「근데 이제 게으르면 자기가 굶어요」 — **사건을 전부 `since(1)` 위에 세웠다**:
      옛 그릇과 왕복이 사라지고(0~0.35) 왼쪽이 차오르고(0.55~1.45) **오른쪽이 1.00 에서
      내려가기 시작해 1.70 에 바닥**에 닿는다. 그 직후 1.78~1.95 에 표정이 바뀐다.
      🔴 `drop` 의 `delayMs 1000` 이 **같은 원점**을 쓰므로 TTS 실측이 필요 없다.
      🔴 페이오프 여유: `since(1)` 은 자막(예상 rel 2.77초) + 샷 꼬리 0.35 = 3.12초까지
      도는데 완성이 1.95초다 — **1.17초 남는다.** 이 샷은 마지막 샷이 아니므로 아웃트로
      카드가 덮을 위험도 구조적으로 없다.

   계속 도는 것 = 왼쪽 곳간 채움의 빗금 스크롤(62 × 최대 46 = 2,852px²) + 그리로 계속
   떨어지는 알갱이(1.3초 주기) + 채움 높이의 잔물결. 오른쪽이 텅 빈 뒤에도 왼쪽이 돈다. */

const A_CX = 67, B_CX = 285;
const ST_HW = 41, ST_TOP = 118, ST_BOT = 190;
const ST_INSET = 10, ST_FILL_BOT = 182, ST_FILL_H = 46;

const PERSON_Y = 234, P_W = 40, P_H = 54;
const B_HOME = 285, B_VISIT = 232;

const GH_L = 148, GH_R = 204, GH_TOP = 106, GH_BOT = 158, GH_CX = 176;
const GH_FILL_L = 158, GH_FILL_R = 194, GH_FILL_BOT = 150, GH_FILL_H = 26;
const LANE_Y = 76;

const DROP_X = 44;
const GR = 7;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (!txt || alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    const bowl = (lx, rx, top, bot, alpha, lw = 4) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = lw;
      ctx.lineCap = 'round'; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(lx, top); ctx.lineTo(lx, bot); ctx.lineTo(rx, bot); ctx.lineTo(rx, top);
      ctx.stroke();
      ctx.restore();
    };

    const fill = (x0, y0, x1, y1, alpha, speed) => {
      if (x1 - x0 < 2 || y1 - y0 < 2 || alpha <= 0.01) return;
      ctx.save();
      ctx.beginPath(); ctx.rect(x0, y0, x1 - x0, y1 - y0); ctx.clip();
      ctx.globalAlpha = alpha * 0.42;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
      ctx.globalAlpha = alpha * 0.95;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      const h = y1 - y0, S = 18, off = ((t * speed) % S + S) % S;
      for (let x = x0 - h - S + off; x < x1 + S; x += S) {
        ctx.beginPath(); ctx.moveTo(x, y1); ctx.lineTo(x + h, y0); ctx.stroke();
      }
      ctx.restore();
    };

    const person = (cx, alpha) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - P_W / 2, PERSON_Y - P_H / 2, P_W, P_H, P_W / 2);
      ctx.stroke();
      ctx.restore();
    };

    const grain = (x, y, alpha) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(x - GR / 2, y - GR / 2, GR, GR);
      ctx.restore();
    };

    const c0 = cue(spec.pastCue ?? 0, 0.15, 0.80);
    const st = ease(clamp(c0 / 0.14));
    if (st <= 0.02) { ctx.textAlign = 'left'; return; }

    const s1 = since(spec.splitCue ?? 1);
    const gone = s1 > 0 ? ease(clamp(s1 / 0.35)) : 0;
    const riseK = s1 > 0 ? ease(clamp((s1 - 0.55) / 0.90)) : 0;
    const fallK = s1 > 0 ? ease(clamp((s1 - 1.00) / 0.70)) : 0;

    /* ── 예전 : 옛 그릇 앞으로 걸어가 하나 받아 온다 ──
       훅(commonpot)의 움직임을 그대로 다시 돌린 뒤 지운다. */
    const ghost = st * (1 - gone);
    const wu = frac(Math.max(0, since(spec.pastCue ?? 0)) / 1.6);
    let bx = B_HOME;
    if (wu < 0.28) bx = lerp(B_HOME, B_VISIT, ease(wu / 0.28));
    else if (wu < 0.50) bx = B_VISIT;
    else if (wu < 0.78) bx = lerp(B_VISIT, B_HOME, ease((wu - 0.50) / 0.28));
    bx = lerp(bx, B_HOME, gone);            // 옛 그릇이 사라지면 제자리로 돌아온다

    /* ── 사람 둘 ───────────────────────────────────── */
    person(A_CX, st);
    person(bx, st);
    ctext(spec.busyFace, A_CX, PERSON_Y + 5, 800, 13.5, tone('ink'), P_W - 8, st);
    /* 오른쪽 표정은 겹치지 않게 갈아 끼운다 — 먼저 지우고(1.62~1.76) 나중에 켠다(1.78~1.95) */
    const fOut = s1 > 0 ? ease(clamp((s1 - 1.62) / 0.14)) : 0;
    const fIn = s1 > 0 ? ease(clamp((s1 - 1.78) / 0.17)) : 0;
    ctext(spec.idleFace, bx, PERSON_Y + 5, 800, 13.5, tone('ink'), P_W - 8, st * (1 - fOut));
    ctext(spec.hungryFace, bx, PERSON_Y + 5, 800, 13.5, tone('ink'), P_W - 8, st * fIn);

    /* 오른쪽 이름표는 그 사람을 따라간다 — 걸어간 사람이 누군지 안 끊긴다.
       가장 왼쪽(bx 232)에서도 글립 182~282 라 「농사꾼」(47~87)과 95px 뜬다. */
    ctext(spec.busyName, A_CX, 284, 800, 11, tone('sub'), 108, st);
    ctext(spec.idleName, bx, 284, 800, 10, tone('sub'), 116, st);

    /* ── 옛 그릇 ───────────────────────────────────
       🔴 흰 획을 0.62 로 낮춰 그린다 — 「지금 있는 것」인 곳간 둘과 위계를 가르려는
       것이고, 그 자체가 「예전」이라는 뜻을 진다. 흰색 계열이라 팔레트는 안전하고
       0.62 는 알파 158 이라 팔레트 검사 문턱(64)보다 한참 위다. */
    if (ghost > 0.02) {
      bowl(GH_L, GH_R, GH_TOP, GH_BOT, ghost * 0.62);
      fill(GH_FILL_L, GH_FILL_BOT - GH_FILL_H, GH_FILL_R, GH_FILL_BOT, ghost * 0.75, 16);
      ctext(spec.pastLabel, GH_CX, 180, 800, 12.5, tone('sub'), 92, ghost);

      /* 그릇에서 나와 그 사람에게로 — 입으로 솟아 위로 가로지른 뒤 내려간다 */
      if (wu >= 0.28 && wu < 0.50) {
        const gp = (wu - 0.28) / 0.22;
        let gx, gy;
        if (gp < 0.30) { gx = GH_CX; gy = lerp(GH_FILL_BOT - 10, LANE_Y, gp / 0.30); }
        else if (gp < 0.62) { gx = lerp(GH_CX, B_VISIT, (gp - 0.30) / 0.32); gy = LANE_Y; }
        else { gx = B_VISIT; gy = lerp(LANE_Y, 196, (gp - 0.62) / 0.38); }
        grain(gx, gy, ghost * ease(clamp(gp / 0.10)) * (1 - ease(clamp((gp - 0.84) / 0.16))));
      }
    }

    /* ── 각자의 곳간 : 서로 반대로 간다 ─────────────── */
    bowl(A_CX - ST_HW, A_CX + ST_HW, ST_TOP, ST_BOT, st);
    bowl(B_CX - ST_HW, B_CX + ST_HW, ST_TOP, ST_BOT, st);

    const ripple = 2 * Math.sin(t * 2.3);
    const hA = ST_FILL_H * lerp(0.50, 1.0, riseK) + ripple * riseK;
    const hB = ST_FILL_H * lerp(0.50, 0.0, fallK);
    fill(A_CX - ST_HW + ST_INSET, ST_FILL_BOT - hA, A_CX + ST_HW - ST_INSET, ST_FILL_BOT, st, 21);
    fill(B_CX - ST_HW + ST_INSET, ST_FILL_BOT - hB, B_CX + ST_HW - ST_INSET, ST_FILL_BOT, st, 17);

    /* ── 왼쪽으로는 계속 들어온다 ───────────────────── */
    {
      const p = frac(t / 1.3);
      const gy = lerp(90, 158, p);
      const ga = st * ease(clamp(p / 0.10)) * (1 - ease(clamp((p - 0.72) / 0.28)));
      grain(DROP_X, gy, ga);
    }

    ctx.textAlign = 'left';
  }
};
