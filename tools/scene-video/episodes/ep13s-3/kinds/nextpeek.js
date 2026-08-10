import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편 기능의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 재료는 **이 글이 아니라 다음 회차 원문**(M11 개인화 경제)에서 뽑았다.
   이 글 끝의 「다음 이야기 예고」 문단은 블로그의 다음 글 예고라 쓰지 않았다
   (ep02s·ep11s·ep12s 가 그 형태로 세 번 틀렸다).
   근거 문장 — `tools/blog-automation/published/2026-07-30-unity-goap-m11-personalized-economy.html`:
   > "마을 한복판에 창고가 하나 있고, 주민 넷이 거기서 꺼내 먹습니다."
   > "그런데 이번 회차에 그 공용 창고를 지우자, 창고와 아무 상관 없어 보이던 곳들이
   >  줄줄이 같이 무너졌습니다."
   > "…식량·집·밭·모닥불을 전부 개인 소유로 쪼갠 M11 '개인화 경제' 밀스톤 이야기입니다."

   🔴 `ep14s` 는 아직 분할 전이라 **밀스톤 층위**로만 그렸다 — 사건 단위로 그리면
   나중에 14-1편이 그 사건이 아닐 수 있다.
   🔴 미리보기·예고 음성·`outro.next` 카드 셋이 같은 것을 가리킨다: **공용 창고를 지운다.**

   **0.5초 루프** — ①가운데 「공용 창고」 칸이 줄어들며 사라지고 연결선이 끊긴다
   ②주민 넷 각자 위로 작은 강조색 칸이 하나씩 붙는다 ③「개인 소유」가 켜진다
   ④칸들이 먼저 꺼진 **뒤** 가운데 칸이 돌아오며 되감긴다.

   🔴 본문의 도형(가로 눈금 위를 나아가는 표식)을 여기로 끌고 오지 않았다 —
   다음 편의 사건은 시간이 아니라 **소유의 자리**라 축이 다르다. 물려받은 문장은
   색 규약 하나뿐이다: **흰색 = 지금 있는 것 / 강조색 = 다음 편이 바꾸는 것.**

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 좌표와 시간 두 겹으로 막았다.
     ⓐ 좌표 : 공용 창고(ink) 86~148 · 개인 칸(accent) 170.5~193.5 · 주민(ink) 201.5~226.5.
        위아래로 각각 21px · 8px 뜬다.
     ⓑ 시간 : 연결선(track = 흰색 12%)은 개인 칸과 **같은 x** 를 지나므로 위상으로 갈랐다 —
        연결선은 위상 0.30 에 알파 0 이고 개인 칸은 0.36 에야 시작한다. 되감을 때도
        개인 칸이 0.88 에 완전히 꺼진 뒤 0.88 부터 연결선이 돌아온다.

   🔴 화면에 수를 한 개도 안 올렸다. 주민 넷은 `spec.marks` 로만 있고 세는 라벨이 없다
      (M11 원문의 「주민 넷」이 근거지만, 숫자를 글자로 적으면 이 편의 수치가 되어 버린다).
   🔴 수치는 전부 `spec` 경유다(`marks` · `loopSec`).

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   세로 예산(캔버스 307): 최저 = 「개인 소유」 글립 바닥 258. 49px 남는다.
   가로: 주민 넷 45~307(캔버스 352). */

const BOX_W = 132, BOX_H = 62, BOX_CY = 117;
const OWN_W = 30, OWN_H = 20, OWN_CY = 182;
const MARK_Y = 214, MARK_R = 11;

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

    const n = Math.max(2, spec.marks ?? 4);
    const gap = Math.min(80, (w - 112) / (n - 1));
    const mx0 = w / 2 - gap * (n - 1) / 2;

    /* ── 0.5초 테스트 장면 ─────────────────────────── */
    const u = frac(t / (spec.loopSec ?? 0.5));
    const gone = ease(clamp(u / 0.30));
    const back = ease(clamp((u - 0.88) / 0.12));
    const boxA = clamp((1 - gone) + back) * pk;
    const own = ease(clamp((u - 0.36) / 0.30)) * (1 - ease(clamp((u - 0.80) / 0.08))) * pk;

    /* ── 연결선 : 다 같이 한 창고에서 꺼내 먹는다 ───── */
    if (boxA > 0.01) {
      ctx.save();
      ctx.globalAlpha = boxA * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 8]);
      ctx.lineDashOffset = -((t * 24) % 16);
      for (let i = 0; i < n; i++) {
        ctx.beginPath();
        ctx.moveTo(w / 2, BOX_CY + BOX_H / 2);
        ctx.lineTo(mx0 + i * gap, MARK_Y - MARK_R - 3);
        ctx.stroke();
      }
      ctx.restore();
    }

    /* ── 공용 창고 : 줄어들며 사라진다 ──────────────── */
    if (boxA > 0.01) {
      const sc = 0.42 + 0.58 * boxA;
      const bw = BOX_W * sc, bh = BOX_H * sc;
      ctx.save();
      ctx.globalAlpha = boxA;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, w / 2 - bw / 2, BOX_CY - bh / 2, bw, bh, 9); ctx.stroke();
      ctx.restore();
      if (spec.commonLabel) {
        ctext(spec.commonLabel, w / 2, BOX_CY + 5, 800, 13.5, tone('ink'), bw - 14, boxA);
      }
    }

    /* ── 각자에게 붙는 칸 ──────────────────────────── */
    if (own > 0.01) {
      ctx.save();
      ctx.globalAlpha = own;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      /* 🔴 **위에서** 내려온다. 아래에서 올라오게 하면 등장 첫 프레임의 바닥이
         주민 표식(201.5)과 겹친다 — 흰색과 강조색이 만나는 자리가 된다.
         뜻으로도 이쪽이 맞는다: 공용 창고가 있던 자리에서 각자에게 내려온다. */
      const cy = lerp(OWN_CY - 14, OWN_CY, own);
      for (let i = 0; i < n; i++) {
        roundRect(ctx, mx0 + i * gap - OWN_W / 2, cy - OWN_H / 2, OWN_W, OWN_H, 5);
        ctx.stroke();
      }
      clearShadow(ctx);
      ctx.restore();
      if (spec.ownLabel) ctext(spec.ownLabel, w / 2, 254, 900, 14, tone('accent'), w - 60, own);
    }

    /* ── 주민 넷 ───────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    for (let i = 0; i < n; i++) {
      ctx.beginPath(); ctx.arc(mx0 + i * gap, MARK_Y, MARK_R, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.restore();

    ctx.textAlign = 'left';
  }
};
