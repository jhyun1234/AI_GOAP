---
name: project_implementation_phase11
description: 11단계 Win/Lose 조건 시스템 구현 완료 — 수정 4파일, GameResultType enum, 이벤트 패턴
metadata:
  type: project
---

## 11단계 구현 완료 — Win/Lose 조건 시스템 (2026-06-28)

**Why:** 1~10단계 완료 후 게임 결과 판정 로직 추가. GDD §11 확정 조건 기반.

**How to apply:** 다음 단계(12단계 경로탐색 등) 진행 시 GameManager에 _gameEnded 플래그가 존재함을 인지할 것. 틱 코루틴은 TriggerGameResult() 호출 시 정지된다.

### 수정된 파일 (4개)

1. `Assets/Scripts/Core/AuthoritativeWorldState.cs`
   - `WatchtowerBuilt` 바로 아래에 필드 2개 추가:
     - `SilverCitadelBuilt` (bool) — 최종 승리 조건
     - `TownHallEverBuilt` (bool) — Town Hall 파괴 패배 오탐 방지

2. `Assets/Scripts/Core/BuildingQueue.cs`
   - `ApplyBuildingCompletedEffect()` switch 문에 2개 추가:
     - `"TownHall"` case에 `worldState.TownHallEverBuilt = true` 추가
     - `"SilverCitadel"` case 신규 추가 (`worldState.SilverCitadelBuilt = true`)

3. `Assets/Scripts/Core/GameManager.cs`
   - 상수 region 신규 추가: `WIN1_SURVIVAL_DAYS=30f`, `WIN1_MIN_VILLAGERS=5`, `LOSE_TOWNHALL_MAX_VILLAGERS=2`
   - Private Fields에 `_gameEnded = false` 추가
   - 공개 프로퍼티 region에 `GameResultType` enum + `OnGameResultEvent` 추가
   - `GameTickCoroutine()` 내 EnemyGroup 틱 이후 `CheckWinLoseConditions()` 호출 추가
   - `CheckWinLoseConditions()` + `TriggerGameResult()` region 신규 추가
     - TriggerGameResult()는 틱 코루틴 정지 후 이벤트 발행

4. `Assets/Scripts/UI/GameResultPanel.cs` — 신규 파일
   - `GameManager.OnGameResultEvent` 구독 (Start / OnDestroy)
   - `HandleGameResult(GameResultType result)`: 4종 결과 텍스트 설정 + `_panelRoot.SetActive(true)`
   - `OnRetryClicked()`: `SceneManager.LoadScene(currentSceneName)`
   - `OnMainMenuClicked()`: 동일 씬 재로드 (TODO: 메인 메뉴 씬 구현 후 교체)

### 승리/패배 판정 우선순위
1. Win3_Prosperity — SilverCitadelBuilt == true (최우선)
2. Win1_Survival   — TownHallBuilt + 주민 ≥5 + GameTime ≥30
3. Lose_Annihilated — aliveCount <= 0
4. Lose_TownHallFallen — TownHallEverBuilt && !TownHallBuilt && aliveCount <= 2

### Win2(팩션 제압/동맹)
이번 단계 범위 밖. `GameResultType.None` 유지, 향후 추가 예정.

### 씬 배치 필수 사항 (GameResultPanel)
- Canvas 아래 Panel GameObject에 `GameResultPanel` 컴포넌트 부착
- Inspector: `_panelRoot`, `_titleText(TMP_Text)`, `_descriptionText(TMP_Text)`, `_retryButton`, `_mainMenuButton` 연결
- `_panelRoot`는 씬 저장 시 비활성(SetActive=false) 상태여야 함
- `TextMesh Pro` 패키지 임포트 필요
