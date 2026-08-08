import {
  disp, mono, ease, easeOut, clamp, lerp, frac, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from './lib.js';

/* intro-brand — 시리즈 공용 인트로 (ADR-V25-11, 2026-08-08 사용자 지시).

   🔴 이 파일은 「그림은 회차가 소유한다」(engine.js 규약 2)의 **명시된 예외**다.
   그 규약은 본문 그림의 획일화를 막는 것이고, 인트로는 반대로 **매회 같아야
   브랜딩이 된다**(아웃트로 카드와 같은 지위). 회차는 kinds/intro.js 에서
   `export { default } from '../../../engine/intro-brand.js';` 한 줄로 재수출한다 —
   check.mjs 의 `kind 파일 존재` 가 회차 폴더를 보므로 파일은 회차에 있어야 한다.

   무엇을 그리나 (사용자 지시: "Claude Code가 스스로 코딩하고 게임 위치를 잡는
   애니메이션"): 위에 터미널 창이 코드를 한 자씩 치고, 명령이 끝날 때마다 아래
   미니 맵에 커서가 날아가 나무 → 집 → 주민 둘을 차례로 배치한다. 주민 하나는
   ^-^ 표정을 단다 — 표정 문자 도입(2026-08-08)의 첫 사용처.

   타이밍: 전부 인트로 자막 한 줄의 cue(0) 에 물린다. t 로 도는 것은 사건이 아닌
   것뿐이다(커서 깜빡임 · 주민 흔들림 · 점선 흐름) — 문턱 없음. */

const LINES = ['$ claude code', '> build my village', 'placing...'];
const BOB = 2.3;          // 주민이 제자리에서 흔들리는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const k = ease(cue(spec.introCue ?? 0, 0.1, 0.92));

    /* ── 터미널 창 (위 40%) ─────────────────────────── */
    const TX = 16, TY = 12, TW = w - 32, TH = h * 0.36;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, TX, TY, TW, TH, 8); ctx.stroke();
    // 타이틀 점 셋
    for (let i = 0; i < 3; i++) {
      ctx.fillStyle = i === 2 ? tone('accent') : tone('track');
      ctx.beginPath(); ctx.arc(TX + 16 + i * 14, TY + 15, 3.5, 0, 7); ctx.fill();
    }

    /* 코드 타이핑 — k 0~0.55 에 세 줄이 순서대로 찍힌다 */
    const typed = clamp(k / 0.55);
    const totalCh = LINES.reduce((a, l) => a + l.length, 0);
    let budget = Math.round(typed * totalCh);
    ctx.textAlign = 'left';
    LINES.forEach((line, i) => {
      const n = Math.max(0, Math.min(line.length, budget)); budget -= line.length;
      if (!n) return;
      ctx.font = mono(700, 12.5);
      ctx.fillStyle = i === 0 ? tone('ink') : tone('sub');
      ctx.fillText(line.slice(0, n), TX + 14, TY + 40 + i * 19);
      // 캐럿 — 지금 치는 줄 끝에서 깜빡인다 (주기적 · 사건 아님)
      if (n < line.length || (i === LINES.length - 1 && frac(t / 0.9) < 0.55)) {
        const cw = ctx.measureText(line.slice(0, n)).width;
        ctx.fillStyle = tone('accent');
        ctx.fillRect(TX + 15 + cw, TY + 30 + i * 19, 7, 12);
      }
    });

    /* ── 미니 맵 (아래) ────────────────────────────── */
    const MY = TY + TH + 18, MH = h - MY - 14;
    const ground = MY + MH - 12;
    // 바닥 레일 — 계속 흐르는 점선
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([7, 9]); ctx.lineDashOffset = -t * 12;
    ctx.beginPath(); ctx.moveTo(TX, ground); ctx.lineTo(TX + TW, ground); ctx.stroke();
    ctx.setLineDash([]);

    /* 배치물 넷 — 나무 · 집 · 주민 둘. 각자 k 문턱에서 spring 으로 앉는다 */
    const items = [
      { at: 0.34, x: w * 0.22, draw: drawTree },
      { at: 0.52, x: w * 0.50, draw: drawHouse },
      { at: 0.72, x: w * 0.74, draw: (c, x, y, s) => drawVillager(c, x, y, s, '^-^', t) },
      { at: 0.86, x: w * 0.88, draw: (c, x, y, s) => drawVillager(c, x, y, s, null, t + 0.7) }
    ];
    for (const it of items) {
      const ik = clamp((k - it.at) / 0.13);
      if (ik <= 0) continue;
      const s = spring(ik);
      it.draw(ctx, it.x, ground, s);
    }

    /* 커서 — 다음 배치 지점으로 미리 날아간다. 전부 끝나면 오른쪽 밖으로 물러난다 */
    let target = items.find(it => k < it.at + 0.06);
    const cx = target ? target.x : w * 0.94;
    const prev = items[Math.max(0, items.indexOf(target) - 1)] || items[0];
    const seg = target
      ? clamp((k - (items.indexOf(target) ? prev.at + 0.06 : 0)) /
        ((target.at + 0.06) - (items.indexOf(target) ? prev.at + 0.06 : 0) || 1e-6))
      : 1;
    const fromX = target && items.indexOf(target) ? prev.x : TX + 20;
    const curX = lerp(fromX, cx, easeOut(seg));
    const curY = ground - 58 - Math.sin(t / 1.7 * Math.PI * 2) * 2;
    if (k > 0.05 && k < 0.97) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(curX, curY - 8); ctx.lineTo(curX, curY + 8);
      ctx.moveTo(curX - 8, curY); ctx.lineTo(curX + 8, curY);
      ctx.stroke();
      ctx.strokeStyle = tone('track');
      ctx.beginPath(); ctx.arc(curX, curY, 12, 0, 7); ctx.stroke();
    }
  }
};

/* ── 배치물 ─────────────────────────────────────────
   전부 (ctx, 바닥 x, 바닥 y, spring 배율) 을 받아 바닥에 앉힌다 */

function drawTree(ctx, x, y, s) {
  ctx.save(); ctx.translate(x, y); ctx.scale(s, s);
  ctx.fillStyle = tone('accent');
  ctx.beginPath(); ctx.moveTo(0, -46); ctx.lineTo(-15, -14); ctx.lineTo(15, -14); ctx.closePath(); ctx.fill();
  ctx.fillStyle = tone('sub'); ctx.fillRect(-3, -14, 6, 14);
  ctx.restore();
}

function drawHouse(ctx, x, y, s) {
  ctx.save(); ctx.translate(x, y); ctx.scale(s, s);
  ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
  ctx.strokeRect(-20, -30, 40, 30);
  ctx.beginPath(); ctx.moveTo(-25, -30); ctx.lineTo(0, -50); ctx.lineTo(25, -30); ctx.closePath();
  ctx.fillStyle = tone('accent'); ctx.fill();
  ctx.restore();
}

/** 주민 — 얼굴 원 + (옵션) 텍스트 표정. face 가 null 이면 민얼굴. bobT 로 제자리 흔들림 */
function drawVillager(ctx, x, y, s, face, bobT) {
  const bob = Math.sin(bobT / BOB * Math.PI * 2) * 2.5;
  ctx.save(); ctx.translate(x, y - 16 + bob); ctx.scale(s, s);
  ctx.fillStyle = tone('ink');
  ctx.beginPath(); ctx.arc(0, -14, 13, 0, 7); ctx.fill();
  ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
  ctx.beginPath(); ctx.moveTo(0, -1); ctx.lineTo(0, 12); ctx.stroke();   // 몸통
  if (face) {
    ctx.fillStyle = '#000'; ctx.textAlign = 'center';
    ctx.font = disp(800, 10);
    ctx.fillText(face, 0, -10.5);
  }
  ctx.restore();
}
