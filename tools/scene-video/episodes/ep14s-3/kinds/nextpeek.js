import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편 기능의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 재료는 이 편의 사건이 아니라 **다음 편(`ep14s-4`)이 맡은 사건**에서 뽑았다.
   근거 문장(같은 원문 12단계 절):
   > "마지막 조각입니다. 성격별 선호 거리를 넣었는데도 집이 생각만큼 멀리 가지 않았습니다."
   > "공용 모닥불이 마을을 물리적으로 붙잡아두는 중력이었던 겁니다."
   기획 브리프 §8 이 「화면에서 움직이는 것 = 집들이 바깥으로 밀려나려다 가운데 모닥불에
   당겨져 다시 붙는다」까지 지정했고 그대로 그렸다.

   ⚠️ 원문 끝의 「다음 이야기 예고」 문단(성향 벡터)은 **블로그의 다음 글** 예고라 안 썼다 —
   ep02s·ep11s·ep12s 가 그 계열로 세 번 틀렸다. 그건 `ep14s-4` 가 `ep15s` 를 부를 재료다.
   🔴 미리보기 · 예고 음성 · `outro.next` 카드 셋이 같은 것을 가리킨다:
      **성격마다 거리를 줬는데 마을이 안 흩어진다.**

   **0.5초 루프** — ①집 넷이 바깥으로 벌어져 나간다 ②가장 멀리 간 순간 안쪽으로 확 당겨져
   다시 붙는다 ③잠깐 붙어 있다가 되풀이된다.

   🔴 본문의 도형(자리 위에 서는 집)에서 **집만 물려받고 축은 바꿨다** — 다음 편의 사건은
   「누가 집을 짓느냐」가 아니라 「집이 어디에 서느냐」라 세로 층이 아니라 **거리**가 축이다.
   물려받은 문장은 색 규약 하나뿐이다: **흰색 = 지금 있는 것 / 강조색 = 다음 편이 바꾸는 것.**

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **거리로 갈랐다.**
     불(accent)의 글로우 포함 반경이 약 29px 인데, 집(ink)이 가장 가까운 순간에도
     중심에서 42.8px 떨어져 있다(r = 74 · 세로 0.62 배 타원 배치 기준). 13.8px 뜬다.
     🔴 당겨지는 줄(track)은 **일부러 안 그렸다** — 흰색 12% 선이 불의 글로우와 만나는
     자리가 생긴다. 당김은 선이 아니라 **되돌아오는 움직임**이 진다.

   🔴 화면에 수를 한 개도 안 올렸다. 집 넷은 `spec.houses` 로만 있다.

   세로 예산(캔버스 307): 최저 = 「모닥불」 글립 바닥 240. 67px 남는다.
   가로: 집이 가장 멀 때 50~302(캔버스 352).

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다. */

const CY = 148;
const R_IN = 74, R_OUT = 130, YSQ = 0.62;
const HOUSE_W = 38, HOUSE_H = 32;
const FIRE_W = 26, FIRE_H = 34;
const FIRE_LABEL_Y = 236;
const ANGLES = [35, 145, 215, 325];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01 || !txt) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    /* ── 구획 이름 (창 밖) ─────────────────────────── */
    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    const CX = w / 2;
    const n = Math.min(ANGLES.length, Math.max(2, spec.houses ?? 4));

    /* ── 0.5초 테스트 장면 : 나가려다 되당겨진다 ────── */
    const u = frac(t / (spec.loopSec ?? 0.5));
    const k = u < 0.55
      ? ease(u / 0.55)                          // 바깥으로 벌어진다
      : (u < 0.80 ? 1 - ease((u - 0.55) / 0.25) // 확 당겨져 되돌아온다
        : 0);                                   // 붙어 있다
    const r = lerp(R_IN, R_OUT, k);

    /* ── 가운데 불 ─────────────────────────────────── */
    {
      const fh = FIRE_H + 2 * Math.sin(t * 7.3);
      const blur = 12 + 4 * Math.sin(t * 5.1);
      ctx.save();
      ctx.globalAlpha = pk;
      setShadow(ctx, GLOW, blur);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      const flame = (fw, hh) => {
        ctx.beginPath();
        ctx.moveTo(CX, CY - hh / 2);
        ctx.quadraticCurveTo(CX + fw / 2, CY - hh / 6, CX + fw * 0.31, CY + hh / 2);
        ctx.lineTo(CX - fw * 0.31, CY + hh / 2);
        ctx.quadraticCurveTo(CX - fw / 2, CY - hh / 6, CX, CY - hh / 2);
        ctx.closePath();
        ctx.stroke();
      };
      flame(FIRE_W, fh);
      clearShadow(ctx);
      ctx.globalAlpha = pk * 0.55;
      flame(FIRE_W * 0.5, fh * 0.52);
      ctx.restore();
    }
    ctext(spec.fireLabel, CX, FIRE_LABEL_Y, 900, 14, tone('accent'), w - 60, pk);

    /* ── 집 넷 : 벌어졌다가 다시 붙는다 ─────────────── */
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    for (let i = 0; i < n; i++) {
      const a = ANGLES[i] * Math.PI / 180;
      const hx = CX + r * Math.cos(a);
      const hy = CY + r * YSQ * Math.sin(a);
      const base = hy + HOUSE_H / 2;
      const bodyW = HOUSE_W * 0.78, bodyH = HOUSE_H * 0.60, eave = base - bodyH;
      ctx.beginPath();
      ctx.moveTo(hx - bodyW / 2, base);
      ctx.lineTo(hx - bodyW / 2, eave);
      ctx.lineTo(hx - HOUSE_W / 2, eave);
      ctx.lineTo(hx, base - HOUSE_H);
      ctx.lineTo(hx + HOUSE_W / 2, eave);
      ctx.lineTo(hx + bodyW / 2, eave);
      ctx.lineTo(hx + bodyW / 2, base);
      ctx.closePath();
      ctx.stroke();
    }
    ctx.restore();

    ctx.textAlign = 'left';
  }
};
