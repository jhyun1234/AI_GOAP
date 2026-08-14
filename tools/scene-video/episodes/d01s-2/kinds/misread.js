import {
  ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, disp, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* misread — 내가 화면을 어떻게 읽었나. 이 편에서 **틀린 쪽**을 그리는 유일한 샷이다.

   근거(원문 = `devlog/sessions/2026-08-11.md` 14:38 커밋 본문 · 사용자 육성):
   > "아 이 사람 주민 근처에 다가오면 적을 없애게 해뒀구나."
   → 화면에서 움직이는 것 = **표식 하나가 다가가고, 앞에 있던 것들이 닿기도 전에
     차례로 꺼진다.** 그리고 지나간 자리마다 가위표가 남는다.

   🔴 이것이 사실이 아니라 **내가 읽은 것**임을 화면이 스스로 말해야 한다 — 그래서
   ①표식이 적에 **닿지 않는다**(닿기 40px 전에 이미 꺼져 있다) ②마지막에 라벨
   「내가 읽은 것」이 뜬다. 라벨을 가려도 ①이 남아 「내가 본 것이지 부딪힌 것이
   아니다」가 읽힌다(ADR-V25-15).

   ⏱ 사건을 `cue` 가 아니라 **`since(0)`·`since(1)`** 위에 세웠다 — `sfx.delayMs` 와
   원점이 같아 **자막 길이가 실측에서 바뀌어도 소리와 그림이 안 어긋난다**(ep16s-5 선례).

   ── 걷기 좌표계 ──
     w  = ease(clamp((since(0) − 0.25) / 1.35))        → 0.25초에 출발, 1.60초에 도착
     vx = 60 + 232·w
     i번째 적(cx = 150·200·250)의 알파 = 1 − ease(clamp((vx − (cx − 70)) / 30))
       → 표식이 **70px 앞**에서 흐려지기 시작해 **40px 앞**에서 완전히 꺼진다.
     꺼지는 시각: cx150 = 0.37~0.54초 · cx200 = 0.66~0.83초 · cx250 = 0.95~1.12초.

   ── 겹침 감사 (선 반폭 1.5 · 흔들림 1.5 · GLOW blur 8 포함) ──
   축 A(강조색↔흰색):
     · 알파 0.05 가 남은 마지막 프레임에서 vx ≈ cx − 43.5 → 흰 표식 오른쪽 끝
       cx − 43.5 + 6 + 1.5 = cx − 36 / 강조색 삼각형 왼쪽 끝 cx − 15 − 1.5 = cx − 16.5
       → **19.5px**, GLOW 8 을 빼도 **11.5px**. (알파 0 이 된 뒤에는 겹칠 것이 없다.)
     · 도착한 흰 표식 왼쪽 끝 292 − 6 − 1.5 = 284.5 / 마지막 가위표 오른쪽 끝
       250 + 9 + 2 = 261 → **23.5px**, GLOW 를 빼면 **15.5px**.
     · 가위표 아래 끝 168 + 9 + 2 + 8(GLOW) = 187 / 라벨 글자 위 236 − 14 = 222
       → **35px**.
   축 B(흰색↔흰색): 흰 것은 「사람 표식」과 「라벨」 둘이고 세로로 **37.5px**
     (표식 아래 184.5 ↔ 라벨 글자 위 222) 떨어져 있다. 서로 다른 것을 가리키는
     흰 글자 쌍은 없다(라벨이 하나뿐).

   세로 예산(캔버스 307): 최저 = 라벨 밑선 약 240. 67px 남는다.
   가로(캔버스 352): 최좌 = 출발점 60 − 7.5 = 52.5 · 최우 = 도착점 292 + 7.5 = 299.5.

   계속 도는 것: 표식이 232px 를 1.35초에 → 5.7px/프레임(30fps). 도착 뒤에도
   `bob = 1.5·sin(8.2t)` 로 세로로 계속 흔들려 정적이 안 생긴다. */

const ENEMY = [150, 200, 250];
const BASE_Y = 182, TRI_H = 26, TRI_HW = 15, ROW_Y = 168;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    ctx.lineJoin = 'round';

    const s0 = Math.max(0, since(spec.walkCue ?? 0));
    const s1 = Math.max(0, since(spec.readCue ?? 1));
    const w = ease(clamp((s0 - 0.25) / 1.35));
    const vx = lerp(60, 292, w);
    const bob = 1.5 * Math.sin(t * 8.2);

    /* ── 앞에 서 있던 것들 — 닿기 전에 꺼진다 ── */
    ctx.save();
    ctx.strokeStyle = tone('fail');
    ctx.lineWidth = 3;
    for (const cx of ENEMY) {
      const a = 1 - ease(clamp((vx - (cx - 70)) / 30));
      if (a <= 0.01) continue;
      ctx.globalAlpha = a;
      setShadow(ctx, FAIL_GLOW, 8);
      ctx.beginPath();
      ctx.moveTo(cx, BASE_Y - TRI_H);
      ctx.lineTo(cx + TRI_HW, BASE_Y);
      ctx.lineTo(cx - TRI_HW, BASE_Y);
      ctx.closePath();
      ctx.stroke();
      clearShadow(ctx);
    }
    ctx.restore();

    /* ── 지나간 자리 = 가위표 ── */
    ctx.save();
    ctx.strokeStyle = tone('fail');
    ctx.lineWidth = 4;
    ctx.lineCap = 'round';
    ENEMY.forEach((cx, i) => {
      /* 🔴 문턱을 0.50 → 0.25 로 당겼다(검수 정정 7) — 걷기가 끝나는 since(0) 1.60 부터
         가위표가 뜨기까지 화면에 흰 캡슐 하나뿐인 구간이 1.7초였다. 정적 게이트는
         캡슐의 흔들림으로 통과했지만(최대 0.8s) **비어 보이는 것은 게이트 밖의 문제**다.
         🔑 걷기 매핑(1.35)은 일부러 안 건드렸다 — 그 값이 S1 sweep 의 검증된 정렬
         (사건 종료 1.12 ↔ 「없어졌어요」 개시 1.779)을 떠받치고 있다. */
      const k = ease(clamp((s1 - 0.25 - i * 0.22) / 0.30));
      if (k <= 0.02) return;
      const r = 9 * k;
      ctx.globalAlpha = k;
      setShadow(ctx, FAIL_GLOW, 8);
      ctx.beginPath();
      ctx.moveTo(cx - r, ROW_Y - r); ctx.lineTo(cx + r, ROW_Y + r);
      ctx.moveTo(cx + r, ROW_Y - r); ctx.lineTo(cx - r, ROW_Y + r);
      ctx.stroke();
      clearShadow(ctx);
    });
    ctx.restore();

    /* ── 다가가는 사람 ── */
    ctx.save();
    ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3;
    roundRect(ctx, vx - 6, ROW_Y - 15 + bob, 12, 30, 6);
    ctx.stroke();
    ctx.restore();

    /* ── 이건 화면이 아니라 내 해석이다 ── */
    const la = ease(clamp((s1 - 0.25) / 0.45));
    if (spec.readLabel && la > 0.02) {
      ctx.save();
      ctx.globalAlpha = la;
      ctx.textAlign = 'center';
      ctx.font = disp(800, 14);
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.readLabel, 176, 236);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
