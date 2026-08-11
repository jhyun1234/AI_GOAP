/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* frozen — 훅. 늑대가 다가오는데 주민이 안 도망간다.

   원문 근거:
   > "배고파. 밥. 밥. 밥. …어? 방금 뭐가 나를 물었지?"
   > "화면에서 이건 굶주린 주민이 늑대 앞에 굳어 선 채로 물리는 장면으로 보입니다."
   > "늑대가 다가와서 겁먹고 도망가야 할 때, 배고픈 주민은 밥을 먹으러 가는 거 아니냐고요."

   2.4초 루프 : 흰 쐐기(늑대)가 왼쪽에서 다가온다 · 흰 동그라미(주민)는 오른쪽 아래
   흰 네모(밥) 쪽으로만 자꾸 기울어진다 · 그 위로 **강조색 도망길**이 화살표를 흘리며
   계속 켜져 있는데 아무도 안 탄다 → 쐐기가 닿으면 주민이 흔들리고 표정이 `>_<` 로
   바뀌며 **도망길이 꺼진다.**

   🖼 라벨을 다 가려도 읽힌다 — 「뾰족한 것이 다가오는데 동그라미는 반대편 네모 쪽으로만
   기울다가 흔들린다. 위쪽으로 난 길은 끝내 아무도 안 지나간다.」

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **좌표로 갈랐다.**
     강조색은 도망길 하나뿐이다: 축이 (188,186)→(300,108) 이고 획 3 + 글로우 6 이라
     축에서 **±7.5** 가 envelope 다(꺾쇠도 축에서 ±6 + 획 1.5 = ±7.5 로 같은 봉투 안).
     · 주민(흰) : 가장 많이 기운 순간 중심 (158,200) · 반지름 13.5(획 포함).
       축의 시작점 (188,186) 까지 거리 33.1 → 33.1 − 13.5 − 7.5 = **12.1px** 뜬다.
     · 표정 글립(흰) : 주민을 따라 움직인다. 기울임(≤8)에는 `(1 − bite)` 가, 흔들림(≤3.2)
       에는 `bite` 가 곱해져 **둘이 동시에 최대가 될 수 없다** — 오른쪽 최대는 158.
       상자 x 145~171 · y 155~170. 가장 가까운 모서리 (171,170) 에서 축 시작점 (188,186)
       까지 23.3 → **15.8px** 뜬다.
     · 바닥선(흰, y 218 · 획 바깥 216.5) : 축의 최저점이 186 이고 envelope 193.5 →
       **23px** 뜬다.
     · 밥 네모(흰, y 192.5~211.5 · x 287.5~312.5) : 그 x 구간에서 축은 y 115.7~108,
       envelope 아래 끝 123.2 → **69px** 뜬다.
     · 「밥」 글립(흰, y 175~189) : 같은 x 구간의 envelope 아래 끝 124.6 → **50px** 뜬다.
     🔑 「도망」 라벨은 **강조색**이라 도망길과 겹쳐도 규약 위반이 아니다(같은 색).

   세로 예산(캔버스 307) : 최저 = 「늑대」 글립 바닥 243. 64px 남는다.
   가로(캔버스 352) : 오른쪽 최대 = 바닥선 끝 329.5, 왼쪽 최소 = 바닥선 끝 22.5.

   ⏱ 계속 도는 것 = 꺾쇠 셋이 도망길을 따라 초당 136px 로 흐른다(30fps 한 프레임에
   4.5px) + 늑대 접근(0.97px/프레임) + 주민의 기울임(최대 22px/초).
   위상은 전부 `frac()` 로 양수 정규화했고 `setLineDash` 를 한 군데도 안 썼다. */

const GY = 218;                       // 바닥선
const VX = 150, VY = 200, VR = 12;    // 주민
const FX = 300, FY = 202;             // 밥
const AX0 = 188, AY0 = 186;           // 도망길 시작
const AX1 = 300, AY1 = 108;           // 도망길 끝
const DX = AX1 - AX0, DY = AY1 - AY0; // (112, -78)
const ALEN = Math.sqrt(DX * DX + DY * DY);
const UX = DX / ALEN, UY = DY / ALEN;
const PX = -UY, PY = UX;              // 축에 수직인 단위벡터

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
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

    const st = ease(clamp(cue(0, 0.15, 0.7) / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    const loop = spec.loopSec ?? 2.4;
    const u = frac(t / loop);
    const ap = ease(clamp(u / 0.72));                       // 늑대 접근
    const bite = ease(clamp((u - 0.72) / 0.10));            // 닿는다
    /* 🔴 되감기는 **양끝을 다 0 으로** 만든다. 끝만 0 으로 하면 위상이 1 → 0 으로 감길 때
       알파가 0 에서 1 로 튀어(늑대 위치도 128 → 78 로 튀어) 반 초마다 '팝' 이 보인다. */
    const fade = ease(clamp(u / 0.08)) * (1 - ease(clamp((u - 0.88) / 0.12)));
    const A = st * fade;

    /* ── 바닥선 : 점선을 fillRect 조각으로 놓는다 ──────────
       🔴 `setLineDash` 를 쓰지 않는다 — 긴 가로 점선이 30fps 3패스에서 알파가 갈린
       전례(결정성 반려)가 있다. 위상은 안 움직이므로 여기선 고정 격자다. */
    ctx.save();
    ctx.globalAlpha = A * 0.55;
    ctx.fillStyle = tone('ink');
    for (let x = 24; x < 328; x += 15) ctx.fillRect(x, GY - 1.5, 9, 3);
    ctx.restore();

    /* ── 밥(흰 네모) ─────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = A;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, FX - 11, FY - 8, 22, 16, 3); ctx.stroke();
    ctx.restore();
    if (spec.foodLabel) ctext(spec.foodLabel, FX, FY - 16, 800, 13, tone('ink'), 60, A);

    /* ── 도망길(강조색) : 축 + 흐르는 꺾쇠 셋 ──────────────
       주민이 안 쓰는 길이라 **닿는 순간 꺼진다**(1 − bite). */
    const pathA = A * (1 - bite);
    if (pathA > 0.01) {
      ctx.save();
      ctx.globalAlpha = pathA;
      setShadow(ctx, GLOW, 6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(AX0, AY0); ctx.lineTo(AX1, AY1); ctx.stroke();
      clearShadow(ctx);

      /* 꺾쇠 셋 — 초당 136px. 이게 이 샷의 상시 운동이다. */
      for (let i = 0; i < 3; i++) {
        const s = frac(t / (spec.flowSec ?? 1.0) + i / 3);
        const px = AX0 + DX * s, py = AY0 + DY * s;
        const bx = px - UX * 7, by = py - UY * 7;
        ctx.beginPath();
        ctx.moveTo(bx + PX * 6, by + PY * 6); ctx.lineTo(px, py);
        ctx.lineTo(bx - PX * 6, by - PY * 6);
        ctx.stroke();
      }
      ctx.restore();
      if (spec.fleeLabel) ctext(spec.fleeLabel, 288, 90, 900, 14, tone('accent'), 96, pathA);
    }

    /* ── 늑대(흰 쐐기) ───────────────────────────────── */
    const tip = lerp(78, 128, ap);
    ctx.save();
    ctx.globalAlpha = A;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    ctx.beginPath();
    ctx.moveTo(tip, VY);
    ctx.lineTo(tip - 34, VY - 14);
    ctx.lineTo(tip - 34, VY + 14);
    ctx.closePath(); ctx.stroke();
    ctx.restore();
    if (spec.wolfLabel) ctext(spec.wolfLabel, 96, 240, 800, 11, tone('ink'), 70, A * 0.85);

    /* ── 주민 ────────────────────────────────────────
       기울임 = 밥 쪽으로 최대 8px(상시 · `t`) · 흔들림 = 닿은 뒤에만. */
    const lean = 8 * (0.5 + 0.5 * Math.sin(t * 5.6)) * (1 - bite);
    const shake = bite * 3.2 * Math.sin(t * 31);
    const vx = VX + lean + shake;
    ctx.save();
    ctx.globalAlpha = A;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(vx, VY, VR, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    const face = bite > 0.5 ? (spec.faceHurt ?? '>_<') : (spec.faceCalm ?? '·_·');
    ctext(face, vx, 166, 800, 15, tone('ink'), 62, A);

    ctx.textAlign = 'left';
  }
};
