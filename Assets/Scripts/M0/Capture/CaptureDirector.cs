using UnityEngine;

namespace AIVillage.M0.Capture
{
    /// <summary>
    /// 무인 촬영 세션의 관전 감독 (롱폼 트랙 W3, ADR-LF-4·LF-8).
    ///
    /// 씬에 존재하지 않는다 — 촬영 세션(CaptureSessionRunner)이 Play 진입 때 만들어
    /// 붙인다. 게임플레이에 쓰지 않고(읽기·구독만), 세션이 아니면 어떤 코드도 돌지
    /// 않는다. 판단 규칙:
    ///   ① 사건 이벤트를 구독해 cuts.json 에 전부 기록한다 (버리는 선별은 나중에,
    ///      기록은 지금 — 원본 raw.mp4 가 상시 녹화라 기록만 있으면 컷은 살아 있다)
    ///   ② 우선순위가 현재 관심사 이상이거나 유지 시간이 끝났으면 카메라를 사건
    ///      좌표로 보낸다 (attended). 낮으면 기록만 하고 카메라는 안 움직인다.
    ///   ③ 사건이 없는 구간은 B-roll — 살아 있는 주민 하나를 골라 따라간다.
    ///
    /// 우선순위 (숫자 클수록 선점): 사망 100 > 파괴 90 > 재해 85 > 타격·공성 80 >
    /// 예고 60 > 격퇴·사냥 50 > 완공·입주 40 > 개인사(부상·거부…) 30 > B-roll 10.
    /// 값은 서사 무게 순의 제안치다 — 수확(S4)이 빈약하면 여기부터 조정한다.
    /// </summary>
    public sealed class CaptureDirector : MonoBehaviour
    {
        /// <summary>녹화 시작 프레임 — Runner 가 StartRecording 직후 넣는다.</summary>
        public int RecordStartFrame;
        public string SessionDir;
        public float PlannedMinutes = 30f;

        /// <summary>순항 배속 (사용자 제안 2026-08-09) — 사건이 없을 때는 빨리 감아 수확
        /// 밀도를 올리고, **사건이 터지면 1×로 내려** 클립이 자연 속도로 찍히게 한다.
        /// 4 = 숫자키 3 단계와 동일. 쓰기는 PlayerInputController.RequestSpeed 창구만
        /// (배속 소유권 단일성). 방랑자 제안이 대기 중이면 게임의 1× 강제를 존중한다.</summary>
        public float CruiseSpeed = 4f;

        /// <summary>세션 종료(계획 시간 도달) 통지 — Runner 가 녹화 정지·Play 종료를 맡는다.</summary>
        public event System.Action OnSessionComplete;

        private const float HOLD_SEC = 6f;        // 한 사건을 비추는 최소 시간
        private const float BROLL_EVERY = 20f;    // 사건이 없을 때 B-roll 마킹 간격
        private const float CAM_LERP = 2.5f;      // 카메라 추적 속도 (지수 감쇠 계수)

        private M0SimulationLoop _sim;
        private CutLog _log;
        private Camera _cam;
        private Behaviour _playerCamCtrl;         // 촬영 동안 꺼 두는 CameraController (파일 무수정)
        private PlayerInputController _input;     // 배속 창구 (RequestSpeed) — 소유권은 그쪽

        private Vector3 _target;
        private int _curPriority;
        private float _holdUntil;                 // ⏱ 배속이 오가므로 시계는 전부 unscaledTime
        private float _nextBrollAt;
        private float _slowUntil;                 // 사건 촬영용 1× 유지 구간
        private float _curSpeed = 1f;             // 마지막으로 요청한 배속 (재요청 방지)
        private bool _done;

        private void Start()
        {
            _sim = FindAnyObjectByType<M0SimulationLoop>();
            if (_sim == null)
            {
                // 세션을 끝내 준다 — 여기서 그냥 꺼지면 Runner 가 종료 통지를 못 받아
                // CLI 세션이 영원히 안 끝난다 (실측 2026-08-09, 빈 씬 사고의 후반부).
                Debug.LogError("[Capture] M0SimulationLoop 이 없다 — 촬영 불가, 세션 종료");
                enabled = false;
                OnSessionComplete?.Invoke();
                return;
            }
            _log = new CutLog(SessionDir);

            _cam = Camera.main;
            if (_cam != null)
            {
                // 재사용 라이브러리(CameraController)는 수정 금지 — 컴포넌트만 세션 동안 끈다.
                // 무인 세션이라 입력 주인이 없고, 켠 채 두면 관전 이동과 서로 덮어쓴다.
                _playerCamCtrl = _cam.GetComponent("CameraController") as Behaviour;
                if (_playerCamCtrl != null) _playerCamCtrl.enabled = false;
                _target = _cam.transform.position;
            }
            _input = FindAnyObjectByType<PlayerInputController>();

            Subscribe();
            _nextBrollAt = Time.unscaledTime + 3f;
            Debug.Log($"[Capture] 세션 시작 — {PlannedMinutes:F0}분 · 순항 ×{CruiseSpeed:0.#} · {SessionDir}");
        }

        private void OnDestroy()
        {
            if (_playerCamCtrl != null) _playerCamCtrl.enabled = true;
            if (_input != null && _curSpeed != 1f) _input.RequestSpeed(1f);   // 배속 원복
        }

        private void Subscribe()
        {
            // 사망 — Chronicle 에 없는 유일한 대형 사건 (SimulationLoop 가산 훅)
            _sim.OnAgentDied += (id, name, x, y) =>
                Mark("death", 100, new Vector3(x, y, 0f), $"{name}({id})");

            // 개인사 — Chronicle 훅. 서비스 이벤트가 따로 있는 종류(완공·격퇴·사냥·입주)는
            // 여기서 받지 않는다 — 두 번 기록된다 (PriorityOfChronicle 이 -1 로 거른다).
            _sim.Chronicle.OnEventRecorded += ev =>
            {
                int pri = PriorityOfChronicle(ev.Kind);
                if (pri < 0) return;
                Mark("chronicle-" + ev.Kind, pri, AgentPos(ev.SubjectId),
                     $"{ev.SubjectId}" + (string.IsNullOrEmpty(ev.OtherId) ? "" : $" ↔ {ev.OtherId}"));
            };

            var th = _sim.Threats;
            if (th != null)
            {
                th.OnForecast += t => Mark("forecast", 60, VillageCenter(), t.name);
                th.OnStruck += (t, isFarm, n, tile, victims) =>
                    Mark(isFarm ? "strike-farm" : "strike-villager", 80,
                         new Vector3(tile.x, tile.y, 0f), $"{t.name} ×{n}");
                th.OnStructureStruck += (t, slot, tile, before, after) =>
                    Mark("siege-hit", 80, new Vector3(tile.x, tile.y, 0f),
                         $"{t.name} {slot} {before:F0}→{after:F0}");
                th.OnStructureDestroyed += (t, slot, tile) =>
                    Mark("siege-break", 90, new Vector3(tile.x, tile.y, 0f), $"{t.name} {slot}");
                th.OnRaidRepelled += (t, n, ids) => Mark("repelled", 50, VillageCenter(), $"{t.name} ×{n}");
            }
            if (_sim.Combat != null)
                _sim.Combat.OnHunted += (t, atkId, drop, day) =>
                    Mark("hunted", 50, AgentPos(atkId), $"{t.name} → {atkId} (고기 {drop})");
            if (_sim.Construction != null)
                _sim.Construction.OnCompleted += (b, x, y, builderId) =>
                    Mark("built", 40, new Vector3(x, y, 0f), $"{b.name} — {builderId}");
            if (_sim.Ownership != null)
                _sim.Ownership.OnAssigned += (tile, slot, ownerId) =>
                    Mark("gothome", 40, new Vector3(tile.x, tile.y, 0f), ownerId);
            if (_sim.Disaster != null)
                _sim.Disaster.OnStruck += (d, n) => Mark("disaster", 85, VillageCenter(), $"{d.name} ×{n}");
            if (_sim.Season != null)
                _sim.Season.OnSeasonChanged += s => Mark("season", 30, VillageCenter(), s.name);
            if (_sim.Wanderers != null)
                _sim.Wanderers.OnOffered += (name, tile) =>
                    Mark("wanderer", 70, new Vector3(tile.x, tile.y, 0f), name);
        }

        /// <summary>Chronicle 개인사의 우선순위. 서비스 이벤트로 이미 받는 종류는 -1(중복 방지).</summary>
        private static int PriorityOfChronicle(EventId kind) => kind switch
        {
            EventId.Injured => 35,
            EventId.Healed => 30,
            EventId.OrderRefused => 30,
            EventId.OrderTaken => 20,
            EventId.Traded => 25,
            EventId.FoodShared => 30,
            _ => -1,   // GotHome·Built·Repelled·Hunted — 서비스 이벤트 쪽이 정본
        };

        private void Mark(string kind, int priority, Vector3 pos, string detail)
        {
            if (_done) return;
            float recSec = (Time.frameCount - RecordStartFrame) / 30f;
            bool attend = priority >= _curPriority || Time.unscaledTime >= _holdUntil;
            if (attend)
            {
                _target = new Vector3(pos.x, pos.y, _cam != null ? _cam.transform.position.z : -10f);
                _curPriority = priority;
                _holdUntil = Time.unscaledTime + HOLD_SEC;
                // 사건은 1× 화면으로 찍는다 (사용자 제안 2026-08-09) — 배속 화면은 주민이
                // 우스꽝스럽게 빨라 클립으로 못 쓴다. B-roll(10)은 순항 그대로 둔다.
                if (priority >= 30) _slowUntil = Time.unscaledTime + HOLD_SEC;
            }
            _log.Add(new CutLog.Cut
            {
                RecSec = recSec, Kind = kind, Detail = detail, Pos = pos, Attended = attend,
                CamDist = _cam != null ? Vector2.Distance(_cam.transform.position, pos) : -1f,
                Speed = Time.timeScale,
            });
        }

        private void Update()
        {
            if (_done) return;

            // 계획 시간 도달 — 영상 시간(프레임/30) 기준. CapFPS 라 벽시계보다 이 값이 정본.
            if ((Time.frameCount - RecordStartFrame) / 30f >= PlannedMinutes * 60f)
            {
                _done = true;
                Debug.Log($"[Capture] 세션 종료 — 컷 {_log.Count}건");
                OnSessionComplete?.Invoke();
                return;
            }

            // 사건이 조용하면 B-roll — 마을의 일상을 기록만 남기고 따라간다
            if (Time.unscaledTime >= _holdUntil && Time.unscaledTime >= _nextBrollAt)
            {
                _nextBrollAt = Time.unscaledTime + BROLL_EVERY;
                Vector3 p = RandomAlivePos();
                Mark("broll", 10, p, "일상");
            }

            /* ── 순항 배속 (사용자 제안 2026-08-09) ─────
               사건 촬영 구간(_slowUntil)과 방랑자 제안 대기 중에는 1× — 앞엣것은 클립
               화질(자연 속도), 뒤엣것은 게임의 개입 설계(프롬프트 1× 강제)를 존중한다.
               그 밖에는 CruiseSpeed 로 감아 수확 밀도를 올린다. 요청은 값이 바뀔 때만
               (RequestSpeed 는 HUD 알림을 띄우므로 매 프레임 부르면 알림이 도배된다). */
            if (_input != null)
            {
                bool slow = Time.unscaledTime < _slowUntil
                         || _sim.Wanderers?.HasPendingOffer == true;
                float want = slow ? 1f : CruiseSpeed;
                if (!Mathf.Approximately(want, _curSpeed))
                {
                    _input.RequestSpeed(want);
                    _curSpeed = want;
                }
            }
        }

        private void LateUpdate()
        {
            if (_cam == null || _done) return;
            // 지수 감쇠 추적 — 프레임률 무관 동일 감속 (CapFPS 30 고정이지만 습관대로)
            _cam.transform.position = Vector3.Lerp(
                _cam.transform.position, _target, 1f - Mathf.Exp(-CAM_LERP * Time.deltaTime));
        }

        private Vector3 AgentPos(string agentId)
        {
            var agents = _sim.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (a != null && a.AgentId == agentId) return a.transform.position;
            }
            return _target; // 못 찾으면 현 관심사 유지 (기록은 남는다)
        }

        private Vector3 VillageCenter()
        {
            var agents = _sim.Agents;
            Vector3 sum = Vector3.zero; int n = 0;
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (a == null || a.State == AgentState.Dead) continue;
                sum += a.transform.position; n++;
            }
            return n > 0 ? sum / n : _target;
        }

        private Vector3 RandomAlivePos()
        {
            var agents = _sim.Agents;
            int alive = 0;
            for (int i = 0; i < agents.Count; i++)
                if (agents[i] != null && agents[i].State != AgentState.Dead) alive++;
            if (alive == 0) return VillageCenter();
            int pick = (Time.frameCount / 7) % alive;   // 세션 안에서 결정적 순환 (Random 미사용)
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (a == null || a.State == AgentState.Dead) continue;
                if (pick-- == 0) return a.transform.position;
            }
            return VillageCenter();
        }
    }
}
