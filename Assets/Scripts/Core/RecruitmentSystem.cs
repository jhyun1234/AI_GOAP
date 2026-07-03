/// <summary>
/// RecruitmentSystem.cs - 주민 모집 처리 싱글턴
///
/// 역할(Role): TryRecruit() 호출 시 자원 유효성 검사 → 자원 차감 → 주민 Instantiate →
///             VillagerBrain 초기 스탯 설정 → GameManager 등록까지 원자적으로 처리한다.
/// 사용법(Usage): 씬에 빈 GameObject("_RecruitmentSystem")를 만들고 이 컴포넌트를 부착한다.
///               RecruitmentPanel이 이 싱글턴의 TryRecruit()을 호출한다.
/// 의존성(Dependencies):
///   - GameManager.cs (RegisterNewVillager, BaseTileX/Y)
///   - AuthoritativeWorldState.cs (자원 차감)
///   - VillagerFSM.cs, VillagerBrain.cs (스폰 후 초기화)
///   - VillagerRecruitData.cs (모집 항목 데이터)
///
/// 실행 순서: GameManager(-80) 이후, VillagerFSM(0) 이전 → -60
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-07-02
/// </summary>

using UnityEngine;
using AIVillage.AI;

namespace AIVillage.Core
{
    /// <summary>
    /// 주민 모집의 모든 처리를 담당하는 싱글턴 MonoBehaviour.
    /// ExecutionOrder -60: GameManager(-80) 이후에 초기화되어 Instance 참조가 보장된다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class RecruitmentSystem : MonoBehaviour
    {
        #region ── 싱글턴 ──

        /// <summary>씬 내 유일한 RecruitmentSystem 인스턴스. RecruitmentPanel이 참조한다.</summary>
        public static RecruitmentSystem Instance { get; private set; }

        #endregion

        #region ── Serialized Fields ──

        [Tooltip("기본 스폰 오프셋. 기지 타일 위치에서 이 값만큼 월드 좌표를 더한 위치에 스폰된다.")]
        [SerializeField] private Vector2 _spawnOffset = Vector2.zero;

        [Tooltip("스폰 위치 랜덤 반경 (월드 단위). 0이면 기지 중심에 정확히 스폰. " +
                 "여러 주민이 동시 모집 시 겹침 방지를 위해 0.4f 이상을 권장한다.")]
        [SerializeField] private float _spawnJitterRadius = 0.4f;

        #endregion

        #region ── Unity 생명주기 ──

        /// <summary>싱글턴 설정. 중복 인스턴스를 Destroy한다.</summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[RecruitmentSystem] 중복 인스턴스 감지. 이 인스턴스를 Destroy합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        #endregion

        #region ── 공개 API ──

        /// <summary>
        /// 자원 충족 여부와 건물 해금 여부를 함께 확인한다.
        /// RecruitmentPanel이 버튼 interactable 갱신 시 사용한다.
        /// </summary>
        /// <param name="data">확인할 모집 항목 데이터.</param>
        public bool CanRecruit(VillagerRecruitData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[RecruitmentSystem] CanRecruit: data가 null입니다.");
                return false;
            }

            AuthoritativeWorldState ws = AuthoritativeWorldState.Instance;
            if (ws == null) return false;

            return data.IsUnlocked(ws) && data.CanAfford(ws);
        }

        /// <summary>
        /// 주민 모집을 실행한다. 자원 차감 → Instantiate → Brain 초기화 → GameManager 등록 순서로 진행.
        /// GameManager나 villagerPrefab이 null이면 실패 로그를 출력하고 false를 반환한다.
        /// </summary>
        /// <param name="data">모집할 주민의 데이터 정의.</param>
        /// <param name="villagerPrefab">스폰할 주민 Prefab. VillagerFSM 컴포넌트를 포함해야 한다.</param>
        /// <returns>모집 성공 여부.</returns>
        public bool TryRecruit(VillagerRecruitData data, GameObject villagerPrefab)
        {
            // ── 사전 조건 방어 ──────────────────────────────────────────────────
            if (data == null)
            {
                Debug.LogWarning("[RecruitmentSystem] TryRecruit: data가 null입니다.");
                return false;
            }

            if (villagerPrefab == null)
            {
                Debug.LogWarning("[RecruitmentSystem] TryRecruit: villagerPrefab이 null입니다. " +
                                 "RecruitmentPanel Inspector에서 _villagerPrefab을 연결하세요.");
                return false;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[RecruitmentSystem] TryRecruit: GameManager.Instance가 null입니다.");
                return false;
            }

            AuthoritativeWorldState ws = AuthoritativeWorldState.Instance;
            if (ws == null)
            {
                Debug.LogWarning("[RecruitmentSystem] TryRecruit: AuthoritativeWorldState.Instance가 null입니다.");
                return false;
            }

            // ── 해금 조건 및 자원 충족 재확인 ─────────────────────────────────────
            // 버튼 클릭 시점과 실제 차감 시점 사이에 자원이 변동될 수 있으므로 재확인한다.
            if (!data.IsUnlocked(ws))
            {
                Debug.Log($"[RecruitmentSystem] TryRecruit 실패: 해금 조건 미충족. " +
                          $"recruitId={data.recruitId}, 필요={data.unlockRequirement}");
                return false;
            }

            if (!data.CanAfford(ws))
            {
                Debug.Log($"[RecruitmentSystem] TryRecruit 실패: 자원 부족. " +
                          $"recruitId={data.recruitId}, " +
                          $"식량필요={data.cookedFoodCost}(보유={ws.CookedFoodStock:F0}), " +
                          $"철={data.ironCost}(보유={ws.IronStock:F0}), " +
                          $"구리={data.copperCost}(보유={ws.CopperStock:F0}), " +
                          $"은={data.silverCost}(보유={ws.SilverStock:F0})");
                return false;
            }

            // ── 스폰 위치 계산 ─────────────────────────────────────────────────
            // 기지 타일 좌표를 월드 좌표로 사용하고 랜덤 지터를 더한다.
            float jitterX = Random.Range(-_spawnJitterRadius, _spawnJitterRadius);
            float jitterY = Random.Range(-_spawnJitterRadius, _spawnJitterRadius);
            Vector3 spawnPos = new Vector3(
                GameManager.Instance.BaseTileX + _spawnOffset.x + jitterX,
                GameManager.Instance.BaseTileY + _spawnOffset.y + jitterY,
                0f);

            // ── Instantiate (자원 차감 이전) ──────────────────────────────────
            // 자원을 차감하기 전에 컴포넌트 유효성을 먼저 검사한다.
            // 이 순서를 지켜야 Prefab 설정 오류 시 자원이 영구 소실되는 것을 방지할 수 있다.
            GameObject go = Instantiate(villagerPrefab, spawnPos, Quaternion.identity);
            go.name = $"Villager_{data.role}_{data.recruitId}";

            VillagerFSM fsm = go.GetComponent<VillagerFSM>();
            if (fsm == null)
            {
                Debug.LogError("[RecruitmentSystem] TryRecruit: Prefab에 VillagerFSM이 없습니다. " +
                               "자원은 차감되지 않았습니다. Prefab 설정을 확인하세요.");
                Destroy(go);
                return false;
            }

            // ── 자원 차감 (컴포넌트 유효성 확인 후) ─────────────────────────────
            ws.CookedFoodStock -= data.cookedFoodCost;
            ws.IronStock       -= data.ironCost;
            ws.CopperStock     -= data.copperCost;
            ws.SilverStock     -= data.silverCost;

            // ── VillagerBrain 초기 스탯 설정 ─────────────────────────────────
            // Brain은 VillagerFSM.Awake()에서 이미 생성됐으므로 null이 아님이 보장된다.
            fsm.Brain.InitFromRecruitData(data);
            fsm.Brain.TileX = GameManager.Instance.BaseTileX;
            fsm.Brain.TileY = GameManager.Instance.BaseTileY;

            // ── GameManager 등록 ──────────────────────────────────────────────
            GameManager.Instance.RegisterNewVillager(fsm);

            Debug.Log($"[RecruitmentSystem] 모집 완료. " +
                      $"recruitId={data.recruitId}, 역할={data.role}, 이름={data.displayName}, " +
                      $"체력={fsm.Brain.HealthLevel:F0}, 충성도={fsm.Brain.LoyaltyLevel:F0}");

            return true;
        }

        #endregion
    }
}
