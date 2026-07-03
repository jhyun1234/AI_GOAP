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

        // RefreshLoop 코루틴 핸들 — OnDisable 시 명시적 정지
        private Coroutine _refreshCoroutine;

        /// <summary>
        /// ResourceNodeSpawner에서 호출하는 유일한 초기화 진입점.
        /// Awake/Start 대신 이 메서드가 초기화를 담당한다.
        /// </summary>
        public void Init(ResourceNode node, Color fullColor, Color emptyColor, float nodeSize, Sprite sprite)
        {
            _node      = node;
            _fullColor = fullColor;
            _emptyColor = emptyColor;

            // RequireComponent 덕분에 이미 추가되어 있음
            _sr              = GetComponent<SpriteRenderer>();
            _sr.sprite       = sprite;
            _sr.sortingOrder = 2; // 타일맵(0) + FoW 오버레이(1) 위에 렌더
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

            // 자원 잔량 비율에 따라 색상 보간 (1.0 = 가득, 0.0 = 고갈)
            float ratio = _node.MaxAmount > 0f ? _node.CurrentAmount / _node.MaxAmount : 0f;
            _sr.color = Color.Lerp(_emptyColor, _fullColor, ratio);
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
