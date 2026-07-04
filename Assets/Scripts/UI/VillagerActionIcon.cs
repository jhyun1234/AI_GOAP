/// <summary>
/// VillagerActionIcon.cs - 주민 머리 위 현재 행동 아이콘 (7장 Level 1 플랜 가시화)
///
/// 역할(Role): 각 주민 GameObject의 자식으로 배치되어 현재 Action을 단문 한자/기호로 표시한다.
///             P0 위기(배고픔·부상·탈진) 시 경고 아이콘으로 자동 전환된다.
///             GameManager.CollectAndSetupVillagers()에서 자동 생성된다.
///
/// 의존성(Dependencies): TextMeshPro, AIVillage.AI (VillagerFSM, VillagerBrain)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-07-04
/// </summary>

using UnityEngine;
using TMPro;
using AIVillage.AI;

namespace AIVillage.UI
{
    /// <summary>
    /// 주민 머리 위에 현재 Action/상태를 WorldSpace TextMeshPro로 표시하는 컴포넌트.
    /// VillagerFSM이 있는 부모 GameObject의 자식에 자동 배치된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillagerActionIcon : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        private const float UPDATE_INTERVAL = 0.25f;  // 갱신 주기(초)
        private const float ICON_FONT_SIZE  = 3.5f;   // WorldSpace TMP 폰트 크기

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        private TextMeshPro _tmp;
        private VillagerFSM _fsm;
        private float       _nextUpdate;

        // ══════════════════════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>GameManager가 자동 생성 후 VillagerFSM을 주입한다.</summary>
        public void Initialize(VillagerFSM fsm)
        {
            _fsm = fsm;

            if (_tmp == null)
            {
                _tmp              = gameObject.AddComponent<TextMeshPro>();
                _tmp.alignment    = TextAlignmentOptions.Center;
                _tmp.fontSize     = ICON_FONT_SIZE;
                _tmp.sortingOrder = 10;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        private void Update()
        {
            if (Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + UPDATE_INTERVAL;

            if (_tmp == null || _fsm == null || _fsm.Brain == null)
            {
                _tmp?.SetText("");
                return;
            }

            _tmp.SetText(BuildIconText(_fsm.Brain));
        }

        // ══════════════════════════════════════════════════════════════════════
        // 아이콘 텍스트 생성
        // ══════════════════════════════════════════════════════════════════════

        private static string BuildIconText(VillagerBrain b)
        {
            if (!b.IsAlive) return "";

            // P0 위기 오버라이드
            if (b.HungerLevel  > 80f) return "<color=#FF4444>⚠허기</color>";
            if (b.HealthLevel  < 20f) return "<color=#FF0000>⚠부상</color>";
            if (b.FatigueLevel > 90f) return "<color=#FF8800>⚠탈진</color>";

            switch (b.FSMState)
            {
                case VillagerState.Fighting:   return "<color=#FF3333>戦</color>";
                case VillagerState.Fleeing:    return "<color=#FF8800>逃</color>";
                case VillagerState.Planning:
                case VillagerState.Replanning: return "<color=#AAAAAA>...</color>";
                case VillagerState.Idle:       return "<color=#666666>•</color>";
                case VillagerState.Executing:  return GetActionIcon(b.CurrentActionId);
                default:                       return "";
            }
        }

        private static string GetActionIcon(string actionId)
        {
            if (actionId == null) return "";
            switch (actionId)
            {
                case "ChopWood":           return "<color=#88FF44>木</color>";
                case "MineStone":          return "<color=#CCCCCC>石</color>";
                case "MineIron":           return "<color=#88AAFF>Fe</color>";
                case "MineCopper":         return "<color=#FF9944>Cu</color>";
                case "HarvestWildBerries": return "<color=#88FF88>草</color>";
                case "EatCookedFood":
                case "EatRawFood":         return "<color=#FFCC44>食</color>";
                case "CookMeal":           return "<color=#FF8844>煮</color>";
                case "Sleep":              return "<color=#8888FF>眠</color>";
                case "RestOnGround":       return "<color=#AAAAFF>休</color>";
                case "Explore":            return "<color=#44AAFF>探</color>";
                case "MoveToBase":         return "<color=#FFCC44>⇒</color>";
                case "AttackEnemy":        return "<color=#FF4444>攻</color>";
                case "SeekMedicalAid":     return "<color=#FF6666>治</color>";
                default:
                    if (actionId.StartsWith("Build", System.StringComparison.Ordinal))
                        return "<color=#FF8844>建</color>";
                    return "<color=#AAAAAA>?</color>";
            }
        }
    }
}
