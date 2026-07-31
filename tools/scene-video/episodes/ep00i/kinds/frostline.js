import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* frostline — 이 편에서 유일하게 위에서 아래로 내려오는 것.

   나머지 여덟 그림은 전부 아래에서 위로, 안에서 밖으로 뻗는다. 겨울만 반대다 —
   그게 이 편의 뜻이다. 마을이 스스로 자라는 유일한 힘이고, 그걸 위에서 누르는 유일한 것이
   계절이다.

   ADR-M6-2 / CLAUDE.md 게임 건강 기준: 겨울 채집 봉쇄(ForageFrozen)는 **야생 채집만** 막고
   밭과 저장분은 안 막는다("겨울 생명줄"). 그래서 서리선은 화면 중간에서 멎는다 —
   전부 얼리는 그림을 그리면 규칙을 잘못 옮긴 것이 된다.

   계속 도는 것 = 서리선 위를 가로질러 흐르는 알갱이(위치 이동).

   🔴 야생 열매 다섯은 밀도이지 게임의 노드 수가 아니다. 개수 라벨을 안 붙인다. */

const DRIFT = 2.9;
const LINE_TOP = 26, LINE_BOT = 152;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    const dk = ease(cue(spec.dropCue ?? 0, 0.15, 0.6));
    const fk = ease(cue(spec.freezeCue ?? 1, 0.12, 0.6));
    const sk = ease(cue(spec.surviveCue ?? 2, 0.12, 0.55));

    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'center';

    const lineY = lerp(LINE_TOP, LINE_BOT, dk);

    /* ── 야생 열매 ─────────────────────────────── */
    const nWild = spec.wild ?? 5;
    ctx.font = disp(700, 11);
    ctx.fillStyle = tone('sub');
    ctx.textAlign = 'left';
    ctx.fillText(spec.wildLabel || '야생 열매', 16, 76);
    ctx.textAlign = 'center';

    for (let i = 0; i < nWild; i++) {
      const x = 46 + i * 65;
      const y = 104 + (i % 2) * 26;
      const frozen = lineY > y - 6 && fk > 0.05;
      const c = frozen ? tone('track') : tone('ink');

      ctx.lineWidth = 4; ctx.strokeStyle = c; ctx.fillStyle = '#000';
      ctx.beginPath(); ctx.arc(x, y, 12, 0, Math.PI * 2); ctx.fill(); ctx.stroke();

      if (frozen) {
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.globalAlpha = 0.85;
        ctx.beginPath();
        ctx.moveTo(x - 8, y - 8); ctx.lineTo(x + 8, y + 8);
        ctx.moveTo(x + 8, y - 8); ctx.lineTo(x - 8, y + 8);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    // 얼어붙음 표식 — 서리선이 열매를 다 지난 뒤
    if (fk > 0.55) {
      ctx.globalAlpha = clamp((fk - 0.55) / 0.35);
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 12);
      ctx.textAlign = 'right';
      ctx.fillText(spec.frozenMark || '얼어붙음', w - 16, 76);
      ctx.textAlign = 'center';
      ctx.globalAlpha = 1;
    }

    /* ── 서리선 ─────────────────────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5;
    ctx.globalAlpha = lerp(0.4, 1, dk);
    // 🔴 가장자리에 닿지 않게 12px 씩 물린다. 폭을 꽉 채우면 좌우 잘림 점검이
    //    "샷의 100% 동안 가장자리에 닿아 있다" 로 읽어 실패시킨다(한도 25%).
    ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(12, lineY); ctx.lineTo(w - 12, lineY); ctx.stroke();
    ctx.lineCap = 'butt';
    ctx.globalAlpha = 1;

    // 겨울 이름표 — 선의 왼쪽 위에 붙어 함께 내려온다
    {
      ctx.font = disp(900, 15);
      const label = spec.frostLabel || '겨울';
      const tw = ctx.measureText(label).width;
      ctx.fillStyle = '#000';
      roundRect(ctx, 16, lineY - 26, tw + 18, 22, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, 16, lineY - 26, tw + 18, 22, 5); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('ink');
      ctx.fillText(label, 25, lineY - 9);
      ctx.textAlign = 'center';
    }

    // 계속 도는 것 : 선 위를 가로지르는 알갱이 셋
    for (let i = 0; i < 3; i++) {
      const u = frac(t / DRIFT + i / 3);
      const x = lerp(14, w - 14, u);
      ctx.fillStyle = tone('sub');
      ctx.globalAlpha = 0.6;
      ctx.beginPath(); ctx.arc(x, lineY - 7 + i * 5, 3, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 선 아래 : 살아남는 둘 ─────────────────── */
    const survive = spec.survive || [];
    const bw = 132, bh = 56, by = 208;
    const xs = [w / 2 - bw - 10, w / 2 + 10];

    survive.forEach((s, i) => {
      const k = clamp((sk - i * 0.16) / 0.55);
      const on = k > 0.05;
      const x = xs[i];
      ctx.lineWidth = 4;
      // 🔴 켜지기 전 테두리를 track 에서 sub 로 올렸다. track(알파 0.12)으로 두니 두 상자가
      //    "아직 안 켜진 것" 이 아니라 "죽은 것" 으로 보였다(스틸 47s·50s).
      //    이 둘은 얼어붙은 열매와 달리 처음부터 살아 있는 것이다.
      ctx.strokeStyle = on ? tone('accent') : tone('sub');
      ctx.fillStyle = '#000';
      if (on) setShadow(ctx, GLOW, lerp(0, 14, k), 0);
      roundRect(ctx, x, by, bw, bh, 8);
      ctx.fill(); ctx.stroke();
      clearShadow(ctx);

      ctx.fillStyle = on ? tone('accent') : tone('sub');
      ctx.font = disp(900, 17);
      ctx.fillText(s, x + bw / 2, by + bh / 2 + 7);
    });

    ctx.textAlign = 'left';
  }
};
