# 3D 네이티브 구현 계획 — ep15s-1 (1~4단계)

## 📍 지금 어디까지 왔나 (2026-08-12 세션 끝)

**작업 1~8 전부 끝났다. 검사 43/43 통과.** 브랜치 `exp/palette4`, 작업 트리 깨끗함, push 안 함.

| 작업 | 상태 | 산출물 |
|---|---|---|
| 1 스크립트 리포로 + `/3d` 라우트 | ✅ | `blender3d/` · `serve.js` |
| 2 측정 도구 | ✅ | `probe.py` · `palette.py` · `_check.py` |
| 3 팔레트 3층 | ✅ | `stage.py` |
| 4 마을 | ✅ | `village.py` → `3d/models/village.blend` |
| 5 동작 여덟 | ✅ | `motions.py` (bpy 없이 검사됨) |
| 6 군무·파열 | ✅ | `unison.py` · `3d/proof/unison/unison.mp4` |
| 7 대본·제목 재작성 + TTS | ✅ | 13줄 45.46초 · `episodes/ep15s-1/notes/3d-rewrite.md` |
| 8 인트로 | ✅ | `render_intro.py` → `3d/_intro/intro.mp4` |
| — 훅(8비트·9.58초) | ✅ | `render_hook.py` → `3d/ep15s-1/hook/hook.mp4` · `intro_hook.mp4` |
| — 본문2(6비트·8.94초) | ✅ | `instrument.py` · `render_body2.py` → `3d/ep15s-1/body2/body2.mp4` |
| — 소품 다섯 다시 | ✅ | `village.py` 함수화 + **`blender3d/PROPS.md`** · `render_prop_look.py` |
| — 본문1(5비트·7.09초) | ✅ | `render_body1.py` → `3d/ep15s-1/body1/` |
| — 본문3(7비트·10.49초) | ✅ | `render_body3.py` → `3d/ep15s-1/body3/` |
| — 아웃트로(7.20초) | ✅ | `render_outro.py` → `3d/ep15s-1/outro/` |
| — **여섯 샷 통짜 그림** | ✅ | `3d/ep15s-1/ep15s-1_picture.mp4` (48.6초, 소리 없음) |

(경로의 `3d/` 는 전부 `D:\AI_GOAP-videos\3d\` 아래다.)

### 🔴 5비트 판은 반려됐다 — 무엇이 틀렸었나

판정: **「제자리에서 가만히 걷는 모습은 별로 인상 깊지 않았다. 비트를 여덟쯤으로 늘리고
주민이 다 같이 걷는 모습까지 넣어 달라.」**

원인은 비트 수가 아니었다. **여섯이 아무 데도 안 갔다** — 다리는 저었는데 `arm.location`
을 한 번도 안 건드려서 발밑 좌표가 첫 프레임 그대로였다. 화면에서 안 변한 것은 몇 번
반복해도 안 변한다. 🔴 **또 같은 층이었다**: 비트(연출)를 세는 동안 틀린 것은 피사체였다.

고친 것(8비트 판):
- `unison.march(t, beat)` 가 자리와 방향을 민다. 나아가는 속도는 `motions.WALK_SPEED` —
  다리가 만드는 보폭(2·다리길이·sin 진폭 × 2걸음 × 주기)에서 **역산한 값**이라 발이 안 미끄러진다
- 걸음 진폭 26° → 34°. 26° 는 훅 내내 1.9 m 라 「걸어갔다」로 안 읽혔다(실측). 34° 는 2.4 m
- `motions.blend` — 걷다 멈추는 이음새. 없으면 허벅지가 반 주기에서 0 으로 순간이동한다
- 성격 표식을 주민에게 묶었다. 안 묶으면 걸어가도 표식만 처음 자리에 남는다
- 카메라는 **겨냥만 따라간다.** 자리까지 따라붙이면 배경이 안 흘러서 또 제자리걸음이 된다

### 🔴 그다음 판정 — 「마을 안쪽으로 걷는 동선」

판정: **「대본 한 줄 늘려 9~10초로. 마을 안쪽으로 걷는 동선이 좋아 보인다.」**

- 대본: SH 에 「가는 곳도, 멈추는 자리도 같았고요.」 한 줄. **6.61 → 9.582 초.**
  🔴 훅 길이를 손으로 안 적는다 — `render_hook.py` 가 `timed.json` 에서 SH 를 읽는다.
- 🔴 **가로 대열은 이 마을에 물리적으로 못 들어간다.** 폭 5.75 m 인데 나무와 모닥불 사이
  통로가 3.06 m 다. 간격·중심을 다 훑어도 우물이나 모닥불이 누군가의 차선에 걸린다.
  그래서 여섯을 **세로 한 줄**로 돌려세웠다 — 차선이 0.44 m 하나가 되고, 무엇보다
  「여섯이 한 줄로 같은 걸음을 한다」가 이 편의 사건 ①에 더 가깝다.
- 궤적 검사를 `render_hook.py` 에 넣었다. 매 렌더마다 여섯의 궤적과 소품 거리를 재고
  뚫으면 **렌더가 죽는다.** 장애물은 주민 키(0.95) 기준으로 고른다 — 밭(0.05)은 밟고
  지나가고, 처마(밑동 1.05)는 밑으로 지나간다.
- 🔴 **태양 방위가 월드에 박혀 있었다.** 주민을 마을 안쪽으로 돌려세우자 여섯이 통째로
  역광이 됐다. `stage.key_from_view(loc, at)` 가 카메라에서 역산한다 — 승인된 판의
  상대 각(시선 -67.3°, 고도 46°)을 그대로 재현하므로 노출 보정은 그대로 산다.
- 비트: 멈춤과 돌기를 **한 비트로 합쳤다.** 둘 다 그림이 거의 안 변해서 나란히 두면
  한복판에 2.4 초 홀드가 생긴다(실측). 남은 비트는 걷기에 줘서 **이동 4.17 m.**

측정(인접 프레임 차): 서 있는 비트 0.0004~0.001 · 걷는 비트 **0.004~0.006**.
정지 구간 최장 1.2 초. 클리핑 0(peak 222/255). 유채색 보라 하나.
인트로 끝 ↔ 훅 첫 프레임 차 0.008 — 컷 없이 이어진다.

### 계기층이 화면에 올라왔다 — 본문2

설계 §2 의 셋째 층(시안)을 이 편에서 처음 쓴다. 30 칸짜리 표가 마을 **위에** 뜨고,
셋에 보라가 닿고, 둘이 꺼지고, 하나만 남고, 그 하나로 실 여섯이 내려온다.
칸 수·불 켜지는 칸은 `scene.json` 의 S2 spec 이 정본이고 길이는 `timed.json` 이 정본이다.

- 🔴 **첫 판은 8.9 초짜리 홀드였다.** 여섯 비트 전부 인접 프레임 차 0.0004~0.0009 —
  `probe.frame_diff` 가 정적이라 부르는 0.0008 언저리다. 칸이 뜨고 꺼지는 사건은 화면의
  몇 %라 그것만으로는 그림이 안 변한다. **카메라가 표를 돌게** 해서 고쳤다(19°) —
  공중의 표와 땅의 마을이 시차로 어긋나는 것이 「표가 세계 위에 떠 있다」를 보이는 길이다.
  지금 0.0010~0.0023, 여섯 비트 전부 통과.
- 🔴 **설계표의 개발자 시점 높이 9.0 으로는 안 된다.** 70mm 는 수평 화각 28.7° 라
  표와 밭의 여섯을 한 프레임에 넣으려면 17m 는 떨어져야 하고 높이가 13.6 이 된다.
  렌즈를 줄이는 대신 거리를 벌렸다(눌러 보이는 것이 이 시점의 뜻이므로).
- 🔴 `probe.metrics` 에 **최소 화소 문턱**(0.1%)을 넣었다. 시안이 어두운 바닥과 섞이면
  한기(217°) 대역으로 밀려서, 한기 372px 이 「위험색이 떴다」로 잡히고 있었다.
  경계 잡음까지 위반으로 세면 게이트를 아무도 안 믿는다.

### 소품 다섯을 다시 만들었다 — 어휘집은 `blender3d/PROPS.md`

「모닥불부터. 불이 없는 게 제일 이상해」 → 우물·밭·집·나무까지. 매번 결론이 같았다:
**상자 하나는 아무것으로도 안 읽힌다.** 모닥불은 돌 테두리와 장작이, 우물은 줄과 두레박이,
집은 파낸 구멍 둘이, 밭은 파묻은 둥근 두둑이 그것을 그것으로 만들었다.

🔑 소품은 이제 **좌표를 받는 함수**다(`house` `tree` `well` `field` `flame`). 회차마다
다시 만들지 마라 — 규칙과 함정은 `PROPS.md` 에 있고 `README.md` 맨 위가 그리로 보낸다.

이번에 새로 밟은 함정 셋(전부 PROPS.md 에 적음):
- 🔴 `o.dimensions` 는 **로컬** 바운딩박스라 **회전이 안 들어간다.** `view_layer.update()` 를
  불러도 그대로다. 눕힌 두둑이 높이 1.36 기둥으로 잡혀 궤적 검사가 밭 전체를 벽으로 봤다.
  회전이 있는 물체는 `bound_box` 를 `matrix_world` 로 옮겨서 재라(`village._world_bounds`).
- 🔴 소품끼리 **겹쳐서 한 물건이 된다.** 모닥불이 훅 카메라에서 우물 뒤 2.6° 라
  「불타는 우물」로 보였다. 자리는 월드 거리가 아니라 **카메라에서 본 각도**로 확인해라.
- 🔴 불리언이 만든 새 면은 **벽 재질을 물려받는다.** 파기만 하면 문이 회색 패널이다.

측정: 프레임당 뜻층 인트로 1 · 훅 2 · 본문2 1 · 최장 정적 0.7 초(상한 3.0).

### 본문1·본문3·아웃트로 — 샷 여섯이 다 섰다

`ep15s-1_picture.mp4` 가 여섯 샷을 대본 순서대로 이어 붙인 **그림만의 통짜**다(48.6초).
자막·HUD·소리는 아직 없다 — 그건 엔진 캔버스 합성(2차 계획 2번)이 할 일이다.

- **본문1** — 🔴 **설계 §4-1 을 글자 그대로는 못 지었다.** 「①밭에 같이 ②우물에 같이
  ③불에 같이」는 세 자리를 실제로 도는 데 **12.9 초**가 든다(9.30m ÷ 0.72m/s). 대본은
  7.09 초, 1.8 배 모자란다. 빠르게 걷게 하면 발이 미끄러지고 그건 5비트 훅이 반려된 그
  문제의 반대편이다. 🔑 그래서 **자리가 아니라 박자로** 지었다 — 대본도 장소가 아니라
  때를 말한다(「밭에 가는 **때**도 불 쬐는 **때**도」). 여섯이 같은 순간에 같은 일로
  갈아탄다(`motions.sequence`). ④ 는 **자리만** 흩어진다.
- **본문3** — 180 → 36 → 코드가 채움 → **컷 ↓** → 군무가 깨진다. 색 순서가 곧 이야기다:
  보라(사람이 적은 것) → 초록(코드). 🔴 두 색을 **한 프레임에 겹치면 안 된다**(앰버까지
  셋이 된다) — 초록이 들어오는 순간 서른여섯은 시안으로 돌아간다.
- **아웃트로** — 🔴 `loopSec 0.5` 는 2D 장치라 안 썼다. 26m 마을에서 반 초마다 한기 띠가
  지나가면 반복이 아니라 **점멸**이고, 인계 문서의 되돌린 목록에 이미 있다. 세 번 다가오되
  **매번 더 가까이** 오는 것으로 뜻을 지켰다. 🔴 굳은 뒤 화면이 3.1 초 멈춰 정적 상한(3.0)을
  넘겼는데, 고칠 자리는 카메라가 아니라 이야기였다 — 위험은 멈추지 않는다. 한기가 굳은
  여섯을 덮고 지나가게 하니 0.1 초가 됐다.

전 샷 실측: 프레임당 뜻층 ≤2 · 최장 정적 ≤0.7 초(상한 3.0) · 클리핑 0.

⚠️ `3d/ep15s-1/{s1,s2,s3,so}` 570MB 는 **반려된 2D 번역판**의 잔재다. 지우지 않고 뒀다.

### 🔴 다음 세션이 **가장 먼저** 할 일

**사용자 판정을 받는다** — `ep15s-1_picture.mp4` 를 보고, 자막 없이 처음 0.3 초에
**「표식은 다른데 움직임이 하나다」** 가 읽히는가.

- **읽힌다** → 아래 2차 계획으로 간다.
- **안 읽힌다** → 연출 모드 A(군무·파열)를 다시 본다. 설계 §1 의 B(하루 따라가기)·
  C(개발자 시점)를 다시 검토하는 것이지 **카메라·조명을 만지는 것이 아니다.**
  🔴 이 프로젝트는 「밋밋하다」에 카메라를 만지다 여덟 판을 돌린 전례가 있다(인계 문서 §1).

### 바로 돌릴 수 있는 명령

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video/blender3d"
for t in probe palette village motions unison intro; do python test_$t.py; done
```

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python "C:/Users/anjyo/AI_GOAP-video/tools/scene-video/blender3d/render_hook.py"
```

### 2차 계획 — 아직 안 쓴 것

설계 §9 의 5~7 단계다. **작업 6 판정을 받은 뒤에 쓴다**(A 안이 무효화되면 샷 설계가 통째로 바뀐다).

1. **본문 샷 셋 + 아웃트로 렌더** — 설계 §4-1 의 비트 시트대로. 본문2~3 은 개발자 시점(C)이고
   계기층(시안)이 처음 등장한다. 이 편에서 컷은 그 둘뿐이다.
2. **엔진 캔버스 합성 kind** — `/3d/<ep>/<shot>/frames/*.png` 를 `drawImage` 로 그리고
   자막·HUD·성격 표식 라벨을 위에 얹는다. 🔴 캔버스 **안**이어야 게이트 33종이 산다(설계 §7-1).
   기존 `episodes/ep15s-1/kinds/*.js` 다섯은 2D 도형용이라 이때 교체된다.
3. **게이트 둘 갱신** — 팔레트 4색 강제 → 3층 규약 / 정적 3초 → 비트 선언
   (🔴 비트 게이트는 **영상을 보고 나서** 확정하기로 했다 — 설계 §8-2).

### 알아 둘 것

- **길이 상한이 1분대로 열렸다.** 상한이 열린 것이지 길게 만들라는 것이 아니다 —
  길이는 비트를 세서 나온다(설계 §4-1). 홀드 1.5초 상한은 그대로다.
- 🔴 **훅 길이는 대본에서 나온다.** SH 내레이션 길이이고 엔진 타임라인이 그 값으로 돈다
  (`build/timed.json`). 비트를 늘리면 길어지는 것이 아니라 **비트가 짧아진다.**
  길게 가려면 대본을 늘려야 한다 — 한 줄 더해 6.606 → 9.582 초가 됐다.
- **D 드라이브 `episodes/` 는 지웠다.** 리포 build 의 사본이었다(video.mp4 해시 대조 확인).
  이제 D 에는 `3d/` 와 `clips/` 만 있다. **코드는 C, 산출물은 D.**
- **TTS 는 리포 쪽 `episodes/<ep>/build/` 에 쓴다**(`lib-node.mjs` 의 `epDir`).
- `stash@{0}` 에 폐기한 SDF 판이 들어 있다. 되살릴 일은 없지만 지우지도 않았다.

---

> **에이전트 작업자에게:** 이 계획은 작업 단위로 실행한다. 각 단계는 체크박스(`- [ ]`)다.

**목표:** 마을과 주민이 피사체인 3D 네이티브 파이프라인을 세우고, 이 설계의 가장 큰
미검증 가정(「여섯이 완벽히 같은 동작을 하면 0.3 초에 읽힌다」)을 실물로 확인한다.

**설계:** 블렌더가 1080×846 알파 PNG 를 굽고, 엔진 캔버스가 그것을 `drawImage` 로 그린 뒤
자막·HUD·계기층을 위에 얹는다. 그림이 캔버스 안에 있어야 `check.mjs` 게이트 33 종이 계속 돈다.

**기술:** Blender 5.2 (EEVEE, `--background --python`) · Node(기존 엔진·serve.js) · ffmpeg

**정본 스펙:** [`2026-08-12-3d-native-design.md`](./2026-08-12-3d-native-design.md)

## 전역 제약

- 렌더 크기는 **1080 × 846**. 엔진은 이 그림을 쇼츠 1920 프레임의 **y=420** 에 놓는다.
- `sc.view_transform = 'Standard'`. 필믹이면 팔레트가 밀린다.
- `sc.render.engine = 'BLENDER_EEVEE'`. 5.2 의 enum 은 이것 하나다(`BLENDER_EEVEE_NEXT` 는 없다).
- 뼈 축 규약: **X = 앞뒤 · Z = 좌우 · Y = 비틀림.** 팔다리는 뼈가 아래를 향하므로 **X 음수가 앞**.
- 팔레트 3층: 세계(무채색 + `#B8845C`) / 뜻(`#00FF88` `#FFB35C` `#5B9DFF` `#FF5C5C` `#C77DFF`, **한 프레임에 둘까지**) / 계기(`#5BE9FF`, 공중 전용).
- 코드는 리포(`C:\Users\anjyo\AI_GOAP-video`, worktree, 브랜치 `exp/palette4`).
  산출물은 `D:\AI_GOAP-videos\3d\`.
- 블렌더 실행: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python <스크립트>`

### 블렌더 함정 여섯 — 전부 한 번씩 밟았다. 다시 밟지 마라

1. GLB 임포트 객체는 `rotation_mode` 가 `'QUATERNION'` 이라 `rotation_euler` 가 **무시된다.**
2. Blender 5.2 는 `Material.use_nodes` 를 아직 본다. 안 켜면 **기본 회색 0.8** 로 렌더된다.
3. 바닥을 눕혀 보면 **프레넬 정반사**가 알베도를 압도한다. `Specular IOR Level = 0`.
4. `Standard` 는 롤오프가 없어 광량을 올리면 **바로 흰색 클리핑.** 눈이 아니라 픽셀을 재라.
5. 뼈 roll 을 자동 계산에 맡기면 축이 뼈마다 갈린다. `calculate_roll(type='GLOBAL_POS_X')`.
6. 유채색 물체를 스케일 0·발광 0 으로 끄면 **밑색이 남는다.** `hide_render` 로 빼라.

---

## 파일 구조

**리포에 만든다** — `tools/scene-video/blender3d/`

| 파일 | 책임 |
|---|---|
| `stage.py` | 조명·카메라·렌더 설정·팔레트 3층 재질. 모든 샷이 부른다 |
| `village.py` | 마을 소품 생성(집·나무·밭·모닥불·우물) → `village.blend` |
| `rig.py` | 주민 뼈대 + 자동 웨이트 → `villager_rigged.blend` |
| `motions.py` | 동작 여덟. 순수 함수 — 시간을 받아 뼈 회전 dict 를 돌려준다 |
| `unison.py` | 군무 — 여섯에게 **같은** 포즈를 적용. 이 설계의 핵심 가정 |
| `probe.py` | 렌더 PNG 를 읽어 지표를 낸다. 게이트와 테스트가 쓴다 |
| `README.md` | 함정 여섯 + 사용법 |

**리포에서 고친다**

| 파일 | 무엇 |
|---|---|
| `serve.js:8-17,23-24` | `/3d/` 라우트 추가 — 엔진이 D 드라이브 프레임을 **같은 오리진**으로 받게 |

**D 드라이브 산출물** — `D:\AI_GOAP-videos\3d\`
`models/village.blend` · `models/villager_rigged.blend` · `<ep>/<shot>/frames/*.png`

---

## 작업 1: 스크립트를 리포로 옮기고 `/3d/` 라우트를 연다

**파일:**
- 이동: `D:\AI_GOAP-videos\3d\build\{stage,rig,motions_test}.py` → `tools/scene-video/blender3d/`
- 수정: `tools/scene-video/serve.js`
- 생성: `tools/scene-video/blender3d/README.md`

**인터페이스:**
- 생산: `stage.build()` · `stage.light_camera()` · `stage.aim()` · `stage.rigged()` · `stage.pose()` — 이후 모든 작업이 쓴다
- 생산: HTTP 경로 `/3d/<ep>/<shot>/frames/0000.png` → `D:\AI_GOAP-videos\3d\...`

🔴 **왜 라우트가 필요한가.** `serve.js` 의 `ROOT` 는 `tools/scene-video` 라 D 드라이브를 못 준다.
그리고 `file://` 이나 다른 오리진에서 이미지를 받아 캔버스에 그리면 **캔버스가 오염되어
`getImageData` 가 예외를 던진다** — 그 순간 팔레트·정적·잘림·결정성 게이트가 전부 죽는다.
`/clips/` 가 이미 같은 문제를 같은 방식으로 풀고 있다(`serve.js:11,23-24`).

- [ ] **단계 1: 파일을 옮기고 경로 상수를 고친다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video"
mkdir -p blender3d
cp "D:/AI_GOAP-videos/3d/build/stage.py" blender3d/stage.py
cp "D:/AI_GOAP-videos/3d/build/rig.py" blender3d/rig.py
```

`blender3d/stage.py` 의 모델 경로를 리포 기준으로 고친다:

```python
# 이 파일은 tools/scene-video/blender3d/ 에 있다. 모델은 D 드라이브 산출물이다.
MODEL = r"C:\Users\anjyo\AI_GOAP-video\tools\scene-video\blender\Shorts.blend"
OUT_ROOT = r"D:\AI_GOAP-videos\3d"
RIG_BLEND = os.path.join(OUT_ROOT, 'models', 'villager_rigged.blend')
```

`blender3d/rig.py` 도 같은 값을 쓰도록 `import stage` 후 `stage.MODEL` / `stage.RIG_BLEND` 를 참조한다.

- [ ] **단계 2: `/3d/` 라우트를 추가한다**

`serve.js:11` 아래에 상수를 추가:

```js
const THREED = process.env.SCENE_3D_ROOT || 'D:\\AI_GOAP-videos\\3d';
```

`serve.js:23-24` 의 base 선택을 세 갈래로 바꾼다:

```js
  const isClip = p.startsWith('/clips/');
  const is3d = p.startsWith('/3d/');
  const base = isClip ? CLIPS : is3d ? THREED : ROOT;
  const rel = isClip ? p.slice(7) : is3d ? p.slice(4) : p;
  const file = path.join(base, rel);
```

- [ ] **단계 3: 라우트가 실제로 파일을 주는지 확인한다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video"
PORT=4399 node serve.js &
sleep 1
curl -s -o /dev/null -w "%{http_code} %{content_type} %{size_download}\n" \
  "http://localhost:4399/3d/ep15s-1/hook/frames/0000.png"
kill %1
```

기대: `200 image/png 500000` 근처. 404 가 나오면 경로 슬라이스(`p.slice(4)`)를 다시 본다.

- [ ] **단계 4: README 에 함정 여섯을 적는다**

`blender3d/README.md` 에 전역 제약의 「함정 여섯」을 그대로 옮기고, 실행 명령과
`stage.EXPOSURE` / `stage.LANE_LEVEL` 손잡이 설명을 적는다.
🔴 값을 적지 말고 **왜 그 값인지**를 적어라 — 값만 있으면 다음 사람이 눈대중으로 고친다.

- [ ] **단계 5: 커밋**

```bash
cd "C:/Users/anjyo/AI_GOAP-video"
git add tools/scene-video/blender3d tools/scene-video/serve.js
git commit -m "feat(video): 블렌더 스크립트를 리포로 · serve.js 에 /3d 라우트

엔진이 D 드라이브 3D 프레임을 같은 오리진으로 받아야 한다. 다른 오리진이면
캔버스가 오염돼 getImageData 가 던지고 게이트 33 종이 전부 죽는다."
```

---

## 작업 2: 측정 도구 `probe.py`

**파일:**
- 생성: `tools/scene-video/blender3d/probe.py`
- 테스트: `tools/scene-video/blender3d/test_probe.py`

**인터페이스:**
- 생산: `probe.read_png(path) -> dict(w, h, rgba: bytes)`
- 생산: `probe.metrics(path) -> dict` — 키: `alpha_cover`(0~1), `peak_lum`(0~255),
  `chroma_hues`(뜻층 색 이름 집합), `instrument_px`(시안 화소 수)
- 생산: `probe.frame_diff(path_a, path_b) -> float` — 평균 절대차 ÷ 255 (check.mjs 와 같은 잣대)

🔴 **눈대중을 안 남긴다.** 이번 세션에서 「길이 밝다」·「인물이 어둡다」를 눈으로 판단하다
세 판을 헛돌렸고, 픽셀을 재고 나서야 원인(노출 클리핑)이 나왔다. 이 도구가 그 재발 방지다.

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`blender3d/test_probe.py`:

```python
import os, subprocess, sys, tempfile
import probe

HERE = os.path.dirname(os.path.abspath(__file__))
FIXTURE = os.path.join(HERE, 'fixtures', 'swatch.png')


def test_metrics_reads_a_known_swatch():
    """가로로 4등분한 견본: 투명 · 흰색 · 강조 초록 · 계기 시안."""
    m = probe.metrics(FIXTURE)
    assert abs(m['alpha_cover'] - 0.75) < 0.02, m['alpha_cover']
    assert m['peak_lum'] > 240, m['peak_lum']
    assert m['chroma_hues'] == {'green'}, m['chroma_hues']
    assert m['instrument_px'] > 0


def test_frame_diff_zero_for_same_file():
    assert probe.frame_diff(FIXTURE, FIXTURE) == 0.0
```

- [ ] **단계 2: 견본 이미지를 만든다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video/blender3d"
mkdir -p fixtures
ffmpeg -y -f lavfi -i "color=c=black@0.0:s=100x40,format=rgba" \
  -f lavfi -i "color=c=white:s=100x40" \
  -f lavfi -i "color=c=0x00FF88:s=100x40" \
  -f lavfi -i "color=c=0x5BE9FF:s=100x40" \
  -filter_complex "[0][1][2][3]hstack=inputs=4,format=rgba" \
  -frames:v 1 fixtures/swatch.png
```

- [ ] **단계 3: 테스트를 돌려 실패를 확인한다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video/blender3d"
python -m pytest test_probe.py -v
```
기대: `ModuleNotFoundError: No module named 'probe'`

- [ ] **단계 4: `probe.py` 를 쓴다**

```python
"""렌더 PNG 를 재는 도구. 눈대중을 안 남긴다.

🔴 ffmpeg 로 raw RGBA 를 뽑아 읽는다 — 이 저장소는 의존성 0 이 원칙이라
   Pillow 를 새로 깔지 않는다. ffmpeg 는 이미 파이프라인이 쓰고 있다."""
import subprocess

# 뜻층 다섯의 색상각(HSV hue, 도). 판정은 정확한 값이 아니라 **색상각 근처**로 한다 —
# 안티에일리어싱 경계에 두 색의 중간값이 생기는데 그건 위반이 아니다(check.mjs 와 같은 이유).
MEANING_HUES = {'green': 152, 'amber': 33, 'chill': 217, 'red': 0, 'violet': 275}
INSTRUMENT_HUE = 189          # 계기층 시안 #5BE9FF
HUE_TOL = 18
SAT_MIN = 0.25                # 이보다 낮으면 회색 계열 — 세계층이다


def _raw(path):
    out = subprocess.run(
        ['ffmpeg', '-v', 'error', '-i', path, '-f', 'rawvideo', '-pix_fmt', 'rgba', '-'],
        capture_output=True, check=True).stdout
    dim = subprocess.run(
        ['ffprobe', '-v', 'error', '-select_streams', 'v:0',
         '-show_entries', 'stream=width,height', '-of', 'csv=p=0:s=x', path],
        capture_output=True, check=True, text=True).stdout.strip()
    w, h = (int(v) for v in dim.split('x'))
    return w, h, out


def read_png(path):
    w, h, rgba = _raw(path)
    return {'w': w, 'h': h, 'rgba': rgba}


def _hue_sat(r, g, b):
    mx, mn = max(r, g, b), min(r, g, b)
    if mx == 0:
        return 0.0, 0.0
    sat = (mx - mn) / mx
    if mx == mn:
        return 0.0, sat
    if mx == r:
        hue = 60 * (((g - b) / (mx - mn)) % 6)
    elif mx == g:
        hue = 60 * ((b - r) / (mx - mn) + 2)
    else:
        hue = 60 * ((r - g) / (mx - mn) + 4)
    return hue, sat


def metrics(path):
    d = read_png(path)
    rgba, n = d['rgba'], d['w'] * d['h']
    opaque = peak = instrument = 0
    hues = set()
    for i in range(0, n * 4, 4):
        a = rgba[i + 3]
        if a < 120:
            continue
        opaque += 1
        r, g, b = rgba[i], rgba[i + 1], rgba[i + 2]
        peak = max(peak, (r * 299 + g * 587 + b * 114) // 1000)
        hue, sat = _hue_sat(r, g, b)
        if sat < SAT_MIN:
            continue
        if abs(((hue - INSTRUMENT_HUE + 180) % 360) - 180) <= HUE_TOL:
            instrument += 1
            continue
        for name, h0 in MEANING_HUES.items():
            if abs(((hue - h0 + 180) % 360) - 180) <= HUE_TOL:
                hues.add(name)
                break
    return {'alpha_cover': opaque / n, 'peak_lum': peak,
            'chroma_hues': hues, 'instrument_px': instrument}


def frame_diff(a, b):
    """check.mjs 와 같은 잣대 — 적색 채널 평균 절대차 ÷ 255."""
    da, db = read_png(a), read_png(b)
    ra, rb = da['rgba'], db['rgba']
    if len(ra) != len(rb):
        raise ValueError('크기가 다르다')
    n = len(ra) // 4
    return sum(abs(ra[i * 4] - rb[i * 4]) for i in range(n)) / n / 255
```

- [ ] **단계 5: 테스트를 돌려 통과를 확인한다**

```bash
python -m pytest test_probe.py -v
```
기대: 2 passed

- [ ] **단계 6: 커밋**

```bash
git add tools/scene-video/blender3d/probe.py tools/scene-video/blender3d/test_probe.py tools/scene-video/blender3d/fixtures
git commit -m "feat(video): 렌더 측정 도구 probe.py — 눈대중을 안 남긴다"
```

---

## 작업 3: 팔레트 3층을 `stage.py` 에 넣는다

**파일:**
- 수정: `tools/scene-video/blender3d/stage.py`
- 테스트: `tools/scene-video/blender3d/test_palette.py`

**인터페이스:**
- 소비: `probe.metrics`(작업 2)
- 생산: `stage.PALETTE` dict · `stage.world_mat(albedo)` · `stage.meaning_mat(name, strength)` ·
  `stage.instrument_mat()` — 이후 모든 샷이 색을 이것으로만 만든다

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`blender3d/test_palette.py`:

```python
import os, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__))
BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
OUT = os.path.join(HERE, 'fixtures', 'palette_probe.png')


def _render(script):
    subprocess.run([BLENDER, '--background', '--factory-startup', '--python', script],
                   check=True, capture_output=True)


def test_meaning_colors_land_on_their_hue():
    """뜻층 다섯을 한 장에 찍고, probe 가 다섯을 다 알아보는지 본다."""
    _render(os.path.join(HERE, 'render_palette_probe.py'))
    sys.path.insert(0, HERE)
    import probe
    m = probe.metrics(OUT)
    assert m['chroma_hues'] == {'green', 'amber', 'chill', 'red', 'violet'}, m['chroma_hues']
    assert m['peak_lum'] < 250, ('클리핑됐다', m['peak_lum'])
```

- [ ] **단계 2: 테스트를 돌려 실패를 확인한다**

```bash
python -m pytest test_palette.py -v
```
기대: `FileNotFoundError: render_palette_probe.py`

- [ ] **단계 3: `stage.py` 에 팔레트를 넣는다**

```python
# ── 팔레트 3층 (스펙 §2) ─────────────────────────────
# 🔴 색을 여기 밖에서 만들지 마라. 뜻이 없는 색이 들어오는 순간 중구난방이 시작된다.
PALETTE = {
    # 세계층 — 예산에 안 들어간다. 저채도라 배경으로 물러난다
    'earth':      (0.42, 0.24, 0.13),      # #B8845C 선형 근사
    # 뜻층 — 한 프레임에 최대 둘
    'green':      (0.000, 1.000, 0.246),   # #00FF88 코드/시스템
    'amber':      (1.000, 0.446, 0.113),   # #FFB35C 삶·온기
    'chill':      (0.100, 0.320, 1.000),   # #5B9DFF 오는 중인 위험·추위
    'red':        (1.000, 0.100, 0.100),   # #FF5C5C 이미 벌어진 실패·죽음
    'violet':     (0.539, 0.196, 1.000),   # #C77DFF 성격·개성
    # 계기층 — 공중 전용. 세계 물체에 절대 안 쓴다
    'instrument': (0.100, 0.800, 1.000),   # #5BE9FF
}
MEANING = ('green', 'amber', 'chill', 'red', 'violet')


def world_mat(albedo=(0.06, 0.06, 0.065), earth=False):
    """세계층 — 지형·건물·나무·주민 몸."""
    base = PALETTE['earth'] if earth else albedo
    return _mat('world', base)


def meaning_mat(name, strength=0.0, albedo_scale=0.10):
    """뜻층. 작은 도형은 strength 2.2, **넓은 면은 0.5 근처**로 내리고 albedo 를 올려라 —
    화면 3분의 1을 2.2 로 발광시키면 눈이 아픈 덩어리가 된다(실측)."""
    assert name in MEANING, f'뜻층이 아니다: {name}'
    c = PALETTE[name]
    return _mat(f'meaning_{name}', tuple(v * albedo_scale for v in c), emit=c, strength=strength)


def instrument_mat(strength=2.0):
    """계기층 — 마을 위에 뜨는 수치·표 전용."""
    c = PALETTE['instrument']
    return _mat('instrument', (0.0, 0.05, 0.06), emit=c, strength=strength)
```

- [ ] **단계 4: 견본 렌더 스크립트를 쓴다**

`blender3d/render_palette_probe.py`:

```python
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'fixtures', 'palette_probe.png')
bpy.ops.wm.read_factory_settings(use_empty=True)
col = bpy.context.collection
for i, name in enumerate(stage.MEANING):
    bpy.ops.mesh.primitive_cube_add(size=1, location=((i - 2) * 0.9, 0, 0.3))
    o = bpy.context.object
    o.scale = (0.6, 0.6, 0.6)
    o.data.materials.append(stage.meaning_mat(name, strength=2.2))
cam = stage.light_camera(res=(600, 200))
stage.aim(cam, (0, -6.0, 1.2), (0, 0, 0.3))
bpy.context.scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
```

- [ ] **단계 5: 테스트를 돌려 통과를 확인한다**

```bash
python -m pytest test_palette.py -v
```
기대: 1 passed. 실패하면 `HUE_TOL` 이 아니라 **선형 RGB 값**을 의심하라 —
`view_transform='Standard'` 라 선형값이 그대로 sRGB 로 나간다.

- [ ] **단계 6: 커밋**

```bash
git add tools/scene-video/blender3d/stage.py tools/scene-video/blender3d/test_palette.py tools/scene-video/blender3d/render_palette_probe.py
git commit -m "feat(video): 팔레트 3층 규약을 stage.py 에 — 4색 규약 폐기"
```

---

## 작업 4: `village.py` — 마을을 짓는다

**파일:**
- 생성: `tools/scene-video/blender3d/village.py`
- 테스트: `tools/scene-video/blender3d/test_village.py`

**인터페이스:**
- 소비: `stage.world_mat`(작업 3)
- 생산: `village.build() -> dict` — 키: `ground`, `houses`(list, 3), `trees`(list),
  `field`, `well`, `campfire`. 각 값은 `bpy.types.Object`
- 생산: `village.SPOTS` — `{'field': Vector, 'well': Vector, 'tree': Vector, 'fire': Vector}`
  주민이 일하러 가는 자리. 작업 6 의 군무가 이 좌표를 쓴다
- 생산: 산출물 `D:\AI_GOAP-videos\3d\models\village.blend`

🔴 **전부 프리미티브로 만든다.** 캐릭터가 6,786 면 플랫 셰이딩이라 매끈한 소품을 넣으면
그때 일관성이 깨진다. 면 수는 렌더 속도와 무관하다(실측 — 스펙 §7-4).

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`blender3d/test_village.py`:

```python
import os, subprocess, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
REPORT = os.path.join(HERE, 'fixtures', 'village_report.json')


def test_village_has_the_pieces_and_fits_the_stage():
    subprocess.run([BLENDER, '--background', '--factory-startup', '--python',
                    os.path.join(HERE, 'village.py')], check=True, capture_output=True)
    r = json.load(open(REPORT, encoding='utf-8'))
    assert len(r['houses']) == 3, r['houses']
    assert len(r['trees']) >= 3, r['trees']
    # 마을이 주민(키 1.0)에 대해 말이 되는 크기여야 한다
    assert 1.4 <= r['house_height'] <= 2.6, r['house_height']
    # 주민이 갈 자리 넷이 서로 충분히 떨어져 있어야 궤적이 겹치는 게 보인다
    xs = r['spots']
    assert len(xs) == 4
    for i in range(4):
        for j in range(i + 1, 4):
            dx = xs[i][0] - xs[j][0]
            dy = xs[i][1] - xs[j][1]
            assert (dx * dx + dy * dy) ** 0.5 > 1.8, (i, j)
    # 유채색이 한 개도 없어야 한다 — 마을은 세계층이다
    assert r['chroma_hues'] == [], r['chroma_hues']
```

- [ ] **단계 2: 테스트를 돌려 실패를 확인한다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video/blender3d"
python -m pytest test_village.py -v
```
기대: `FileNotFoundError: village.py`

- [ ] **단계 3: `village.py` 를 쓴다**

```python
"""마을 — 전부 프리미티브. 주민(키 1.0)이 기준 축척이다.
   blender --background --factory-startup --python village.py

🔴 매끈하게 만들지 마라. 캐릭터가 플랫 셰이딩 6,786 면이다 — 소품만 매끈하면 그때 깨진다.
🔴 유채색을 쓰지 마라. 마을은 **세계층**이고, 유채색은 뜻이 있을 때만 뜬다."""
import bpy, sys, os, math, json
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage

OUT = os.path.join(stage.OUT_ROOT, 'models', 'village.blend')
REPORT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'fixtures', 'village_report.json')

HOUSES = [(-3.4, 2.6, 25), (0.2, 3.9, -10), (3.6, 2.2, -32)]     # x, y, 회전(도)
TREES = [(-4.6, 0.4), (4.9, 0.9), (-2.2, 5.2), (2.9, 5.6)]
SPOTS = {
    'field': Vector((-2.6, -1.4, 0)),
    'well':  Vector((1.9, -1.1, 0)),
    'tree':  Vector((4.5, 1.0, 0)),
    'fire':  Vector((-0.2, 0.9, 0)),
}


def _cube(loc, scale, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.scale = scale
    o.rotation_mode = 'XYZ'
    o.rotation_euler = (0, 0, math.radians(rot_z))
    o.data.materials.append(mat)
    return o


def _cone(loc, r, h, mat, rot_z=0.0, verts=6):
    """지붕·나무. verts 6 이면 각이 보인다 — 캐릭터의 패싯과 같은 결이다."""
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, depth=h, location=loc)
    o = bpy.context.object
    o.rotation_mode = 'XYZ'
    o.rotation_euler = (0, 0, math.radians(rot_z))
    o.data.materials.append(mat)
    return o


def build():
    col = bpy.context.collection
    wall = stage.world_mat((0.085, 0.085, 0.092))
    roof = stage.world_mat(earth=True)
    ground_m = stage.world_mat((0.030, 0.032, 0.038))
    stone = stage.world_mat((0.065, 0.066, 0.070))

    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    ground = bpy.context.object
    ground.scale = (26, 26, 1)
    ground.data.materials.append(ground_m)

    houses = []
    for x, y, rz in HOUSES:
        body = _cube((x, y, 0.55), (1.5, 1.3, 1.1), wall, rz)
        _cone((x, y, 1.45), 1.25, 0.8, roof, rz, verts=4)
        houses.append(body)

    trees = []
    for x, y in TREES:
        _cube((x, y, 0.32), (0.16, 0.16, 0.64), roof)
        trees.append(_cone((x, y, 1.05), 0.62, 1.15, stage.world_mat((0.055, 0.070, 0.052))))

    # 밭 — 낮은 이랑 여섯. 여섯이라는 수가 이 회차에서 뜻을 진다
    field = _cube((SPOTS['field'].x, SPOTS['field'].y, 0.03), (2.2, 1.6, 0.05), roof)
    for i in range(6):
        _cube((SPOTS['field'].x - 0.9 + i * 0.36, SPOTS['field'].y, 0.08),
              (0.12, 1.5, 0.09), stage.world_mat((0.050, 0.038, 0.028)))

    well = _cube((SPOTS['well'].x, SPOTS['well'].y, 0.22), (0.62, 0.62, 0.44), stone)
    campfire = _cube((SPOTS['fire'].x, SPOTS['fire'].y, 0.10), (0.55, 0.55, 0.20), stone)

    return {'ground': ground, 'houses': houses, 'trees': trees,
            'field': field, 'well': well, 'campfire': campfire}


if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    json.dump({
        'houses': [h.name for h in v['houses']],
        'trees': [t.name for t in v['trees']],
        'house_height': 1.45 + 0.4,
        'spots': [[s.x, s.y] for s in SPOTS.values()],
        'chroma_hues': [],          # 마을은 세계층 — 유채색 재질을 하나도 안 쓴다
    }, open(REPORT, 'w', encoding='utf-8'), ensure_ascii=False)
    print('[village] saved', OUT)
```

- [ ] **단계 4: 테스트를 돌려 통과를 확인한다**

```bash
python -m pytest test_village.py -v
```
기대: 1 passed

- [ ] **단계 5: 마을을 눈으로 본다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video/blender3d"
python - <<'EOF' > /tmp/vshot.py
print(open('render_palette_probe.py').read()
      .replace("import stage", "import stage, village")
      .replace("palette_probe.png", "village_look.png"))
EOF
```
대신 아래를 직접 쓰는 편이 낫다 — `blender3d/render_village_look.py`:

```python
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
cam = stage.light_camera()
stage.aim(cam, (6.2, -8.4, 4.2), (0, 1.2, 0.6))
bpy.context.scene.render.filepath = os.path.join(stage.OUT_ROOT, 'models', 'village_look.png')
bpy.ops.render.render(write_still=True)
```

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
  --python render_village_look.py
ffmpeg -y -f lavfi -i "color=c=0x0E1117:s=1080x846" -i "D:/AI_GOAP-videos/3d/models/village_look.png" \
  -filter_complex "[0][1]overlay=0:0:shortest=1" -frames:v 1 "D:/AI_GOAP-videos/3d/models/village_look_bg.png"
```

🔴 **여기서 멈추고 사람에게 보여라.** 마을이 이상하면 다음 작업 전부가 헛돈다.

- [ ] **단계 6: 커밋**

```bash
git add tools/scene-video/blender3d/village.py tools/scene-video/blender3d/test_village.py tools/scene-video/blender3d/render_village_look.py
git commit -m "feat(video): 마을을 프리미티브로 짓는다 — village.py"
```

---

## 작업 5: `motions.py` — 동작 여덟

**파일:**
- 생성: `tools/scene-video/blender3d/motions.py`
- 테스트: `tools/scene-video/blender3d/test_motions.py`

**인터페이스:**
- 생산: `motions.MOTIONS` — `{이름: 함수}`. 이름 여덟: `look_up` `walk` `stop` `farm` `chop` `draw` `reach` `freeze`
- 생산: 각 함수 `f(t: float) -> dict[str, tuple[float, float, float]]` — 뼈 이름 → (X, Y, Z) 라디안.
  **순수 함수다.** bpy 를 안 만진다 — 그래서 블렌더 없이 테스트할 수 있다
- 소비: `stage.pose(arm, spec)`(이미 있음)이 이 dict 를 받는다

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`blender3d/test_motions.py`:

```python
import math
import motions

BONES = {'hips', 'spine', 'neck', 'head'} | {
    f'{p}.{s}' for s in ('L', 'R')
    for p in ('shoulder', 'upperarm', 'forearm', 'hand', 'thigh', 'shin', 'foot')}


def test_all_eight_exist():
    assert set(motions.MOTIONS) == {
        'look_up', 'walk', 'stop', 'farm', 'chop', 'draw', 'reach', 'freeze'}


def test_every_motion_only_names_real_bones():
    for name, fn in motions.MOTIONS.items():
        for t in (0.0, 0.37, 0.9):
            for bone in fn(t):
                assert bone in BONES, (name, bone)


def test_every_motion_actually_moves_something():
    """0 회전만 돌려주는 동작은 동작이 아니다."""
    for name, fn in motions.MOTIONS.items():
        if name in ('stop', 'freeze'):
            continue
        moved = any(abs(v) > 0.01
                    for t in (0.1, 0.3, 0.5, 0.7)
                    for rot in fn(t).values() for v in rot)
        assert moved, name


def test_walk_is_cyclic():
    """한 주기 뒤에는 같은 자리로 돌아와야 이어 붙였을 때 안 튄다."""
    a, b = motions.MOTIONS['walk'](0.0), motions.MOTIONS['walk'](1.0 / motions.WALK_HZ)
    for bone in a:
        for x, y in zip(a[bone], b[bone]):
            assert abs(x - y) < 1e-9, bone


def test_limbs_swing_forward_with_negative_x():
    """축 규약(rig.py): X 음수가 앞이다. 뻗기는 팔을 앞으로 보낸다."""
    r = motions.MOTIONS['reach'](0.9)
    assert r['upperarm.L'][0] < -0.3, r['upperarm.L']
```

- [ ] **단계 2: 테스트를 돌려 실패를 확인한다**

```bash
python -m pytest test_motions.py -v
```
기대: `ModuleNotFoundError: No module named 'motions'`

- [ ] **단계 3: `motions.py` 를 쓴다**

```python
"""동작 여덟. **순수 함수다** — bpy 를 안 만지므로 블렌더 없이 테스트된다.

🔴 축 규약(rig.py 가 roll 을 GLOBAL_POS_X 로 고정했다):
   **X = 앞뒤 · Z = 좌우 · Y = 비틀림.** 팔다리는 뼈가 아래를 향하므로 **X 음수가 앞**.
🔴 안 쓰는 동작은 안 만든다. 필요한 동작이 생기면 그 회차가 처음 만든다."""
import math

R = math.radians
WALK_HZ = 1.15          # motions.mjs 의 walk 와 같은 주기


def _ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def look_up(t):
    k = _ease(t / 0.35)
    return {'neck': (R(-14) * k, 0, 0), 'head': (R(-12) * k, 0, 0),
            'spine': (R(-4) * k, 0, 0)}


def walk(t):
    ph = 2 * math.pi * WALK_HZ * t
    s, c = math.sin(ph), math.cos(ph)
    knee_l = max(0.0, math.sin(ph - 0.9))
    knee_r = max(0.0, math.sin(ph + math.pi - 0.9))
    return {
        'thigh.L': (R(-26) * s, 0, 0), 'shin.L': (R(34) * knee_l, 0, 0),
        'thigh.R': (R(26) * s, 0, 0), 'shin.R': (R(34) * knee_r, 0, 0),
        'foot.L': (R(12) * s, 0, 0), 'foot.R': (R(-12) * s, 0, 0),
        'upperarm.L': (R(17) * s, 0, 0), 'upperarm.R': (R(-17) * s, 0, 0),
        'forearm.L': (R(-12) * (1 - c) / 2, 0, 0),
        'forearm.R': (R(-12) * (1 + c) / 2, 0, 0),
        'spine': (R(2) * c, 0, 0),
    }


def stop(t):
    """멈춤 — 숨만 쉰다. 완전히 굳으면 마네킹으로 읽힌다."""
    b = math.sin(2 * math.pi * 0.26 * t)
    return {'spine': (R(1.2) * b, 0, 0), 'neck': (R(-0.8) * b, 0, 0)}


def farm(t):
    """밭일 — 허리를 굽혀 앞으로 훑는다. 주기 1.4 초."""
    ph = 2 * math.pi * t / 1.4
    s = math.sin(ph)
    return {'spine': (R(-32) + R(9) * s, 0, 0), 'neck': (R(-10), 0, 0),
            'upperarm.L': (R(-46) + R(16) * s, 0, 0), 'forearm.L': (R(-28), 0, 0),
            'upperarm.R': (R(-46) - R(16) * s, 0, 0), 'forearm.R': (R(-28), 0, 0),
            'thigh.L': (R(-8), 0, 0), 'thigh.R': (R(-8), 0, 0)}


def chop(t):
    """장작 — 들었다 내리친다. 주기 1.1 초. 내리칠 때가 빠르다."""
    u = (t % 1.1) / 1.1
    swing = _ease(u / 0.62) if u < 0.62 else 1 - _ease((u - 0.62) / 0.38)
    return {'upperarm.L': (R(-150) + R(140) * (1 - swing), 0, 0),
            'upperarm.R': (R(-150) + R(140) * (1 - swing), 0, 0),
            'forearm.L': (R(-20), 0, 0), 'forearm.R': (R(-20), 0, 0),
            'spine': (R(-6) - R(16) * (1 - swing), 0, 0)}


def draw(t):
    """우물 — 두레박 줄을 번갈아 당긴다. 주기 1.6 초."""
    ph = 2 * math.pi * t / 1.6
    s = math.sin(ph)
    return {'upperarm.L': (R(-70) + R(26) * s, 0, 0), 'forearm.L': (R(-52) - R(24) * s, 0, 0),
            'upperarm.R': (R(-70) - R(26) * s, 0, 0), 'forearm.R': (R(-52) + R(24) * s, 0, 0),
            'spine': (R(-9), 0, 0)}


def reach(t):
    """뻗기 — 0.62 초에 다 뻗고 그 뒤로는 **한 톨도 안 움직인다**(굳음의 준비)."""
    k = _ease(min(t, 0.62) / 0.62)
    return {'upperarm.L': (R(-58) * k, 0, R(6)), 'forearm.L': (R(-30) * k, 0, 0),
            'upperarm.R': (R(-40) * k, 0, R(-6)), 'forearm.R': (R(-22) * k, 0, 0),
            'spine': (R(-9) * k, 0, 0), 'neck': (R(-5) * k, 0, 0)}


def freeze(t):
    """굳음 — 뻗은 자세 그대로 정지. 숨도 안 쉰다. 그게 이 동작의 뜻이다."""
    return reach(1.0)


MOTIONS = {'look_up': look_up, 'walk': walk, 'stop': stop, 'farm': farm,
           'chop': chop, 'draw': draw, 'reach': reach, 'freeze': freeze}
```

- [ ] **단계 4: 테스트를 돌려 통과를 확인한다**

```bash
python -m pytest test_motions.py -v
```
기대: 5 passed

- [ ] **단계 5: 동작 여덟을 컨택트 시트로 본다**

`blender3d/render_motion_sheet.py`:

```python
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, motions

OUT = os.path.join(stage.OUT_ROOT, 'models', 'motion_sheet')
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
mesh, arm = stage.rigged(loc=(0, 0, 0), rot_z=45)
cam = stage.light_camera(res=(560, 800))
stage.aim(cam, (-1.55, -1.60, 0.72), (0, 0, 0.50))
for name, fn in motions.MOTIONS.items():
    stage.pose(arm, fn(0.45))
    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('[motion]', name)
```

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
  --python render_motion_sheet.py
cd "D:/AI_GOAP-videos/3d/models/motion_sheet"
for f in look_up walk stop farm chop draw reach freeze; do
  ffmpeg -y -loglevel error -f lavfi -i "color=c=0x0E1117:s=560x800" -i $f.png \
    -filter_complex "[0][1]overlay=0:0:shortest=1,scale=200:-1" -frames:v 1 c_$f.png
done
ffmpeg -y -loglevel error -i c_look_up.png -i c_walk.png -i c_stop.png -i c_farm.png \
  -i c_chop.png -i c_draw.png -i c_reach.png -i c_freeze.png \
  -filter_complex "[0][1][2][3][4][5][6][7]hstack=inputs=8" sheet.png
```

🔴 **웨이트가 새는지 여기서 본다.** `chop` 은 팔을 머리 위로 들므로 골반이 딸려 오면 여기서 보인다.

- [ ] **단계 6: 커밋**

```bash
git add tools/scene-video/blender3d/motions.py tools/scene-video/blender3d/test_motions.py tools/scene-video/blender3d/render_motion_sheet.py
git commit -m "feat(video): 동작 여덟 — 순수 함수라 블렌더 없이 테스트된다"
```

---

## 작업 6: 군무 — 이 설계의 가장 큰 가정을 검증한다

**파일:**
- 생성: `tools/scene-video/blender3d/unison.py`
- 생성: `tools/scene-video/blender3d/render_unison_proof.py`
- 테스트: `tools/scene-video/blender3d/test_unison.py`

**인터페이스:**
- 소비: `motions.MOTIONS`(작업 5) · `village.SPOTS`(작업 4) · `stage.rigged` `stage.pose`
- 생산: `unison.place(n=6) -> list[armature]` — 광장에 여섯을 세운다
- 생산: `unison.apply(arms, motion_name, t)` — **여섯 전부에 같은 포즈**를 넣는다
- 생산: `unison.break_apart(arms, t, k)` — `k` 0→1 로 여섯이 **서로 다른 동작**으로 갈라진다

🔴 **이 작업이 설계의 시금석이다.** 스펙 §9 가 「여섯이 완벽히 같은 동작을 하면 0.3 초에
읽힌다」를 가장 큰 미검증 가정으로 표시했다. 여기서 안 읽히면 A 안(군무·파열) 전체를 다시 본다.

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`blender3d/test_unison.py`:

```python
import math
import motions
import unison


def test_unison_gives_every_villager_the_identical_pose():
    """「같다」는 위상 오차 0 이다. 부동소수 수준에서 정확히 같아야 한다."""
    for t in (0.0, 0.21, 0.63, 1.4):
        specs = [unison.pose_at('walk', t, i) for i in range(6)]
        first = specs[0]
        for i, s in enumerate(specs[1:], 1):
            assert s.keys() == first.keys(), i
            for bone in first:
                assert s[bone] == first[bone], (t, i, bone)


def test_break_apart_gives_every_villager_a_different_motion():
    """「갈라진다」는 위상이 아니라 **동작 자체**가 다른 것이다."""
    names = [unison.motion_at(t=0.0, i=i, k=1.0) for i in range(6)]
    assert len(set(names)) >= 4, names


def test_break_apart_is_still_unison_at_k_zero():
    names = [unison.motion_at(t=0.0, i=i, k=0.0) for i in range(6)]
    assert len(set(names)) == 1, names
```

- [ ] **단계 2: 테스트를 돌려 실패를 확인한다**

```bash
python -m pytest test_unison.py -v
```
기대: `ModuleNotFoundError: No module named 'unison'`

- [ ] **단계 3: `unison.py` 를 쓴다**

```python
"""군무와 파열. 이 설계(스펙 §1 모드 A)의 뼈대다.

🔴 **중간은 없다.**
   「같다」 = 위상 오차 0. 여섯이 완전히 같은 프레임에 같은 포즈.
   「갈라진다」 = 위상이 아니라 **동작 자체가 다르다.**
   살짝 어긋난 여섯은 「대충 비슷한 여섯」으로 읽히고, 그게 2D 번역판이 어색했던 이유의 절반이다."""
import math
import motions

N = 6
# 광장에 서는 자리. 여섯이 한 화면에 들어오면서 서로 안 가리는 배치다.
STANDS = [(-2.10, -0.30), (-1.26, 0.35), (-0.42, -0.20),
          (0.42, 0.40), (1.26, -0.25), (2.10, 0.30)]
# 갈라질 때 여섯이 고르는 동작. 사건 ③ 「열다섯 쌍 전부 갈라짐」의 3D 판이다.
BREAK = ['farm', 'chop', 'draw', 'walk', 'stop', 'farm']


def motion_at(t, i, k):
    """i 번째 주민이 지금 무슨 동작을 하는가. k 0 이면 여섯이 같고, 1 이면 갈라진다."""
    return BREAK[i] if k >= 0.5 else 'walk'


def pose_at(motion_name, t, i):
    """🔴 i 를 **안 쓴다.** 이게 군무다 — 쓰는 순간 위상이 어긋난다."""
    return motions.MOTIONS[motion_name](t)


def place(n=N, rot_z=90):
    """광장에 n 명을 세운다. bpy 가 필요하므로 블렌더 안에서만 부른다."""
    import stage
    arms = []
    for i in range(n):
        x, y = STANDS[i]
        mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=rot_z)
        arms.append(arm)
    return arms


def apply(arms, motion_name, t):
    import stage
    spec = pose_at(motion_name, t, 0)
    for arm in arms:
        stage.pose(arm, spec)


def break_apart(arms, t, k):
    import stage
    for i, arm in enumerate(arms):
        stage.pose(arm, motions.MOTIONS[motion_at(t, i, k)](t))
```

- [ ] **단계 4: 테스트를 돌려 통과를 확인한다**

```bash
python -m pytest test_unison.py -v
```
기대: 3 passed

- [ ] **단계 5: 3 초 증명 영상을 렌더한다**

`blender3d/render_unison_proof.py`:

```python
"""군무 3 초 → 파열 1.5 초. 사람이 보고 판정한다."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison

OUT = os.path.join(stage.OUT_ROOT, 'proof', 'unison', 'frames')
FPS, DUR, BREAK_AT = 30, 4.5, 3.0
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
arms = unison.place()
cam = stage.light_camera()

for fi in range(round(DUR * FPS) + 1):
    t = fi / FPS
    k = 0.0 if t < BREAK_AT else min(1.0, (t - BREAK_AT) / 0.4)
    if k < 0.5:
        unison.apply(arms, 'walk', t)
    else:
        unison.break_apart(arms, t, k)
    u = t / DUR
    stage.aim(cam, (3.4 - 1.9 * u, -5.6 - 0.5 * u, 1.55 + 0.35 * u), (0, 0.2, 0.55))
    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)
print('[unison] done', OUT)
```

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video/blender3d"
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
  --python render_unison_proof.py
cd "D:/AI_GOAP-videos/3d/proof/unison"
ffmpeg -y -loglevel error -f lavfi -i "color=c=0x0E1117:s=1080x1920:r=30" \
  -framerate 30 -i frames/%04d.png \
  -filter_complex "[0][1]overlay=0:420:shortest=1" \
  -c:v libx264 -pix_fmt yuv420p -crf 18 unison.mp4
```

- [ ] **단계 6: 🔴 여기서 멈추고 사람에게 판정을 받는다**

물을 것은 하나다 — **자막 없이, 처음 0.3 초에 「여섯이 똑같다」가 읽히는가.**
읽히면 다음 작업으로 간다. 안 읽히면 A 안을 다시 본다(스펙 §1 로 되돌아가 B·C 를 다시 검토).

- [ ] **단계 7: 커밋**

```bash
git add tools/scene-video/blender3d/unison.py tools/scene-video/blender3d/test_unison.py tools/scene-video/blender3d/render_unison_proof.py
git commit -m "feat(video): 군무와 파열 — 설계의 가장 큰 가정을 실물로 확인한다"
```

---

## 작업 7: 대본 재작성 + TTS 재합성

**파일:**
- 수정: `tools/scene-video/episodes/ep15s-1/scene.json`
- 생성: `tools/scene-video/episodes/ep15s-1/notes/3d-rewrite.md`
- 산출: `episodes/ep15s-1/build/{audio/*.wav, full.wav, timed.json}`

**인터페이스:**
- 생산: 새 `timed.json` — 이후 모든 샷 렌더가 여기서 길이를 읽는다

🔴 **사건 셋은 안 바꾼다.** ①이름표만 여섯이고 하루는 같았다 ②목표 30 개 중 성격이 닿는 건
셋뿐 ③180 칸을 36 줄로 바꾸니 열다섯 쌍이 갈렸다. 이걸 지켜야 제목 이행·형제편 경계·
`ep14s-4` 가 이미 내보낸 예고가 그대로 산다.

🔴 **바꾸는 것은 도형을 전제한 낱말뿐이다.** 「백여든 칸」·「서른여섯 줄」은 화면에 칸과 줄이
있어야 성립한다. 장면에 있는 것(주민·할 일·겹침·갈라짐)으로 다시 쓴다.
「세어 보니 셋뿐」처럼 장면에서 그대로 보이는 말은 안 건드린다.

- [ ] **단계 1: 지금 문장을 표로 뽑는다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video"
python -c "
import json,io,sys
sys.stdout=io.TextIOWrapper(sys.stdout.buffer,encoding='utf-8')
d=json.load(open('episodes/ep15s-1/scene.json',encoding='utf-8'))
for i,s in enumerate(d['shots']):
    for j,l in enumerate(s.get('lines',[])):
        print(f'{i}.{j}  {l[\"text\"]!r}')
"
```

- [ ] **단계 2: 도형 전제 낱말을 표시한다**

`episodes/ep15s-1/notes/3d-rewrite.md` 에 세 열짜리 표를 만든다:
`지금 문장` · `도형을 전제하는가` · `새 문장`.
🔴 **「도형을 전제하는가」가 아니오면 새 문장 칸을 비워 둔다** — 안 건드리는 것이 기본값이다.

- [ ] **단계 3: `scene.json` 의 `lines[].text` 와 `say` 를 바꾼다**

각 샷의 `reads` 필드도 함께 고친다 — 그 필드는 「자막을 가려도 무엇이 읽히는가」의
선언이고, 그림이 통째로 바뀌었으므로 지금 값은 전부 거짓이다.

- [ ] **단계 4: TTS 를 재합성한다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video"
node tools/scene-video/tts.mjs ep15s-1
```

- [ ] **단계 5: 새 타이밍을 확인한다**

```bash
python -c "
import json,io,sys
sys.stdout=io.TextIOWrapper(sys.stdout.buffer,encoding='utf-8')
d=json.load(open('D:/AI_GOAP-videos/episodes/ep15s-1/build/timed.json',encoding='utf-8'))
tot=0
for i,s in enumerate(d['shots']):
    dur=sum(l['dur']+l.get('pause',0) for l in s['lines'])+350
    tot+=dur; print(i, s['id'], '%.3f'%(dur/1000))
print('total %.2f'%(tot/1000))
"
```
기대: 총 34 초 근처. 사건 밀도를 올리며 짧아지는 것은 허용한다(스펙 §4).

- [ ] **단계 6: 커밋**

```bash
git add tools/scene-video/episodes/ep15s-1/scene.json tools/scene-video/episodes/ep15s-1/notes/3d-rewrite.md
git commit -m "feat(video): ep15s-1 대본을 3D 장면 전제로 다시 쓴다

사건 셋은 그대로다. 도형을 전제한 낱말(칸·줄)만 장면에 있는 것으로 바꿨다."
```

---

## 작업 8: 인트로 — 마을이 지어지는 3.5 초

**파일:**
- 생성: `tools/scene-video/blender3d/render_intro.py`
- 산출: `D:\AI_GOAP-videos\3d\_intro\frames\*.png` (회차 밖 — 45 편이 공유한다)
- 테스트: `tools/scene-video/blender3d/test_intro.py`

**인터페이스:**
- 소비: `village.build()`(작업 4) · `unison.place()`(작업 6) · `stage`
- 생산: 프레임 106 장. **한 번만 렌더해서 모든 회차가 같은 것을 쓴다**

🔑 인트로가 짓는 그 마을이 본문의 무대다. 끝나는 순간 카메라가 컷 없이 훅으로 내려간다.

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`blender3d/test_intro.py`:

```python
import os, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import probe
FRAMES = r"D:\AI_GOAP-videos\3d\_intro\frames"


def test_intro_starts_empty_and_ends_with_a_village():
    first = probe.metrics(os.path.join(FRAMES, '0000.png'))
    last = probe.metrics(os.path.join(FRAMES, '0105.png'))
    assert first['alpha_cover'] < last['alpha_cover'] * 0.6, (first, last)


def test_intro_never_shows_more_than_two_meaning_colors():
    """스펙 §2 — 한 프레임에 뜻층 유채색은 둘까지."""
    for n in range(0, 106, 7):
        m = probe.metrics(os.path.join(FRAMES, '%04d.png' % n))
        assert len(m['chroma_hues']) <= 2, (n, m['chroma_hues'])


def test_intro_is_exactly_106_frames():
    """3.543 초 × 30fps. 엔진 타임라인과 어긋나면 뒤 회차가 통째로 밀린다."""
    assert len([f for f in os.listdir(FRAMES) if f.endswith('.png')]) == 106
```

- [ ] **단계 2: 테스트를 돌려 실패를 확인한다**

```bash
python -m pytest test_intro.py -v
```
기대: `FileNotFoundError` — 프레임이 아직 없다

- [ ] **단계 3: `render_intro.py` 를 쓴다**

```python
"""인트로 — 마을이 지어지는 3.5 초. **45 편이 공유하므로 한 번만 렌더한다.**

0.0~0.6  빈 땅. 초록 획(코드)이 화면을 가로지른다
0.6~1.8  집 셋이 차례로 솟는다
1.8~2.6  나무와 밭이 놓인다
2.6~3.2  주민 여섯이 하나씩 놓인다 → 마지막 하나가 놓이는 순간 여섯이 동시에 고개를 든다
3.2~3.5  브랜드(자막은 엔진이 얹는다)

🔴 유채색은 초록 하나뿐이다(코드=시스템이 한 일). 스펙 §2 의 「한 프레임에 둘까지」를
   인트로는 하나로 지킨다 — 여기서 색을 더 쓰면 본문이 쓸 예산을 미리 태운다."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison, motions

OUT = os.path.join(stage.OUT_ROOT, '_intro', 'frames')
FPS, DUR = 30, 3.543
os.makedirs(OUT, exist_ok=True)


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
v = village.build()
arms = unison.place()
cam = stage.light_camera()

# 코드 획 — 초록. 땅을 가로지르며 지나간 자리에서 마을이 솟는다
sweep = stage.meaning_mat('green', strength=2.4)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.02))
line = bpy.context.object
line.scale = (0.10, 26, 0.02)
line.data.materials.append(sweep)

RISE = {}                      # 오브젝트 → (솟기 시작, 걸리는 시간)
for i, h in enumerate(v['houses']):
    RISE[h] = (0.60 + i * 0.40, 0.42)
for i, t_ in enumerate(v['trees']):
    RISE[t_] = (1.80 + i * 0.14, 0.34)
RISE[v['field']] = (2.10, 0.36)
RISE[v['well']] = (2.30, 0.32)
RISE[v['campfire']] = (2.45, 0.30)
BASE_Z = {o: o.location.z for o in RISE}
BASE_SZ = {o: o.scale.z for o in RISE}

for fi in range(round(DUR * FPS) + 1):
    t = fi / FPS

    line.location.x = -13 + 26 * ease(t / 0.60)
    line.hide_render = t > 0.66

    for o, (t0, span) in RISE.items():
        k = ease((t - t0) / span)
        o.scale.z = max(BASE_SZ[o] * k, 1e-4)
        o.location.z = BASE_Z[o] * k
        o.hide_render = k <= 0.001

    for i, arm in enumerate(arms):
        t0 = 2.60 + i * 0.10
        k = ease((t - t0) / 0.20)
        arm.hide_render = k <= 0.001
        for ob in arm.children:
            ob.hide_render = arm.hide_render
        arm.location.z = -0.6 * (1 - k)
        # 마지막 하나가 놓인 뒤 여섯이 **동시에** 고개를 든다 — 훅으로 이어지는 비트
        stage.pose(arm, motions.MOTIONS['look_up'](max(0.0, t - 3.20)))

    u = t / DUR
    stage.aim(cam, (7.4 - 1.6 * u, -9.2 + 1.1 * u, 5.2 - 1.0 * u), (0, 1.0, 0.6))

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[intro] done', OUT)
```

- [ ] **단계 4: 렌더하고 테스트를 돌린다**

```bash
cd "C:/Users/anjyo/AI_GOAP-video/tools/scene-video/blender3d"
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
  --python render_intro.py
python -m pytest test_intro.py -v
```
기대: 3 passed

- [ ] **단계 5: 영상으로 보고 사람에게 보여준다**

```bash
cd "D:/AI_GOAP-videos/3d/_intro"
ffmpeg -y -loglevel error -f lavfi -i "color=c=0x0E1117:s=1080x1920:r=30" \
  -framerate 30 -i frames/%04d.png \
  -filter_complex "[0][1]overlay=0:420:shortest=1" \
  -c:v libx264 -pix_fmt yuv420p -crf 18 intro.mp4
```

- [ ] **단계 6: 커밋**

```bash
git add tools/scene-video/blender3d/render_intro.py tools/scene-video/blender3d/test_intro.py
git commit -m "feat(video): 인트로를 3D 로 — 마을이 지어지고 여섯이 동시에 고개를 든다

45 편이 공유하므로 한 번만 렌더한다. 인트로가 짓는 마을이 본문의 무대다."
```

---

## 이 계획이 다루지 않는 것 — 5~7 단계

스펙 §9 의 5~7 단계(본문 샷 다섯 · 엔진 캔버스 합성 · 게이트 둘 갱신)는 **이 계획에 안 넣었다.**

이유: **작업 6 이 A 안을 무효화할 수 있다.** 「여섯이 완벽히 같은 동작을 하면 0.3 초에
읽힌다」가 안 읽히면 연출 모드를 다시 고르게 되고, 그러면 샷 다섯의 설계가 통째로 바뀐다.
있을지 없을지 모르는 샷의 작업 단계를 지금 쓰는 것은 낭비다.

**작업 6 의 판정을 받은 뒤 두 번째 계획을 쓴다.** 그때 다룰 것:
- 훅 · 본문 셋 · 아웃트로 렌더
- `engine/` 에 3D 프레임 합성 kind — `/3d/` 에서 받아 `drawImage`
- `check.mjs` 팔레트 게이트 3층 규약 교체
- `check.mjs` 정적 게이트 → 비트 선언 게이트 (🔴 스펙 §8-2 — **영상을 보고 나서 확정**)

---

## 자체 검토 기록

- **스펙 대조:** §1 연출 모드 → 작업 6(A 구현, B·C 는 문서로만) · §2 팔레트 → 작업 3 ·
  §3 무대·자산 → 작업 4 · §4 비트 시트 → 2 차 계획 · §5 인트로 → 작업 8 ·
  §6 동작·카메라 → 작업 5·6 · §7 파이프라인 → 작업 1 · §8 게이트 → 2 차 계획 ·
  §9 구현 순서 → 이 계획이 1~4 단계를 덮는다.
- **타입 일관성:** `stage.pose(arm, spec)` 가 받는 dict 의 형태(뼈이름 → 3-튜플 라디안)를
  `motions.MOTIONS[*]` 가 돌려주는 형태와 맞췄다. `unison.pose_at` 도 같은 형태를 그대로 넘긴다.
- **고친 것:** 작업 8 의 `render_intro.py` 에 `cam` 정의가 빠져 있었다. 주의로 때우지 않고
  코드에 `cam = stage.light_camera()` 를 넣었다 — 계획에 「구현자가 알아서」가 들어가면
  그 자리가 반드시 틀린다.
- **알려진 빈틈:** 없다.
