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

        // 픽킹 반경 (M22-W3R3 — 줌 불변): 舊 월드 고정 1.5타일은 줌인(orthoSize 3)에서 화면
        // 270px로 부풀어 엉뚱한 대상을 잡았고("클릭이 정확히 안 된다"의 정체 절반), 줌아웃에선
        // 감각보다 좁았다. 화면 픽셀 기준을 월드로 환산한다 — 어느 줌에서든 손맛이 같다.
        private const float PICK_RADIUS_PX = 28f;         // UX 상수 (화면 기준)
        private const float PICK_RADIUS_WORLD_MIN = 0.6f; // 초근접 줌에서도 스프라이트 반 칸은 잡히게

        private float PickRadiusWorld()
            => Mathf.Max(PICK_RADIUS_WORLD_MIN,
                         PICK_RADIUS_PX * (_camera.orthographicSize * 2f) / Mathf.Max(1, Screen.height));

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
        // 브러시 (M22-3차 W2, ADR-M22-14): 모드 중 1/2/3 = 울타리/함정/망루. 모드가 다른 입력을
        // 소비하므로 배속 숫자키와 충돌 없음 (배속 처리는 모드 조기 반환 뒤에 있다 — 전제 ⑦).
        private int _defenseBrush; // 0=울타리 1=함정 2=망루
        private bool _defenseErasing; // Shift+드래그 = 철거 (M22-W8) — 이 드래그가 긋기인가 지우기인가
        private Vector2Int _defenseDragStart;
        private LineRenderer _zonePreview; // 프리뷰 줄 (표현 전용 — 확정 계획 마커는 DefensePlanView 몫)
        private LineRenderer _hoverCell;   // 마우스 칸 하이라이트 (M22-W3R3 — 마우스→타일이 눈에 보이게)
        private string _lastModeInfo;      // 모드 정보줄 캐시 — 같은 문자열 재대입 방지
        private static readonly Color ZoneOkColor = new Color(0.3f, 0.9f, 0.35f, 0.7f);  // 초록 = 설치 가능
        private static readonly Color ZoneBadColor = new Color(0.95f, 0.25f, 0.2f, 0.7f); // 빨강 = 설치 불가
        private static readonly Color ZoneEraseColor = new Color(1f, 0.6f, 0.15f, 0.7f);  // 주황 = 철거 (W8)

        private VillagerAgent _selected;
        private GameObject _ring;
        private Camera _camera;
        // 연대기 패널의 행 캐시 (M15-W3) — 여는 순간의 목록과 클릭 매핑을 일치시킨다.
        // 목록 문구와 같은 순서 = BuildChronicleRows 단일 출처.
        private readonly List<ChronicleArchive.RunEntry> _chronicleRows = new List<ChronicleArchive.RunEntry>();
        private AIVillage.UI.CameraController _cameraCtrl; // 상태줄 클릭 점프 (M13-B 후속) — null이면 점프 생략
#if UNITY_EDITOR
        private int _debugRaceIdx = -1; // 디버그 종족 순환 소환 커서 (첫 누름 = 0번 종족)

        /// <summary>디버그 방어선 반지름 (타일). 마을 앵커 기준 사각 링.</summary>
        private const int DEBUG_RING_RADIUS = 9;

        /// <summary>압력 부스트 1회분 (디버그 상수 — 밸런스 아님). 6이면 한 번에 최저 해금
        /// (기사단 시설 우선)이 열리고, 세 번이면 고블린 우회(14)까지 닿는다.</summary>
        private const int DEBUG_PRESSURE_STEP = 6;
#endif

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
            // 문 잠금 토글 (M22-2차 W1, ADR-M22-9) — L 하나가 유일한 통로. B 모드 밖·안 공용
            // (키보드라 오클릭 위험 없음 — 모드 소비 대상은 마우스 입력이다).
            if (Input.GetKeyDown(KeyCode.L))
            {
                M0SimulationLoop sim0 = M0SimulationLoop.Instance;
                // 잠글 문이 없으면 no-op — 보이지 않는 상태 전환 금지. 단 해제는 문 0개여도
                // 허용한다 (잠금 중 전부 파괴되면 열 수 없는 상태가 된다 — DefenseService 주석).
                if (sim0.Defense != null && !sim0.Defense.GatesLocked && !sim0.Defense.HasBuiltGates)
                    sim0.Hud?.Notify("잠글 문이 없습니다");
                else
                    sim0.Hud?.Notify(sim0.ToggleGateLock()
                        ? "문을 걸어 잠갔습니다 (L = 해제)"
                        : "문을 열었습니다");
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

            // 디버그 소환 (2026-08-10) — 습격은 게임일이 쌓여야 오는데(첫 웨이브 5일·오크 12일·
            // 타락천사 30일), 그림과 전투 표현을 고치려면 지금 와야 한다. Ctrl 조합 = 오폭 방지
            // (위 Ctrl+F9와 같은 규약). 소환 자체는 정규 통로를 그대로 지난다 — 여기서 보는 것과
            // 실전에서 오는 것이 같아야 관측이 값을 한다.
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                ThreatService threats = M0SimulationLoop.Instance.Threats;
                SeasonHud dbgHud = M0SimulationLoop.Instance.Hud;

                // 다음 습격 당기기 — 지금 압력이 사는 정규 편성이 그대로 온다 (예고도 뜬다).
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    if (threats == null) dbgHud?.Notify("디버그 — 이 판엔 위협 축이 없습니다");
                    else
                    {
                        threats.DebugStrikeNow();
                        dbgHud?.Notify("디버그 — 다음 습격을 지금 부릅니다 (Ctrl+F10)");
                    }
                }

                // 종족 순환 소환 — 해금일을 건너뛰고 배열 순서대로 하나씩. 이름으로 고르지
                // 않는다 (ADR-M24-4) — 종족이 늘면 이 키가 저절로 그만큼 돈다.
                if (Input.GetKeyDown(KeyCode.F11))
                {
                    IReadOnlyList<ThreatSO> races = threats != null ? threats.Races : null;
                    if (races == null || races.Count == 0)
                        dbgHud?.Notify("디버그 — 배포된 종족이 없습니다");
                    else
                    {
                        _debugRaceIdx = (_debugRaceIdx + 1) % races.Count;
                        ThreatSO summoned = threats.DebugSummonRace(races[_debugRaceIdx]);
                        dbgHud?.Notify($"디버그 소환 — {summoned.DisplayName} " +
                                       $"({_debugRaceIdx + 1}/{races.Count} · Ctrl+F11로 다음 종족)");
                    }
                }

                // 방어선 즉석 배치 — 침입 특성 8개 중 **3개가 지킬 것이 있어야만 발동한다**
                // (시설 우선·우회·함정 학습) + 망루 탑승 트리거도 망루가 있어야 걸린다.
                // 2026-08-10 S8 관측에서 44일을 돌렸는데 울타리 0·망루 0이었다 — 그 구간에서는
                // 특성의 절반이 화면에 나올 수가 없다. 🔴 F9는 디버그 전멸이라 F8을 쓴다.
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    (int f, int g, int t) = M0SimulationLoop.Instance.DebugBuildDefenseRing(DEBUG_RING_RADIUS);
                    dbgHud?.Notify($"디버그 방어선 — 울타리 {f} · 문 {g} · 망루 {t} " +
                                   "(망루는 북·서 두 변에만 — 전 변 감시면 우회가 안 보인다)");
                }

                // 압력 부스트 — 특성 해금이 압력 6~22에 걸려 있는데 초반 판은 5 안팎이라,
                // 이 키가 없으면 **네 종족이 전부 정면으로 오는 구간만** 관측된다 (2026-08-10 Play).
                // 🔴 되돌릴 수 없다 (ADR-M24-1) — 낮은 압력을 다시 보려면 새 판을 시작한다.
                // 🔑 S8을 재려면 F12로 압력을 올린 뒤 **F10**(예고 경유)을 쓴다. F11은 예고를
                //    건너뛰므로 "예고를 보고 다르게 준비한다"를 관측할 수 없다.
                if (Input.GetKeyDown(KeyCode.F12) && threats != null)
                {
                    int now = threats.DebugBoostPressure(DEBUG_PRESSURE_STEP);
                    dbgHud?.Notify($"디버그 — 압력 +{DEBUG_PRESSURE_STEP} → 현재 {now} " +
                                   "(되돌릴 수 없음 · 다음은 Ctrl+F10으로 예고부터)");
                    Debug.Log($"[Debug] 압력 부스트 → {now} — 해금: " +
                              $"기사단 시설우선 6 · 고블린 야습 8 · 오크 결사 10 · " +
                              $"기사단 다지점 12 · 고블린 우회 14 · 고블린 함정학습 22");
                }
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
            // (러너가 고른다). 우클릭으로 특정 위협을 찍게 하면 개체 지목 문법이 새로 생기는데,
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
                             world, PickRadiusWorld(), out VillagerRecord grave))
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
            _defenseBrush = 0; // 진입은 언제나 울타리 브러시 — 예측 가능한 손버릇 (ADR-M22-14)
            _lastModeInfo = null;
            Deselect();
        }

        private void ExitDefenseZoneMode(string notice)
        {
            _defenseZoneMode = false;
            _defenseDragging = false;
            if (_zonePreview != null) _zonePreview.gameObject.SetActive(false);
            if (_hoverCell != null) _hoverCell.gameObject.SetActive(false);
            M0SimulationLoop.Instance.Hud?.ClearModeInfo();
            if (!string.IsNullOrEmpty(notice))
                M0SimulationLoop.Instance.Hud?.Notify(notice);
        }

        /// <summary>모드 정보줄 갱신 (M22-W3R3) — 상시 안내는 Notify(휘발)가 아니라 전용 줄로.
        /// 같은 문자열이면 재대입하지 않는다 (TMP 리레이아웃 방지).</summary>
        private void SetModeInfo(string line)
        {
            if (line == _lastModeInfo) return;
            _lastModeInfo = line;
            M0SimulationLoop.Instance.Hud?.SetModeInfo(line);
        }

        /// <summary>모드 중 매 프레임 (M22-W3R2): 좌드래그 = 울타리 한 줄 (우세축 직선 스냅 +
        /// 기존 줄 곁 시작점 달라붙기 = 줄 연결), 우클릭 = 문 1칸. 놓으면 초록일 때만 계획 추가,
        /// **모드는 유지** — 줄을 이어 그린다. 색 판정은 지정 시점 재고 기준 (선차감·예약 없음).</summary>
        private void TickDefenseZoneMode()
        {
            M0SimulationLoop sim = M0SimulationLoop.Instance;
            Vector2 world = MouseWorld();
            var cur = new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));
            int stock = sim.World.GetStock(SlotId.WoodStock);
            DrawHoverCell(cur); // 마우스가 가리키는 칸을 항상 보여준다 (W3R3 — 어긋남 체감 제거)

            // 브러시 전환 (M22-3차 W2, ADR-M22-14) — 드래그 중엔 무시 (긋는 도중 종류가 바뀌면
            // 줄이 오염된다). 모드가 입력을 소비하므로 배속 숫자키에 닿지 않는다 (전제 ⑦).
            if (!_defenseDragging)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) _defenseBrush = 0;
                else if (Input.GetKeyDown(KeyCode.Alpha2)) _defenseBrush = 1;
                else if (Input.GetKeyDown(KeyCode.Alpha3)) _defenseBrush = 2;
            }

            // 우클릭 = 문 계획 / 완공 울타리면 문 전환 (M22-2차 W2 — 같은 손짓, 같은 뜻:
            // "여기가 출입구다"). 문은 울타리 줄의 어휘 — 울타리 브러시 전용. 드래그 중엔 무시.
            if (!_defenseDragging && Input.GetMouseButtonDown(1))
            {
                if (_defenseBrush != 0)
                {
                    sim.Hud?.Notify("문은 울타리 브러시(1)에서 우클릭으로 냅니다");
                    return;
                }
                int gateWood = sim.DefenseGateWood;
                // 전환 대상(완공 울타리)이면 순 비용 판정 — 철거 회수를 셈에 넣는다 (사용자 확정)
                bool convertTarget = sim.Defense != null && sim.Defense.CanConvertToGateAt(cur);
                int refundIfAny = convertTarget ? sim.DefenseFenceWood : 0;
                if (!M0SimulationLoop.ConversionAffordable(stock, refundIfAny, gateWood))
                    sim.Hud?.Notify($"나무가 부족합니다 — 문 필요 {gateWood} / 보유 {stock}" +
                                    (convertTarget ? $" (회수 {refundIfAny} 포함)" : ""));
                else if (convertTarget && sim.TryConvertFenceToGateAt(cur, out int refund))
                    sim.Hud?.Notify($"울타리를 헐고 문을 냅니다 — 나무 {refund} 회수 · 문 {gateWood}");
                else if (!convertTarget && sim.TryAddDefenseGate(cur))
                    sim.Hud?.Notify($"문 계획 — ({cur.x},{cur.y}) · 나무 {gateWood}");
                else
                    sim.Hud?.Notify("여기에는 문을 둘 수 없습니다");
                return;
            }

            if (!_defenseDragging)
            {
                if (_zonePreview != null) _zonePreview.gameObject.SetActive(false); // 줄은 드래그 중에만
                SetModeInfo(ComposeBrushInfo(sim, stock));
                if (Input.GetMouseButtonDown(0))
                {
                    bool erasing = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    // 망루 브러시 = 1칸 설치 (드래그 없음, M22-3차 W2). Shift는 공용 철거 드래그로.
                    if (!erasing && _defenseBrush == 2)
                    {
                        int twood = sim.DefenseTowerWood, tstone = sim.DefenseTowerStone;
                        int stoneStock = sim.World.GetStock(SlotId.StoneStock);
                        if (stock < twood || stoneStock < tstone)
                            sim.Hud?.Notify($"자재가 부족합니다 — 망루 나무 {twood}·돌 {tstone} / " +
                                            $"보유 나무 {stock}·돌 {stoneStock}");
                        else if (sim.TryAddDefenseTower(cur))
                            sim.Hud?.Notify($"망루 계획 — ({cur.x},{cur.y}) · 나무 {twood}+돌 {tstone}");
                        else
                            sim.Hud?.Notify("여기에는 망루를 둘 수 없습니다");
                        return;
                    }
                    _defenseDragging = true;
                    // Shift = 철거 드래그 (M22-W8) — 지우기는 달라붙지 않는다 (가리킨 곳을 지운다)
                    _defenseErasing = erasing;
                    // 시작점 달라붙기 (줄 연결) — 울타리 브러시 전용. 함정 줄이 울타리 곁에
                    // 달라붙으면 길목이 아니라 벽에 깔린다 (의도와 반대).
                    _defenseDragStart =
                        !_defenseErasing && _defenseBrush == 0 && sim.Defense != null
                        && sim.Defense.TryGetNearestPlanOrStructureTile(cur, SNAP_RANGE_TILES, out Vector2Int snap)
                            ? snap : cur;
                }
                return;
            }

            Vector2Int end = DefenseService.SnapLineEnd(_defenseDragStart, cur); // 우세축 직선

            // 철거 드래그 (M22-W8) — 계획은 무료 취소, 완공 시설은 철거 + 나무 회수
            if (_defenseErasing)
            {
                var eraseTiles = DefenseService.LineTiles(_defenseDragStart, end);
                DrawZonePreviewLine(_defenseDragStart, end, ZoneEraseColor);
                SetModeInfo($"철거 — 줄 {eraseTiles.Count}칸 (계획 무료 · 시설은 나무 회수)");
                if (Input.GetMouseButtonUp(0))
                {
                    _defenseDragging = false; // 모드는 유지
                    int removed = 0, refunded = 0;
                    foreach (Vector2Int t in eraseTiles)
                        if (sim.TryDemolishDefenseAt(t, out int refund)) { removed++; refunded += refund; }
                    sim.Hud?.Notify(removed > 0
                        ? $"철거 — {removed}칸{(refunded > 0 ? $" · 나무 {refunded} 회수" : "")}"
                        : "철거할 것이 없습니다");
                }
                return;
            }

            // 줄 긋기 — 울타리/함정 공용 문법, 비용·창구·최소 길이만 브러시가 가른다 (M22-3차 W2)
            bool isTrap = _defenseBrush == 1;
            string label = isTrap ? "함정" : "울타리";
            int unitCost = isTrap ? sim.DefenseTrapWood : sim.DefenseFenceWood;
            int minTiles = isTrap ? 1 : LINE_MIN_TILES; // 함정은 1칸도 함정 — "1칸 줄" 규칙은 벽의 논리
            var tiles = DefenseService.LineTiles(_defenseDragStart, end);
            int required = tiles.Count * unitCost;
            bool lengthOk = tiles.Count >= minTiles;
            bool ok = lengthOk && stock >= required;
            DrawZonePreviewLine(_defenseDragStart, end, ok ? ZoneOkColor : ZoneBadColor);
            SetModeInfo(ok
                ? $"{label} 줄 {tiles.Count}칸 = 나무 {required} / 보유 {stock} — 놓으면 설치"
                : (!lengthOk ? $"{label} 줄 — 너무 짧습니다 (최소 {minTiles}칸)"
                             : $"{label} 줄 {tiles.Count}칸 = 나무 {required} / 보유 {stock} — 부족!"));

            if (Input.GetMouseButtonUp(0))
            {
                _defenseDragging = false; // 모드는 유지 — 이어 그린다
                if (!ok)
                {
                    sim.Hud?.Notify(!lengthOk
                        ? $"너무 짧습니다 — 최소 {minTiles}칸"
                        : $"나무가 부족합니다 — 필요 {required} / 보유 {stock}");
                    return;
                }
                DefenseService.PlanResult r = isTrap
                    ? sim.AddDefenseTrapLine(_defenseDragStart, end)
                    : sim.AddDefenseFenceLine(_defenseDragStart, end);
                // 🔴 M22-4차 W3 — 舊 문구는 **빠진 칸을 말하지 않았다.** "8칸 계획"만 보고 넘어간
                //    플레이어의 담에는 구멍이 남았고 그리로 고블린이 드나들었다 (사용자 Play).
                //    막힌 칸이 0이면 괄호를 안 붙인다 — 평소 문구를 어지럽히지 않는다.
                sim.Hud?.Notify(!r.Nothing
                    ? $"{label} 줄 계획 — {r.Added}칸 · 나무 {r.Added * unitCost}"
                      + (r.Blocked > 0 ? $" (⚠ {r.Blocked}칸은 나무·돌에 막혀 대기 — 먼저 치웁니다)" : "")
                    : "지을 수 있는 칸이 없습니다 (막힘·중복)");
            }
        }

        /// <summary>브러시별 대기 정보줄 (M22-3차 W2) — [1울타리 2함정 3망루] 접두가 전환 안내를 겸한다.</summary>
        private string ComposeBrushInfo(M0SimulationLoop sim, int stock)
        {
            const string brushes = "[1울타리 2함정 3망루]";
            switch (_defenseBrush)
            {
                case 1:
                    return $"함정 깔기 {brushes} — 나무 {stock} · 좌드래그 = 줄({sim.DefenseTrapWood}목/칸, " +
                           "위협이 밟으면 터짐) · Shift+드래그 = 철거 · ESC 종료";
                case 2:
                    return $"망루 놓기 {brushes} — 나무 {stock}·돌 {sim.World.GetStock(SlotId.StoneStock)} · " +
                           $"좌클릭 = 1기({sim.DefenseTowerWood}목+{sim.DefenseTowerStone}석) · " +
                           "Shift+드래그 = 철거 · ESC 종료";
                default:
                    return $"울타리 그리기 {brushes} — 나무 {stock} · 좌드래그 = 줄({sim.DefenseFenceWood}목/칸) · " +
                           $"우클릭 = 문({sim.DefenseGateWood}목) · Shift+드래그 = 철거 · ESC 종료";
            }
        }

        /// <summary>마우스 칸 하이라이트 (표현 전용, M22-W3R3) — 지금 어느 타일을 가리키는지
        /// 눈에 보이게. 프리뷰 줄과 별개로 항상 그린다 (칸 경계 = 타일 중심 ±0.5).</summary>
        private void DrawHoverCell(Vector2Int tile)
        {
            if (_hoverCell == null)
            {
                var go = new GameObject("DefenseHoverCell");
                _hoverCell = go.AddComponent<LineRenderer>();
                _hoverCell.useWorldSpace = true;
                _hoverCell.loop = true;
                _hoverCell.positionCount = 4;
                _hoverCell.widthMultiplier = 0.06f;
                _hoverCell.numCornerVertices = 0;
                _hoverCell.material = new Material(Shader.Find("Sprites/Default"));
                _hoverCell.startColor = _hoverCell.endColor = new Color(1f, 1f, 1f, 0.8f);
                _hoverCell.sortingOrder = WorldSort.Decal; // 바닥 칸 표시 — 월드 물건 아래
            }
            _hoverCell.gameObject.SetActive(true);
            _hoverCell.SetPositions(new[]
            {
                new Vector3(tile.x - 0.5f, tile.y - 0.5f, 0f),
                new Vector3(tile.x + 0.5f, tile.y - 0.5f, 0f),
                new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f),
                new Vector3(tile.x - 0.5f, tile.y + 0.5f, 0f),
            });
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
                _zonePreview.sortingOrder = WorldSort.Decal; // 줄 미리보기도 바닥 데칼
            }
            _zonePreview.gameObject.SetActive(true);
            _zonePreview.startColor = _zonePreview.endColor = color;
            _zonePreview.SetPositions(new[]
            {
                new Vector3(a.x, a.y, 0f),
                new Vector3(b.x, b.y, 0f),
            });
        }

        /// <summary>외부 배속 요청 창구 (롱폼 촬영 W3 보강, 2026-08-09) — 유일한 사용처는
        /// CaptureDirector(무인 촬영 순항 배속). 소유권은 여전히 이 클래스다: 쓰기가
        /// SetSpeed 하나를 통과하므로 "지금 몇 배속인가" 판정이 갈리지 않는다.
        /// Time.timeScale 직접 쓰기가 진짜 위반이고, 이 창구가 그걸 막는 길이다.</summary>
        public void RequestSpeed(float scale) => SetSpeed(scale, notify: false);
        // notify:false — 촬영 감독은 사건마다 배속을 오가는데, 그때마다 HUD 토스트가
        // 뜨면 녹화 화면에 「배속 ×1」이 찍힌다. 로그는 남긴다(진단 경로 유지).

        /// <summary>배속 설정의 유일한 쓰기 지점 (2026-08-07) — 배속·일시정지·자동 실시간이
        /// 전부 여기를 통과한다. 0(정지)은 복귀 대상으로 기억하지 않는다.</summary>
        private void SetSpeed(float scale, bool notify = true)
        {
            if (scale > 0f) _resumeSpeed = scale;
            Time.timeScale = scale;
            string label = scale > 0f ? $"배속 ×{scale:0.#}" : "일시정지 — [0] 해제";
            if (notify) M0SimulationLoop.Instance.Hud?.Notify(label);
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
            float bestDist = PickRadiusWorld();
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
            float bestDist = PickRadiusWorld();
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
            // 링은 하나뿐 — 옛 주인이 계속 깊이를 쓰면 두 사람이 같은 링을 밀고 당긴다
            if (_selected != null && _selected != agent && _ring != null)
                _selected.DetachSelectionRing(_ring.GetComponent<SpriteRenderer>());
            _selected = agent;
            if (_ring == null)
            {
                _ring = new GameObject("SelectionRing");
                var sr = _ring.AddComponent<SpriteRenderer>();
                sr.sprite = M0Sprites.Circle;
                sr.color = new Color(1f, 0.9f, 0.3f, 0.45f); // 반투명 노랑
                _ring.transform.localScale = Vector3.one * 1.2f;
            }
            _ring.transform.SetParent(agent.transform, worldPositionStays: false);
            _ring.transform.localPosition = Vector3.zero;
            _ring.SetActive(true);
            // 링의 깊이는 주민이 매 프레임 맞춘다 (같이 움직이니까 — WorldSort.Select).
            // 고정 층으로 두면 주민이 집 뒤로 가도 링만 집 위에 남는다.
            _selected.AttachSelectionRing(_ring.GetComponent<SpriteRenderer>());
            M0SimulationLoop.Instance.Hud?.SetSelected(agent); // 정보줄 표시 (M7-A)
            // 선택 = 추적 (M13-B 후속, 몰입 카메라). WASD를 누르면 추적만 풀리고 선택은
            // 유지된다 — 명령 동선(선택 유지 + 멀리 있는 노드 찾아 우클릭)이 끊기면 안 된다.
            _cameraCtrl?.Follow(agent.transform);
        }

        private void Deselect()
        {
            if (_selected != null && _ring != null)
                _selected.DetachSelectionRing(_ring.GetComponent<SpriteRenderer>());
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
