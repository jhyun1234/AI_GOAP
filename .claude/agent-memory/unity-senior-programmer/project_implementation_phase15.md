---
name: project-implementation-phase15
description: 15단계 주민 모집 시스템 구현 완료 — VillagerRecruitData ScriptableObject, RecruitmentSystem, RecruitmentPanel, VillagerBrain/GameManager/HUDManager 수정
metadata:
  type: project
---

15단계 주민 모집 시스템이 2026-07-02에 구현 완료됐다.

**Why:** 하드코딩 없이 ScriptableObject 기반으로 기획팀이 Inspector에서 수치를 조정할 수 있도록 설계.

**신규 파일 3개:**
- `Assets/Scripts/Core/VillagerRecruitData.cs` — ScriptableObject. RecruitTier/BuildingUnlockRequirement enum 동일 파일 정의. CanAfford()/IsUnlocked() 헬퍼 포함.
- `Assets/Scripts/Core/RecruitmentSystem.cs` — MonoBehaviour 싱글턴, ExecutionOrder -60. TryRecruit() 원자 처리(자원차감→Instantiate→Brain초기화→RegisterNewVillager).
- `Assets/Scripts/UI/RecruitmentPanel.cs` — Inspector 슬롯 방식(최대 8개). OnEnable/OnDisable 코루틴 패턴. 람다 클로저 인덱스 오류 방지(로컬 idx 변수). BuildCostString() 한국어 형식.

**수정 파일 3개:**
- `Assets/Scripts/AI/VillagerBrain.cs` — InitFromRecruitData(VillagerRecruitData) 메서드 추가. Random.Range로 min/max 범위 스탯 초기화.
- `Assets/Scripts/Core/GameManager.cs` — _recruitmentSystem 필드 추가, Start() Step 7-a에서 FindObjectOfType<RecruitmentSystem>() 수집.
- `Assets/Scripts/UI/HUDManager.cs` — _recruitmentPanel SerializeField 추가, _townHallWatchCoroutine 추가, Start()에서 WatchTownHallCoroutine() 시작, OnDestroy 정리.

**핵심 설계 결정:**
- RecruitmentPanel은 HUDManager의 WatchTownHallCoroutine(0.5초 폴링)이 TownHallBuilt 감지 시 SetActive(true) 호출 → OnEnable에서 자동으로 RefreshCoroutine 시작
- TownHall 해금 패널은 한 번 활성화되면 비활성화하지 않으므로 WatchTownHallCoroutine은 yield break로 자가 종료
- 기획서 비용표(15+2티어 항목) Inspector에서 에셋 15개 생성 필요 — 코드로 하드코딩 없음

**How to apply:** 다음 세션에서 수정 대상 파일을 찾을 때 이 목록 참조.
