/// <summary>
/// VillagerRecruitData.cs - 주민 모집 1개 항목의 모든 데이터를 담는 ScriptableObject
///
/// 역할(Role): 역할, 티어, 해금 조건, 모집 비용, 초기 스탯 범위를 Inspector에서 조정 가능하게 보관한다.
///             RecruitmentSystem.TryRecruit()이 이 데이터를 읽어 자원 차감과 주민 초기화를 수행한다.
/// 사용법(Usage): Project 창 우클릭 → Create → AIVillage → VillagerRecruitData로 생성.
///               Inspector에서 수치 입력 후 RecruitmentPanel._recruitOptions 배열에 드래그한다.
/// 의존성(Dependencies): AIVillage.AI.AgentRole, AIVillage.Core.AuthoritativeWorldState
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-07-02
/// </summary>

using UnityEngine;
using AIVillage.AI;

namespace AIVillage.Core
{
    /// <summary>
    /// 주민 티어. Tier1=기본, Tier2=숙련(Forge 필요), Tier3=영웅(Watchtower 필요).
    /// </summary>
    public enum RecruitTier
    {
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3
    }

    /// <summary>
    /// 해금에 필요한 건물. IsUnlocked()에서 AuthoritativeWorldState 플래그와 대조한다.
    /// </summary>
    public enum BuildingUnlockRequirement
    {
        TownHall,    // TownHallBuilt == true
        Forge,       // ForgeBuilt == true
        Watchtower   // WatchtowerBuilt == true
    }

    /// <summary>
    /// 주민 모집 1개 항목의 정의 데이터.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/VillagerRecruitData", fileName = "VillagerRecruitData")]
    public class VillagerRecruitData : ScriptableObject
    {
        #region ── 식별 ──

        [Tooltip("코드 내부 식별자. 예: \"warrior_t1\". 중복 없이 고유하게 설정한다.")]
        [SerializeField] public string recruitId;

        [Tooltip("UI에 표시될 한국어 이름. 예: \"전사\", \"숙련 나무꾼\"")]
        [SerializeField] public string displayName;

        [Tooltip("이 모집 항목이 생성하는 주민의 직업 역할.")]
        [SerializeField] public AgentRole role;

        [Tooltip("모집 티어. Tier1=기본, Tier2=숙련(Forge), Tier3=영웅(Watchtower).")]
        [SerializeField] public RecruitTier tier;

        [Tooltip("이 항목을 활성화하는 데 필요한 건물. TownHall/Forge/Watchtower 중 선택.")]
        [SerializeField] public BuildingUnlockRequirement unlockRequirement;

        #endregion

        #region ── 모집 비용 (GDD §5.5 기준) ──

        [Tooltip("조리된 식량 소비량. 기획서 §5.5 수치 참조.")]
        [SerializeField] public float cookedFoodCost;

        [Tooltip("철 소비량.")]
        [SerializeField] public float ironCost;

        [Tooltip("구리 소비량.")]
        [SerializeField] public float copperCost;

        [Tooltip("은 소비량. 3티어 영웅 주민에만 해당.")]
        [SerializeField] public float silverCost;

        #endregion

        #region ── 초기 스탯 범위 ──

        // RecruitmentSystem이 Random.Range(min, max)로 실제 초기값을 선택한다.

        [Tooltip("스폰 시 체력 최솟값 (0~100).")]
        [SerializeField] public float healthMin = 70f;

        [Tooltip("스폰 시 체력 최댓값 (0~100).")]
        [SerializeField] public float healthMax = 90f;

        [Tooltip("스폰 시 포만감 최솟값. 높을수록 배부름.")]
        [SerializeField] public float satietyMin = 70f;

        [Tooltip("스폰 시 포만감 최댓값.")]
        [SerializeField] public float satietyMax = 90f;

        [Tooltip("스폰 시 피로도 최솟값.")]
        [SerializeField] public float fatigueMin = 5f;

        [Tooltip("스폰 시 피로도 최댓값.")]
        [SerializeField] public float fatigueMax = 25f;

        [Tooltip("스폰 시 기분 최솟값 (0~100).")]
        [SerializeField] public float moodMin = 60f;

        [Tooltip("스폰 시 기분 최댓값.")]
        [SerializeField] public float moodMax = 80f;

        [Tooltip("스폰 시 충성도 최솟값 (0~100). 충성도가 낮으면 명령 거부 확률 증가.")]
        [SerializeField] public float loyaltyMin = 40f;

        [Tooltip("스폰 시 충성도 최댓값.")]
        [SerializeField] public float loyaltyMax = 70f;

        [Tooltip("공격 배율. 일반=1.0f, 숙련 전사=1.35f, 영웅 전사=1.8f. " +
                 "VillagerBrain.AttackModifier에 적용된다.")]
        [SerializeField] public float attackModifier = 1.0f;

        #endregion

        #region ── 특수 시작 플래그 ──

        [Tooltip("true이면 스폰 직후 Brain.HasWeapon = true. Warrior 계열에 사용.")]
        [SerializeField] public bool startWithWeapon = false;

        [Tooltip("true이면 스폰 직후 Brain.HasTool = true. 대부분의 직업에 적용.")]
        [SerializeField] public bool startWithTool = true;

        #endregion

        #region ── F-A 성격 (재미 로드맵 P0) ──

        [Header("F-A 성격")]
        [Tooltip("None이면 RecruitmentSystem이 6종(Coward~Curious) 중 랜덤 배분.")]
        [SerializeField] public Personality personality = Personality.None;

        #endregion

        #region ── 공개 헬퍼 메서드 ──

        /// <summary>
        /// 현재 자원 재고가 이 항목의 모집 비용을 감당할 수 있는지 확인한다.
        /// WorldState가 null이면 false를 반환한다.
        /// </summary>
        /// <param name="ws">비교 대상 AuthoritativeWorldState 인스턴스.</param>
        public bool CanAfford(AuthoritativeWorldState ws)
        {
            if (ws == null)
            {
                Debug.LogWarning($"[VillagerRecruitData] CanAfford: ws가 null입니다. recruitId={recruitId}");
                return false;
            }

            return ws.CookedFoodStock >= cookedFoodCost
                && ws.IronStock       >= ironCost
                && ws.CopperStock     >= copperCost
                && ws.SilverStock     >= silverCost;
        }

        /// <summary>
        /// 필요한 건물이 완성되어 있어 이 항목이 해금됐는지 확인한다.
        /// WorldState가 null이면 false를 반환한다.
        /// </summary>
        /// <param name="ws">비교 대상 AuthoritativeWorldState 인스턴스.</param>
        public bool IsUnlocked(AuthoritativeWorldState ws)
        {
            if (ws == null)
            {
                Debug.LogWarning($"[VillagerRecruitData] IsUnlocked: ws가 null입니다. recruitId={recruitId}");
                return false;
            }

            switch (unlockRequirement)
            {
                case BuildingUnlockRequirement.TownHall:   return ws.TownHallBuilt;
                case BuildingUnlockRequirement.Forge:      return ws.ForgeBuilt;
                case BuildingUnlockRequirement.Watchtower: return ws.WatchtowerBuilt;
                default:
                    Debug.LogWarning($"[VillagerRecruitData] IsUnlocked: 알 수 없는 unlockRequirement={unlockRequirement}");
                    return false;
            }
        }

        #endregion
    }
}
