/// <summary>
/// VillagerOverviewPanel.cs - 주민 전체 현황 + 선택 주민 상세 통합 패널
///
/// 역할(Role): 항상 화면에 표시되는 주민 목록 패널.
///             - 전체 주민의 활동 상태(채취/이동/탐색/건설)를 색상으로 구분하여 표시
///             - 주민 클릭 시 상단에 선택 주민의 역할·목표·행동·수치를 상세 표시
///             - VillagerStatusPanel 기능을 통합하여 패널 1개로 모든 정보 제공
///
/// 의존성(Dependencies): GameManager.cs, VillagerFSM.cs, VillagerBrain.cs, TextMeshPro
/// </summary>

using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;
using AIVillage.Core;
using AIVillage.AI;

namespace AIVillage.UI
{
    [DisallowMultipleComponent]
    public sealed class VillagerOverviewPanel : MonoBehaviour
    {
        #region ── 상수 ──

        private const float REFRESH_INTERVAL    = 0.5f;
        private const int   SB_INITIAL_CAPACITY = 1024;

        #endregion

        #region ── Serialized Fields ──

        [Tooltip("전체 주민 현황 + 선택 주민 상세를 출력하는 TMP_Text.")]
        [SerializeField] private TMP_Text _overviewText;

        #endregion

        #region ── Private Fields ──

        private readonly StringBuilder _sb  = new StringBuilder(SB_INITIAL_CAPACITY);
        private WaitForSeconds         _wait;
        private VillagerFSM            _selectedFsm;

        #endregion

        #region ── Unity 생명주기 ──

        private void Awake()
        {
            _wait = new WaitForSeconds(REFRESH_INTERVAL);
        }

        #endregion

        #region ── 공개 메서드 ──

        /// <summary>HUDManager에서 주민 선택/해제 시 호출한다.</summary>
        public void SetSelectedVillager(VillagerFSM fsm)
        {
            _selectedFsm = fsm;
        }

        /// <summary>HUDManager가 Start()에서 StartCoroutine으로 시작하는 갱신 루프.</summary>
        public IEnumerator RefreshCoroutine()
        {
            while (true)
            {
                yield return _wait;

                if (_overviewText == null) continue;

                _sb.Clear();

                // ── 선택 주민 상세 블록 ──────────────────────────────────────
                if (_selectedFsm != null && _selectedFsm.Brain != null)
                {
                    AppendSelectedDetail(_selectedFsm);
                    _sb.Append('\n');
                }

                // ── 전체 주민 목록 ────────────────────────────────────────────
                var villagers = GameManager.Instance?.Villagers;
                if (villagers != null)
                {
                    int total = villagers.Count;
                    for (int i = 0; i < total; i++)
                    {
                        AppendVillagerLine(villagers[i]);
                        if (i < total - 1) _sb.Append('\n');
                    }
                }
                else
                {
                    _sb.Append("<color=#888888>주민 목록 없음</color>");
                }

                _overviewText.SetText(_sb.ToString());
            }
        }

        #endregion

        #region ── 선택 주민 상세 블록 ──

        private void AppendSelectedDetail(VillagerFSM fsm)
        {
            VillagerBrain b = fsm.Brain;

            // 헤더 (이름 옆에 F-A 성격 라벨 인라인)
            _sb.Append("<color=#FFFF88>▶ ");
            _sb.Append(fsm.gameObject.name);
            _sb.Append("</color>  <color=");
            _sb.Append(PersonalityData.HexColor(b.Personality));
            _sb.Append(">[");
            _sb.Append(PersonalityData.KoreanLabel(b.Personality));
            _sb.Append("]</color>\n");

            // 역할 · 목표 · 행동
            _sb.Append("역할: <color=#AADDFF>");
            _sb.Append(RoleToKorean(b.Role));
            _sb.Append("</color>  목표: <color=#AADDFF>");
            _sb.Append(GoalToKorean(b.CurrentGoalId));
            _sb.Append("</color>\n");

            _sb.Append("행동: <color=#AADDFF>");
            _sb.Append(ActionToKorean(b.CurrentActionId));
            _sb.Append(fsm.IsMoving ? " (이동중)" : "");
            _sb.Append("</color>\n");

            // 수치 바
            _sb.Append("HP ");
            _sb.Append(b.HealthLevel.ToString("F0"));
            _sb.Append("  허기 ");
            _sb.Append(b.HungerLevel.ToString("F0"));
            _sb.Append("  피로 ");
            _sb.Append(b.FatigueLevel.ToString("F0"));
            _sb.Append("  기분 ");
            _sb.Append(b.MoodLevel.ToString("F0"));
            _sb.Append("  충성 ");
            _sb.Append(b.LoyaltyLevel.ToString("F0"));
            _sb.Append('\n');

            // 사고 체인 시각화 (7장 Level 2)
            AppendPlanChain(b);

            _sb.Append("<color=#555555>────────────────</color>");
        }

        /// <summary>선택 주민의 GOAP 플랜 체인을 사고 흐름으로 출력한다 (7장 Level 2).</summary>
        private void AppendPlanChain(VillagerBrain b)
        {
            if (b.FSMState == VillagerState.Planning || b.FSMState == VillagerState.Replanning)
            {
                _sb.Append("<color=#FFCC44>▷ 계획 수립 중...</color>\n");
                return;
            }

            var fullPlan = b.CurrentPlanFull;
            if (fullPlan == null || fullPlan.Count == 0) return;

            // 완료된 스텝 수 계산: 큐에 남은 수 + (현재 실행 중이면 1)
            int remaining     = b.CurrentPlan.Count;
            int total         = fullPlan.Count;
            bool hasCurrentAction = b.CurrentActionId != null && b.FSMState == VillagerState.Executing;
            int completedCount = total - remaining - (hasCurrentAction ? 1 : 0);

            _sb.Append("▷ <color=#888888>");
            _sb.Append(GoalToKorean(b.CurrentGoalId));
            _sb.Append("</color> → ");

            // [N1] 12스텝 플랜 대응: 패널 가로 넘침 방지를 위해 최대 8스텝까지 표시
            const int MAX_DISPLAY = 8;
            int displayCount = total <= MAX_DISPLAY ? total : MAX_DISPLAY;

            for (int idx = 0; idx < displayCount; idx++)
            {
                if (idx > 0) _sb.Append(" ▸ ");
                string act = fullPlan[idx];
                bool isDone    = idx < completedCount;
                bool isCurrent = hasCurrentAction && idx == completedCount;

                if (isDone)
                {
                    _sb.Append("<color=#444444><s>");
                    _sb.Append(ActionToKorean(act));
                    _sb.Append("</s></color>");
                }
                else if (isCurrent)
                {
                    _sb.Append("<color=#FFFF44><b>▶");
                    _sb.Append(ActionToKorean(act));
                    _sb.Append("</b></color>");
                }
                else
                {
                    _sb.Append("<color=#AAAAAA>");
                    _sb.Append(ActionToKorean(act));
                    _sb.Append("</color>");
                }
            }

            if (total > MAX_DISPLAY)
            {
                _sb.Append($" <color=#666666>▸ …(+{total - MAX_DISPLAY})</color>");
            }

            _sb.Append('\n');
        }

        #endregion

        #region ── 주민 목록 한 줄 ──

        private void AppendVillagerLine(VillagerFSM fsm)
        {
            if (fsm == null) { _sb.Append("(null)"); return; }

            VillagerBrain b = fsm.Brain;
            if (b == null) { _sb.Append("(null brain)"); return; }

            bool isSelected = (fsm == _selectedFsm);

            // 선택됨 강조
            if (isSelected) _sb.Append("<color=#FFFF88>");

            // 생사 아이콘
            _sb.Append(b.IsAlive ? "●" : "×");
            _sb.Append(' ');

            // 이름
            _sb.Append(fsm.gameObject.name);
            _sb.Append("  [");

            // 활동 라벨 + 색상
            bool moving = fsm.IsMoving;
            _sb.Append("<color=");
            _sb.Append(GetActivityColor(b, moving));
            _sb.Append('>');
            _sb.Append(GetActivityLabel(b, moving));
            _sb.Append("</color>]");

            // 세부 작업
            _sb.Append("  ");
            _sb.Append(GetActionDetail(b, moving));

            // HP
            _sb.Append("  HP:");
            _sb.Append(b.HealthLevel.ToString("F0"));

            if (isSelected) _sb.Append("</color>");
        }

        #endregion

        #region ── 활동 분류 헬퍼 ──

        private static string GetActivityLabel(VillagerBrain b, bool isMoving)
        {
            switch (b.FSMState)
            {
                case VillagerState.Dead:        return "사망";
                case VillagerState.Fighting:    return "전투중";
                case VillagerState.Fleeing:     return "도주중";
                case VillagerState.Idle:        return "대기중";
                case VillagerState.Planning:
                case VillagerState.Replanning:  return "계획중";
                case VillagerState.LOD_FSM:     return "원거리";
                case VillagerState.Executing:
                    string a = b.CurrentActionId ?? "";
                    if (IsGatherAction(a))  return isMoving ? "이동중▶채취" : "채취중";
                    if (a == "Explore")     return "탐색중";
                    if (IsBuildAction(a))   return isMoving ? "이동중▶건설" : "건설중";
                    if (a == "MoveToBase")  return "귀환중";
                    return isMoving ? "이동중" : "실행중";
                default:                    return "대기중";
            }
        }

        private static string GetActivityColor(VillagerBrain b, bool isMoving)
        {
            switch (b.FSMState)
            {
                case VillagerState.Dead:        return "#888888";
                case VillagerState.Fighting:    return "#FF3333";
                case VillagerState.Fleeing:     return "#FF8800";
                case VillagerState.Idle:        return "#AAAAAA";
                case VillagerState.Planning:
                case VillagerState.Replanning:  return "#AAAAAA";
                case VillagerState.Executing:
                    string a = b.CurrentActionId ?? "";
                    if (IsGatherAction(a)) return isMoving ? "#FFCC00" : "#00CC44";
                    if (a == "Explore")    return "#44AAFF";
                    if (IsBuildAction(a))  return isMoving ? "#FFCC00" : "#FF6644";
                    if (a == "MoveToBase") return "#FFCC00";
                    return "#FFFFFF";
                default: return "#AAAAAA";
            }
        }

        private static string GetActionDetail(VillagerBrain b, bool isMoving)
        {
            if (b.FSMState != VillagerState.Executing)
                return b.CurrentGoalId != null ? GoalToKorean(b.CurrentGoalId) : "—";

            switch (b.CurrentActionId)
            {
                case "ChopWood":           return isMoving ? "▶나무"  : "나무 +10";
                case "MineStone":          return isMoving ? "▶돌"    : "돌 +8";
                case "MineIron":           return isMoving ? "▶철"    : "철 +5";
                case "MineCopper":         return isMoving ? "▶구리"  : "구리 +3";
                case "HarvestWildBerries": return isMoving ? "▶베리"  : "식량 +5";
                case "Explore":            return "탐색";
                case "MoveToBase":         return "기지로";
                case "EatCookedFood":
                case "EatRawFood":         return "식사중";
                case "CookMeal":           return "요리중";
                case "Sleep":              return "수면중";
                case "RestOnGround":       return "휴식중";
                case "SeekMedicalAid":     return "치료중";
                case null:                 return "—";
                default:                   return b.CurrentActionId;
            }
        }

        private static bool IsGatherAction(string id)
        {
            switch (id)
            {
                case "ChopWood":
                case "MineStone":
                case "MineIron":
                case "MineCopper":
                case "HarvestWildBerries":
                    return true;
                default: return false;
            }
        }

        private static bool IsBuildAction(string id)
            => id != null && id.StartsWith("Build", System.StringComparison.Ordinal);

        #endregion

        #region ── 한국어 로컬라이제이션 ──

        private static string RoleToKorean(AgentRole role)
        {
            switch (role)
            {
                case AgentRole.Lumberjack: return "나무꾼";
                case AgentRole.Miner:      return "광부";
                case AgentRole.Builder:    return "건축가";
                case AgentRole.Cook:       return "요리사";
                case AgentRole.Warrior:    return "전사";
                case AgentRole.Medic:      return "의사";
                case AgentRole.Explorer:   return "탐험가";
                default:                   return "역할없음";
            }
        }

        private static string GoalToKorean(string goalId)
        {
            if (goalId == null) return "없음";
            switch (goalId)
            {
                case "GatherResources": return "자원수집";
                case "GatherWood":      return "나무수집";
                case "GatherStone":     return "돌수집";
                case "GatherIron":      return "철수집";
                case "GatherCopper":    return "구리수집";
                case "BuildStructure":  return "건설";
                case "Explore":         return "탐험";
                case "SurviveHunger":   return "식량확보";
                case "SurviveInjury":   return "치료";
                case "SurviveFatigue":  return "휴식";
                case "DefendVillage":   return "방어";
                case "CookMeal":        return "요리";
                default:                return goalId;
            }
        }

        private static string ActionToKorean(string actionId)
        {
            if (actionId == null) return "없음";
            switch (actionId)
            {
                case "ChopWood":           return "나무 채취";
                case "MineStone":          return "돌 채굴";
                case "MineIron":           return "철 채굴";
                case "MineCopper":         return "구리 채굴";
                case "HarvestWildBerries": return "베리 수확";
                case "Explore":            return "탐색";
                case "MoveToBase":         return "기지 귀환";
                case "EatCookedFood":      return "식사(조리식)";
                case "EatRawFood":         return "식사(생식)";
                case "CookMeal":           return "요리";
                case "Sleep":              return "수면";
                case "RestOnGround":       return "휴식";
                case "SeekMedicalAid":     return "치료";
                case "BuildCampfire":      return "모닥불 건설";
                case "BuildHouse":         return "집 건설";
                case "BuildStorehouse":    return "창고 건설";
                case "BuildTownHall":      return "타운홀 건설";
                case "BuildForge":         return "대장간 건설";
                case "BuildWatchtower":    return "망루 건설";
                case "AttackEnemy":        return "적 공격";
                case "FleeFromEnemy":      return "도주";
                default:                   return actionId;
            }
        }

        #endregion
    }
}
