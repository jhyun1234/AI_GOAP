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

        private const float UPDATE_INTERVAL = 0.25f;

        [SerializeField] private float _iconFontSize = 5.0f;
        [SerializeField] private float _iconWidth    = 3.0f;
        [SerializeField] private float _iconHeight   = 1.2f;

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        private TextMeshPro _tmp;
        private VillagerFSM _fsm;
        private float       _nextUpdate;
        private string      _thoughtOverride;
        private float       _thoughtTimer;

        // ══════════════════════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>GameManager가 자동 생성 후 VillagerFSM과 폰트를 주입한다.</summary>
        public void Initialize(VillagerFSM fsm, TMP_FontAsset font = null)
        {
            _fsm = fsm;

            if (_tmp == null)
            {
                _tmp                        = gameObject.AddComponent<TextMeshPro>();
                _tmp.alignment              = TextAlignmentOptions.Center;
                _tmp.fontSize                = _iconFontSize;
                _tmp.sortingOrder            = 10;
                _tmp.enableWordWrapping      = false;
                _tmp.overflowMode            = TextOverflowModes.Overflow;
                _tmp.rectTransform.sizeDelta = new Vector2(_iconWidth, _iconHeight);
            }

            if (font != null)
                _tmp.font = font;
        }

        /// <summary>일정 시간 동안 행동 아이콘 대신 사고 텍스트를 WorldSpace로 표시한다.</summary>
        public void ShowThought(string text, float duration)
        {
            _thoughtOverride = $"<color=#FFEE88>\"{text}\"</color>";
            _thoughtTimer    = duration;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        private void Update()
        {
            // 매 프레임 카메라를 향해 회전 (빌보드)
            if (Camera.main != null)
                transform.LookAt(
                    transform.position + Camera.main.transform.rotation * Vector3.forward,
                    Camera.main.transform.rotation * Vector3.up);

            // Inspector에서 폰트 크기를 바꿨을 때 즉시 반영
            if (_tmp != null && !Mathf.Approximately(_tmp.fontSize, _iconFontSize))
                _tmp.fontSize = _iconFontSize;

            // 사고 말풍선 타이머
            if (_thoughtTimer > 0f)
            {
                _thoughtTimer -= Time.deltaTime;
                if (_tmp != null && _thoughtOverride != null)
                    _tmp.SetText(_thoughtOverride);
                return;
            }

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
            if (b.SatietyLevel < 20f) return "<color=#FF4444>[!]허기</color>";
            if (b.HealthLevel  < 20f) return "<color=#FF0000>[!]부상</color>";
            if (b.FatigueLevel > 90f) return "<color=#FF8800>[!]탈진</color>";

            switch (b.FSMState)
            {
                case VillagerState.Fighting:   return "<color=#FF3333>전투</color>";
                case VillagerState.Fleeing:    return "<color=#FF8800>도주</color>";
                case VillagerState.Planning:
                case VillagerState.Replanning: return "<color=#AAAAAA>...</color>";
                case VillagerState.Idle:       return "<color=#666666>대기</color>";
                case VillagerState.Executing:  return GetActionIcon(b.CurrentActionId);
                default:                       return "";
            }
        }

        private static string GetActionIcon(string actionId)
        {
            if (actionId == null) return "";
            switch (actionId)
            {
                case "ChopWood":           return "<color=#88FF44>벌목</color>";
                case "MineStone":          return "<color=#CCCCCC>채석</color>";
                case "MineIron":           return "<color=#88AAFF>철광</color>";
                case "MineCopper":         return "<color=#FF9944>동광</color>";
                case "HarvestWildBerries": return "<color=#88FF88>채집</color>";
                case "EatCookedFood":
                case "EatRawFood":         return "<color=#FFCC44>식사</color>";
                case "CookMeal":           return "<color=#FF8844>요리</color>";
                case "Sleep":              return "<color=#8888FF>수면</color>";
                case "RestOnGround":       return "<color=#AAAAFF>휴식</color>";
                case "Explore":            return "<color=#44AAFF>탐색</color>";
                case "MoveToBase":         return "<color=#FFCC44>귀환</color>";
                case "AttackEnemy":        return "<color=#FF4444>공격</color>";
                case "SeekMedicalAid":     return "<color=#FF6666>치료</color>";
                default:
                    if (actionId.StartsWith("Build", System.StringComparison.Ordinal))
                        return "<color=#FF8844>건설</color>";
                    return "<color=#AAAAAA>?</color>";
            }
        }
    }
}
