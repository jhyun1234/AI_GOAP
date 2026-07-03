/// <summary>
/// BuildingQueuePanel.cs - 건설 대기열 표시 패널
///
/// 역할(Role): BuildingQueue.GetAllEntries()를 0.3초마다 폴링하여
///             현재 대기열 항목을 동적으로 생성/바인딩한다.
///             항목 수가 변경될 때만 자식 오브젝트를 재생성하고,
///             동일하면 Bind()만 호출하여 GC 할당을 줄인다.
///
/// 사용법(Usage):
///   1. Canvas 아래 BuildingQueuePanel GameObject에 부착한다.
///   2. Inspector에서 _contentParent(ScrollView Content Transform)와
///      _queueItemPrefab(BuildingQueueItemView가 붙은 프리팹)을 연결한다.
///   3. HUDManager.Start()에서 RefreshCoroutine()을 StartCoroutine으로 시작한다.
///
/// 의존성(Dependencies): BuildingQueue.cs, TextMeshPro, UnityEngine.UI
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// 건설 대기열을 동적으로 표시하는 패널.
    /// 항목 수 변경 시 자식 재생성, 동일 시 Bind()로 값만 갱신한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingQueuePanel : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        private const float REFRESH_INTERVAL = 0.3f; // 대기열 갱신 주기 (초)

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("대기열 항목들이 배치될 부모 Transform. ScrollView의 Content를 연결한다.")]
        [SerializeField] private Transform _contentParent;

        [Tooltip("대기열 항목 1개를 표시하는 프리팹. BuildingQueueItemView 컴포넌트가 있어야 한다.")]
        [SerializeField] private GameObject _queueItemPrefab;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        private readonly List<BuildingQueueItemView> _itemViews = new List<BuildingQueueItemView>();
        private WaitForSeconds _waitForSeconds;
        private Coroutine _refreshCoroutine;
        private int _prevItemCount = -1; // -1: 초기화 안 됨 (첫 프레임 강제 갱신)

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        private void Awake()
        {
            _waitForSeconds = new WaitForSeconds(REFRESH_INTERVAL);
        }

        private void OnDestroy()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 메서드 ──

        /// <summary>
        /// 0.3초마다 BuildingQueue를 폴링하여 대기열 항목을 갱신하는 코루틴.
        /// HUDManager.Start()에서 StartCoroutine(buildingQueuePanel.RefreshCoroutine())으로 시작된다.
        ///
        /// 최적화:
        ///   - 항목 수 동일 → Bind()만 호출 (Instantiate/Destroy 없음)
        ///   - 항목 수 변경 → 전체 재생성
        /// </summary>
        public IEnumerator RefreshCoroutine()
        {
            while (true)
            {
                yield return _waitForSeconds;

                if (BuildingQueue.Instance == null) continue;
                if (_contentParent == null) continue;

                IReadOnlyList<BuildingQueueEntry> entries = BuildingQueue.Instance.GetAllEntries();
                int currentCount = entries.Count;

                if (currentCount != _prevItemCount)
                {
                    RebuildItemViews(entries);
                    _prevItemCount = currentCount;
                }
                else
                {
                    for (int i = 0; i < _itemViews.Count && i < entries.Count; i++)
                    {
                        _itemViews[i].Bind(entries[i]);
                    }
                }
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 메서드 ──

        /// <summary>
        /// 기존 자식 오브젝트를 모두 Destroy하고 새 항목으로 재생성한다.
        /// </summary>
        private void RebuildItemViews(IReadOnlyList<BuildingQueueEntry> entries)
        {
            foreach (BuildingQueueItemView view in _itemViews)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _itemViews.Clear();

            if (_queueItemPrefab == null)
            {
                Debug.LogWarning("[BuildingQueuePanel] RebuildItemViews: _queueItemPrefab이 null입니다. " +
                                 "Inspector에서 프리팹을 연결하세요.");
                return;
            }

            foreach (BuildingQueueEntry entry in entries)
            {
                GameObject itemGO = Instantiate(_queueItemPrefab, _contentParent);
                BuildingQueueItemView view = itemGO.GetComponent<BuildingQueueItemView>();

                if (view == null)
                {
                    Debug.LogWarning("[BuildingQueuePanel] RebuildItemViews: 프리팹에 " +
                                     "BuildingQueueItemView 컴포넌트가 없습니다.");
                    Destroy(itemGO);
                    continue;
                }

                view.Bind(entry);
                _itemViews.Add(view);
            }
        }

        #endregion

    }
}
