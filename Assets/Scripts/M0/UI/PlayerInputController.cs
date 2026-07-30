using System;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 촌장(플레이어) 입력 (M1-C):
    ///   좌클릭 주민 = 선택 (선택 링) / 빈 곳 좌클릭·ESC = 해제
    ///   선택 중 우클릭 자원 노드 = "저거 캐와" 명령 (자원 타입 → Order goal 매핑)
    ///   선택 중 우클릭 주민 자신 = 명령 취소
    /// 콜라이더 없이 거리 픽킹 — 반경은 Inspector 튜닝 (UX 상수).
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputController : MonoBehaviour
    {
        [Serializable]
        public struct OrderMapping
        {
            [Tooltip("우클릭한 노드의 자원 타입")]
            public ResourceType Resource;
            [Tooltip("주입할 명령 goal (Priority 60 Order_* 에셋)")]
            public GoalSO Order;
        }

        [Tooltip("자원 타입 → 명령 goal 매핑. 매핑 없는 타입은 명령 불가 (Iron 등 M1 미지원).")]
        [SerializeField] private OrderMapping[] _orders;

        [Tooltip("Shift+우클릭 명령에 거는 보상 (M6-E — 설득 수단 1호). 비면 보상 명령 불가.")]
        [SerializeField] private RewardSO _rewardOnOrder;

        [Tooltip("주민 선택 픽킹 반경 (타일)")]
        [SerializeField] private float _villagerPickRadius = 0.8f;

        [Tooltip("노드 명령 픽킹 반경 (타일)")]
        [SerializeField] private float _nodePickRadius = 1.5f;

        [Tooltip("배속 단계 (관측·테스트용 빨리 감기) — 숫자키 1부터 순서대로 Time.timeScale 적용. " +
                 "전역 배속이라 시뮬·이동·대사·쿨다운이 같은 비율로 일관 (게임 로직 무수정).")]
        [SerializeField] private float[] _speedSteps = { 1f, 2f, 4f, 8f };

        private VillagerAgent _selected;
        private GameObject _ring;
        private Camera _camera;
        private AIVillage.UI.CameraController _cameraCtrl; // 상태줄 클릭 점프 (M13-B 후속) — null이면 점프 생략

        private void Start()
        {
            _camera = Camera.main;
            _cameraCtrl = _camera != null ? _camera.GetComponent<AIVillage.UI.CameraController>() : null;
        }

        private void Update()
        {
            if (M0SimulationLoop.Instance == null || _camera == null) return;

            if (Input.GetKeyDown(KeyCode.Escape)) Deselect();

            // 배속 (M10 관측 도구) — 숫자키 1=1× 2=2× 3=4× 4=8× (기본 배열 기준, Inspector 조정 가능)
            if (_speedSteps != null)
                for (int i = 0; i < _speedSteps.Length && i < 9; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    {
                        Time.timeScale = Mathf.Max(0.1f, _speedSteps[i]);
                        M0SimulationLoop.Instance.Hud?.Notify($"배속 ×{Time.timeScale:0.#}");
                        Debug.Log($"[PlayerInput] 배속 ×{Time.timeScale:0.#}");
                    }

            // 방랑자 수락/거절 (M10-E, ADR-M10-7 — 판정 입력은 이 키뿐. 술렁임은 표현 전용).
            // 프롬프트 활성 중에만 판독 (⚠️③ — 다른 단축키와 충돌 방지). 이중 입력은 서비스가 차단.
            WandererService wanderers = M0SimulationLoop.Instance.Wanderers;
            if (wanderers != null && wanderers.HasPendingOffer)
            {
                if (Input.GetKeyDown(KeyCode.Y)) wanderers.Resolve(true);
                else if (Input.GetKeyDown(KeyCode.N)) wanderers.Resolve(false);
            }

            if (Input.GetMouseButtonDown(0))
            {
                // 상태 알림 클릭 (M13-B 후속) — 굶는 주민 줄 = 그 주민에게 카메라 점프 + 선택
                // (선택까지 해야 곧바로 우클릭 명령이 가능 — 개입 동선 단축).
                // 화면 UI 판독이 월드 픽킹보다 먼저다: 줄 뒤에 겹친 다른 주민을 집는 오클릭 방지.
                int statusLine = M0SimulationLoop.Instance.Hud?.PickStatusLine(Input.mousePosition) ?? -1;
                if (statusLine >= 0)
                {
                    VillagerAgent starving = M0SimulationLoop.Instance.FindStarvingVillagerAt(statusLine);
                    if (starving != null)
                    {
                        Select(starving);
                        _cameraCtrl?.FocusOn(starving.transform.position);
                    }
                    return; // 상태줄 클릭은 월드 클릭이 아니다 — 부상·위협 줄도 소비만 (오탈선택 방지)
                }

                VillagerAgent hit = PickVillager(MouseWorld());
                if (hit != null) Select(hit);
                else Deselect();
            }

            if (Input.GetMouseButtonDown(1) && _selected != null)
            {
                Vector2 world = MouseWorld();

                // 자기 자신 우클릭 = 명령 취소
                VillagerAgent hitVillager = PickVillager(world);
                if (hitVillager == _selected)
                {
                    _selected.CancelOrder();
                    return;
                }

                // 발견된 노드 우클릭 = 채집 명령
                ResourceNode node = PickNode(world);
                if (node == null) return;

                GoalSO order = FindOrder(node.ResourceType);
                if (order == null)
                {
                    Debug.Log($"[PlayerInput] {node.ResourceType}에 대한 명령 매핑 없음 (M1 미지원 자원).");
                    return;
                }

                // Shift+우클릭 = 보상 명령 (M6-E) — 거부당한 주민에게 "그럼 이건 어때?"
                bool withReward = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                RewardSO reward = withReward ? _rewardOnOrder : null;
                if (withReward && reward == null)
                    Debug.Log("[PlayerInput] 보상 에셋(_rewardOnOrder) 미배선 — 일반 명령으로 하달.");

                // 지목 노드 동봉 — 수락/거부 피드백은 주민의 말풍선·로그가 담당
                var result = _selected.TryGiveOrder(order, node, reward);
                if (result == VillagerAgent.OrderResult.FailedNoStock)
                {
                    Debug.Log($"[PlayerInput] 보상 하달 실패 — {reward.DisplayName} 재고 부족 (약속은 재고가 담보).");
                    M0SimulationLoop.Instance.Hud?.Notify($"보상을 걸 {reward.DisplayName} 재고가 부족합니다");
                }
            }
        }

        private Vector2 MouseWorld()
        {
            Vector3 w = _camera.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(w.x, w.y);
        }

        private VillagerAgent PickVillager(Vector2 world)
        {
            VillagerAgent best = null;
            float bestDist = _villagerPickRadius;
            foreach (VillagerAgent a in M0SimulationLoop.Instance.Agents)
            {
                if (a == null) continue;
                float d = Vector2.Distance(world, a.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = a;
                }
            }
            return best;
        }

        private ResourceNode PickNode(Vector2 world)
        {
            ResourceNode best = null;
            float bestDist = _nodePickRadius;
            foreach (ResourceNode n in M0SimulationLoop.Instance.Discovery.Nodes)
            {
                if (!n.IsDiscovered) continue; // 못 본 노드에는 명령 불가 (FoW 공정성)
                float d = Vector2.Distance(world, new Vector2(n.TileX, n.TileY));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }
            return best;
        }

        private GoalSO FindOrder(ResourceType type)
        {
            if (_orders == null) return null;
            foreach (OrderMapping m in _orders)
                if (m.Resource == type)
                    return m.Order;
            return null;
        }

        private void Select(VillagerAgent agent)
        {
            _selected = agent;
            if (_ring == null)
            {
                _ring = new GameObject("SelectionRing");
                var sr = _ring.AddComponent<SpriteRenderer>();
                sr.sprite = M0Sprites.Circle;
                sr.color = new Color(1f, 0.9f, 0.3f, 0.45f); // 반투명 노랑
                sr.sortingOrder = 9;                          // 주민(10) 바로 아래
                _ring.transform.localScale = Vector3.one * 1.2f;
            }
            _ring.transform.SetParent(agent.transform, worldPositionStays: false);
            _ring.transform.localPosition = Vector3.zero;
            _ring.SetActive(true);
            M0SimulationLoop.Instance.Hud?.SetSelected(agent); // 정보줄 표시 (M7-A)
        }

        private void Deselect()
        {
            _selected = null;
            if (_ring != null)
            {
                _ring.transform.SetParent(null);
                _ring.SetActive(false);
            }
            M0SimulationLoop.Instance.Hud?.SetSelected(null); // 정보줄 소거 (M7-A)
        }
    }
}
