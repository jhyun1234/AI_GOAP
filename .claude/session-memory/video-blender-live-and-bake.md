---
name: video-blender-live-and-bake
description: GUI 블렌더에서 회차를 실시간으로 보며 고치는 법(live.py)과 샷을 병렬로 굽는 법(bake.py)
metadata: 
  node_type: memory
  type: project
  originSessionId: 8d9403ae-b887-41b8-9448-19ef42797642
  modified: 2026-08-13T03:36:36.087Z
---

2026-08-13, 파이프라인이 헤드리스 전용이라 사용자 화면에 블렌더가 안 떴다. 두 길을 갈라 놓았다.

**보는 길 — `blender3d/live.py`** (BlenderMCP 로 붙은 GUI 블렌더 안에서)
```python
import live; ns = live.show('render_body1.py'); live.frame(ns, 3.4)
```
그다음 재생(Space)하면 돈다. **키프레임이 아니다** — 프레임 변경 핸들러가 렌더와 **같은 `draw`** 를
부른다(계산 228fps). 그래서 「본 것과 구운 것이 다르다」가 안 생긴다.

**굽는 길 — `blender3d/bake.py`**  `python bake.py` (기본 `-j 3`)
샷 여섯을 프로세스로 나눈다. 실측 벽시계 1.54×(`-j 3`) · 1.80×(`-j 6`).
🔴 샷 수만큼 안 빨라진다(같은 GPU). **천장은 가장 느린 샷 하나**다 — body3.

🔴 **블렌더 종료 코드를 믿지 마라.** 스크립트가 단언에서 죽어도 0 을 준다 — 궤적 검사에
걸려 한 프레임도 안 구운 샷을 `ok` 로 보고했고, 앞 판 프레임이 남아 **낡은 그림이 최종본으로
나갔다.** `bake.py` 가 이제 출력에서 `Traceback` 을 직접 잡는다.
🔴 **길이가 줄면 꼬리 프레임이 남는다.** NF 316→315 로 줄며 낡은 `0315.png` 가 살아남았다.
`stage.bake` 가 전체 렌더 전에 폴더를 비운다(`BAKE_RANGE` 부분 렌더는 안 지운다).

**GUI 에서만 터지는 함정 넷** (헤드리스는 `--factory-startup` 이라 안 드러난다)
1. `bpy.ops.wm.read_factory_settings()` 가 **MCP 애드온을 꺼서 연결을 끊는다.** `live._wipe` 로 대체
2. 스크립트가 `bpy.context.object` 를 쓰는데 애드온 컨텍스트엔 그 속성이 없다 — 뷰포트 컨텍스트를 씌운다
3. `stage._rig_src` 캐시가 지워진 객체를 가리킨다(첫 판은 되고 **둘째 판이 죽는다**)
4. 뷰포트 스크린샷은 오버레이 없이 오프스크린으로 뜬다 — **프레임 경계가 안 보인다.**
   구도 판정은 한 장 구워서 봐라

**Why:** 렌더 스크립트가 `for fi in range(NF)` 안에서 프레임마다 구웠기 때문에 「보기」가 불가능했다.
`stage.bake(OUT, NF, FPS, draw, 태그)` 로 바꿔 계산을 핸들러로 빼면서 두 길이 하나가 됐다.
속도 이득(7%)은 곁다리다.

**How to apply:**
- 파이썬 계산은 병목이 아니다 — 214프레임 **0.64초**, 렌더 61.6초의 1%다. 여기 손대지 마라.
- 더 줄이려면 **샘플 수**다(64→16 이 43%). 화질이 바뀌므로 사용자 판정 사항이다. 아직 안 정했다.
- `BAKE_RANGE="0:23"` 로 앞 몇 프레임만 굽는다 — 재거나 눈으로 볼 때.
- 산출물을 건드리기 전에 `SCENE_3D_ROOT` 를 스크래치로 돌려 굽고 **픽셀 비교**해라
  (전환할 때 이 방법으로 샷 여섯 maxdiff 0.0 을 확인하고 D 를 안 건드린 채 검증했다).

관련: [[video-3d-figure-is-blender-model]] · [[video-motion-vocabulary]]
