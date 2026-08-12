import {
  disp, ease, easeOut, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, PALETTE, depthGrad, camAt
} from '../../../engine/lib.js';

/* namefade — 훅. **내가 이름을 아는 주민 하나**가 무덤 무리로 걸어 들어가 가라앉고,
   그 자리에 비석이 솟으면 이름표가 꺼진다. 남는 것은 구별이 안 되는 일곱이다.

   원문 근거(`2026-08-04-unity-goap-m13-events-and-traces.html`):
   > "Day 45에 주민 일곱 명이 죽었습니다."
   > "죽은 자리에는 회색 원이 하나씩 남습니다. 무덤입니다. 그런데 그 원에는 아무것도 안
   >  적혀 있습니다. 화면을 가득 채운 회색 원 일곱 개를 보면서, 저는 그중 누가 제가
   >  아침에 지켜보던 그 주민인지 알 수 없었습니다."

   ── 2026-08-12 개정 v4 (그림 어휘 교체) ────────────────────────────
   🔴 **루프를 없애고 한 번의 사건으로 바꿨다.** v3 까지는 2.6초 루프를 두 바퀴 돌렸다.
      루프는 「이런 일이 계속 일어난다」를 말하려 한 것이었는데, 실제로는 **같은 것을 두 번
      보는 것**이라 두 번째 바퀴에서 눈이 화면을 떠난다. 훅에서 그건 치명적이다.
      지금은 5.0초에 걸쳐 한 이야기가 처음부터 끝까지 간다 — 걷는다 → 가라앉는다 →
      비석이 솟는다 → 이름표가 꺼진다 → 일곱이 똑같아진다.
   🔴 **형상을 그 물건처럼 바꿨다.** 동그라미 → 비석, 캡슐 → 사람 실루엣, 색 막대 → **이름표**
      (사람 머리 위에 달린 판). 이름표가 사람에 붙어 있어야 「그의 이름」으로 읽힌다.
   🔴 **크다.** 사람 키 104(캔버스 34%), 앞 비석 높이 120. 옛 표식은 52 · 동그라미 지름 40.

   🔴 이 편의 색 규약을 여기서 세운다:
      **강조색 = 이름, 또는 이름이 들어갈 자리 / 흰색 = 그 밖의 전부(사람·비석·수).**
      그래서 이 샷은 강조색이 **켜져 있다가 꺼지는** 유일한 샷이고, 본문 두 샷에서는 한 번도
      안 켜지며, carvename 에서 처음으로 다시 채워진다.
   🔴 화면에 올린 글자는 「Day 45」 하나뿐이다. 「Day」는 게임이 무덤에 실제로 찍는 표기
      (`{shortName} · Day {day}`)의 인용이라 영어로 둔다(ADR-V-10 예외 ①). 45 는 원문
      문장의 값이고 이 편 소유다.
   🔴 무덤은 **일곱**이다(원문 수치). 뒤 여섯 + 주인공이 가라앉은 자리 하나.

   ⏱ 전부 `cue(0)` 위에 세웠다(훅 자막 하나뿐인 샷이다). 효과음은 안 달았다 —
      훅에서 소리를 쓰면 이 편의 두 마디(도장·새김)가 흐려진다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색은 이름표(y 92~110)뿐이고 사람 머리 꼭대기는 y 124 다 → **14px** 뜬다.
            이름표가 비석의 파인 홈으로 내려앉는 마지막 0.3초에는 홈(배경색) 안에 들어가는데,
            겹치는 상대가 자기 홈의 벽이라 의도한 쌍이다.
     축 B · 흰 글자는 「Day 45」 하나뿐이다(y 26, 왼쪽 정렬 · 아래 도형과 60px 넘게 뜬다).
            흰 실루엣끼리는 뒤 비석이 서로 8px 간격인데 「늘어서 있다」를 만드는 의도한 쌍이다.
   세로 예산(캔버스 307): 최저 = 지평선 262(45px 남음). 최고 = 이름표 글로우 84.
   가로: 뒤 비석 줄 왼쪽 18 / 오른쪽 334. 바깥 2px 띠에서 좌우 16 뜬다.
     확대(1.45 · focus 주인공 자리)에서는 양옆 비석이 밖으로 나간다 → `checks.edge` 선언.

   계속 도는 것: **없다.** 사건이 끝난 뒤 남는 것은 이름 자리의 커서 깜빡임뿐이다. */

const GROUND = 262;
const FIG = { h: 122, w: 50 };
const START_X = 54, SLOT_X = 176;                  // 걸어가는 거리
const TAG = { w: 52, h: 18, y: 262 - 122 - 26 };   // 머리 26px 위
const MAIN = { w: 104, h: 142 };                   // 주인공 자리에 솟는 비석
const SLOT = { w: 68, h: 24, dy: 66 };

/* 뒤 여섯 — [x, 크기배율]. 주인공 자리(176)를 비워 두고 좌우로 셋씩. */
const BACK = [[24, 0.40], [74, 0.50], [122, 0.58], [230, 0.58], [278, 0.50], [328, 0.40]];

function stone(ctx, x, y, w, h) {
  const r = w / 2;
  ctx.beginPath();
  ctx.moveTo(x - r, y); ctx.lineTo(x - r, y - h + r);
  ctx.arc(x, y - h + r, r, Math.PI, 0);
  ctx.lineTo(x + r, y); ctx.closePath();
}

function person(ctx, x, y, hgt, wid) {
  const hr = wid * 0.26;
  const bodyTop = y - hgt + hr * 2 + 1;
  ctx.beginPath(); ctx.arc(x, y - hgt + hr, hr, 0, Math.PI * 2); ctx.closePath(); ctx.fill();
  ctx.beginPath();
  ctx.moveTo(x - wid * 0.50, bodyTop + 10);
  ctx.quadraticCurveTo(x, bodyTop - 9, x + wid * 0.50, bodyTop + 10);
  ctx.lineTo(x + wid * 0.40, y); ctx.lineTo(x - wid * 0.40, y);
  ctx.closePath(); ctx.fill();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);
    ctx.textBaseline = 'alphabetic';

    const st = ease(cue(0, 0.15, 0.20));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* 한 번의 사건 — 훅 자막(4.7초) 위에 펼친다. 되풀이하지 않는다. */
    const walk = easeOut(clamp((t - 0.45) / 1.05));      // 걸어 들어간다
    const sink = ease(clamp((t - 1.60) / 0.55));         // 가라앉는다
    const rise = clamp((t - 1.85) / 0.45);               // 비석이 솟는다
    const tagOff = ease(clamp((t - 2.55) / 0.50));       // 이름표가 꺼진다
    const cursor = clamp((t - 3.10) / 0.30);             // 빈 이름 자리만 남는다

    if (spec.dayLabel) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.dayLabel, 18, 26);
      ctx.restore();
    }

    /* ── 지평선 ───────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st * 0.28;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(10, GROUND); ctx.lineTo(342, GROUND); ctx.stroke();
    ctx.restore();

    /* ── 이미 이름을 잃은 여섯 ─────────────────────── */
    ctx.save();
    ctx.globalAlpha = st * 0.72;
    ctx.fillStyle = tone('ink');
    for (const [x, sc] of BACK) {
      stone(ctx, x, GROUND, MAIN.w * sc, MAIN.h * sc);
      ctx.fill();
    }
    ctx.restore();

    const fx = lerp(START_X, SLOT_X, walk);

    /* ── 일곱 번째 — 걸어와 가라앉는다 ─────────────── */
    if (sink < 1) {
      ctx.save();
      ctx.globalAlpha = st;
      ctx.beginPath(); ctx.rect(0, 0, 352, GROUND); ctx.clip();
      ctx.fillStyle = tone('ink');
      person(ctx, fx, GROUND + FIG.h * sink, FIG.h, FIG.w);
      ctx.restore();
    }

    /* ── 그 자리에 비석이 솟는다 ───────────────────── */
    const sy = GROUND - MAIN.h + SLOT.dy;
    if (rise > 0.01) {
      const k = clamp(easeOut(rise) + 0.16 * Math.sin(Math.PI * clamp(rise)) * (1 - clamp(rise)) * 2);
      ctx.save();
      ctx.globalAlpha = st;
      ctx.beginPath(); ctx.rect(0, 0, 352, GROUND); ctx.clip();
      const yb = GROUND + MAIN.h * (1 - k);
      ctx.fillStyle = depthGrad(ctx, SLOT_X - MAIN.w / 2, yb - MAIN.h, SLOT_X + MAIN.w / 2, yb, 'ink');
      stone(ctx, SLOT_X, yb, MAIN.w, MAIN.h);
      ctx.fill();
      ctx.globalCompositeOperation = 'destination-out';
      ctx.fillStyle = '#000';
      ctx.fillRect(SLOT_X - SLOT.w / 2, yb - MAIN.h + SLOT.dy, SLOT.w, SLOT.h);
      ctx.restore();
    }

    /* ── 이름표 — 이 편에서 강조색이 뜻하는 것. 무덤이 되면 꺼진다 ── */
    const ta = st * (1 - tagOff);
    if (ta > 0.01) {
      const ty = lerp(TAG.y, sy + 2, ease(clamp((t - 2.30) / 0.45)));
      ctx.save();
      ctx.globalAlpha = ta;
      setShadow(ctx, GLOW, 10);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(fx - TAG.w / 2, ty, TAG.w, TAG.h);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 커서 — 쓸 자리는 남았는데 아무도 안 쓴다 ──── */
    if (cursor > 0.01) {
      const on = ((t - 3.10) % 1.1) < 0.6;
      if (on) {
        ctx.save();
        ctx.globalAlpha = st * cursor * 0.9;
        setShadow(ctx, GLOW, 8);
        ctx.fillStyle = tone('accent');
        ctx.fillRect(SLOT_X - 2.5, sy + 4, 5, SLOT.h - 8);
        clearShadow(ctx);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
