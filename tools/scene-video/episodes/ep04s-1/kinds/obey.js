import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* obey — 같은 명령이 두 게임에서 다르게 끝난다. **이 편의 첫 샷이다.**

   위에 촌장의 명령 상자가 하나 있고 거기서 통로 둘이 아래로 갈라진다.
   왼쪽은 보통 게임이다 — 명령 표식이 끊임없이 떨어져 유닛에 그대로 닿고, 닿을 때마다
   아래의 '복종'이 다시 밝아진다. 이건 사건이 아니라 성질이라 자막에 안 물리고 t 로 돈다.
   오른쪽은 이 게임이다 — 같은 표식이 주민 앞에서 막에 부딪혀 되올라간다(refuseCue).
   이건 사건이라 그것을 부르는 자막에 물렸다.

   🔴 왜 세로인가. 1차본(ep04s oneline)은 같은 대비를 가로 두 줄로 그렸고 그것을 **마지막
   샷**에서 했다. 재분할에서 이 대비가 0초로 올라오면서(제목 이행 게이트) 구도를 다시 잡았다 —
   명령이 위에서 내려온다는 것 자체가 촌장과 주민의 관계이고, 덤으로 마지막 샷 verdict 의
   가로 왕복과 방향이 갈려 같은 사건을 두 번 그려도 같은 그림이 되지 않는다.

   🔴 화면에는 'RTS 유닛'이라고 적고 나레이션은 '게임 속 유닛'이라고 말한다. 약어는 눈으로는
   넘어가지만 귀로는 파싱이 실패한다(ep04s 가 '알티에스' 낭독을 없앤 처리를 이어받았다).

   계속 도는 것 = 왼쪽 통로에 1.7초 주기로 떨어지는 표식 둘(0.5 주기 어긋나 있다) +
   두 통로의 점선 흐름 + 표식이 닿는 순간 다시 밝아지는 유닛 테두리와 '복종'. */

const FALL = 1.7;    // 왼쪽에 명령이 다시 떨어지는 주기(초)
const FLOW = 1.6;    // 통로 점선이 한 칸 흐르는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const ok = ease(cue(spec.obeyCue ?? 0, 0.15, 0.55));
    const mk = ease(cue(spec.refuseCue ?? 1, 0.15, 0.9));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const LX = 88, RX = w - 88;
    const TOP = 52, BOT = 172, CY = 192;
    const LAB = 148;                       // 라벨 최대 폭 — 좌우 어느 쪽도 화면 밖으로 안 나간다

    /* ── 명령의 출처 ──────────────────────────────── */
    {
      const k = clamp(ok * 2.2);
      const cw = 132, ch = 26, cx = w / 2 - cw / 2;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, cx, 8, cw, ch, 3); ctx.stroke();
      ctx.textAlign = 'center';
      const s = spec.orderLabel || '촌장의 명령';
      const fs = fit(s, 800, 12.5, cw - 14);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(s, w / 2, 26);
      ctx.globalAlpha = 1;
    }

    /* ── 갈라지는 두 통로 ─────────────────────────── */
    {
      ctx.globalAlpha = clamp(ok * 2);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(w / 2, 34); ctx.lineTo(w / 2, 44);
      ctx.moveTo(LX, 44); ctx.lineTo(RX, 44);
      ctx.stroke();

      ctx.setLineDash([6, 8]);
      ctx.lineDashOffset = -frac(t / FLOW) * 14;
      ctx.beginPath();
      ctx.moveTo(LX, 44); ctx.lineTo(LX, BOT);
      ctx.stroke();
      if (mk > 0.02) {
        ctx.beginPath();
        ctx.moveTo(RX, 44); ctx.lineTo(RX, BOT);
        ctx.stroke();
      } else {
        ctx.setLineDash([]);
        ctx.beginPath();
        ctx.moveTo(RX, 44); ctx.lineTo(RX, BOT);
        ctx.stroke();
      }
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 왼쪽 — 언제나 닿는다 ─────────────────────── */
    /* 표식 둘이 반 주기 어긋나 떨어진다. 닿는 순간(u > 0.86)을 hit 로 모아 두고
       유닛 테두리와 '복종'을 그만큼 다시 밝힌다 — 반복이 곧 이 열의 뜻이다. */
    let hit = 0;
    for (let i = 0; i < 2; i++) {
      const u = frac(t / FALL + i * 0.5);
      hit = Math.max(hit, clamp((u - 0.86) / 0.14));
      const my = lerp(TOP, BOT, u);
      ctx.globalAlpha = clamp(ok * 2) * (0.3 + 0.7 * Math.sin(Math.PI * u));
      ctx.fillStyle = tone('ink');
      ctx.fillRect(LX - 5.5, my - 3, 11, 6);
      ctx.globalAlpha = 1;
    }
    {
      const k = clamp(ok * 1.8);
      ctx.globalAlpha = k;
      ctx.lineWidth = 3 + 1.5 * hit;
      ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.arc(LX, CY, 17, 0, Math.PI * 2); ctx.stroke();

      ctx.textAlign = 'center';
      const who = spec.rts?.who || '';
      ctx.font = disp(700, fit(who, 700, 10.5, LAB, 8));
      ctx.fillStyle = tone('sub');
      ctx.fillText(who, LX, 228);

      const act = spec.rts?.act || '';
      ctx.globalAlpha = k * (0.72 + 0.28 * hit);
      ctx.font = disp(900, fit(act, 900, 21, LAB, 13));
      ctx.fillStyle = tone('ink');
      ctx.fillText(act, LX, 258);

      const note = spec.rts?.note || '';
      if (note) {
        ctx.globalAlpha = k * 0.9;
        ctx.font = disp(700, fit(note, 700, 9.5, LAB, 7.5));
        ctx.fillStyle = tone('sub');
        ctx.fillText(note, LX, 276);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 오른쪽 — 여기서 멈춘다 ───────────────────── */
    {
      const k = clamp(ok * 1.8);
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.arc(RX, CY, 19, 0, Math.PI * 2); ctx.stroke();

      ctx.textAlign = 'center';
      const who = spec.mine?.who || '';
      ctx.font = disp(700, fit(who, 700, 10.5, LAB, 8));
      ctx.fillStyle = tone('sub');
      ctx.fillText(who, RX, 228);
      ctx.globalAlpha = 1;
    }

    if (mk > 0.01) {
      /* 표식이 내려오다 막에 닿고(travel=1, mk=0.82) 되올라간다.
         🔊 이 자리에 thud 가 붙는다 — 유도는 씬 JSON 의 sfx.why 에 있다. */
      const travel = ease(clamp(mk / 0.82));
      const back = ease(clamp((mk - 0.86) / 0.14));
      const my = lerp(TOP, 150, travel) - 40 * back;

      const bk = clamp((mk - 0.45) / 0.3);
      if (bk > 0.02) {
        ctx.globalAlpha = bk;
        setShadow(ctx, GLOW, 12, 0);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.arc(RX, CY, 30, -Math.PI / 2 - 0.85 * bk, -Math.PI / 2 + 0.85 * bk);
        ctx.stroke();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }

      ctx.globalAlpha = clamp(mk * 4) * (1 - 0.4 * back);
      ctx.fillStyle = tone('ink');
      ctx.fillRect(RX - 5.5, my - 3, 11, 6);
      ctx.globalAlpha = 1;

      const wk = clamp((mk - 0.84) / 0.16);
      if (wk > 0.02) {
        ctx.globalAlpha = wk;
        ctx.textAlign = 'center';
        const act = spec.mine?.act || '';
        ctx.font = disp(900, fit(act, 900, 21, LAB, 13));
        ctx.fillStyle = tone('accent');
        ctx.fillText(act, RX, 258);

        const note = spec.mine?.note || '';
        if (note) {
          ctx.globalAlpha = wk * 0.9;
          ctx.font = disp(700, fit(note, 700, 9.5, LAB, 7.5));
          ctx.fillStyle = tone('sub');
          ctx.fillText(note, RX, 276);
        }
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
