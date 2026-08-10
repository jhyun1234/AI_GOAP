import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* deathclock — 훅(4단 고정 구조 ②). 「죽음의 시계」가 사망 눈금에 한 번도 안 닿는다.

   원문 근거:
   > "지나가던 주민이 0.1초만 곁에 스쳐도 죽음의 시계가 처음으로 되감기니까요."
   > "그런데 그렇게 만들면 사망이 사실상 불가능해집니다."
   > "손이 모자라서 사람이 죽는 상황을 만들려고 이 축을 넣었는데, 정작 그 상황이 영원히 안 오게 됩니다."

   🔴 **이 회차의 기본 도형은 「끝 눈금을 향해 나아가는 표식 하나」다.**
   훅만 그 한 줄을 **원으로 말아 놓은 형태**다 — 원문이 이 장치를 「죽음의 시계」라고
   직접 이름 붙였기 때문이고, 본문 네 샷(가로 눈금)과 한눈에 갈리게 하려는 것이기도 하다.
   본문 S2(graze)도 되감기를 그리지만 거기는 **원인(스치는 통행인)과 라벨(0.1초)**이 있고
   여기는 **결과만** 있다 — 훅은 "왜"를 안 준다.

   🔴 사망 눈금은 12시(u=1)에 두고 바늘은 u=0.86 에서 되감긴다. 잔상 두 겹이 쌓여
   "몇 번을 돌아도 저기 한 칸을 못 넘는다"가 자막 없이 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **반지름으로 갈랐다.**
     중심 표식(ink) 139~165 · 바늘(ink) r 32~56 · 고리(track) r 70 ·
     진행 호(accent) r 70 · 잔상(accent) r 79·87 · 사망 눈금(accent) r 93~107.
     흰 것은 전부 r ≤ 56 안, 강조색은 전부 r ≥ 70 밖. 14px 이 빈다.
   ⚠️ 고리(track = 흰색 12%)와 진행 호(accent)가 같은 r 70 을 쓴다 — 그래서 고리는
     **호가 아직 안 지난 구간만** 그린다(각도로 잘라 낸다). 겹치는 픽셀이 0 이다.

   세로 예산(캔버스 307): 사망 라벨 24~40 · 눈금 45~59 · 잔상 65~239 ·
   고리 82~222 · 중심 표식 139~165. 최저 239(잔상 바닥) — 캔버스 바닥까지 68px 남는다.
   가로: 잔상 지름 174 라 중심 w/2 기준 좌우 87px, 352 폭에서 89~263.

   계속 도는 것 = 고리 점선의 회전(t) · 바늘(t 루프) · 진행 호. 되감기 뒤 짧은 정지
   구간(주기의 16%, 0.24초)에도 고리 점선은 계속 돈다. */

const CY = 152;
const RING = 70;
const HAND0 = 32, HAND1 = 56;
const BOX_W = 46, BOX_H = 26;
const GH = [79, 87];               // 잔상 두 겹
const TICK0 = 93, TICK1 = 107;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const cx = w / 2;

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    /* 훅은 체류가 걸린 자리라 0초 프레임에서 이미 무대가 서 있어야 한다 */
    const c0 = cue(spec.hookCue ?? 0, 0.15, 0.85);
    const st = ease(clamp(c0 / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const P = spec.cycleSec ?? 1.5;
    const STOP = clamp(spec.stopAt ?? 0.86, 0.1, 0.95);
    const p = frac(t / P);
    const u = p < 0.68 ? STOP * ease(p / 0.68)
      : p < 0.84 ? STOP * (1 - ease((p - 0.68) / 0.16))
        : 0;
    const A0 = -Math.PI / 2;
    const ang = k => A0 + k * Math.PI * 2;

    /* ── 구획 이름 ─────────────────────────────────── */
    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 고리 : 아직 안 지난 구간만 (accent 호와 픽셀을 안 나눈다) ── */
    /* 🔑 회전은 **점선 무늬**에만 걸린다 — 호의 시작·끝 각도에서 같은 값을 빼므로
       고리 자체는 제자리에 있고 대시만 흐른다(정적 방지). */
    const rot = (t * 0.32) % (Math.PI * 2);
    ctx.save();
    ctx.translate(cx, CY);
    ctx.rotate(rot);
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([10, 9]);
    ctx.beginPath();
    ctx.arc(0, 0, RING, ang(u) - rot, ang(1) - rot);
    ctx.stroke();
    ctx.restore();

    /* ── 잔상 : 지난 바퀴들이 멈춘 자리 ──────────────── */
    const laps = Math.min(GH.length, Math.floor(t / P));
    for (let i = 0; i < laps; i++) {
      ctx.save();
      ctx.globalAlpha = st * (0.34 - i * 0.12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.arc(cx, CY, GH[i], ang(0), ang(STOP));
      ctx.stroke();
      ctx.restore();
    }

    /* ── 진행 호 ───────────────────────────────────── */
    if (u > 0.004) {
      ctx.save();
      ctx.globalAlpha = st;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5; ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.arc(cx, CY, RING, ang(0), ang(u));
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 사망 눈금 : 12시. 한 번도 안 켜진다 ─────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5; ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(cx, CY - TICK0); ctx.lineTo(cx, CY - TICK1);
    ctx.stroke();
    ctx.restore();
    if (spec.endLabel) ctext(spec.endLabel, cx, 37, 900, 15, tone('accent'), w * 0.5, st);

    /* ── 중심 표식 : 다친 주민 ─────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, cx - BOX_W / 2, CY - BOX_H / 2, BOX_W, BOX_H, 7);
    ctx.stroke();
    ctx.restore();
    if (spec.face) ctext(spec.face, cx, CY + 5, 800, 14, tone('ink'), BOX_W - 10, st);

    /* ── 바늘 ──────────────────────────────────────── */
    const a = ang(u);
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4; ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(cx + Math.cos(a) * HAND0, CY + Math.sin(a) * HAND0);
    ctx.lineTo(cx + Math.cos(a) * HAND1, CY + Math.sin(a) * HAND1);
    ctx.stroke();
    ctx.restore();

    ctx.textAlign = 'left';
  }
};
