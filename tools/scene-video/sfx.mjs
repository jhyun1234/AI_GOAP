/* 효과음 합성 — 받아 쓰지 않고 만들어 쓴다.
   사용: render.mjs 가 track 을 조립할 때 부른다. 단독 실행은 미리듣기용.
     node tools/scene-video/sfx.mjs preview        모든 소리를 이어 붙여 sfx-preview.wav

   ## 왜 합성인가
   - **결정성**: 이 프로젝트의 1번 규약이다. 같은 인자면 같은 파형이라 mp4 추출 전제가 안 깨진다.
     난수는 씨앗을 받는 xorshift 뿐 — Math.random() 을 쓰지 않는다.
   - **의존성 0**: 음원을 받지 않는다. 라이선스 문제도 없다.
   - **결이 맞는다**: 화면이 3색·기하 도형의 추상이라 녹음된 실사 소리보다 합성음이 어울린다.

   ## 음량
   나레이션 트랙 실측(ep02s) = 피크 0.489 · RMS 0.051. 효과음은 **그 아래에 깔린다** —
   기본 피크 0.10 (나레이션 대비 약 -14dB). 말을 먹으면 안 되기 때문이다.
   🔑 소리는 **나레이션이 실제로 부르는 사건에만** 넣는다. 심심해서 채우면 싸구려가 된다
   ("정적을 장식으로 메우지 말라"는 그림 쪽 교훈과 같다).
*/

const TAU = Math.PI * 2;

/* 씨앗 있는 난수 — 같은 씨앗이면 같은 잡음. Math.random() 금지. */
function rng(seed) {
  let s = (seed | 0) || 1;
  return () => { s ^= s << 13; s ^= s >>> 17; s ^= s << 5; return ((s >>> 0) / 4294967296) * 2 - 1; };
}

/* 감쇠 포락선. a=어택(초), d=감쇠 지수. 어택이 0이면 클릭이 딱딱하게 들린다 —
   1ms 라도 줘야 스피커에서 '탁' 하고 튀지 않는다. */
const env = (i, n, rate, a = 0.002, d = 5) => {
  const t = i / rate, dur = n / rate;
  const atk = a > 0 ? Math.min(1, t / a) : 1;
  return atk * Math.exp(-d * (t / dur));
};

const alloc = (dur, rate) => new Float32Array(Math.max(1, Math.round(dur * rate)));

/* ── 소리들 ────────────────────────────────────────
   전부 (opt, rate) → Float32Array. 피크는 마지막에 정규화하므로 여기선 모양만 만든다. */
const KIT = {
  /* 딱 — 무언가가 자리에 앉는다. 카드가 꽂히고, 표식이 도착한다. */
  tick(o = {}, rate) {
    const dur = o.dur ?? 0.06, f = o.freq ?? 1400, out = alloc(dur, rate), n = out.length;
    for (let i = 0; i < n; i++) out[i] = Math.sin(TAU * f * i / rate) * env(i, n, rate, 0.001, 22);
    return out;
  },

  /* 툭 — 낮고 짧게. 벽에 닿거나 막히는 자리.
     🔴 처음엔 92Hz 순음이었다. **휴대폰 스피커는 300Hz 아래를 거의 못 낸다** —
     계측상 멀쩡해도 정작 볼 기기에서는 안 들린다. 기음을 올리고 배음을 얹어
     작은 스피커에도 남는 성분을 만든다. 저역은 분위기로만 깔린다. */
  thud(o = {}, rate) {
    const dur = o.dur ?? 0.18, f = o.freq ?? 150, out = alloc(dur, rate), n = out.length;
    for (let i = 0; i < n; i++) {
      const t = i / rate, e = env(i, n, rate, 0.002, 6);
      const g = f * (1 - 0.3 * t / (n / rate));                 // 살짝 떨어진다
      out[i] = (Math.sin(TAU * g * t) * 0.6
             +  Math.sin(TAU * g * 2 * t) * 0.3                 // 300Hz 대 — 폰에서 들리는 성분
             +  Math.sin(TAU * g * 3 * t) * 0.15) * e;
    }
    return out;
  },

  /* 철컥 — 두 단계. 잡음 전이 + 낮은 몸통. 잠기거나 확정되는 자리(커밋·규칙). */
  latch(o = {}, rate) {
    const dur = o.dur ?? 0.14, out = alloc(dur, rate), n = out.length, r = rng(o.seed ?? 7);
    for (let i = 0; i < n; i++) {
      const t = i / rate;
      const click = r() * Math.exp(-260 * t);                     // 아주 짧은 전이
      // 몸통도 배음을 얹는다 — 180Hz 순음만으로는 폰 스피커에서 사라진다
      const body = (Math.sin(TAU * 340 * t) * 0.6 + Math.sin(TAU * 680 * t) * 0.25)
                 * env(i, n, rate, 0.001, 14);
      out[i] = click * 0.5 + body * 0.7;
    }
    return out;
  },

  /* 차오름 — 아래에서 위로. 쌓이거나 굵어지는 자리. */
  riser(o = {}, rate) {
    const dur = o.dur ?? 0.9, f0 = o.from ?? 160, f1 = o.to ?? 520;
    const out = alloc(dur, rate), n = out.length;
    let ph = 0;
    for (let i = 0; i < n; i++) {
      const k = i / n, f = f0 + (f1 - f0) * k * k;
      ph += TAU * f / rate;
      const a = Math.min(1, k * 4) * (1 - k * k);                 // 들어왔다 사라진다
      out[i] = Math.sin(ph) * a;
    }
    return out;
  },

  /* 스윽 — 걸러진 잡음이 지나간다. 쓸고 지나가거나 지우는 자리. */
  sweep(o = {}, rate) {
    const dur = o.dur ?? 0.5, out = alloc(dur, rate), n = out.length, r = rng(o.seed ?? 11);
    let lp = 0;
    for (let i = 0; i < n; i++) {
      const k = i / n;
      const cut = 0.02 + 0.5 * Math.sin(Math.PI * k);             // 가운데서 밝아졌다 어두워진다
      lp += (r() - lp) * cut;                                     // 1차 저역통과
      out[i] = lp * Math.sin(Math.PI * k);                        // 양끝을 0 으로
    }
    return out;
  },

  /* 뚝 — 위에서 아래로. 뒤집히거나 떨어지는 자리. */
  drop(o = {}, rate) {
    const dur = o.dur ?? 0.28, f0 = o.from ?? 480, f1 = o.to ?? 120;
    const out = alloc(dur, rate), n = out.length;
    let ph = 0;
    for (let i = 0; i < n; i++) {
      const k = i / n;
      ph += TAU * (f0 + (f1 - f0) * Math.sqrt(k)) / rate;
      out[i] = Math.sin(ph) * env(i, n, rate, 0.002, 5);
    }
    return out;
  },
};

export const SFX_KINDS = Object.keys(KIT);

/* 소리 하나를 만든다. gain 은 **피크 기준** — 파형을 정규화한 뒤 곱하므로
   소리마다 체감 크기가 들쭉날쭉하지 않다. */
export function synth(kind, opt = {}, rate = 44100) {
  const make = KIT[kind];
  if (!make) throw new Error(`모르는 효과음: ${kind} (있는 것: ${SFX_KINDS.join(', ')})`);
  const pcm = make(opt, rate);

  /* 🔴 **피크가 아니라 단기 RMS 로 맞춘다.**
     처음엔 피크 0.10 으로 정규화했더니 사용자 판정이 *"들리는 것도 있고 너무 작아서 안 들리는
     것도 있다"* 였다. 재 보니 그럴 만했다 — 피크가 같아도 체감은 tick −13.8dB · latch −12.8dB
     (riser 기준). **짧고 뾰족한 소리는 피크만 크고 에너지가 없다.**
     사람 귀는 피크가 아니라 에너지를 듣는다. 50ms 창의 최대 RMS 로 맞추면 고르게 들린다.

     그래서 `gain` 의 뜻이 바뀌었다 — **목표 단기 RMS** 다. 나레이션 RMS 가 0.051 이므로
     기본 0.030 은 그보다 조금 아래다. 피크는 따로 천장을 씌워 지직거림을 막는다. */
  const win = Math.min(pcm.length, Math.round(rate * 0.05));
  let loud = 0;
  for (let i = 0; i + win <= pcm.length; i += Math.max(1, Math.round(win / 4))) {
    let s = 0; for (let j = i; j < i + win; j++) s += pcm[j] * pcm[j];
    loud = Math.max(loud, Math.sqrt(s / win));
  }
  if (loud === 0) { let s = 0; for (const v of pcm) s += v * v; loud = Math.sqrt(s / pcm.length); }
  if (loud > 0) {
    let g = (opt.gain ?? 0.030) / loud;
    let peak = 0;
    for (const v of pcm) { const a = Math.abs(v); if (a > peak) peak = a; }
    const ceil = opt.peakCeil ?? 0.34;              // 나레이션 피크 0.489 아래로
    if (peak * g > ceil) g = ceil / peak;
    for (let i = 0; i < pcm.length; i++) pcm[i] *= g;
  }
  return pcm;
}

/* 미리듣기 — 소리를 눈으로 못 보니 귀로 확인할 파일을 만든다. */
if (process.argv[1] && process.argv[1].endsWith('sfx.mjs') && process.argv[2] === 'preview') {
  const fs = await import('fs');
  const path = await import('path');
  const rate = 44100, gap = Math.round(rate * 0.45);
  const parts = SFX_KINDS.map(k => synth(k, {}, rate));
  const total = parts.reduce((a, p) => a + p.length + gap, 0);
  const out = new Float32Array(total);
  let at = 0;
  for (const p of parts) { out.set(p, at); at += p.length + gap; }
  const b = Buffer.alloc(44 + out.length * 2);
  b.write('RIFF', 0); b.writeUInt32LE(36 + out.length * 2, 4); b.write('WAVE', 8);
  b.write('fmt ', 12); b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22);
  b.writeUInt32LE(rate, 24); b.writeUInt32LE(rate * 2, 28); b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34);
  b.write('data', 36); b.writeUInt32LE(out.length * 2, 40);
  for (let i = 0; i < out.length; i++) b.writeInt16LE(Math.max(-32768, Math.min(32767, out[i] * 32767)), 44 + i * 2);
  const f = path.join(path.dirname(new URL(import.meta.url).pathname.slice(1)), 'sfx-preview.wav');
  fs.writeFileSync(f, b);
  console.log(`${SFX_KINDS.length}종 · ${(out.length / rate).toFixed(1)}초 → ${f}`);
  console.log(SFX_KINDS.map((k, i) => `  ${(i * 0.45 + parts.slice(0, i).reduce((a, p) => a + p.length / rate, 0)).toFixed(2)}s  ${k}`).join('\n'));
}
