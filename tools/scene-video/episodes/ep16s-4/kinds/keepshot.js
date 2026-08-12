import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* keepshot — 고치려던 자리는 손도 안 댔고, 지워지기 전에 한 장이 떨어져 나와 살아남는다.

   원문 근거(「10단계 — 죽음이 지우지 못한 것 (C3)」):
   > "처음 계획할 때 저는 관계를 지우는 그 함수를 「M13의 유일한 수술」이라고 적어 뒀습니다.
   >  조건 분기를 넣어 죽은 사람의 기록은 안 지우게 만들 생각이었거든요.
   >  그런데 결국 그 함수는 한 글자도 안 고쳤습니다.
   >  지우기 전에 사진을 찍어 두는 것으로 충분했으니까요."
   🔑 **「사진」은 원문이 스스로 쓴 낱말이다** — 비유를 새로 지은 것이 아니다(ADR-V25-14).

   화면이 지는 세 가지:
     ① 흰 점선 사각 = 「고치려고 찍어 둔 자리」. 그것이 **손도 안 댄 채 그냥 꺼지는 것**이
        「한 글자도 안 고쳤다」의 그림판이다. 그래서 자막에 「수술」·「조건 분기」가 없어도 선다.
     ② 네 모서리 틀이 바깥에서 안으로 모여 닫히는 것 = 사진을 찍는 순간.
        **since(1) 0.48초에 닫힘이 완성**되고 그 자리에 tick(delayMs 480)이 울린다.
     ③ 흰 막대가 왼쪽에서 오른쪽으로 지나가며 위쪽 줄을 지우는데 **아래 것은 그대로 남는다.**

   색 규약(회차 공통): **강조색 = 기록(과 그 복사본) / 흰색 = 표시 · 틀 · 지우는 것.**
   🔑 지워진 것과 남은 것이 **같은 색**인 게 이 편의 논지다 — 없어진 것과 남긴 것이 같은 물건이다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다:
     · 점선 사각 오른끝 86 ↔ 강조 줄 glow 왼끝 96 → **10px**.
     · 지우개는 지우는 물건이라 접촉이 필연 → 코드 문턱 **선단 + 8px**(`EDGE`). 축 A 최소 **8px**.
     · 브래킷 왼위 세로 조각(x 82.5~85.5) ↔ 강조 glow 왼끝 96 → **10.5px** ·
       위 가로 조각(y 86.5~89.5) ↔ 강조 glow 위끝 104 → **14.5px** ·
       아래 가로 조각(y 146.5~149.5) ↔ 강조 glow 바닥 132 → **14.5px**.
     · 라벨 A 글립 바닥 79 ↔ 강조 glow 위끝 104 → **25px** ·
       라벨 B 글립 꼭대기 270 ↔ 복사본 glow 바닥 250 + 1.5(선 반폭) + 8 = 259.5 → **10.5px**.
       (복사본의 맥동은 위치가 아니라 **알파**로 줬다 — 숨 진폭이 이 간격을 깎으면 안 된다.)
   🔴 흰↔흰(서로 다른 것): 라벨 A(y 66~79)와 라벨 B(y 270~283)는 세로 **191px**.
     지우개(x 60~72)와 브래킷 왼 세로 조각(82.5~85.5)은 **10.5px**.
     ⚠️ **점선 사각(x 46~86)과 브래킷(x 84~284)은 좌표로는 2px 붙는다 — 시간으로 갈랐다.**
     점선은 `since(1) 0.22` 에 알파가 0 이 되고 브래킷은 `0.28` 부터 뜬다.
     **한 프레임도 함께 있지 않다.** (브래킷이 밖에서 모여드는 첫 위치 x 58 도 같은 이유로 안전하다.)
     점선 사각과 그것이 감싸는 지우개는 **의도된 쌍**이다.

   세로 예산(캔버스 307): 최저 = 라벨 B 글립 바닥 283. **24px** 남는다.
   가로(캔버스 352): 최대 = 브래킷이 가장 벌어진 순간의 오른끝 310. **42px** 남는다.
      (지우개 최종 오른끝은 302 다.)

   ⏱ 사건은 전부 `since(1)` 위에 있다 — `sfx.delayMs` 와 원점이 같아 자막 길이가 바뀌어도
      셔터음 자리를 다시 안 푼다. 0.00~0.22 점선 꺼짐 / 0.28~0.48 틀 닫힘 / 0.50~0.95 복사본
      내려앉음 / 0.60~0.75 틀 꺼짐 / 0.85~1.30 지움.
   계속 도는 것(30fps 기준): 점선 사각 점선 흐름 44px/s → 1.5px/프레임 ·
      강조 줄 밝기 파동 · 복사본 맥동(진폭 1.5px · 5.4rad/s). */

const BAR_X0 = 104, BAR_X1 = 264, BAR_Y = 112, BAR_H = 12;
const ERA_HW = 6, ERA_Y0 = 100, ERA_Y1 = 136, EDGE = ERA_HW + 8;
const MARK_X0 = 46, MARK_X1 = 86, MARK_Y0 = 92, MARK_Y1 = 144;
const BR_X0 = 84, BR_X1 = 284, BR_Y0 = 88, BR_Y1 = 148, BR_LEN = 24;
const CP_HW = 58, CP_HH = 22, CP_CY = 228;

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
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const dashRun = (x0, y0, x1, y1, seg, gap, off, alpha) => {
      if (alpha <= 0.01) return;
      const P = seg + gap;
      const len = Math.hypot(x1 - x0, y1 - y0);
      const ux = (x1 - x0) / len, uy = (y1 - y0) / len;
      const ph = ((off % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('ink');
      for (let s = -P + ph; s < len; s += P) {
        const a = Math.max(0, s), b = Math.min(len, s + seg);
        if (b <= a) continue;
        const ax = x0 + ux * a, ay = y0 + uy * a;
        const bx = x0 + ux * b, by = y0 + uy * b;
        ctx.fillRect(
          Math.min(ax, bx) - (uy ? 1.5 : 0),
          Math.min(ay, by) - (ux ? 1.5 : 0),
          Math.abs(bx - ax) + (uy ? 3 : 0),
          Math.abs(by - ay) + (ux ? 3 : 0));
      }
      ctx.restore();
    };

    const st = ease(cue(spec.markCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const s = since(spec.shotCue ?? 1);
    const gone = ease(clamp(s / 0.22));                    // 점선 표시가 그냥 꺼진다
    const shot = ease(clamp((s - 0.28) / 0.20));           // 틀이 닫힌다 (완성 0.48)
    const shotOut = ease(clamp((s - 0.60) / 0.15));        // 틀이 꺼진다
    const copy = ease(clamp((s - 0.50) / 0.45));           // 복사본이 내려앉는다
    const erase = ease(clamp((s - 0.85) / 0.45));          // 위쪽 줄이 지워진다

    ctext(spec.topLabel, 176, 76, 800, 12.5, tone('sub'), 140, st);

    /* ── 기록 줄 (강조색) — 지우개 선단 + 8px 앞까지만 그린다 ── */
    const xe = lerp(66, 296, erase);
    const a0 = Math.max(BAR_X0, xe + EDGE);
    if (BAR_X1 - a0 > 0.5) {
      ctx.save();
      ctx.globalAlpha = st * (0.82 + 0.18 * (0.5 + 0.5 * Math.sin(t * 3.9)));
      setShadow(ctx, FAIL_GLOW, 8);
      ctx.fillStyle = tone('fail');
      ctx.fillRect(a0, BAR_Y, BAR_X1 - a0, BAR_H);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 고치려고 찍어 둔 자리 (흰 점선) — 손도 안 댄 채 꺼진다 ── */
    const ma = st * (1 - gone);
    if (ma > 0.01) {
      const off = -t * 44;
      dashRun(MARK_X0, MARK_Y0, MARK_X1, MARK_Y0, 9, 7, off, ma * 0.9);
      dashRun(MARK_X1, MARK_Y0, MARK_X1, MARK_Y1, 9, 7, off, ma * 0.9);
      dashRun(MARK_X1, MARK_Y1, MARK_X0, MARK_Y1, 9, 7, off, ma * 0.9);
      dashRun(MARK_X0, MARK_Y1, MARK_X0, MARK_Y0, 9, 7, off, ma * 0.9);
    }

    /* ── 지우개 (흰색) ── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.fillStyle = tone('ink');
    ctx.fillRect(xe - ERA_HW, ERA_Y0, ERA_HW * 2, ERA_Y1 - ERA_Y0);
    ctx.restore();

    /* ── 사진 틀 (흰 코너 넷) — 바깥에서 안으로 모여 닫힌다 ── */
    const ba = st * shot * (1 - shotOut);
    if (ba > 0.01) {
      const sp = lerp(26, 0, shot);
      ctx.save();
      ctx.globalAlpha = ba;
      ctx.fillStyle = tone('ink');
      const cs = [
        [BR_X0 - sp, BR_Y0 - sp, 1, 1],
        [BR_X1 + sp, BR_Y0 - sp, -1, 1],
        [BR_X0 - sp, BR_Y1 + sp, 1, -1],
        [BR_X1 + sp, BR_Y1 + sp, -1, -1]
      ];
      for (const [cx, cy, sx, sy] of cs) {
        ctx.fillRect(Math.min(cx, cx + sx * BR_LEN) - 1.5, cy - 1.5, BR_LEN + 3, 3);
        ctx.fillRect(cx - 1.5, Math.min(cy, cy + sy * BR_LEN) - 1.5, 3, BR_LEN + 3);
      }
      ctx.restore();
    }

    /* ── 떨어져 나온 한 장 (강조색) — 위에서 아래로 내려앉아 그대로 남는다 ── */
    const ca = st * ease(clamp(copy / 0.35));
    if (ca > 0.01) {
      const k = lerp(0.32, 1, copy);
      const cy = lerp(120, CP_CY, copy);
      ctx.save();
      /* 🔴 맥동을 **위치가 아니라 알파**로 준다 — 세로 예산(라벨 B 와의 10.5px)이
         숨 진폭만큼 깎이면 안 되기 때문이다. */
      ctx.globalAlpha = ca * (0.84 + 0.16 * (0.5 + 0.5 * Math.sin(t * 5.4)));
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.strokeRect(176 - CP_HW * k, cy - CP_HH * k, CP_HW * 2 * k, CP_HH * 2 * k);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(176 - 42 * k, cy - 10 * k - 3 * k, 84 * k, 6 * k);
      ctx.fillRect(176 - 42 * k, cy + 4 * k - 3 * k, 84 * k, 6 * k);
      clearShadow(ctx);
      ctx.restore();
    }

    ctext(spec.keepLabel, 176, 280, 800, 12.5, tone('sub'), 140, st * ease(clamp(copy / 0.5)));

    ctx.textAlign = 'left';
  }
};
