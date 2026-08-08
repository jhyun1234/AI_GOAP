using System;
using System.Collections.Generic;
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

        [Tooltip("「싸워라」 명령 goal (M21-W4 — Goal_Fight). 선택 주민에게 F 키로 하달한다. " +
                 "비면 명령 불가(중립 불변식 — 위협 축이 없는 판에서도 조용하다).\n" +
                 "⚠️ 완전 징집이 아니다: JudgeOrder 를 그대로 지나므로 배고프거나 지친 주민은 거부한다 " +
                 "(ADR-M21-7 · 정체성 C '협상'). 새 거부 사유는 만들지 않는다 — 용기는 성향이 " +
                 "goal 순위에서 이미 말한다.")]
        [SerializeField] private GoalSO _orderFight;

        [Tooltip("주민 선택 픽킹 반경 (타일). 2026-08-07 0.8 → 1.5 — 0.8은 스프라이트보다 좁아 " +
                 "'분명히 눌렀는데 안 잡히는' 클릭이 잦았다. 노드 반경과 같은 값으로 통일.")]
        [SerializeField] private float _villagerPickRadius = 1.5f;

        [Tooltip("노드 명령 픽킹 반경 (타일)")]
        [SerializeField] private float _nodePickRadius = 1.5f;

        [Tooltip("배속 단계 (관측·테스트용 빨리 감기) — 숫자키 1부터 순서대로 Time.timeScale 적용. " +
                 "전역 배속이라 시뮬·이동·대사·쿨다운이 같은 비율로 일관 (게임 로직 무수정). " +
                 "일시정지(0)는 이 배열이 아니라 0키 토글이 맡는다 — 1=1× 손버릇을 밀어내지 않기 위해.")]
        [SerializeField] private float[] _speedSteps = { 1f, 2f, 4f, 8f };

        // 배속 소유권은 이 클래스 하나 (개입 인프라, 2026-08-07). 일시정지 해제·프롬프트 자동
        // 실시간이 전부 SetSpeed를 통과하므로 "지금 몇 배속인가"의 판정이 갈리지 않는다.
        private float _resumeSpeed = 1f;    // 일시정지 직전 배속 — 0키 재입력 시 복귀 대상
        private bool _prevPendingOffer;     // 방랑자 프롬프트 열림 감지 (에지 1회)

        // 방어 울타리 그리기 모드 (M22-W3R2, 사용자 확정 2026-08-08: B 진입 → 좌클릭-드래그 =
        // 울타리 한 줄(우세축 직선 스냅, 기존 줄 곁 시작점 달라붙기) · 우클릭 = 문 1칸 · ESC 종료.
        // 프리뷰 색 = 나무 재고 판정 (초록 = 설치 가능 / 빨강 = 부족·너무 짧음 = 차단).
        // 줄은 여러 번 그어 누적된다 — 모드는 설치 후에도 유지 (줄끼리 이어 긋는 UX).
        private const int LINE_MIN_TILES = 2;    // 알고리즘 상수 — 1칸 "줄"은 줄이 아니다
        private const int SNAP_RANGE_TILES = 1;  // 시작점 달라붙기 반경 (체비쇼프)
        private bool _defenseZoneMode;
        private bool _defenseDragging;
        private Vector2Int _defenseDragStart;
        private LineRenderer _zonePreview; // 프리뷰 줄 (표현 전용 — 확정 계획 마커는 DefensePlanView 몫)
        private static readonly Color ZoneOkColor = new Color(0.3f, 0.9f, 0.35f, 0.7f);  // 초록 = 설치 가능
        private static readonly Color ZoneBadColor = new Color(0.95f, 0.25f, 0.2f, 0.7f); // 빨강 = 설치 불가

        private VillagerAgent _selected;
        private GameObject _ring;
        private Camera _camera;
        // 연대기 패널의 행 캐시 (M15-W3) — 여는 순간의 목록과 클릭 매핑을 일치시킨다.
        // 목록 문구와 같은 순서 = BuildChronicleRows 단일 출처.
        private readonly List<ChronicleArchive.RunEntry> _chronicleRows = new List<ChronicleArchive.RunEntry>();
        private AIVillage.UI.CameraController _cameraCtrl; // 상태줄 클릭 점프 (M13-B 후속) — null이면 점프 생략

        private void Start()
        {
            _camera = Camera.main;
            _cameraCtrl = _camera != null ? _camera.GetComponent<AIVillage.UI.CameraController>() : null;
        }

        private void Update()
        {
            if (M0SimulationLoop.Instance == null || _camera == null) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_defenseZoneMode) { ExitDefenseZoneMode("방어 구역 지정 취소"); return; }
                Deselect();
            }

            // 방어 울타리 그리기 (M22-W3R2) — B 토글. 모드 중에는 다른 입력을 소비한다 (오클릭 방지).
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (_defenseZoneMode) ExitDefenseZoneMode("울타리 그리기 종료");
                else EnterDefenseZoneMode();
                return;
            }
            if (_defenseZoneMode) { TickDefenseZoneMode(); return; }

#if UNITY_EDITOR
            // 디버그 전멸 (M13 — 회고 화면 즉시 관측용): Ctrl+F9. 에디터 전용, 빌드 제외.
            // 조합키 = 오폭 방지 (F9 단독 오타로 판이 날아가면 안 된다). 전 주민을 실제 아사
            // 경로로 처리 — 몇 초 뒤(마지막 대사 노출 후) 전멸 화면이 실전과 동일하게 뜬다.
            if (Input.GetKeyDown(KeyCode.F9)
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                Debug.LogWarning("[PlayerInput] 디버그 전멸 (Ctrl+F9) — 전 주민 아사 처리");
                M0SimulationLoop.Instance.Hud?.Notify("디버그 전멸 — 잠시 후 회고가 표시됩니다");
                IReadOnlyList<VillagerAgent> agents = M0SimulationLoop.Instance.Agents;
                for (int i = agents.Count - 1; i >= 0; i--)
                    if (agents[i] != null) agents[i].DebugKill();
            }
#endif

            // (M19-W4: 세율 T 키·발행 M 키는 화폐와 함께 철거 — 촌장의 손은 명령·보상만 남는다)

            // 연대기 토글 (M15-W3) — C 키 하나가 유일한 열람 통로 (게임 중·전멸 화면 공용).
            // 파일 IO는 여는 순간 1회뿐 (⚠️ 매 프레임 Load 금지). 행 목록은 클릭 매핑용 캐시 —
            // BuildChronicleRows가 목록 문구와 같은 순서를 보장한다 (단일 출처).
            if (Input.GetKeyDown(KeyCode.C))
            {
                SeasonHud hud = M0SimulationLoop.Instance.Hud;
                if (hud != null)
                {
                    if (hud.ChronicleShown) hud.ToggleChronicle(null);
                    else
                    {
                        // 전멸 후엔 현재 판이 이미 아카이브에 마감돼 있다 — 진행 중 행을 겹치지 않는다.
                        // 진행 중엔 반대로 현재 판의 저장분(첫 겨울 이후 존재)을 목록에서 제외한다 —
                        // 라이브 행과 같은 판이 두 줄로 겹친다 (Play 검증에서 발견, 2026-07-31).
                        bool over = hud.GameOverShown;
                        ChronicleArchive.RunEntry current = over
                            ? null : M0SimulationLoop.Instance.SnapshotCurrentRun(ended: false);
                        _chronicleRows.Clear();
                        _chronicleRows.AddRange(SeasonHud.BuildChronicleRows(
                            ChronicleArchive.Load().Runs, current,
                            over ? -1 : M0SimulationLoop.Instance.ArchiveRunIndex));
                        hud.ToggleChronicle(SeasonHud.ComposeChronicleList(_chronicleRows, current != null));
                    }
                }
            }

            // 배속 (M10 관측 도구) — 숫자키 1=1× 2=2× 3=4× 4=8× (기본 배열 기준, Inspector 조정 가능)
            if (_speedSteps != null)
                for (int i = 0; i < _speedSteps.Length && i < 9; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                        SetSpeed(Mathf.Max(0.1f, _speedSteps[i]));

            // 「싸워라」 명령 (F키, M21-W4) — 선택한 주민에게 교전 goal 하달.
            // 우클릭이 아니라 키인 이유: 대상은 "지목한 위협"이 아니라 **최근접 위협**이다
            // (러너가 고른다). 우클릭으로 특정 늑대를 찍게 하면 개체 지목 문법이 새로 생기는데,
            // 그건 명세에 없는 축이다 — 명령은 "가서 싸워라"까지고 누구와인지는 주민이 정한다.
            if (Input.GetKeyDown(KeyCode.F) && _selected != null && _orderFight != null)
            {
                VillagerAgent.OrderResult r = _selected.TryGiveOrder(_orderFight);
                if (r == VillagerAgent.OrderResult.Accepted)
                    M0SimulationLoop.Instance.Hud?.Notify($"{_selected.ShortName}에게 맞서라고 명령했습니다");
                // 거부는 주민의 말풍선·로그가 이미 말한다 (기존 명령 통로와 같은 규약)
            }

            // 일시정지 토글 (0키, 2026-08-07 개입 인프라) — 개입은 "무엇을 할지 정하는 시간"을
            // 필요로 하는데, 최저 배속이 1×면 판을 멈추고 생각할 방법이 없었다. 다시 0을 누르면
            // 정지 직전 배속으로 복귀한다 (8배속으로 관찰하다 멈춘 사람을 1×에 떨구지 않는다).
            // ⚠️ timeScale 0에서도 이 Update는 돈다 — 해제 입력이 막히지 않는다.
            //    카메라 이동·추적은 unscaledDeltaTime으로 옮겨 정지 중에도 둘러볼 수 있다.
            if (Input.GetKeyDown(KeyCode.Alpha0))
                SetSpeed(Time.timeScale > 0f ? 0f : _resumeSpeed);

            // 방랑자 수락/거절 (M10-E, ADR-M10-7 — 판정 입력은 이 키뿐. 술렁임은 표현 전용).
            // 프롬프트 활성 중에만 판독 (⚠️③ — 다른 단축키와 충돌 방지). 이중 입력은 서비스가 차단.
            WandererService wanderers = M0SimulationLoop.Instance.Wanderers;
            if (wanderers != null)
            {
                // 프롬프트가 **열리는 순간** 실시간 보장 (2026-08-07 개입 인프라). 8배속에서는
                // 수락 대기(WandererWaitDays)가 실시간 몇 초로 압축돼 판단하기 전에 방랑자가
                // 떠났다 — 결정을 요구하는 화면은 결정할 시간과 함께 떠야 한다.
                // 에지 1회만 = 프롬프트를 띄워 둔 채 플레이어가 다시 배속을 올리는 건 허용한다.
                bool pending = wanderers.HasPendingOffer;
                if (pending && !_prevPendingOffer && !Mathf.Approximately(Time.timeScale, 1f))
                {
                    SetSpeed(1f);
                    M0SimulationLoop.Instance.Hud?.Notify("방랑자 도착 — 1배속으로 전환");
                }
                _prevPendingOffer = pending;

                if (pending)
                {
                    if (Input.GetKeyDown(KeyCode.Y)) wanderers.Resolve(true);
                    else if (Input.GetKeyDown(KeyCode.N)) wanderers.Resolve(false);
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                SeasonHud seasonHud = M0SimulationLoop.Instance.Hud;

                // 연대기 패널 클릭 (M15-W3) — 패널이 최상단 오버레이이므로 모든 판독보다 먼저.
                // 판 줄이 아니어도 클릭을 소비한다 — 뒤에 겹친 회고 명부·주민·무덤 오클릭 방지.
                if (seasonHud != null && seasonHud.ChronicleShown)
                {
                    if (seasonHud.TryPickChronicleRunIndex(Input.mousePosition, out int runIdx)
                        && runIdx < _chronicleRows.Count)
                        seasonHud.ShowChronicleDetail(SeasonHud.ComposeRunDetail(_chronicleRows[runIdx]));
                    return;
                }

                // 회고 명부 클릭 (M13 — 드릴다운): 명부의 이름 줄 = 그 사람의 연대기를 하단에.
                // 회고 화면이 최상단 오버레이이므로 다른 판독보다 먼저다.
                if (seasonHud != null && seasonHud.GameOverShown
                    && seasonHud.TryPickGameOverRosterIndex(Input.mousePosition, out int rosterIdx))
                {
                    IReadOnlyList<VillagerRecord> roster =
                        M0SimulationLoop.Instance.Chronicle.RosterByBirth();
                    if (rosterIdx < roster.Count)
                    {
                        seasonHud.ShowGameOverDetail(SeasonHud.ComposeGraveInfo(roster[rosterIdx]));
                        return; // 명부 클릭 소비 — 뒤에 겹친 무덤·주민 오클릭 방지
                    }
                }

                // 상태 알림 클릭 (M13-B 후속) — 굶는 주민 줄 = 그 주민에게 카메라 점프 + 선택
                // (선택까지 해야 곧바로 우클릭 명령이 가능 — 개입 동선 단축).
                // 화면 UI 판독이 월드 픽킹보다 먼저다: 줄 뒤에 겹친 다른 주민을 집는 오클릭 방지.
                int statusLine = seasonHud?.PickStatusLine(Input.mousePosition) ?? -1;
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

                Vector2 world = MouseWorld();
                VillagerAgent hit = PickVillager(world);
                if (hit != null) Select(hit);
                // 무덤 조사 (M13) — 산 주민이 항상 우선, 그 다음이 무덤. 죽은 주민의 생전
                // 기록이 정보줄에 뜬다 ("아, 얘가 그 목수였지" — 플레이 도중의 회상).
                else if (M0SimulationLoop.Instance.TryPickGraveRecord(
                             world, _villagerPickRadius, out VillagerRecord grave))
                {
                    Deselect(); // 산 주민 선택·추적 해제 후 정보줄 소유권 이전
                    seasonHud?.SetGraveInfo(SeasonHud.ComposeGraveInfo(grave));
                }
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

        // ── 방어 구역 지정 모드 (M22-W3) ──────────────────────────────────────

        private void EnterDefenseZoneMode()
        {
            _defenseZoneMode = true;
            _defenseDragging = false;
            Deselect();
            M0SimulationLoop.Instance.Hud?.Notify(
                "울타리 그리기 — 드래그 = 울타리 한 줄 (초록 = 가능 · 빨강 = 불가) · 우클릭 = 문 · ESC 종료");
        }

        private void ExitDefenseZoneMode(string notice)
        {
            _defenseZoneMode = false;
            _defenseDragging = false;
            if (_zonePreview != null) _zonePreview.gameObject.SetActive(false);
            if (!string.IsNullOrEmpty(notice))
                M0SimulationLoop.Instance.Hud?.Notify(notice);
        }

        /// <summary>모드 중 매 프레임 (M22-W3R2): 좌드래그 = 울타리 한 줄 (우세축 직선 스냅 +
        /// 기존 줄 곁 시작점 달라붙기 = 줄 연결), 우클릭 = 문 1칸. 놓으면 초록일 때만 계획 추가,
        /// **모드는 유지** — 줄을 이어 그린다. 색 판정은 지정 시점 재고 기준 (선차감·예약 없음).</summary>
        private void TickDefenseZoneMode()
        {
            M0SimulationLoop sim = M0SimulationLoop.Instance;
            Vector2 world = MouseWorld();
            var cur = new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));

            // 우클릭 = 문 계획 (사용자 확정 — 출입구도 플레이어가 정한다). 드래그 중엔 무시.
            if (!_defenseDragging && Input.GetMouseButtonDown(1))
            {
                int gateWood = sim.DefenseGateWood;
                int woodNow = sim.World.GetStock(SlotId.WoodStock);
                if (woodNow < gateWood)
                    sim.Hud?.Notify($"나무가 부족합니다 — 문 필요 {gateWood} / 보유 {woodNow}");
                else if (sim.TryAddDefenseGate(cur))
                    sim.Hud?.Notify($"문 계획 — ({cur.x},{cur.y}) · 나무 {gateWood}");
                else
                    sim.Hud?.Notify("여기에는 문을 둘 수 없습니다");
                return;
            }

            if (!_defenseDragging)
            {
                DrawZonePreviewLine(cur, cur, ZoneBadColor); // 시작 전 1칸 = 줄 미달 = 빨강
                if (Input.GetMouseButtonDown(0))
                {
                    _defenseDragging = true;
                    // 시작점 달라붙기 (줄 연결) — 기존 계획·시설 곁이면 그 칸에서 잇는다
                    _defenseDragStart =
                        sim.Defense != null
                        && sim.Defense.TryGetNearestPlanOrStructureTile(cur, SNAP_RANGE_TILES, out Vector2Int snap)
                            ? snap : cur;
                }
                return;
            }

            Vector2Int end = DefenseService.SnapLineEnd(_defenseDragStart, cur); // 우세축 직선
            var tiles = DefenseService.LineTiles(_defenseDragStart, end);
            int required = tiles.Count * sim.DefenseFenceWood;
            int stock = sim.World.GetStock(SlotId.WoodStock);
            bool lengthOk = tiles.Count >= LINE_MIN_TILES;
            bool ok = lengthOk && stock >= required;
            DrawZonePreviewLine(_defenseDragStart, end, ok ? ZoneOkColor : ZoneBadColor);

            if (Input.GetMouseButtonUp(0))
            {
                _defenseDragging = false; // 모드는 유지 — 이어 그린다
                if (!ok)
                {
                    sim.Hud?.Notify(!lengthOk
                        ? "너무 짧습니다 — 최소 2칸"
                        : $"나무가 부족합니다 — 필요 {required} / 보유 {stock}");
                    return;
                }
                int added = sim.AddDefenseFenceLine(_defenseDragStart, end);
                sim.Hud?.Notify(added > 0
                    ? $"울타리 줄 계획 — {added}칸 · 나무 {added * sim.DefenseFenceWood}"
                    : "지을 수 있는 칸이 없습니다 (막힘·중복)");
            }
        }

        /// <summary>프리뷰 줄 (표현 전용) — 확정 전의 손그림자라 매 프레임 갱신·색 교체.
        /// 확정된 계획 마커는 DefensePlanView가 따로 그린다 (Defense.OnPlanChanged 구독).</summary>
        private void DrawZonePreviewLine(Vector2Int a, Vector2Int b, Color color)
        {
            if (_zonePreview == null)
            {
                var go = new GameObject("DefenseLinePreview");
                _zonePreview = go.AddComponent<LineRenderer>();
                _zonePreview.useWorldSpace = true;
                _zonePreview.loop = false;
                _zonePreview.positionCount = 2;
                _zonePreview.widthMultiplier = 0.35f; // 타일 폭 느낌 — 줄이 지나갈 칸을 보여준다
                _zonePreview.numCornerVertices = 0;
                _zonePreview.material = new Material(Shader.Find("Sprites/Default"));
                _zonePreview.sortingOrder = 8;
            }
            _zonePreview.gameObject.SetActive(true);
            _zonePreview.startColor = _zonePreview.endColor = color;
            _zonePreview.SetPositions(new[]
            {
                new Vector3(a.x, a.y, 0f),
                new Vector3(b.x, b.y, 0f),
            });
        }

        /// <summary>배속 설정의 유일한 쓰기 지점 (2026-08-07) — 배속·일시정지·자동 실시간이
        /// 전부 여기를 통과한다. 0(정지)은 복귀 대상으로 기억하지 않는다.</summary>
        private void SetSpeed(float scale)
        {
            if (scale > 0f) _resumeSpeed = scale;
            Time.timeScale = scale;
            string label = scale > 0f ? $"배속 ×{scale:0.#}" : "일시정지 — [0] 해제";
            M0SimulationLoop.Instance.Hud?.Notify(label);
            Debug.Log($"[PlayerInput] {label}");
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
            // 선택 = 추적 (M13-B 후속, 몰입 카메라). WASD를 누르면 추적만 풀리고 선택은
            // 유지된다 — 명령 동선(선택 유지 + 멀리 있는 노드 찾아 우클릭)이 끊기면 안 된다.
            _cameraCtrl?.Follow(agent.transform);
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
            _cameraCtrl?.StopFollow(); // 선택 해제 = 자유 카메라 복귀 (M13-B 후속)
        }
    }
}
