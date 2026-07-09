/// <summary>
/// VillagerOverviewPanel.cs - 선택 주민 상세 패널
///
/// 역할(Role): 주민을 클릭했을 때만 해당 주민의 상태를 표시한다.
///             선택된 주민이 없으면 패널은 비어있다.
///
/// 의존성(Dependencies): VillagerFSM.cs, VillagerBrain.cs, TextMeshPro
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

        [Tooltip("선택 주민 상세를 출력하는 TMP_Text. 선택 없으면 비어있음.")]
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

                if (_selectedFsm != null && _selectedFsm.Brain != null)
                    AppendSelectedDetail(_selectedFsm);

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
            _sb.Append("  포만 ");
            _sb.Append(b.SatietyLevel.ToString("F0"));
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
