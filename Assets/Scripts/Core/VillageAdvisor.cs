/// <summary>
/// VillageAdvisor.cs - 마을 단위 자율 건물 결정 시스템
///
/// 역할(Role): 마을 전체 상태(자원, 인구, 시간, 침략 횟수)를 주기적으로 평가하여
///             현재 가장 필요한 건물을 BuildingQueue에 자동으로 큐잉한다.
///             플레이어의 직접 입력 없이도 마을이 자율적으로 성장하도록 지원한다.
///
/// 사용법(Usage):
///   1. Hierarchy에서 _Managers 하위에 빈 GameObject("VillageAdvisor")를 생성한다.
///   2. 이 컴포넌트(VillageAdvisor)를 해당 GameObject에 부착한다.
///   3. Inspector 연결 작업 불필요 — GameManager.Start()에서 FindObjectOfType으로 자동 수집된다.
///   4. BuildingQueue, GameManager가 씬에 배치되어 있어야 한다.
///
/// 우선순위 규칙 (6단계, 높을수록 먼저 큐잉):
///   1. Campfire  — 생존자 >= 1명
///   2. House     — 생존자 >= 3명
///   3. Storehouse — 총 자원 합계 >= 80
///   4. Forge     — GameTime >= 5일, Iron > 0
///   5. Watchtower — 침략 횟수 >= 1
///   6. TownHall  — GameTime >= 15일
///
/// 의존성(Dependencies):
///   - AuthoritativeWorldState.cs (월드 플래그 및 자원 재고 읽기)
///   - BuildingQueue.cs (건설 대기열 추가)
///   - GameManager.cs (GameTime, Villagers 목록 접근)
///   - VillagerFSM.cs (Brain.IsAlive로 생존 주민 수 계산)
///
/// 실행 순서(Script Execution Order):
///   GameManager(-80) → FactionAI(-70) → VillageAdvisor(-65) → VillagerFSM(0)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

using System.Collections;
using UnityEngine;
using AIVillage.AI; // VillagerFSM, VillagerBrain

namespace AIVillage.Core
{
    /// <summary>
    /// 마을 전체의 건물 필요도를 주기적으로 평가하고 BuildingQueue에 건설 요청을 자동으로 추가하는
    /// 자율 결정 시스템. 한 번의 평가 사이클에서 최대 1개의 건물만 큐잉한다(우선순위 순).
    /// </summary>
    [DefaultExecutionOrder(-65)] // GameManager(-80) > FactionAI(-70) > VillageAdvisor(-65) > VillagerFSM(0)
    [DisallowMultipleComponent]
    public sealed class VillageAdvisor : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        // ── 우선순위 조건 상수 (기획서 수치) ─────────────────────────────────

        /// <summary>House 건설에 필요한 최소 생존 주민 수 (기획서 수치: 3명).</summary>
        private const int   HOUSE_MIN_VILLAGERS      = 3;

        /// <summary>Storehouse 건설에 필요한 총 자원 합계 임계값 (기획서 수치: 80).</summary>
        private const float STOREHOUSE_STOCK_THRESHOLD = 80f;

        /// <summary>Forge 건설에 필요한 최소 게임 경과 일수 (기획서 수치: 5일).</summary>
        private const float FORGE_MIN_GAME_TIME       = 5f;

        /// <summary>Watchtower 건설에 필요한 최소 침략 횟수 (기획서 수치: 1회).</summary>
        private const int   WATCHTOWER_MIN_RAID_COUNT = 1;

        /// <summary>TownHall 건설에 필요한 최소 게임 경과 일수 (기획서 수치: 15일).</summary>
        private const float TOWNHALL_MIN_GAME_TIME    = 15f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("건물 필요도 평가 주기(초). 기본 5초. 너무 짧으면 BuildingQueue에 중복 요청이 빈번해진다.")]
        [SerializeField] private float _evaluationInterval = 5f; // 기획서 수치: 5초 주기

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        /// <summary>
        /// 코루틴 핸들. OnDestroy에서 안전하게 중단하기 위해 보관한다.
        /// </summary>
        private Coroutine _advisorCoroutine;

        /// <summary>
        /// GC 할당 방지를 위해 WaitForSeconds를 Start에서 1회만 생성하여 재사용한다.
        /// 코루틴 루프에서 매 틱 new WaitForSeconds()를 생성하면 GC 부하가 발생한다.
        /// </summary>
        private WaitForSeconds _waitForEvaluationInterval;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// Start에서 WaitForSeconds 인스턴스를 1회 생성하고 평가 코루틴을 시작한다.
        /// Awake가 아닌 Start를 사용하는 이유: GameManager(-80)의 Awake에서 WorldState와
        /// 기타 시스템이 초기화된 뒤에 코루틴이 실행되도록 보장하기 위함이다.
        /// </summary>
        private void Start()
        {
            // WaitForSeconds를 미리 생성하여 코루틴 루프에서 GC 부하를 방지한다
            _waitForEvaluationInterval = new WaitForSeconds(_evaluationInterval);

            // 평가 코루틴 시작 — 핸들을 저장해 OnDestroy에서 안전하게 중단한다
            _advisorCoroutine = StartCoroutine(AdvisorTickCoroutine());

            Debug.Log($"[VillageAdvisor] 초기화 완료. 평가 주기: {_evaluationInterval}초.");
        }

        /// <summary>
        /// 씬 언로드 또는 GameObject Destroy 시 평가 코루틴을 안전하게 중단한다.
        /// 코루틴이 null인 경우(Start가 호출되지 않은 경우)도 방어 처리한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_advisorCoroutine != null)
            {
                StopCoroutine(_advisorCoroutine);
                _advisorCoroutine = null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 메서드 ──

        // ── 코루틴 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 일정 주기(_evaluationInterval)마다 EvaluateBuildingNeeds()를 호출하는 무한 루프 코루틴.
        /// WaitForSeconds 인스턴스는 Start에서 1회 생성한 캐시를 재사용한다(GC 방지).
        /// </summary>
        private IEnumerator AdvisorTickCoroutine()
        {
            // 첫 평가 전에 한 틱 대기하여 다른 Start()들이 완료될 시간을 확보한다
            yield return _waitForEvaluationInterval;

            while (true)
            {
                EvaluateBuildingNeeds();

                // ─────────────────────────────────────────────────────────────
                // 캐시된 WaitForSeconds를 재사용 — new WaitForSeconds()를 매 루프마다
                // 생성하면 GC 압박이 발생한다. Start에서 1회 생성 후 재사용.
                // ─────────────────────────────────────────────────────────────
                yield return _waitForEvaluationInterval;
            }
        }

        // ── 핵심 평가 로직 ──────────────────────────────────────────────────

        /// <summary>
        /// 마을 상태를 우선순위 순서대로 평가하여 필요한 건물 1개를 BuildingQueue에 추가한다.
        /// 조건을 충족하는 첫 번째 건물을 큐잉한 뒤 즉시 return하여 중복 큐잉을 방지한다.
        ///
        /// 평가 우선순위:
        ///   1. Campfire  → 2. House → 3. Storehouse
        ///   → 4. Forge   → 5. Watchtower → 6. TownHall
        /// </summary>
        private void EvaluateBuildingNeeds()
        {
            // ── null 방어: AuthoritativeWorldState 접근 전 필수 확인 ──────────
            AuthoritativeWorldState ws = AuthoritativeWorldState.Instance;
            if (ws == null)
            {
                Debug.LogWarning("[VillageAdvisor] EvaluateBuildingNeeds: AuthoritativeWorldState.Instance가 null입니다. 평가 건너뜀.");
                return;
            }

            // 생존 주민 수는 여러 규칙에서 공유하므로 1회만 계산한다
            int aliveVillagers = CountAliveVillagers();

            // GameTime은 GameManager가 null인 경우를 방어하여 0으로 폴백한다
            float gameTime = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;

            // ── 우선순위 1: Campfire — 생존자 1명 이상이면 즉시 건설 ──────────
            // 모닥불은 가장 기본적인 생존 시설이므로 다른 모든 건물보다 우선한다
            if (!ws.CampfireBuilt && aliveVillagers >= 1)
            {
                TryEnqueue("Campfire", ws);
                return; // 하나씩만 큐잉: 조건 충족 즉시 return
            }

            // ── 우선순위 2: House — 생존자 3명 이상이면 주거 시설 확보 ─────────
            // 주민이 3명 이상이어야 건물을 짓는 인력과 생활할 인력이 분리 가능하다
            if (!ws.HouseBuilt && aliveVillagers >= HOUSE_MIN_VILLAGERS)
            {
                TryEnqueue("House", ws);
                return;
            }

            // ── 우선순위 3: Storehouse — 총 자원 합계가 80 이상이면 창고 필요 ──
            // 자원이 충분히 쌓였을 때 창고를 지어 잉여 자원 관리 효율을 높인다
            if (!ws.StorehouseBuilt && GetTotalResourceStock(ws) >= STOREHOUSE_STOCK_THRESHOLD)
            {
                TryEnqueue("Storehouse", ws);
                return;
            }

            // ── 우선순위 4: Forge — 5일 이상 경과 + Iron 보유 시 기술 발전 ────
            // 철이 있는 상태에서 일정 시간이 지나야 대장간 건설이 의미 있다
            if (!ws.ForgeBuilt && gameTime >= FORGE_MIN_GAME_TIME && ws.IronStock > 0f)
            {
                TryEnqueue("Forge", ws);
                return;
            }

            // ── 우선순위 5: Watchtower — 침략 경험이 1회 이상이면 방어 시설 ───
            // 한 번이라도 침략을 경험해야 마을이 위협을 인식하고 방어탑을 짓는다
            if (!ws.WatchtowerBuilt && ws.RaidCount >= WATCHTOWER_MIN_RAID_COUNT)
            {
                TryEnqueue("Watchtower", ws);
                return;
            }

            // ── 우선순위 6: TownHall — 15일 이상 경과 시 마을 조직화 ──────────
            // 충분한 시간이 지나 마을이 성숙해졌을 때 중심 건물을 건설한다
            if (!ws.TownHallBuilt && gameTime >= TOWNHALL_MIN_GAME_TIME)
            {
                TryEnqueue("TownHall", ws);
                return;
            }

            // ── 모든 조건 미충족: 현재 큐잉할 건물 없음 ────────────────────────
            // 조건 미충족 시 매 틱 로그를 출력하면 Console이 오염된다 — 출력하지 않는다
        }

        // ── 큐잉 헬퍼 ──────────────────────────────────────────────────────

        /// <summary>
        /// BuildingQueue에 건물 건설 요청을 추가하고 WorldState의 BuildingQueued 플래그를 갱신한다.
        /// BuildingQueue.Instance가 null이거나, 이미 동일 buildingId가 대기 중이면 조용히 무시한다
        /// (중복 방지는 BuildingQueue.EnqueueBuilding()이 처리한다).
        ///
        /// 건설 위치는 항상 (0, 0) 기지 타일로 고정한다 — 기획서 결정 사항.
        /// </summary>
        /// <param name="buildingId">건설할 건물 ID (예: "Campfire", "House")</param>
        /// <param name="ws">WorldState 참조. BuildingQueued 플래그 설정에 사용한다.</param>
        private void TryEnqueue(string buildingId, AuthoritativeWorldState ws)
        {
            // BuildingQueue 싱글턴 null 방어
            if (BuildingQueue.Instance == null)
            {
                Debug.LogWarning($"[VillageAdvisor] TryEnqueue: BuildingQueue.Instance가 null입니다. '{buildingId}' 큐잉 불가.");
                return;
            }

            // ─────────────────────────────────────────────────────────────
            // 건설 위치 (0, 0): 기지 타일로 고정 (기획서 결정 사항)
            // 중복 방지: EnqueueBuilding()이 동일 buildingId가 이미 Pending/InProgress면 false 반환
            // ─────────────────────────────────────────────────────────────
            bool added = BuildingQueue.Instance.EnqueueBuilding(buildingId, 0, 0);

            if (added)
            {
                // 건설 요청이 수락된 경우에만 WorldState 플래그를 true로 갱신한다
                ws.BuildingQueued = true;

                // GameTime을 안전하게 읽어 로그에 포함한다
                float gameTime = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;
                Debug.Log($"[VillageAdvisor] '{buildingId}' 건설 큐잉. GameTime={gameTime:F2}");
            }
            // added == false인 경우: BuildingQueue가 이미 경고를 출력하므로 여기서는 추가 로그 없음
        }

        // ── 집계 헬퍼 ──────────────────────────────────────────────────────

        /// <summary>
        /// 현재 씬에서 살아있는 주민 수를 반환한다.
        /// LINQ 대신 인덱스 기반 for 루프를 사용하여 GC 할당을 방지한다.
        ///
        /// GameManager.Instance가 null이거나 Villagers 목록이 없으면 0을 반환한다.
        /// Brain이 null인 주민은 초기화 미완료로 간주하여 카운트에서 제외한다.
        /// </summary>
        /// <returns>Brain != null 이고 Brain.IsAlive == true인 주민 수.</returns>
        private int CountAliveVillagers()
        {
            // GameManager null 방어
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[VillageAdvisor] CountAliveVillagers: GameManager.Instance가 null입니다. 0 반환.");
                return 0;
            }

            var villagers = GameManager.Instance.Villagers;

            // Villagers 목록 null 방어 (IReadOnlyList는 null일 수 없지만 방어적으로 확인)
            if (villagers == null)
            {
                Debug.LogWarning("[VillageAdvisor] CountAliveVillagers: Villagers 목록이 null입니다. 0 반환.");
                return 0;
            }

            int count = 0;

            // ─────────────────────────────────────────────────────────────
            // LINQ 사용 금지: Count()나 Where()는 내부적으로 열거자를 생성하여
            // GC 부하를 유발한다. 인덱스 기반 for 루프로 GC 할당 없이 순회한다.
            // ─────────────────────────────────────────────────────────────
            int villagerCount = villagers.Count;
            for (int i = 0; i < villagerCount; i++)
            {
                VillagerFSM fsm = villagers[i];

                // VillagerFSM 또는 Brain이 null인 경우 초기화 미완료로 간주하고 건너뜀
                if (fsm == null || fsm.Brain == null)
                {
                    continue;
                }

                if (fsm.Brain.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 5종 자원(Wood, Stone, Iron, Copper, Silver)의 합계를 반환한다.
        /// Storehouse 건설 조건(합계 >= 80) 판단에 사용한다.
        ///
        /// 식량(RawFood, CookedFood)은 소비재이므로 인프라 건설 판단 자원에서 제외한다.
        /// </summary>
        /// <param name="ws">자원 재고를 읽을 WorldState 참조.</param>
        /// <returns>WoodStock + StoneStock + IronStock + CopperStock + SilverStock 합계.</returns>
        private float GetTotalResourceStock(AuthoritativeWorldState ws)
        {
            // ─────────────────────────────────────────────────────────────
            // 합산 공식: 5종 건자재/광물 재고의 단순 합계
            // 식량(RawFood, CookedFood)은 소비재이므로 제외 — 기획서 결정 사항
            // ─────────────────────────────────────────────────────────────
            return ws.WoodStock + ws.StoneStock + ws.IronStock + ws.CopperStock + ws.SilverStock;
        }

        #endregion
    }
}
