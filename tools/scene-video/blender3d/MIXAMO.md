# Mixamo 받는 법 — 이 파일이 장바구니다

> 코드가 사람을 못 만든다. **mixamo.com 은 Adobe 로그인이 필요해서 자동화가 못 받는다** —
> 받는 것은 사람 몫이고, 굽는 것부터는 `mixamo.py` 가 한다.

---

## 0. 어디에 넣나

```
blender3d/mixamo/
  ybot.fbx            ← 캐릭터 (하나)
  clips/<이름>.fbx    ← 동작 (여러 개). **파일 이름이 곧 동작 이름이다**
```

받아서 넣고 나면:

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python mixamo.py -- build
```

굽힌 결과 = `D:\AI_GOAP-videos\3d\models\ybot.blend`. 검사는:

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python mixamo.py -- demo
```

🔴 `.fbx` 는 gitignore 다. Mixamo 는 재배포 금지라 리포에 안 싣는다 — 기기마다 다시 받는다.

---

## 1. 캐릭터 — 한 번만

1. mixamo.com → **Characters** 탭 → `Y Bot` (또는 `X Bot`)
2. **DOWNLOAD**
   - Format: **FBX Binary (.fbx)**
   - Pose: **T-pose**
3. 파일 이름을 `ybot.fbx` 로 바꿔 `blender3d/mixamo/` 에 넣는다

🔑 Y Bot 을 고른 이유: 얼굴도 옷도 없는 무채색이라 마을의 저폴리 톤과 안 싸우고,
**팔레트 4색 계약(색 = 뜻)** 을 옷 색이 안 깨뜨린다. 인물 색은 우리가 머티리얼로 칠한다.

---

## 2. 동작 — 공통 설정 (전부 같다)

각 클립 **DOWNLOAD** 창에서:

| 항목 | 값 |
|---|---|
| Format | **FBX Binary (.fbx)** |
| Skin | **Without Skin** ← 캐릭터는 이미 있다. 켜면 파일만 커진다 |
| Frames per Second | **30** |
| Keyframe Reduction | **none** |

그리고 **걷기·달리기류만** 클립 화면 왼쪽에서 **`In Place` 체크**.
🔴 안 켜면 사람이 무대 밖으로 걸어 나간다 — 이동은 무대의 몫이다(`march`).

---

## 3. 장바구니

🔴 **파일 이름은 안 바꿔도 된다.** `Breathing Idle.fbx`·`Running (1).fbx` 처럼 Mixamo 가
내려준 이름 그대로 넣으면 `mixamo.ALIAS` 가 알아본다. 표의 「저장할 이름」은 **결과로
붙는 동작 이름**이다.
🔴 같은 동작 이름이 둘이 되면 빌드가 **막는다**(조용히 나쁜 쪽이 이기는 것을 한 번 겪었다).

### 1차 — ✅ 다 받음 (전쟁 축 준비 완료)

| 저장할 이름 | Mixamo 검색어 | 비고 |
|---|---|---|
| `idle.fbx` | `Breathing Idle` | ✅ 받음 |
| `walk.fbx` | `Walking` | ✅ 받음 · 지면속도 **1.577 m/s** |
| `run.fbx` | `Running` | ✅ 받음 · **4.954 m/s** |
| `attack.fbx` | `Sword And Shield Slash` | ✅ 받음 · 🔑 0.47m 나아가는데 그건 **찌르는 발**이라 `MOVES` 다 |
| `death.fbx` | `Falling Back Death` | ✅ 받음 (`Standing Death Backward 01`) |

### 2차 — 전쟁 축 (남은 둘)

| 저장할 이름 | Mixamo 검색어 | 비고 |
|---|---|---|
| `hit.fbx` | `Standing React Small From Front` | 맞는다 |
| `block.fbx` | `Sword And Shield Block` | 막는다 |
| `shoot.fbx` | `Standing Draw Arrow` | ✅ 받음 |
| `flee.fbx` | `Standing Sprint Forward` 또는 `Running` + 겁 표정 | 겁/용맹 축의 「겁」쪽 |
| `limp.fbx` | `Injured Walking` | ✅ 받음 (In Place) |

### 3차 — 일상 (전쟁 축을 위해서도 이게 필요하다)

> 🔑 **왜 일상인가.** 함락이 끝이라면, 끝나서 아까울 것이 화면에 있어야 한다.
> 마을이 아무것도 안 하고 있으면 그 마을이 무너져도 잃은 것이 없다.
> 그래서 이 목록의 기준은 「많이」가 아니라 **「잃을 것이 보이는가」**다.

🔴 **일 동작은 셋을 받지 않는다.** 도끼질·곡괭이질·망치질은 **같은 내려치기**고,
무슨 일인지는 **손에 든 것**이 말한다. 도구는 `village.axe()`·`pickaxe()`·`hammer()`·
`wateringcan()` 으로 **이미 다 있다.** 코드의 `mixamo.JOB` 이 그 짝을 들고 있다:

| 게임 `AnimKind` | 클립 | 손에 |
|---|---|---|
| `Chop` | `work` | `axe` |
| `Mine` | `work` | `pickaxe` |
| `Hammer` | `work` | `hammer` |
| `Water` | `water` | `wateringcan` |
| `Attack` | `attack` ✅ | `sword` |

#### 받을 것 — 여섯

| 저장할 이름 | Mixamo 검색어 | 무엇을 사는가 | 확신 |
|---|---|---|---|
| `work` | `Standing Melee Attack Downward` | 🔴 **가장 급하다.** 이 하나가 `AnimKind` 셋을 덮는다 | 높음 |
| `talk` | `Talking` | 마을에 **사람이 산다**. 둘이 마주 서면 그것만으로 관계가 생긴다 | 높음 |
| `sit` | `Sitting Idle` | 쉼. 일하는 사람만 있으면 마을이 아니라 작업장이다 | 높음 |
| `carry` | `Walking With A Bag` 또는 `Carrying` | 자원이 **옮겨진다**. 나르는 사람이 있으면 마을에 경제가 있다. 🔴 **In Place 켠다** | 중간 |
| `look` | `Looking Around` | 경계. 전쟁 축의 **정찰**이 이 몸짓이다 | 중간 |
| `tend` | `Crouching` / `Crouch To Stand` | 밭을 돌본다. `water` 자리에 쓴다 | 중간 |

⚠️ **확신 「중간」은 이름이 그대로 있을지 모른다는 뜻이다.** 검색해서 없으면 비슷한 것을
받고 파일 이름만 위 표의 「저장할 이름」으로 바꿔라 — `mixamo.ALIAS` 에 없는 이름은
**파일 이름 그대로** 동작 이름이 된다.

🔴 **물 주기(`water`)는 Mixamo 에 없다시피 하다.** 찾아봐서 없으면 `tend` 로 대신한다 —
쭈그려 앉은 사람 손에 물뿌리개를 쥐여 주면 그것이 「밭을 돌본다」다. 몸짓이 아니라
**도구가 뜻을 만든다**는 것이 이 표 전체의 원리다.

### 4차 — 있으면 좋은 것 (급하지 않다)

`Waving`(`wave`) · `Yelling`(`shout` — 경보) · `Standing Melee Attack Horizontal`(`slash` 변주)

## 4. 이름 규약

🔴 **파일 이름이 동작 이름이고, 그 이름이 게임의 `AnimKind` 와 같아야 한다.**
갈리면 같은 몸짓을 게임과 영상이 두 이름으로 부르게 된다.
게임 쪽 정본: `Assets/Scripts/M0/Data/ActionSO.cs`

---

## 5. 왜 이 판에는 리타기팅이 없나

캐릭터와 동작이 **같은 뼈대**(`mixamorig:*`)를 쓴다. 옮길 것이 없으니 옮기는 코드도 없다.
앞 판이 밟은 지뢰 여덟 — 오일러 짐벌 · roll 임의값 · 레스트 차이 43° · 자동 웨이트 폭발 ·
척추 3→1 · 축 켤레 — 은 전부 「남의 뼈대를 우리 뼈대로 옮긴다」에서 나왔고,
그 전제를 지우면 같이 사라진다. 그래서 `mocap2pose.py`(351줄)가 통째로 없어진다.
