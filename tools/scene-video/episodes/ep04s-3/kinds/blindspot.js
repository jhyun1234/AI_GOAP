import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* blindspot — 나가는 자리가 비어 있다.

   가로 축은 피로다. 주민 표식이 80 자리에 서서 좌우로만 떤다 — 표식이 **먼저** 있고,
   70 문턱과 거부 구간이 그 위에 그어진다(holeCue). 첫 자막이 "주민은 너무 지치면"으로
   시작하므로 순서도 그쪽이 맞고, 덕분에 첫 프레임이 축 하나로 비어 있지 않다.

   stuckCue 에서 축 아래에 **회복 화살표가 있어야 할 자리**를 만든다 — 점선 화살표 하나와
   그 위에 그어지는 ✕. 이 편의 문제는 "무언가가 잘못 붙어 있다"가 아니라 "붙어 있어야 할
   것이 없다"이므로, 없는 것을 자리로 먼저 보여 주고 fixCue 에서 **같은 좌표**를 실선 둘로
   채운다. 문제와 해결이 화면의 같은 자리를 쓰기 때문에 무엇이 없었는지가 눈에 남는다.
   두 화살표의 길이 차(34 : 62)가 곧 산책 -2 와 모닥불 -4 의 차이다 — 건물이 존재하는
   의미를 회복량으로도 나타낸 설계라, 길이 차이 자체가 그 뜻이다.

   permCue 는 그 상태가 영구가 된다는 판정이다. 위쪽에 한 줄이 서고, 표식의 떨림 폭이
   커지고, 거부 구간의 채움이 진해진다 — 셋 다 같은 pk 에서 갈라진다.

   🔴 축을 눕힌 이유는 "못 나간다 → 나간다"가 방향의 이야기이기 때문이다. 세로로 세우면
   회복이 '떨어짐'으로 읽혀 좋은 것이 나쁜 것처럼 보인다.

   🔴 축 오른쪽 끝에 '100' 을 적지 않는다. 원문에 피로 상한이 없다 — 눈금으로 적으면
   원문에 없는 수치를 화면이 주장하게 된다. 이 축의 숫자는 70 · 80 · 90 셋뿐이고 전부
   원문 6절에 실재한다.

   🔴 이 파일은 같은 글의 재분할로 이 폴더에 왔다(ep04s → ep04s-3). 옛 주석에 있던
   "이 편에서 유일한 가로 축이다 / 앞의 계기(포만감)는 세로였고 우선순위도 세로였다" 는
   **없어진 샷(buffer · rung60)을 근거로 든 문장**이라 통째로 지웠다. 이 편에는 계기도
   사다리도 없다.

   계속 도는 것 = 주민 표식. 갇힌 동안은 제자리 떨림, 풀린 뒤에는 왼쪽으로 흐르는 순환.
   첫 프레임부터 마지막 프레임까지 한 번도 멈추지 않는다. */

const SHAKE = 1.5;   // 갇힌 표식의 떨림 주기(초)
const FLOW = 4.2;    // 풀린 뒤 왼쪽으로 흐르는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const hk = ease(cue(spec.holeCue ?? 0, 0.15, 0.6));
    const sk = ease(cue(spec.stuckCue ?? 1, 0.15, 0.6));
    const pk = ease(cue(spec.permCue ?? 2, 0.15, 0.55));
    const fk = ease(cue(spec.fixCue ?? 3, 0.15, 0.62));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const x0 = 22, x1 = w - 22, ay = 148;
    const xOf = v => x0 + (v / 100) * (x1 - x0);
    const refuse = spec.refuseAt ?? 70;
    const z0 = (spec.zone && spec.zone[0]) ?? 70;
    const z1 = (spec.zone && spec.zone[1]) ?? 90;
    const at = spec.at ?? 80;
    const tx = xOf(refuse), zx0 = xOf(z0), zx1 = xOf(z1);

    const fixed = clamp(fk * 1.7);          // 개정이 얼마나 들어왔나 — 옛 상태를 걷어내는 데 쓴다

    /* ── 축 ───────────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(x0, ay); ctx.lineTo(x1, ay); ctx.stroke();

    ctx.textAlign = 'left';
    ctx.font = disp(800, 11); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.axis || '피로', x0, ay + 30);

    /* ── M1-C 리뷰 칩 ─────────────────────────────── */
    /* S2 relative 와 같은 자리·같은 모양이다. 두 사건을 잇는 것이 이 이름 하나뿐이라
       같은 도장을 두 샷에 찍는다. */
    if (spec.review) {
      const k = clamp(sk * 2.2);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        const fs = fit(spec.review, 800, 12, w - 60, 9);
        ctx.font = disp(800, fs);
        const tw = ctx.measureText(spec.review).width;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, 8, 14, tw + 20, 26, 3); ctx.stroke();
        ctx.textAlign = 'left';
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.review, 18, 32);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 거부 문턱과 거부 구간 ────────────────────── */
    {
      const k = clamp(hk * 1.8);
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(tx, ay - 26); ctx.lineTo(tx, ay + 16); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = mono(700, 15); ctx.fillStyle = tone('ink');
      ctx.fillText(String(refuse), tx, ay - 32);

      // 거부 구간 — permCue 에서 채움이 진해진다(영구가 된다)
      ctx.globalAlpha = k * (0.12 + 0.14 * pk);
      ctx.fillStyle = tone('ink');
      ctx.fillRect(tx, ay - 12, x1 - tx, 24);

      /* 라벨은 오른쪽 정렬이다. 가운데 정렬로 두면 폭이 커질 때 오른쪽 끝이 캔버스
         가장자리 띠(바깥 2열)에 닿는다 — 좌표로 풀어 x 350 까지 나가는 것을 확인하고 바꿨다.
         y 는 ay−50 이다. ay−46 이면 글자 아랫변이 70·90 눈금(ay−32)의 윗변에 붙는다. */
      ctx.globalAlpha = k;
      ctx.textAlign = 'right';
      const s = spec.refuseLabel || '';
      const fs = fit(s, 800, 11.5, 180, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(s, x1, ay - 50);
      ctx.globalAlpha = 1;
    }

    /* ── 사각지대 ───────────────────────────────────
       🔴 2026-08-04 검수 3-B 반영 — 게이트를 sk(stuckCue = 자막 1)에서 pk(permCue = 자막 2)로
       옮겼다. 이 블록이 그리는 것은 점선 상자 · 오른쪽 눈금 「90」 · 「사각지대」 글자 셋인데,
       그 셋을 부르는 것은 자막 2("피로 70에서 90, 거부가 영구가 되는 거죠")다. sk 에 물려
       있던 동안에는 **자막보다 3.6초 먼저** 다 서 있었다(가이드 한도 ±0.67초).
       🔑 자막 1("아무리 쉬어도 피로가 안 풀리더라고요")이 부르는 것은 아래의 ✕ 와 점선
       화살표이고 그쪽은 sk 에 그대로 남는다 — 즉 두 자막이 각자 자기 그림을 갖는다.
       thud(delayMs 810)는 그 ✕ 에 물려 있으므로 이 이동에 영향받지 않는다. */
    if (pk > 0.02) {
      const k = clamp(pk * 1.9);
      ctx.globalAlpha = k;
      ctx.lineWidth = 4;
      ctx.strokeStyle = tone('accent');
      // 개정이 반쯤 들어오면 점선이 실선이 된다 — 구간이 규칙으로 메워졌다는 뜻이다.
      ctx.setLineDash(fk > 0.5 ? [] : [6, 6]);
      roundRect(ctx, zx0, ay - 16, zx1 - zx0, 32, 3); ctx.stroke();
      ctx.setLineDash([]);

      // 오른쪽 끝 눈금 90
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(zx1, ay - 8); ctx.lineTo(zx1, ay + 8); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText(String(z1), zx1, ay - 32);

      // "사각지대" — 개정이 들어오면 사라진다. 더 이상 사각지대가 아니기 때문이다.
      const lk = k * (1 - fixed);
      if (lk > 0.02) {
        ctx.globalAlpha = lk;
        ctx.textAlign = 'center';
        const s = spec.zoneLabel || '사각지대';
        const fs = fit(s, 900, 13, 100, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(s, (zx0 + zx1) / 2, ay + 40);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 영구가 된다 ──────────────────────────────── */
    if (pk > 0.05 && spec.permLabel) {
      const k = clamp(pk * 1.8) * (1 - fixed * 0.8);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.permLabel, 800, 12.5, w - 24, 8.5);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.permLabel, w / 2, 66);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 회복이 붙을 자리 ─────────────────────────
       stuckCue 에서는 비어 있다(점선 화살표 + ✕), fixCue 에서 같은 좌표가 채워진다. */
    const r0 = 206, r1 = 240;
    const ax = xOf(86);

    const emptyK = clamp(sk * 1.9) * (1 - fixed);
    if (emptyK > 0.02) {
      ctx.globalAlpha = emptyK;

      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([5, 5]);
      ctx.beginPath(); ctx.moveTo(ax, r0); ctx.lineTo(ax - 62, r0); ctx.stroke();
      ctx.setLineDash([]);
      ctx.beginPath();
      ctx.moveTo(ax - 62, r0); ctx.lineTo(ax - 55, r0 - 5);
      ctx.moveTo(ax - 62, r0); ctx.lineTo(ax - 55, r0 + 5);
      ctx.stroke();

      // ✕ — 이 샷에서 유일하게 "그어지는" 사건이고 효과음(thud)이 여기에 물려 있다.
      const g = ease(clamp(sk / 0.35));
      if (g > 0.02) {
        const cx = ax - 31, cy = r0, s = 11 * g;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(cx - s, cy - s); ctx.lineTo(cx + s, cy + s);
        ctx.moveTo(cx + s, cy - s); ctx.lineTo(cx - s, cy + s);
        ctx.stroke();
      }

      ctx.textAlign = 'right';
      const s2 = spec.zeroLabel || '';
      const fs = fit(s2, 800, 12, ax - 74 - 8, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(s2, ax - 74, r0 + 5);
      ctx.globalAlpha = 1;
    }

    /* ── 회복이 열린다 ────────────────────────────── */
    const rec = spec.recover || [];
    rec.slice(0, 2).forEach((r, i) => {
      const born = clamp(fk * 2.2 - i * 0.55);
      if (born < 0.02) return;
      const y = i === 0 ? r0 : r1;
      const len = (i === 0 ? 34 : 62) * ease(clamp(born));

      ctx.globalAlpha = clamp(born * 1.6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(ax, y); ctx.lineTo(ax - len, y);
      ctx.moveTo(ax - len, y); ctx.lineTo(ax - len + 7, y - 5);
      ctx.moveTo(ax - len, y); ctx.lineTo(ax - len + 7, y + 5);
      ctx.stroke();

      ctx.textAlign = 'right';
      const nm = r.name || '';
      const fs = fit(nm, 800, 12, ax - 74 - 8, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(nm, ax - 74, y + 5);

      ctx.textAlign = 'left';
      ctx.font = mono(700, 15); ctx.fillStyle = tone('accent');
      ctx.fillText(String(r.d ?? ''), ax + 10, y + 5);
      ctx.globalAlpha = 1;
    });

    /* ── 주민 표식 — 첫 프레임부터 끝까지 돈다 ───── */
    {
      const amp = 4 + 4 * pk;                                  // 갇힘이 깊어질수록 크게 떤다
      const stuckX = xOf(at) + Math.sin(frac(t / SHAKE) * Math.PI * 2) * amp;
      const flowX = xOf(lerp(86, 32, frac(t / FLOW)));
      const mx = lerp(stuckX, flowX, ease(fk));

      setShadow(ctx, GLOW, 10, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      ctx.beginPath(); ctx.arc(mx, ay, 10, 0, Math.PI * 2); ctx.stroke();
      clearShadow(ctx);

      // 80 — 원문이 든 구체 사례("피로 80 상태로 모닥불 곁에서 계속 쉬면서도").
      // 표식이 흔들려도 눈금은 제자리에 둔다. 흔들리는 숫자는 값이 아니라 잡음이다.
      const nk = clamp(sk * 1.8) * (1 - fixed);
      if (nk > 0.02) {
        ctx.globalAlpha = nk;
        ctx.textAlign = 'center';
        ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
        ctx.fillText(String(at), xOf(at), ay - 22);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 개정 ─────────────────────────────────────── */
    if (fk > 0.55 && spec.adr) {
      const k = clamp((fk - 0.55) / 0.45);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.adr, 800, 11.5, w - 16, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.adr, 8, 288);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
