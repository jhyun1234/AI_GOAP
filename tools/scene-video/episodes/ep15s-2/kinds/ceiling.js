/* 📐 이 파일의 좌표는 **획 바깥**(`lineWidth/2` 포함) 기준이다.
   경로 좌표를 적을 땐 `(경로)` 라고 표시한다. */
import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* ceiling — 내가 걸어 둔 안전장치. 「어떤 성격도 배고픔보다 앞설 수 없다」.

   원문 근거:
   > "우선순위 작업을 하면서 저는 안전장치를 하나 걸었습니다. 어떤 성격도 배고픔보다
   >  앞설 수 없다. 성격 때문에 밥을 못 먹고 굶어 죽는 주민이 나오면 그건 개성이 아니라
   >  불공정이니까요. 자동 검사가 모든 목표에 대해 이 보증을 걸도록 만들고, 뿌듯해하고
   >  있었습니다."

   자막 0(안전장치를 걸어 뒀거든요) : 「배고픔」 흰 칸 **위로 빗금 뚜껑이 가운데에서
     양옆으로 자라며** 닫힌다. (🔴 밖에서 밀려 들어오는 등장을 안 쓴다 — 이 캔버스에서
     translate 등장은 거의 항상 가장자리를 문다.)
   자막 1(어떤 성격도 배고픔보다 앞설 수 없다) : 아래에서 올라오던 강조색 칸에
     「성격」 이름이 켜진다. 칸은 처음부터 계속 올라왔다 눌려 내려가기를 되풀이한다.

   🔴 **막는 것은 뚜껑이 아니라 「배고픔」 흰 칸이다.** 강조색 칸의 최고점은 획 바깥 176.5,
   흰 칸 바닥은 164.5, 뚜껑은 109.5~132.5 — **뚜껑까지는 44px 이 남고 한 번도 안 닿는다.**
   뚜껑은 「이 위는 잠겼다」는 표시이고 실제 벽은 흰 칸이다. 1차 주석·`reads` 가 「뚜껑 앞에서
   막혀」라고 적어 검수가 정정을 요구했다 — 뜻은 오히려 이쪽이 정확하다(원문의 규칙이
   「**배고픔**보다 앞설 수 없다」이지 「뚜껑을 못 넘는다」가 아니다).

   🖼 라벨을 다 가려도 읽힌다 — 「아래 것이 위로 올라오려다 매번 막혀 도로 내려간다.」

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 좌표로 갈랐다.**
     · 흰 것 : 「안전장치」 글립 y 90~103 · 뚜껑 y 109.5~132.5 · 「배고픔」 칸 y 135.5~164.5
       (칸 안의 글자 포함).
     · 강조색 것 : 「성격」 칸. 가장 높이 올라간 순간 중심 191 → 획 바깥 **176.5**,
       글로우(blur 5)까지 **171.5**. 흰 칸 바닥 164.5 와 **7px** 뜬다.
       가장 내려간 순간 중심 223 → 글로우 바닥 242.5.
     x 는 일부러 겹쳐 뒀다(같은 기둥이라는 뜻이다) — 세로가 안 만나므로 픽셀은 안 겹친다.

   세로 예산(캔버스 307) : 최저 = 강조색 칸 글로우 바닥 242.5. 64.5px 남는다.
     최고 = 「안전장치」 글립 top 90. 80px 남는다.
   가로(캔버스 352) : 흰 칸 94.5~257.5 · 강조색 글로우 99.5~252.5. 좌우 94.5px 남는다.

   ⏱ 계속 도는 것 = 강조색 칸의 오르내림(주기 1.2초 · 32px · 30fps 한 프레임에 최대
     2.8px) + 뚜껑 빗금이 초당 44px 로 흐른다. 위상은 `((x % P) + P) % P` 로 양수 정규화.
     `setLineDash` 는 한 군데도 안 썼다. */

const LID_Y = 111, LID_H = 20;        // 뚜껑
const BAR_Y = 150, BAR_H = 26;        // 배고픔 칸 중심·높이
const BAR_HW = 80;                    // 배고픔 칸 반너비
const ACC_HW = 70;                    // 성격 칸 반너비
const ACC_LO = 223, ACC_HI = 191;     // 성격 칸 중심의 아래/위 끝
const HATCH_P = 16;                   // 빗금 간격

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const CX = w / 2;

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const c0 = cue(spec.lockCue ?? 0, 0.15, 0.7);
    const st = ease(clamp(c0 / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 배고픔 칸(흰) ─────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, CX - BAR_HW, BAR_Y - BAR_H / 2, BAR_HW * 2, BAR_H, 4); ctx.stroke();
    ctx.restore();
    if (spec.barLabel) ctext(spec.barLabel, CX, BAR_Y + 6, 900, 15, tone('ink'), BAR_HW * 2 - 24, st);

    /* ── 성격 칸(강조색) : 올라왔다 눌려 내려간다 ────────
       상시 운동이라 `t` 로 돈다(사건은 자막에 물린다). */
    const bounce = 0.5 + 0.5 * Math.sin(t * 5.24);
    const accY = lerp(ACC_LO, ACC_HI, bounce);
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 5);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, CX - ACC_HW, accY - BAR_H / 2, ACC_HW * 2, BAR_H, 4); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();

    /* 이름은 자막 1 이 부를 때 켜지고 **끝까지 안 꺼진다**(ADR-V25-15 ②).
       🔑 원점을 0.12초 앞당겨(`s1 + 0.12`) **말보다 살짝 먼저** 뜨게 했다 — `since()` 는
       자막 전이면 음수를 돌려주므로 hard guard 없이도 clamp 로 0 에서 시작한다. */
    const s1 = since(spec.nameCue ?? 1);
    const nameA = st * ease(clamp((s1 + 0.12) / 0.32));
    if (spec.accLabel) ctext(spec.accLabel, CX, accY + 6, 900, 15, tone('accent'), ACC_HW * 2 - 24, nameA);

    /* ── 뚜껑(흰) : 가운데에서 양옆으로 자란다 ──────────
       🔑 뚜껑은 **벽이 아니라 표시**다. 강조색 칸이 실제로 막히는 곳은 바로 위 「배고픔」
       흰 칸(바닥 164.5)이고 뚜껑(109.5~132.5)까지는 44px 이 남는다. 칸이 그 벽에 닿을 때
       뚜껑이 함께 밝아져 **「이 위는 잠겨 있다」를 색이 아니라 밝기로** 말한다. */
    const grow = ease(clamp(c0 / 0.5));
    const hw = BAR_HW * grow;
    if (hw > 2) {
      const hot = ease(clamp((bounce - 0.86) / 0.14));
      ctx.save();
      ctx.globalAlpha = st * (0.55 + 0.45 * hot);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, CX - hw, LID_Y, hw * 2, LID_H, 2); ctx.stroke();

      ctx.save();
      ctx.beginPath(); ctx.rect(CX - hw + 2, LID_Y + 2, hw * 2 - 4, LID_H - 4); ctx.clip();
      ctx.lineWidth = 3; ctx.lineCap = 'butt';
      const off = ((t * 44) % HATCH_P + HATCH_P) % HATCH_P;
      for (let x = CX - hw - LID_H - HATCH_P; x < CX + hw + HATCH_P; x += HATCH_P) {
        ctx.beginPath();
        ctx.moveTo(x + off, LID_Y + LID_H);
        ctx.lineTo(x + off + LID_H, LID_Y);
        ctx.stroke();
      }
      ctx.restore();
      ctx.restore();
    }
    if (spec.lidLabel) ctext(spec.lidLabel, CX, 100, 800, 12.5, tone('ink'), 160, st * grow);

    ctx.textAlign = 'left';
  }
};
