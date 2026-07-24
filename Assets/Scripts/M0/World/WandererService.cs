using System;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방랑자 제안의 순수 상태 코어 (M10-E, 게이트 M10-T5) — 열림·해소·시간 초과 판정만.
    /// 이중 해소 방어(TryResolve 1회성)가 스폰 1회 보장의 심장이다 (명세 ⚠️③의 짝).
    /// </summary>
    public struct WandererOffer
    {
        public bool Pending;
        public float DeadlineDay;

        public static WandererOffer Open(float gameTimeDay, float waitDays)
            => new WandererOffer { Pending = true, DeadlineDay = gameTimeDay + waitDays };

        /// <summary>해소 시도 — 첫 호출만 true. 이중 Y·미제안 상태의 Resolve는 전부 false.</summary>
        public bool TryResolve()
        {
            if (!Pending) return false;
            Pending = false;
            return true;
        }

        public bool TimedOut(float gameTimeDay) => Pending && gameTimeDay >= DeadlineDay;
    }

    /// <summary>
    /// 방랑자 (M10-E) — 상실의 회복 축. 주기 도착 → 후보 제시(도착 시 1회 롤 — UI가 보여준
    /// 그 사람이 온다, 명세 ⚠️①) → 플레이어 Y/N 해소 (ADR-M10-7: 판정 입력은 키뿐, 술렁임은
    /// 표현 전용). 후보는 시뮬 외부 (ADR-M10-9: 표현 마커만 — RegisterAgent·FoodDaysLeft 불포함),
    /// 합류는 SimulationLoop.SpawnVillager 문 하나를 지난다.
    /// sim 결합 사유: 인구 문·스폰 풀·표현 창구(HUD·마커·술렁임)가 전부 SimulationLoop 소유 —
    /// 서비스는 그 창구만 호출하고 시뮬 상태는 쓰지 않는다.
    /// 세이브 대상 = _nextArriveDay (ADR-M10-10). 진행 중 후보·마커는 저장 안 함 (재도착).
    /// </summary>
    public sealed class WandererService
    {
        private readonly WorldConfigSO _config;
        private readonly M0SimulationLoop _sim;

        private float _nextArriveDay;   // 다음 도착 시각 (게임일). 세이브 대상
        private int _arriveSerial;      // 진입점 시드용 누적 서수 (ThreatService 패턴)
        private float _lastTickDay;     // Resolve(입력 이벤트)가 다음 도착 예약에 쓰는 현재 시각

        private WandererOffer _offer;
        private WandererMarker _marker;
        private PersonalitySO _candPersonality;
        private JobSO _candJob;

        /// <summary>제안 열림 (프롬프트 문구, 후보 위치) — HUD 프롬프트·술렁임·알림 구독 (표현).</summary>
        public event Action<string, Vector2Int> OnOffered;

        /// <summary>제안 해소 (수락 여부) — HUD 프롬프트 소거·알림 구독 (표현).</summary>
        public event Action<bool> OnResolved;

        /// <summary>Y/N 입력을 받을 상태인가 — PlayerInputController 전용 (⚠️③: 프롬프트 중에만 키 판독).</summary>
        public bool HasPendingOffer => _offer.Pending;

        public WandererService(WorldConfigSO config, M0SimulationLoop sim)
        {
            _config = config;
            _sim = sim;
            _nextArriveDay = config.WandererIntervalDays; // 첫 도착 = 시작 + 주기 (위협 6과 위상차 5)
        }

        public void Tick(float gameTime)
        {
            _lastTickDay = gameTime;

            // 응답 방치 — 자동 퇴장 (결정을 미루는 것도 결정이다)
            if (_offer.TimedOut(gameTime))
            {
                Resolve(false);
                return;
            }

            // 도착 — 진행 중 제안·마커가 없을 때만 (한 번에 한 명)
            if (!_offer.Pending && _marker == null && gameTime >= _nextArriveDay)
                Arrive();
        }

        private void Arrive()
        {
            _arriveSerial++;
            // 후보 확정 — 도착 시 1회 롤 (⚠️①). 스폰 풀 재사용 (M4~M5 파이프라인).
            _candPersonality = _sim.PickRandomPersonality();
            _candJob = _sim.PickRandomJob();

            // 진입점 = 위협과 같은 결정적 가장자리 선택 (순수 함수 재사용)
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            uint seed = StableHash.Fnv1a(_arriveSerial.ToString(), "wanderer");
            Vector2Int entry = ThreatService.EntryPoint(seed, minX, maxX, minY, maxY);

            // 목적지 = 마을 중심(기지) 곁 (M11-K — 공용 모닥불이 사라져 방랑자 초점은 기지다).
            // 통행 가능 타일만 (건물 위 금지)
            var fire = new Vector2Int(_config.BaseTileX, _config.BaseTileY);
            Vector2Int dest = MapBounds.PickWalkableNear(IsWalkable, fire.x, fire.y, 3);

            PathResult path = _sim.Pathfinder.FindPath(entry.x, entry.y, dest.x, dest.y);
            if (path.Kind == PathResultKind.Unreachable)
            {
                Debug.LogWarning($"[Wanderer] 진입 경로 없음 ({entry.x},{entry.y})→({dest.x},{dest.y}) — 이번 도착 생략");
                _nextArriveDay = _lastTickDay + _config.WandererIntervalDays;
                return;
            }

            var go = new GameObject($"Wanderer_{_arriveSerial}");
            go.transform.SetParent(_sim.transform, worldPositionStays: false);
            go.transform.position = new Vector3(entry.x, entry.y, 0f); // ADR-M0-9 — X-Y 평면
            _marker = go.AddComponent<WandererMarker>();
            _marker.Init(entry, path.Kind == PathResultKind.PathFound ? path.Waypoints : null,
                         OpenOffer); // 마을 어귀 도착 순간 제안이 열린다
            Debug.Log($"[Wanderer] 도착 중 — {Describe()} @ ({entry.x},{entry.y})");
        }

        private void OpenOffer()
        {
            _offer = WandererOffer.Open(_lastTickDay, _config.WandererWaitDays);
            string prompt = $"방랑자 도착: {Describe()} — [Y] 수락 / [N] 거절 ({_config.WandererWaitDays:0.#}일 내)";
            Debug.Log($"[Wanderer] 제안 — {Describe()}");
            OnOffered?.Invoke(prompt, new Vector2Int(_marker.TileX, _marker.TileY));
        }

        /// <summary>
        /// 제안 해소 — PlayerInputController(Y/N)와 시간 초과의 유일한 진입. 이중 호출은
        /// WandererOffer.TryResolve가 차단한다 (스폰 1회 보장 — 게이트 M10-T5).
        /// </summary>
        public void Resolve(bool accept)
        {
            if (!_offer.TryResolve()) return;

            if (accept && _marker != null)
            {
                // 인구 문 통과 — 도착 시 롤한 그 후보가 그대로 (⚠️①)
                _sim.SpawnVillager(new Vector2Int(_marker.TileX, _marker.TileY), _candPersonality, _candJob);
                UnityEngine.Object.Destroy(_marker.gameObject); // 마커(표현) → 주민(시뮬) 교대
                Debug.Log($"[Wanderer] 수락 — {Describe()} 합류");
            }
            else
            {
                // 거절·방치·마커 소실 — 왔던 길로 퇴장 (표현)
                _marker?.Leave(ExitPath());
                Debug.Log($"[Wanderer] {(accept ? "수락 불가(마커 소실)" : "거절")} — {Describe()} 떠남");
            }
            _marker = null;
            _candPersonality = null;
            _candJob = null;
            _nextArriveDay = _lastTickDay + _config.WandererIntervalDays;
            OnResolved?.Invoke(accept);
        }

        private System.Collections.Generic.List<Vector2Int> ExitPath()
        {
            if (_marker == null) return null;
            PathResult exit = _sim.Pathfinder.FindPath(_marker.TileX, _marker.TileY,
                                                       _marker.EntryTile.x, _marker.EntryTile.y);
            return exit.Kind == PathResultKind.PathFound ? exit.Waypoints : null; // null = 즉시 소멸
        }

        /// <summary>통행 판정 — VillagerAgent.IsWalkable과 같은 원천(sim.Walkable). 맵 밖 false.</summary>
        private bool IsWalkable(int x, int y)
            => MapBounds.ToArrayIndex(x, y, out int ax, out int ay) && _sim.Walkable[ax, ay];

        private string Describe()
            => $"{(_candPersonality != null ? _candPersonality.DisplayName : "무난한 성격")} · " +
               $"{(_candJob != null ? _candJob.DisplayName : "무직")}";
    }
}
