/// <summary>
/// RecruitmentPanel.cs - 주민 모집 UI 패널
///
/// 역할(Role): VillagerRecruitData 배열을 읽어 버튼 8개(슬롯)에 모집 항목을 표시한다.
///             0.5초 폴링 코루틴으로 자원 충족 여부를 갱신하여 버튼 interactable을 제어한다.
///             Town Hall 해금 전에는 HUDManager가 gameObject.SetActive(false)로 숨긴다.
/// 사용법(Usage):
///   1. Canvas 아래 빈 GameObject("RecruitmentPanel")에 부착하고 초기에는 비활성화한다.
///   2. Inspector에서 _recruitOptions(최대 8개 에셋), _villagerPrefab,
///      _recruitButtons(8개 Button), _costTexts(8개 TMP_Text), _nameTexts(8개 TMP_Text)를 연결한다.
///   3. HUDManager가 TownHallBuilt 감지 시 gameObject.SetActive(true)를 호출하면 자동 시작된다.
/// 의존성(Dependencies):
///   - RecruitmentSystem.cs, VillagerRecruitData.cs
///   - TMPro.TMP_Text (TextMeshPro 패키지 필수)
///   - UnityEngine.UI.Button
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-07-02
/// </summary>

using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// 주민 모집 UI 패널. Inspector 슬롯 방식으로 최대 8개의 모집 항목을 표시한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RecruitmentPanel : MonoBehaviour
    {
        #region ── 상수 ──

        // 슬롯 수 상한 — Inspector 배열 크기와 독립적으로 최대 슬롯 수를 강제한다.
        private const int MAX_SLOTS = 8;

        #endregion

        #region ── Serialized Fields ──

        [Tooltip("모집 항목 ScriptableObject 배열. 최대 8개. " +
                 "Project 창에서 VillagerRecruitData 에셋을 생성 후 이 슬롯에 드래그한다.")]
        [SerializeField] private VillagerRecruitData[] _recruitOptions;

        [Tooltip("스폰할 주민 Prefab. VillagerFSM이 부착된 Prefab을 연결한다.")]
        [SerializeField] private GameObject _villagerPrefab;

        [Tooltip("모집 버튼 배열 (슬롯 0~7). Hierarchy에서 미리 만든 Button을 순서대로 연결한다.")]
        [SerializeField] private Button[] _recruitButtons;

        [Tooltip("버튼별 비용 텍스트 배열 (슬롯 0~7). _recruitButtons와 동일 인덱스로 대응한다. " +
                 "형식 예시: \"식량 25 | 철 12 | 구리 4\"")]
        [SerializeField] private TMP_Text[] _costTexts;

        [Tooltip("버튼별 역할명 텍스트 배열 (슬롯 0~7). VillagerRecruitData.displayName을 표시한다.")]
        [SerializeField] private TMP_Text[] _nameTexts;

        [Tooltip("버튼 interactable 갱신 폴링 간격 (초). 기본값 0.5초.")]
        [SerializeField] private float _refreshInterval = 0.5f;

        #endregion

        #region ── Private Fields ──

        // OnEnable에서 시작하고 OnDisable에서 정지하는 폴링 코루틴 핸들.
        // GOAPDebugOverlay 패턴을 따른다 — 비활성 GameObject에서 코루틴이 실행되는 것을 막는다.
        private Coroutine _refreshCoroutine;

        // WaitForSeconds를 매 틱 할당하지 않도록 캐시한다.
        private WaitForSeconds _wait;

        // 각 버튼의 onClick 콜백 참조. OnDestroy에서 RemoveListener에 사용한다.
        private UnityAction[] _buttonCallbacks;

        #endregion

        #region ── Unity 생명주기 ──

        private void Awake()
        {
            _wait = new WaitForSeconds(_refreshInterval);
            InitializeButtonCallbacks();
            RenderStaticContent();
        }

        /// <summary>
        /// GameObject가 활성화될 때 폴링 코루틴을 시작한다.
        /// HUDManager가 gameObject.SetActive(true)를 호출할 때 자동으로 실행된다.
        /// </summary>
        private void OnEnable()
        {
            if (_refreshCoroutine != null)
                StopCoroutine(_refreshCoroutine);

            _refreshCoroutine = StartCoroutine(RefreshCoroutine());
        }

        /// <summary>
        /// GameObject가 비활성화될 때 폴링 코루틴을 안전하게 중단한다.
        /// 비활성 상태에서 코루틴이 계속 실행되는 Unity 버그를 방지한다.
        /// </summary>
        private void OnDisable()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            // 버튼 리스너 정리 — 씬 언로드 시 이벤트 누수 방지
            if (_recruitButtons == null || _buttonCallbacks == null) return;

            int slotCount = Mathf.Min(
                Mathf.Min(_recruitButtons.Length, _buttonCallbacks.Length),
                MAX_SLOTS);

            for (int i = 0; i < slotCount; i++)
            {
                if (_recruitButtons[i] != null && _buttonCallbacks[i] != null)
                    _recruitButtons[i].onClick.RemoveListener(_buttonCallbacks[i]);
            }
        }

        #endregion

        #region ── 초기화 ──

        /// <summary>
        /// 각 버튼 슬롯에 onClick 콜백을 바인딩한다. Awake에서 한 번만 실행된다.
        /// 람다 클로저가 올바른 인덱스를 캡처하도록 로컬 변수(idx)를 사용한다.
        /// </summary>
        private void InitializeButtonCallbacks()
        {
            if (_recruitButtons == null) return;

            int slotCount = Mathf.Min(_recruitButtons.Length, MAX_SLOTS);
            _buttonCallbacks = new UnityAction[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                if (_recruitButtons[i] == null) continue;

                // 클로저 캡처 오류 방지: 루프 변수 i를 로컬 변수 idx에 복사한다.
                // 람다가 i를 직접 참조하면 루프 종료 후 마지막 값만 캡처된다.
                int idx = i;
                _buttonCallbacks[idx] = () => OnRecruitButtonClicked(idx);
                _recruitButtons[i].onClick.AddListener(_buttonCallbacks[idx]);
            }
        }

        /// <summary>
        /// 변하지 않는 텍스트(역할명, 초기 비용 문자열)를 Awake에서 한 번 렌더링한다.
        /// interactable 갱신은 RefreshCoroutine()에서 0.5초마다 처리한다.
        /// </summary>
        private void RenderStaticContent()
        {
            if (_recruitOptions == null) return;

            int dataCount   = _recruitOptions.Length;
            int buttonCount = _recruitButtons?.Length ?? 0;
            int slotCount   = Mathf.Min(Mathf.Min(dataCount, buttonCount), MAX_SLOTS);

            for (int i = 0; i < slotCount; i++)
            {
                VillagerRecruitData data = _recruitOptions[i];
                bool hasData = data != null;

                // 역할명 텍스트
                if (_nameTexts != null && i < _nameTexts.Length && _nameTexts[i] != null)
                    _nameTexts[i].text = hasData ? data.displayName : string.Empty;

                // 비용 텍스트 — 정적 내용이므로 1회만 빌드
                if (_costTexts != null && i < _costTexts.Length && _costTexts[i] != null)
                    _costTexts[i].text = hasData ? BuildCostString(data) : string.Empty;

                // 데이터 없는 슬롯은 버튼 자체를 숨긴다
                if (_recruitButtons != null && i < _recruitButtons.Length && _recruitButtons[i] != null)
                    _recruitButtons[i].gameObject.SetActive(hasData);
            }

            // 할당된 데이터보다 버튼이 더 많으면 나머지를 숨긴다
            if (_recruitButtons != null)
            {
                for (int i = dataCount; i < _recruitButtons.Length && i < MAX_SLOTS; i++)
                {
                    if (_recruitButtons[i] != null)
                        _recruitButtons[i].gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region ── 폴링 코루틴 ──

        /// <summary>
        /// 0.5초마다 자원 재고를 확인하여 버튼 interactable을 갱신하는 코루틴.
        /// OnEnable에서 시작하고 OnDisable에서 중단된다.
        /// </summary>
        private IEnumerator RefreshCoroutine()
        {
            while (true)
            {
                yield return _wait;
                RefreshButtonInteractable();
            }
        }

        /// <summary>
        /// 각 슬롯의 interactable 상태를 RecruitmentSystem.CanRecruit() 결과로 갱신한다.
        /// RecruitmentSystem이 null이면 모든 버튼을 비활성화한다.
        /// </summary>
        private void RefreshButtonInteractable()
        {
            if (_recruitOptions == null || _recruitButtons == null) return;

            bool systemAvailable = RecruitmentSystem.Instance != null;

            int slotCount = Mathf.Min(
                Mathf.Min(_recruitOptions.Length, _recruitButtons.Length),
                MAX_SLOTS);

            for (int i = 0; i < slotCount; i++)
            {
                if (_recruitButtons[i] == null) continue;
                if (_recruitOptions[i] == null)
                {
                    _recruitButtons[i].interactable = false;
                    continue;
                }

                bool canRecruit = systemAvailable
                    && RecruitmentSystem.Instance.CanRecruit(_recruitOptions[i]);
                _recruitButtons[i].interactable = canRecruit;
            }
        }

        #endregion

        #region ── 버튼 이벤트 ──

        /// <summary>
        /// 슬롯 인덱스에 해당하는 주민 모집을 RecruitmentSystem에 위임한다.
        /// </summary>
        /// <param name="slotIndex">클릭된 버튼의 슬롯 인덱스 (0~7).</param>
        private void OnRecruitButtonClicked(int slotIndex)
        {
            if (_recruitOptions == null || slotIndex >= _recruitOptions.Length)
            {
                Debug.LogWarning($"[RecruitmentPanel] OnRecruitButtonClicked: 슬롯 {slotIndex}에 데이터가 없습니다.");
                return;
            }

            VillagerRecruitData data = _recruitOptions[slotIndex];
            if (data == null)
            {
                Debug.LogWarning($"[RecruitmentPanel] 슬롯 {slotIndex}의 VillagerRecruitData가 null입니다.");
                return;
            }

            if (RecruitmentSystem.Instance == null)
            {
                Debug.LogWarning("[RecruitmentPanel] RecruitmentSystem.Instance가 null입니다. " +
                                 "씬에 RecruitmentSystem 컴포넌트가 있는지 확인하세요.");
                return;
            }

            if (_villagerPrefab == null)
            {
                Debug.LogWarning("[RecruitmentPanel] _villagerPrefab이 null입니다. " +
                                 "Inspector에서 Villager Prefab을 연결하세요.");
                return;
            }

            bool success = RecruitmentSystem.Instance.TryRecruit(data, _villagerPrefab);

            if (success)
            {
                // 모집 성공 즉시 버튼 상태를 갱신하여 UI 지연을 최소화한다
                RefreshButtonInteractable();
            }
        }

        #endregion

        #region ── 비용 문자열 생성 ──

        /// <summary>
        /// 모집 비용을 한국어 형식의 문자열로 변환한다.
        /// 0인 자원은 표시하지 않는다. 모든 비용이 0이면 "무료"를 반환한다.
        ///
        /// 형식 예시: "식량 25 | 철 12 | 구리 4"
        /// </summary>
        /// <param name="data">비용을 읽어올 모집 데이터.</param>
        private string BuildCostString(VillagerRecruitData data)
        {
            StringBuilder sb = new StringBuilder();

            if (data.cookedFoodCost > 0f) AppendCost(sb, "식량", data.cookedFoodCost);
            if (data.ironCost       > 0f) AppendCost(sb, "철",   data.ironCost);
            if (data.copperCost     > 0f) AppendCost(sb, "구리", data.copperCost);
            if (data.silverCost     > 0f) AppendCost(sb, "은",   data.silverCost);

            return sb.Length > 0 ? sb.ToString() : "무료";
        }

        /// <summary>
        /// StringBuilder에 " | " 구분자와 함께 자원 비용 항목을 추가하는 헬퍼.
        /// </summary>
        private void AppendCost(StringBuilder sb, string label, float amount)
        {
            if (sb.Length > 0) sb.Append(" | ");
            // (int) 캐스팅으로 소수점 없이 표시한다
            sb.Append(label).Append(' ').Append((int)amount);
        }

        #endregion
    }
}
