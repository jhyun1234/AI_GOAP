/* scene3d — 블렌더가 구운 3D 프레임을 **캔버스 안에** 그린다.

   🔴 **여기는 엔진이다 — 회차 폴더가 아니다.** 이것은 그림이 아니라 붓이라
      `lib.js` 와 같은 층에 산다. 회차의 kind 는 한 줄로 이걸 재수출한다.
      45 편이 각자 사본을 들고 있으면 버그 하나에 45 벌을 고치게 된다.

   🔴 이것이 3D 전환 아키텍처의 핵심 결정이다(설계 §7-1). PNG 를 영상에 직접 합성하지 않고
      엔진 캔버스가 `drawImage` 로 그린다. **`check.mjs` 는 캔버스 픽셀을 읽기 때문**이다 —
      그림이 캔버스 밖으로 나가는 순간 팔레트·정적·잘림·결정성 게이트 33 종이 전부 장님이 된다.
      그 게이트들은 여덟 판 실패하며 쌓은 자산이라 버릴 수 없다.

   🔴 그래서 프레임은 **같은 오리진**으로 받아야 한다. 다른 오리진에서 받은 이미지를
      캔버스에 그리면 캔버스가 오염되어 `getImageData` 가 예외를 던진다.
      `serve.js` 의 `/3d/` 라우트가 그 일을 한다(`/clips/` 와 같은 방식).

   🔴 프레임 크기는 `.vis` 상자와 **정확히 같다**(970×846 device px). 블렌더 쪽
      `stage.BAND_W/BAND_H` 가 그 값이다. 한쪽만 고치면 좌우가 잘리거나 빈 띠가 생긴다.

   🔑 seek 은 순수 함수여야 한다(엔진 규약 1). 시각 t 는 언제나 같은 파일 하나로 떨어지고,
      그 파일이 아직 안 왔으면 `wait()` 로 약속을 걸어 **그 안에서 다시 그린다.**
      기다리기만 하면 이미 빈 프레임이 찍힌 뒤다 — 클립 레이어가 같은 함정을 밟았었다.
*/
import { fitCanvas, mkCanvas } from './lib.js';

const FPS = 30;
/* 앞뒤로 이만큼만 들고 있는다. 여섯 샷 1,459 장을 다 물고 있으면 디코딩된 비트맵만
   1,459 × 970 × 846 × 4B ≈ 4.8 GB 다 — 브라우저가 죽는다. */
const AHEAD = 20;
const BEHIND = 6;

const cache = new Map();          // "dir/0123" → HTMLImageElement
const bad = new Set();

const path = (dir, i) => `${dir}/${String(i).padStart(4, '0')}.png`;

function get(dir, i) {
  const key = path(dir, i);
  let img = cache.get(key);
  if (!img) {
    img = new Image();
    img.src = key;
    cache.set(key, img);
  }
  return img;
}

/** 앞을 미리 받아 둔다. 순서에 의존하지 않는다 — 없으면 그때 받을 뿐이라 그림은 같다. */
function prefetch(dir, i, last) {
  for (let k = i + 1; k <= Math.min(last, i + AHEAD); k++) get(dir, k);
  for (const key of cache.keys()) {
    const [d, n] = [key.slice(0, key.lastIndexOf('/')), +key.slice(-8, -4)];
    if (d === dir && (n < i - BEHIND || n > i + AHEAD)) cache.delete(key);
  }
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, dur, wait, fail }) {
    const cv = root.querySelector('canvas');
    const { ctx, w, h } = fitCanvas(cv);
    const dir = spec.frames;
    if (!dir) { fail('scene3d: spec.frames 가 없다'); return; }

    /* 🔴 프레임 번호는 **반올림이 아니라 내림**이다. 반올림하면 샷 끝에서 마지막+1 을
       요청하게 되고, 그 한 장이 없어서 마지막 프레임만 비는 일이 생긴다. */
    const last = Math.round(dur * FPS);
    const i = Math.max(0, Math.min(last, Math.floor(t * FPS + 1e-6)));

    const img = get(dir, i);
    const paint = () => ctx.drawImage(img, 0, 0, w, h);

    if (img.complete && img.naturalWidth) paint();
    else if (!bad.has(img.src)) {
      wait(img.decode().then(paint).catch(() => {
        bad.add(img.src);
        fail(`scene3d: 프레임을 못 읽었다 ${img.src}`);
      }));
    }
    prefetch(dir, i, last);
  }
};
