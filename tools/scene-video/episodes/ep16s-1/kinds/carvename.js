import {
  disp, ease, easeOut, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, PALETTE, depthGrad, camAt
} from '../../../engine/lib.js';

/* carvename — 죽고 나면 물어볼 데가 없다. 그래서 **아직 서 있는 동안** 이름을 건네받아
   비석에 새긴다. 그리고 **그 다음에** 쓰러진다.

   원문 근거:
   > "여기서 신경 쓴 건 이름을 언제 넘기느냐였습니다. 오브젝트가 파괴되는 시점에 이름을
   >  읽으면 지연 파괴와 서비스 기록 정리 순서가 얽힙니다. 그래서 죽는 순간, 즉 주민이
   >  아직 살아 있는 함수 안에서 이름을 인자로 넘기게 했습니다."
   > "무덤 표기를 \"{shortName} · Day {day}\" 형식으로 바꿨습니다."

   ── 2026-08-12 개정 v4 (그림 어휘 교체) ────────────────────────────
   🔴 **사건의 순서를 원문의 인과대로 바꿨다.** v3 까지는 「없는 자리를 더듬는다 → 건네받는다」
      였는데, 원문이 말하는 것은 **「살아 있을 때 넘긴다 → 그 다음에 죽는다」** 다.
      그림이 인과 순서로 흐르면 자막 없이도 논지가 읽힌다 — 그게 이 개정의 전부다.
        0.55~1.05  아직 서 있는 사람에게서 이름 조각이 떠올라 날아간다
        1.10       비석의 이름 자리에 **꽂힌다** (임팩트 · latch 효과음 자리)
        1.10~1.50  파인 자리에 이름이 채워진다
        1.30~1.75  **그 다음에** 사람이 땅으로 가라앉는다 (blankstone 의 솟아오름과 짝)
   🔴 **형상을 그 물건처럼 바꿨다.** 캡슐 → 사람 실루엣(머리+몸통, 채움). 동그라미 → 비석.
      얼굴·손가락은 안 그린다(`MINIMAL_DESIGN_LANGUAGE:838`). 실루엣만으로 충분하다.
   🔴 **무한 루프를 걷어냈다.** 더듬는 점선의 왕복, 무덤의 숨쉬기, 새김 뒤의 알파 맥동이
      전부 사라졌다. 그것들은 정적 3초 게이트를 넘기려는 타협이었고, 그래서 화면이 늘
      떨리는데 아무 일도 안 일어났다. 지금은 사건 자체가 게이트를 넘긴다.
   🔴 **크다.** 사람 키 128 = 캔버스 높이 307 의 42%, 비석 높이 150. 옛 표식은 52 였다.

   🔴 색 규약: 이 편에서 처음으로 강조색이 **채워진다.** namefade 에서 꺼졌던 이름이
      여기서 돌아온다 — 그것이 이 편의 뒤집힘이다.
   🔴 **v5 정정: 이름 값을 글자로 새긴다**(사용자 지시). 원문 형식은 `{shortName} · Day {day}` 인데
      실제 값은 원문에 없다. 그래서 이름 자리와 날짜 자리를 **강조색 막대**로 두고,
      형식에 실제로 들어 있는 가운뎃점만 그린다. 「Day」는 게임이 찍는 표기의 인용이다.

   phase (샷 분할):
     phase 0 (S3a) = 사람과 비석이 서로 떨어져 서 있고 이름 자리는 비었다.
                     자막 「죽고 나면 이름을 물어볼 데가 없거든요」
     phase 1 (S3b) = 위 사건이 일어난다. 자막 「그래서 죽는 순간에 이름을 받아 새겼어요」
   🔴 **샷을 갈라도 `since(0)` 원점은 안 움직인다** — 가르기 전 원점은 S3 의 둘째 자막이었고
      가른 뒤 phase 1 의 첫 자막이 바로 그 줄이다. `sfx.delayMs 1100` 을 한 자도 안 고쳤다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색은 ① 날아가는 이름 조각 ② 새겨진 이름 막대뿐이고 **둘 다 비석의 파인 홈
            안이나 그 위 공중**에 있다. 조각의 비행 고도 y 96~110 는 비석 꼭대기 122 보다
            **12px 위**라 흰 실루엣과 안 겹친다. 새겨진 막대는 홈(배경색) 안에 든다 —
            겹치는 상대가 자기 홈의 벽이라 의도한 쌍이다.
     축 B · 흰 글자가 0개다. 사람(오른쪽 끝 108)과 비석(왼쪽 끝 176)은 **68px** 뜬다.
            누울 때 사람의 오른쪽 끝이 134 까지 가지만 그래도 42px 남는다.
   세로 예산(캔버스 307): 최저 = 지평선 268 (39px 남음). 최고 = 비석 꼭대기 **118**,
     날아가는 조각의 정점 **96**.
   가로: 사람 왼쪽 44 / 비석 오른쪽 306. 바깥 2px 띠에서 좌 42 · 우 46 뜬다 —
     phase 0(0.80 축소)에서는 더 넉넉하고, phase 1 이 1.30 까지 다가가면 사람이 왼쪽으로
     나간다(그때 이야기는 이미 비석으로 옮겨 갔다) → `checks.edge` 선언.

   계속 도는 것: **없다.** 새김이 끝난 뒤 남는 움직임은 사람이 눕는 동작뿐이고, 그것도 끝난다.
   정적 구간은 샷 꼬리 0.35초와 자막 여백뿐이라 3초 검사에 못 미친다. */

const GROUND = 268;
const FIG = { x: 82, h: 128, w: 52 };              // 사람 — 캔버스 높이의 42%
const ST = { x: 244, w: 124, h: 150 };             // 비석
const SLOT = { w: 92, h: 30, dy: 62 };             // 이름 자리(파낸 홈)
const CHIP = { w: 40, h: 14 };                     // 이름 조각

function stone(ctx, x, y, w, h) {
  const r = w / 2;
  ctx.beginPath();
  ctx.moveTo(x - r, y); ctx.lineTo(x - r, y - h + r);
  ctx.arc(x, y - h + r, r, Math.PI, 0);
  ctx.lineTo(x + r, y); ctx.closePath();
}

/** 사람 실루엣 — 머리 + 어깨가 좁아지는 몸통. 얼굴·손은 안 그린다. (x, 바닥 y) */
function person(ctx, x, y, hgt, wid) {
  const hr = wid * 0.26;                            // 머리 반지름 — 어깨보다 확실히 작게
  const bodyTop = y - hgt + hr * 2 + 1;
  ctx.beginPath();
  ctx.arc(x, y - hgt + hr, hr, 0, Math.PI * 2);
  ctx.closePath();
  ctx.fill();
  ctx.beginPath();                                  // 몸통 — 어깨에서 발로 살짝 넓어진다
  ctx.moveTo(x - wid * 0.50, bodyTop + 10);
  ctx.quadraticCurveTo(x, bodyTop - 9, x + wid * 0.50, bodyTop + 10);
  ctx.lineTo(x + wid * 0.40, y);
  ctx.lineTo(x - wid * 0.40, y);
  ctx.closePath();
  ctx.fill();
}

/** 임팩트 방사선 — 가이드가 명시 허용한 것(「방사형 spike, loop 금지, 정적」) */
function burst(ctx, x, y, k, n = 12, r0 = 16, len = 30) {
  ctx.save();
  ctx.globalAlpha = k * 0.85;
  ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4; ctx.lineCap = 'round';
  setShadow(ctx, GLOW, 12);
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    const a0 = r0 + 20 * (1 - k), a1 = a0 + len * k;
    ctx.beginPath();
    ctx.moveTo(x + Math.cos(a) * a0, y + Math.sin(a) * a0);
    ctx.lineTo(x + Math.cos(a) * a1, y + Math.sin(a) * a1);
    ctx.stroke();
  }
  clearShadow(ctx);
  ctx.restore();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    camAt(ctx, w, h, spec, t);

    const ph = spec.phase ?? 0;
    const g0 = ph === 0 ? ease(cue(0, 0.15, 0.30)) : 1;
    if (g0 <= 0.01) { ctx.textAlign = 'left'; return; }

    const s1 = ph === 0 ? -1 : since(0);
    const fly = clamp((s1 - 0.55) / 0.55);                 // 이름 조각 비행 0.55~1.10
    const hit = clamp((s1 - 1.06) / 0.10) * clamp((1.38 - s1) / 0.22);   // 꽂힘 임팩트
    const fill = ease(clamp((s1 - 1.10) / 0.40));          // 새김 1.10~1.50
    const fall = ease(clamp((s1 - 1.30) / 0.45));          // 가라앉음 1.30~1.75

    /* ── 지평선 ───────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = g0 * 0.28;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(20, GROUND); ctx.lineTo(332, GROUND); ctx.stroke();
    ctx.restore();

    /* ── 비석 ─────────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = g0;
    /* 꽂히는 순간 한 번 흔들린다 — 맞은 것은 흔들려야 맞은 것으로 읽힌다 */
    const shake = hit > 0.01 ? Math.sin(s1 * 90) * 4 * hit : 0;
    ctx.translate(shake, 0);
    ctx.fillStyle = depthGrad(ctx, ST.x - ST.w / 2, GROUND - ST.h, ST.x + ST.w / 2, GROUND, 'ink');
    stone(ctx, ST.x, GROUND, ST.w, ST.h);
    ctx.fill();

    const sy = GROUND - ST.h + SLOT.dy;
    ctx.globalCompositeOperation = 'destination-out';        // 이름 자리 — 파낸 홈
    ctx.fillStyle = '#000';
    ctx.fillRect(ST.x - SLOT.w / 2, sy, SLOT.w, SLOT.h);
    ctx.globalCompositeOperation = 'source-over';             // 새겨질 이름은 다시 얹는다

    /* 새겨진 이름 — **끌이 지나간 자리처럼 왼쪽부터 파인다.**
       🔴 2026-08-12 v5, 사용자 지시로 **실제 이름 문자열을 새긴다**(전에는 값이 없다는
          이유로 강조색 막대 둘이었다). 값은 `spec.carvedName` 이 진다 — 그림이 문자열을
          소유하면 다음 회차에서 이름이 바뀔 때 그림 파일을 고쳐야 한다.
       🔴 ⚠️ **이 값은 원문 기사에 없다.** 원문은 형식(`{shortName} · Day {day}`)만 주고
          실제 이름은 주지 않는다. 「원문에 없는 값은 화면에 안 적는다」는 이 트랙의 규칙인데,
          여기는 **사용자가 지정한 선언된 예외**다. 검수가 이 줄을 보고 판단하도록 남긴다.
       🔑 왼쪽부터 드러나게 clip 을 쓴다. 통째로 페이드인하면 '나타났다'로 읽히고,
          왼쪽부터 드러나야 '새겨졌다'로 읽힌다. */
    if (fill > 0.01) {
      const name = spec.carvedName ?? '';
      ctx.save();
      ctx.beginPath();
      ctx.rect(ST.x - SLOT.w / 2, sy, SLOT.w * clamp(fill / 0.85), SLOT.h);
      ctx.clip();
      ctx.globalAlpha = g0;
      setShadow(ctx, GLOW, 5);
      ctx.fillStyle = tone('accent');
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      let fsz = 16;
      ctx.font = disp(900, fsz);
      while (fsz > 10 && ctx.measureText(name).width > SLOT.w - 14) {
        fsz -= 0.5; ctx.font = disp(900, fsz);
      }
      ctx.fillText(name, ST.x, sy + SLOT.h / 2 + 1);
      clearShadow(ctx);
      ctx.restore();
      ctx.textBaseline = 'alphabetic';
    }
    ctx.restore();

    /* ── 사람 — 아직 서 있다. 이름을 넘긴 뒤에야 눕는다 ── */
    ctx.save();
    ctx.globalAlpha = g0;
    ctx.beginPath(); ctx.rect(0, 0, 352, GROUND); ctx.clip();   // 땅 밑은 안 보인다
    ctx.fillStyle = tone('ink');
    person(ctx, FIG.x, GROUND + FIG.h * fall, FIG.h, FIG.w);
    ctx.restore();

    /* ── 이름 조각이 건너간다 — 살아 있을 때 넘긴다 ──
       살짝 뒤로 뺐다가(anticipation) 날아간다. 곧장 출발하면 '옮겨졌다'로만 읽힌다. */
    if (fly > 0.005 && fly < 1) {
      const k = fly < 0.18 ? -0.35 * Math.sin(fly / 0.18 * Math.PI) : easeOut((fly - 0.18) / 0.82);
      const x0 = FIG.x, x1 = ST.x;
      const cx = lerp(x0, x1, k);
      const arc = Math.sin(clamp(k) * Math.PI) * 34;         // 포물선
      const cy = lerp(GROUND - FIG.h + 14, sy + SLOT.h / 2, clamp(k)) - arc;
      ctx.save();
      ctx.globalAlpha = g0;
      setShadow(ctx, GLOW, 10);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(cx - CHIP.w / 2, cy - CHIP.h / 2, CHIP.w, CHIP.h);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 꽂힘 임팩트 ─────────────────────────────── */
    if (hit > 0.01) burst(ctx, ST.x, sy + SLOT.h / 2, hit);
  }
};
