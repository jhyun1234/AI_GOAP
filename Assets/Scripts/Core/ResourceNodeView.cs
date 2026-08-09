using System.Collections;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// ResourceNode 1개에 대응하는 시각적 마커 컴포넌트.
    /// ResourceNodeSpawner가 Init()으로 데이터를 주입하여 사용한다.
    ///
    /// 동작 규칙:
    ///   - IsDiscovered == false  → SpriteRenderer 비활성 (FoW 미탐험 구역 비가시)
    ///   - IsDiscovered == true   → CurrentAmount 비율로 nodeColor ↔ depletedColor 보간
    ///   - Update() 사용 금지 — 노드 100개 이상 대비 코루틴 폴링으로 갱신
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ResourceNodeView : MonoBehaviour
    {
        [Tooltip("색상 및 FoW 가시성 갱신 간격 (초)")]
        [SerializeField] private float _refreshInterval = 0.5f;

        private ResourceNode   _node;
        private SpriteRenderer _sr;
        private Color          _fullColor;
        private Color          _emptyColor;
        // 실그림 배선 (2026-08-09). _tintByAmount면 舊 색 원 경로 그대로 (중립).
        private Sprite         _fullSprite;
        private Sprite         _depletedSprite;
        private float          _depletedBelowRatio = 0.25f;
        private bool           _tintByAmount = true;

        // RefreshLoop 코루틴 핸들 — OnDisable 시 명시적 정지
        private Coroutine _refreshCoroutine;

        /// <summary>
        /// ResourceNodeSpawner에서 호출하는 유일한 초기화 진입점.
        /// Awake/Start 대신 이 메서드가 초기화를 담당한다.
        /// </summary>
        public void Init(ResourceNode node, Color fullColor, Color emptyColor, float nodeSize, Sprite sprite,
                         Sprite depletedSprite = null, float depletedBelowRatio = 0.25f,
                         bool tintByAmount = true)
        {
            _node      = node;
            _fullColor = fullColor;
            _emptyColor = emptyColor;
            _fullSprite = sprite;
            _depletedSprite = depletedSprite;
            _depletedBelowRatio = depletedBelowRatio;
            _tintByAmount = tintByAmount;

            // RequireComponent 덕분에 이미 추가되어 있음
            _sr              = GetComponent<SpriteRenderer>();
            _sr.sprite       = sprite;
            // 깊이 = 월드 Y (AIVillage.M0.WorldSort) — 나무가 주민 앞뒤에 제대로 선다.
            // 舊 고정 층(2)은 실그림이 들어오자 주민이 나무를 늘 뚫고 나왔다.
            _sr.sortingOrder = AIVillage.M0.WorldSort.Order(transform.position.y, AIVillage.M0.WorldSort.Node);
            transform.localScale = Vector3.one * nodeSize;

            Refresh();

            // Awake()에서 호출되더라도 GameObject가 활성화되어 있으므로 안전
            _refreshCoroutine = StartCoroutine(RefreshLoop());
        }

        private void OnDisable()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private void OnEnable()
        {
            // 재활성화 시 폴링 재개 (_node가 이미 주입된 경우에만)
            if (_node != null && _refreshCoroutine == null)
                _refreshCoroutine = StartCoroutine(RefreshLoop());
        }

        private IEnumerator RefreshLoop()
        {
            var wait = new WaitForSeconds(_refreshInterval);
            while (_node != null)
            {
                Refresh();
                yield return wait;
            }
        }

        private void Refresh()
        {
            if (_node == null || _sr == null) return;

            // FoW: 미발견 노드는 렌더러를 끄고 나머지 처리 생략
            if (!_node.IsDiscovered)
            {
                _sr.enabled = false;
                return;
            }

            _sr.enabled = true;

            // 자원 잔량 비율 (1.0 = 가득, 0.0 = 고갈)
            float ratio = _node.MaxAmount > 0f ? _node.CurrentAmount / _node.MaxAmount : 0f;

            if (_tintByAmount)
            {
                _sr.color = Color.Lerp(_emptyColor, _fullColor, ratio); // 舊 색 원 경로 (중립)
                return;
            }

            // 실그림 경로 — 색으로 물들이지 않고 **그림을 바꾼다** (그루터기·부스러기).
            // 고갈 그림이 없으면 흐리게만: 잔량이 안 보이면 어느 나무로 갈지 못 고른다.
            bool low = ratio <= _depletedBelowRatio;
            _sr.sprite = low && _depletedSprite != null ? _depletedSprite : _fullSprite;
            _sr.color = low && _depletedSprite == null
                ? new Color(0.62f, 0.62f, 0.62f, 1f)
                : Color.white;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_node == null) return;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.8f,
                $"{_node.ResourceType}\n{_node.CurrentAmount:F0}/{_node.MaxAmount:F0}"
            );
        }
#endif
    }
}
