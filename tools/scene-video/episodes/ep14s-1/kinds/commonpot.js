import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* commonpot — 훅(4단 구조 ②). 마을 한복판에 그릇이 하나 있고, 만드는 쪽과
   가져가는 쪽이 서로 다른데 그릇은 하나다.

   원문 근거(문장 그대로):
   > "마을 한복판에 창고가 하나 있고, 주민 넷이 거기서 꺼내 먹습니다."
   > "주민 한 명이 아무 일도 하지 않고 하루를 보냅니다. 밭에 나가지도, 열매를 주우러
   >  가지도 않습니다. 그런데 배가 고파지면 창고로 걸어가 밥을 먹고, 배가 부른 채로
   >  다시 돌아와 앉습니다."
   > "옆에서 부지런히 밭을 갈던 농사꾼과 하루의 끝이 완전히 똑같았습니다."

   🔴 이 회차의 기본 도형(하나뿐인 그릇 — 그것이 사람 수만큼 갈라지고, 갈라진 뒤에야
   서로 달라진다)의 **첫 형태**다. 여기서는 아직 하나다.
   어휘 셋: 그릇(U자로 열린 흰 획) · 사람(캡슐 + 표정 글자) · 알갱이(강조색 사각).
   색 규약: 흰색 = 마을에 원래 있던 것 / 강조색 = 먹을 것과 그것이 흐르는 자리.

   🔴 `ep13s-3` 의 nextpeek(가운데 둥근 사각 박스 + 방사 점선 + 원형 표식 넷)과
   **같은 사건을 다른 그림으로** 그린다. 방사선도 원형 표식도 사각 박스도 안 쓰고,
   사람도 넷이 아니라 원문 문단이 나란히 놓은 **둘**이다.

   🔴 화면에 수를 한 개도 안 올렸다. 밭 칸이 셋인 것은 가로 폭에 셋이 들어가서지
   개수에 뜻이 있어서가 아니고, 그릇 안은 셀 수 없는 채움(단색 + 흐르는 빗금)이다.

   🔴 흰색과 강조색이 같은 픽셀을 안 나눈다 — 최근접을 다 계산해 뒀다(알갱이 한 변 7,
   반폭 3.5. 획은 lineWidth 4 + round cap 이라 끝에서 2px 더 나간다).
     ⓐ 그릇 채움은 clip(148~204 × ?~196) 안에서만 그린다. 벽 안쪽 획(140/212)에서 8px,
        바닥 안쪽 획(202)에서 6px 들여 잡았다. clip 은 그림자도 자른다.
     ⓑ 밭 칸 안 빗금(흰색)도 clip 으로 가둔다 — 채움 오른끝 92, 알갱이 출발 x 104.5 →
        **12.5px.** 칸 테두리(track) 오른끝 97.5 에서도 7px.
     ⓒ 밭→그릇 알갱이가 그릇 왼벽의 x 폭(132.5~) 에 처음 걸치는 것은 u=0.360 이고,
        그때 알갱이 아래끝 124.1 · 벽 위끝 138 → **13.9px.** 그 뒤로는 계속 멀어진다.
     ⓓ 그릇→사람 알갱이는 v=0 에서 (176, 124) — 그릇 위끝 138 에서 **10.5px**,
        v=1 에서 (250, 112) — 오른쪽 사람 캡슐 위끝 125 에서 **9.5px.**
        오른벽 x 폭에 걸치는 v=0.439 에서는 y 93.3 이라 **44.7px** 뜬다.
     ⓔ 왼쪽 사람 캡슐은 흔들림 ±6 을 넣어 오른끝 80.5(획 포함), 알갱이 출발 104.5 →
        **24px.**
     ⓕ 🔴 `setShadow`/`GLOW` 를 안 썼다. 알갱이가 흰 획 곁을 10~14px 로 지나므로
        blur 10~12 짜리 번짐은 곧 혼합이다. 대비는 굵기와 크기로만 만든다.

   세로 예산(캔버스 307): 최하단 = 「밭」 글립 바닥 약 250. 앉는 동작(+5px)을 넣어도
   오른쪽 사람 캡슐 바닥은 180 이다. 57px 남는다.
   가로: 오른쪽 사람이 제자리(300)에 있을 때 캡슐 오른끝 319(캔버스 352).

   ⏱ 등장 램프만 `cue(0)` 에 물렸고, 되풀이되는 두 사건은 시간 구동이다 —
      밭→그릇 알갱이는 `t`(1.7초 주기), 사람의 왕복은 `since(0)`(2.3초 주기).
      이 샷에는 효과음이 없으므로 `since` 를 쓴 것은 소리 때문이 아니라,
      앞선 인트로 샷의 길이에 왕복 위상이 끌려가지 않게 하려는 것이다.
      한 자막(4.5초쯤)에 왕복이 두 번 들어간다.

   계속 도는 것 = 밭 칸 셋의 빗금 스크롤(각 16×18 = 864px²) + 그릇 채움 빗금 +
   알갱이 셋의 비행 + 사람 둘의 움직임. 자막이 끝난 뒤에도 전부 돈다. */

const TAU = Math.PI * 2;

const FARM_X0 = 16, FARM_W = 24, FARM_GAP = 4, FARM_N = 3;
const FARM_Y = 200, FARM_H = 26;
const FARM_R = FARM_X0 + FARM_N * FARM_W + (FARM_N - 1) * FARM_GAP;   // 96

const BUSY_X = 54, BUSY_Y = 150, BOB = 6;
const P_W = 38, P_H = 50;

const POT_L = 138, POT_R = 214, POT_TOP = 140, POT_BOT = 204;
const FILL_L = 148, FILL_R = 204, FILL_BOT = 196;
const POT_CX = (POT_L + POT_R) / 2;                                    // 176

const SEED_X = 108, SEED_Y = 196;      // 알갱이가 밭을 떠나는 자리
const ARC_TOP = 112, ARC_LIFT = 50;    // 그릇 입구 위

const IDLE_HOME = 300, IDLE_POT = 250, IDLE_Y = 150;

const GR = 7;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    /* 가운데 정렬 글자는 폭을 재서 캔버스 안쪽에 가둔다 */
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

    /* 그릇 — 위가 열린 U자. 무언가 들어가고 나올 수 있다는 것이 형태로 읽힌다. */
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

    /* 채움 = 단색 바탕 + 그 위를 흐르는 빗금. 빗금이 면적을 가진 움직임을 만든다.
       clip 안에서만 그리므로 색이 밖으로 못 샌다. */
    const fill = (x0, y0, x1, y1, color, alpha, speed) => {
      if (x1 - x0 < 2 || y1 - y0 < 2 || alpha <= 0.01) return;
      ctx.save();
      ctx.beginPath(); ctx.rect(x0, y0, x1 - x0, y1 - y0); ctx.clip();
      ctx.globalAlpha = alpha * 0.42;
      ctx.fillStyle = color;
      ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
      ctx.globalAlpha = alpha * 0.95;
      ctx.strokeStyle = color; ctx.lineWidth = 5;
      const h = y1 - y0, S = 18, off = ((t * speed) % S + S) % S;
      for (let x = x0 - h - S + off; x < x1 + S; x += S) {
        ctx.beginPath(); ctx.moveTo(x, y1); ctx.lineTo(x + h, y0); ctx.stroke();
      }
      ctx.restore();
    };

    const person = (cx, cy, face, alpha) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - P_W / 2, cy - P_H / 2, P_W, P_H, P_W / 2);
      ctx.stroke();
      ctx.restore();
      ctext(face, cx, cy + 5, 800, 13, tone('ink'), P_W - 8, alpha);
    };

    const grain = (x, y, alpha) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(x - GR / 2, y - GR / 2, GR, GR);
      ctx.restore();
    };

    /* ── 등장 램프 ─────────────────────────────────── */
    const c0 = cue(spec.potCue ?? 0, 0.15, 0.55);
    const st = ease(clamp(c0 / 0.18));
    if (st <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 그릇 채움 높이 (셀 수 없는 양) ─────────────── */
    const lvl = 30 + 3 * Math.sin(TAU * t / 1.7);
    const fillTop = FILL_BOT - lvl;

    /* ── 밭 : 왼쪽 사람이 계속 가는 곳 ──────────────── */
    for (let i = 0; i < FARM_N; i++) {
      const x = FARM_X0 + i * (FARM_W + FARM_GAP);
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, x, FARM_Y, FARM_W, FARM_H, 5); ctx.stroke();
      ctx.restore();
      fill(x + 4, FARM_Y + 4, x + FARM_W - 4, FARM_Y + FARM_H - 4,
        tone('ink'), st * 0.9, 16 + i * 4);
    }
    ctext(spec.farmLabel, (FARM_X0 + FARM_R) / 2, 246, 800, 12.5, tone('sub'), 90, st);

    /* ── 왼쪽 사람 : 밭 위를 좌우로 오간다 ──────────── */
    person(BUSY_X + BOB * Math.sin(TAU * t / 1.6), BUSY_Y, spec.busyFace, st);

    /* ── 공용 그릇 ─────────────────────────────────── */
    bowl(POT_L, POT_R, POT_TOP, POT_BOT, st);
    fill(FILL_L, fillTop, FILL_R, FILL_BOT, tone('accent'), st, 20);
    ctext(spec.potLabel, POT_CX, 228, 800, 13, tone('ink'), 96, st);

    /* ── 오른쪽 사람 : 배가 고파지면 걸어와 하나 받아 돌아가 앉는다 ──
       사건을 `since(0)` 위에 세워 앞 샷 길이와 무관하게 같은 위상으로 돈다. */
    const L = spec.loopSec ?? 2.3;
    const wu = frac(Math.max(0, since(spec.potCue ?? 0)) / L);
    let ix = IDLE_HOME, sit = 0;
    if (wu < 0.26) ix = lerp(IDLE_HOME, IDLE_POT, ease(wu / 0.26));
    else if (wu < 0.50) ix = IDLE_POT;
    else if (wu < 0.76) ix = lerp(IDLE_POT, IDLE_HOME, ease((wu - 0.50) / 0.26));
    else { ix = IDLE_HOME; sit = ease((wu - 0.76) / 0.16); }
    person(ix, IDLE_Y + sit * 5, spec.idleFace, st);

    /* ── 밭에서 그릇으로 : 누가 채웠는지는 그릇이 알 바 아니다 ── */
    for (let i = 0; i < 3; i++) {
      const p = frac(t / 1.7 + i / 3);
      let gx, gy, ga;
      if (p < 0.74) {
        const u = p / 0.74;
        gx = lerp(SEED_X, POT_CX, u);
        gy = lerp(SEED_Y, ARC_TOP, u) - ARC_LIFT * Math.sin(Math.PI * u);
        ga = st * ease(clamp(u / 0.10));
      } else if (p < 0.90) {
        const v = (p - 0.74) / 0.16;
        gx = POT_CX;
        gy = lerp(ARC_TOP, fillTop - 4, v);
        ga = st * (1 - ease(clamp((v - 0.55) / 0.45)));
      } else continue;
      grain(gx, gy, ga);
    }

    /* ── 그릇에서 오른쪽 사람에게 : 걸어와서 하나 가져간다 ── */
    if (wu >= 0.28 && wu < 0.50) {
      const v = (wu - 0.28) / 0.20;
      const gx = lerp(POT_CX, IDLE_POT, v);
      const gy = lerp(124, 112, v) - 26 * Math.sin(Math.PI * v);
      const ga = st * ease(clamp(v / 0.12)) * (1 - ease(clamp((v - 0.86) / 0.14)));
      grain(gx, gy, ga);
    }

    ctx.textAlign = 'left';
  }
};
