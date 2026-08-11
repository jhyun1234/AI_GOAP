using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방랑자 후보 마커 (M10-E) — 표현 전용 (ADR-M10-9): 주민이 아니다. RegisterAgent·타일 예약·
    /// FoodDaysLeft 전부 불포함 — 수락 순간 SpawnVillager가 진짜 주민으로 교대한다.
    /// 가장자리 → 마을 어귀 걷기 → 도착 통지 → (해소까지 대기) → 퇴장 걷기 → 소멸.
    /// ThreatAgent와 같은 웨이포인트 보간 — 타격이 없어 서비스 결합도 콜백 하나뿐.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WandererMarker : MonoBehaviour
    {
        private const float ARRIVE_EPSILON_SQR = 0.0001f; // 도착 판정 (알고리즘 상수)
        private const float MOVE_SPEED = 1.8f;            // 지친 나그네 걸음 (표현 상수 — 주민 2.0보다 느리게)

        public Vector2Int EntryTile { get; private set; }
        public int TileX => Mathf.RoundToInt(transform.position.x);
        public int TileY => Mathf.RoundToInt(transform.position.y);

        private List<Vector2Int> _path;
        private int _wp;
        private Action _onArrived; // 1회 통지 후 null
        private bool _leaving;

        public void Init(Vector2Int entry, List<Vector2Int> waypoints, Action onArrived)
        {
            EntryTile = entry;
            _path = waypoints; // null = 이미 어귀 (즉시 도착 통지)
            _wp = 0;
            _onArrived = onArrived;

            // 그림 (2026-08-09) — 걸어오는 건 사람이지 점이 아니다. 주민과 **같은 시트·같은
            // 애니메이터**를 쓰되(ThreatAgent가 먼저 낸 길), 연한 초록 색조로 "아직 주민이
            // 아니다"를 남긴다 — 문의 금빛 색조와 같은 문법이다.
            // 시트가 없으면 舊 초록 원 폴백 (중립 불변식).
            AgentSpriteSetSO set = M0SimulationLoop.Instance != null
                ? M0SimulationLoop.Instance.SpriteSet : null;
            if (set != null)
            {
                // 장착의 유일한 지점(SpriteViewRig) 경유 — 舊 좌하단 피벗 보정은 반 칸짜리
                // 어긋남으로 판명(2026-08-10 실측)돼 2026-08-11 이 경로로 이관.
                _sr = SpriteViewRig.Attach(transform, set);
                _anim = new AgentAnimator(_sr, set, Color.white);
                _sr.color = Color.Lerp(Color.white, VisitorTint, 0.3f); // 애니메이터 틴트 위에 덮어쓴다

                // ⚠️ 색조만으로는 부족하다 (2026-08-09 실측): 배경이 초록 들판이라 초록 색조는
                // 눈에 덜 띈다. 이 게임이 "누구인가"를 말하는 방식은 원래 이름표다 —
                // 주민이 이름·직업을 달듯 방문자는 그렇게 달아 준다.
                M0SimulationLoop sim = M0SimulationLoop.Instance;
                if (sim != null && sim.AgentConfig != null)
                    new NameTag(transform, sim.BubbleFont, sim.AgentConfig, "방랑자");
            }
            else
            {
                _sr = gameObject.AddComponent<SpriteRenderer>();
                _sr.sprite = M0Sprites.Circle;
                _sr.color = VisitorTint;
                transform.localScale = Vector3.one * 0.8f;
            }
        }

        // 방문자 색조 — 주민과 한눈에 갈리되 초록 원 시절의 뜻(우호적 손님)을 잇는다
        private static readonly Color VisitorTint = new Color(0.4f, 0.85f, 0.45f, 1f);

        /// <summary>퇴장 (WandererService 전용) — 왔던 길로 걷다 소멸. waypoints null = 즉시 소멸.</summary>
        public void Leave(List<Vector2Int> waypoints)
        {
            _leaving = true;
            _onArrived = null;
            _path = waypoints;
            _wp = 0;
            if (_path == null) Destroy(gameObject);
        }

        private SpriteRenderer _sr;  // 깊이 갱신 대상
        private AgentAnimator _anim; // null = 색 원 폴백
        private Vector2 _facing = Vector2.down; // 멈춰도 마지막으로 보던 쪽을 본다

        private void Update()
        {
            if (_sr != null) _sr.sortingOrder = WorldSort.Order(transform.position.y, WorldSort.Agent);
            bool walking = !(_path == null || _wp >= _path.Count);
            if (_anim != null) _anim.Tick(Time.deltaTime, walking, _facing);

            if (!walking)
            {
                if (_leaving) { Destroy(gameObject); return; }
                if (_onArrived != null) { Action cb = _onArrived; _onArrived = null; cb(); }
                return; // 어귀에서 해소를 기다린다 (제자리)
            }

            var target = new Vector3(_path[_wp].x, _path[_wp].y, 0f); // ADR-M0-9 — X-Y 평면
            Vector3 delta = target - transform.position;
            if (delta.sqrMagnitude > ARRIVE_EPSILON_SQR) _facing = new Vector2(delta.x, delta.y).normalized;
            transform.position = Vector3.MoveTowards(transform.position, target, MOVE_SPEED * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude <= ARRIVE_EPSILON_SQR) _wp++;
        }
    }
}
