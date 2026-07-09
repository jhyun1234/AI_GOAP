/// <summary>
/// VillagerStatusPanel.cs - 선택된 주민 상태 표시 패널
///
/// 역할(Role): SetTarget()으로 VillagerFSM을 지정받아 0.2초 코루틴으로
///             Brain의 수치(HP/허기/피로/기분/충성도)를 텍스트로 표시한다.
///             주민 사망 감지 시 HUDManager.ShowToast()로 알림을 보내고 패널을 닫는다.
///             UI 레이어는 Brain을 읽기 전용으로만 접근한다 (쓰기 절대 금지).
///
/// 의존성(Dependencies): VillagerFSM.cs, VillagerBrain.cs, HUDManager.cs, TextMeshPro
/// </summary>

using System.Collections;
using UnityEngine;
using TMPro;
using AIVillage.AI;

namespace AIVillage.UI
{
    [DisallowMultipleComponent]
    public sealed class VillagerStatusPanel : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        private const float REFRESH_INTERVAL = 0.2f;

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields
        // ══════════════════════════════════════════════════════════════════════

        [Header("텍스트 레이블")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _roleText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _statsText;

        [Header("위치 추적")]
        [Tooltip("선택된 주민 위치로부터의 월드 공간 오프셋.")]
        [SerializeField] private Vector3 _followOffset = new Vector3(1.2f, 1.0f, 0f);

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        private VillagerFSM _targetFSM;
        private WaitForSeconds _waitForSeconds;
        private Coroutine _refreshCoroutine;
        private RectTransform _rectTransform;
        private Camera _mainCamera;

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            _rectTransform  = GetComponent<RectTransform>();
            _mainCamera     = Camera.main;
            _waitForSeconds = new WaitForSeconds(REFRESH_INTERVAL);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_targetFSM == null) return;

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            Vector3 screenPos = _mainCamera.WorldToScreenPoint(
                _targetFSM.transform.position + _followOffset);

            if (screenPos.z < 0f)
            {
                _rectTransform.anchorMin = Vector2.zero;
                _rectTransform.anchorMax = Vector2.zero;
                _rectTransform.anchoredPosition = new Vector2(-9999f, -9999f);
                return;
            }

            float vx = screenPos.x / Screen.width;
            float vy = screenPos.y / Screen.height;
            _rectTransform.anchorMin = new Vector2(vx, vy);
            _rectTransform.anchorMax = new Vector2(vx, vy);
            _rectTransform.anchoredPosition = Vector2.zero;
        }

        private void OnDestroy()
        {
            StopRefreshCoroutine();
        }

        private void OnEnable()
        {
            if (_targetFSM != null && _refreshCoroutine == null)
                _refreshCoroutine = StartCoroutine(RefreshCoroutine());
        }

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        public void SetTarget(VillagerFSM fsm)
        {
            StopRefreshCoroutine();
            _targetFSM = fsm;

            if (_targetFSM == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (!gameObject.activeInHierarchy)
                gameObject.SetActive(true);
            else
                _refreshCoroutine = StartCoroutine(RefreshCoroutine());
        }

        // ══════════════════════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator RefreshCoroutine()
        {
            while (_targetFSM != null)
            {
                yield return _waitForSeconds;

                if (_targetFSM == null) break;

                VillagerBrain brain = _targetFSM.Brain;
                if (brain == null) break;

                if (!brain.IsAlive)
                {
                    HUDManager.Instance?.ShowToast("주민이 사망했습니다.");
                    SetTarget(null);
                    yield break;
                }

                // 이름 (GUID 앞 8자리)
                string shortId = brain.VillagerId?.Length > 8
                    ? brain.VillagerId.Substring(0, 8)
                    : brain.VillagerId ?? "???";

                SetTextSafe(_nameText,   $"주민: {shortId}");
                SetTextSafe(_roleText,   $"역할: {RoleToKorean(brain.Role)}");
                SetTextSafe(_statusText,
                    $"목표: {GoalToKorean(brain.CurrentGoalId)}\n" +
                    $"행동: {ActionToKorean(brain.CurrentActionId)}");

                // 수치는 정수로 표시 (소수점 불필요)
                int hp  = Mathf.RoundToInt(brain.HealthLevel);
                int sa  = Mathf.RoundToInt(brain.SatietyLevel);
                int ft  = Mathf.RoundToInt(brain.FatigueLevel);
                int md  = Mathf.RoundToInt(brain.MoodLevel);
                int ly  = Mathf.RoundToInt(brain.LoyaltyLevel);

                SetTextSafe(_statsText,
                    $"체력: {hp}    포만: {sa}    피로: {ft}\n" +
                    $"기분: {md}    충성도: {ly}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 한국어 번역 (로컬라이제이션 준비 — 향후 LocalizationManager로 교체)
        // ══════════════════════════════════════════════════════════════════════

        private static string RoleToKorean(AgentRole role)
        {
            switch (role)
            {
                case AgentRole.Lumberjack: return "나무꾼";
                case AgentRole.Miner:      return "광부";
                case AgentRole.Builder:    return "건설자";
                case AgentRole.Warrior:    return "전사";
                case AgentRole.Medic:      return "치료사";
                case AgentRole.Cook:       return "요리사";
                case AgentRole.Explorer:   return "탐험가";
                default:                   return "미배정";
            }
        }

        private static string GoalToKorean(string goalId)
        {
            if (goalId == null) return "대기 중";
            switch (goalId)
            {
                case "SurviveHunger":    return "생존: 배고픔";
                case "SurviveInjury":    return "생존: 부상";
                case "SurviveFatigue":   return "생존: 피로";
                case "GatherResources":  return "자원 수집";
                case "GatherWood":       return "나무 수집";
                case "GatherStone":      return "돌 수집";
                case "GatherIron":       return "철 수집";
                case "GatherCopper":     return "구리 수집";
                case "BuildStructure":   return "건물 건설";
                case "DefendVillage":    return "마을 방어";
                case "AttackEnemy":      return "적 공격";
                case "Explore":          return "탐험";
                case "CookMeal":         return "요리";
                case "MoveToBase":       return "기지 이동";
                case "MoveToTarget":     return "이동";
                case "Idle":             return "대기";
                default:                 return goalId; // 미등록 Goal은 원문 그대로
            }
        }

        private static string ActionToKorean(string actionId)
        {
            if (actionId == null) return "-";
            switch (actionId)
            {
                case "ChopWood":             return "나무 베기";
                case "MineStone":            return "돌 채굴";
                case "MineIron":             return "철 채굴";
                case "MineCopper":           return "구리 채굴";
                case "EatCookedFood":        return "식사 (조리)";
                case "EatRawFood":           return "식사 (생식)";
                case "CookMeal":             return "요리 중";
                case "Sleep":                return "수면";
                case "RestOnGround":         return "휴식";
                case "SeekMedicalAid":       return "치료 받기";
                case "MoveToBase":           return "기지로 이동";
                case "CraftPrimitiveWeapon": return "원시 무기 제작";
                case "AttackEnemy":          return "적 공격";
                case "CraftWeapon":          return "무기 제작";
                case "BuildTownHall":        return "시청 건설";
                case "BuildForge":           return "대장간 건설";
                case "BuildStorehouse":      return "창고 건설";
                case "BuildCampfire":        return "모닥불 건설";
                case "Explore":              return "탐험 중";
                case "HarvestWildBerries":   return "야생 열매 채취";
                default:                     return actionId;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼
        // ══════════════════════════════════════════════════════════════════════

        private void StopRefreshCoroutine()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private static void SetTextSafe(TMP_Text text, string value)
        {
            if (text == null) return;
            text.SetText(value);
        }
    }
}
