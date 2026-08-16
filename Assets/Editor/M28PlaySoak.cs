using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AIVillage.M0;

namespace AIVillage.EditorTools
{
    /// <summary>
    /// Play 실측 대행 (배치 소크 — M27 §7 · M28 §7의 기계 측정분).
    ///
    /// Day 1까지 1배속(하루=600초 스톱워치 실측) → 이후 Time.timeScale 6배속으로 첫 겨울
    /// 끝(Day 13)까지 방치한다. 배속이 유효한 근거: 심 틱 = WaitForSeconds(스케일 시간)
    /// 코루틴이라 게임 전체가 균등히 빨라져 밸런스 비율이 보존된다 (실시간 체감만 압축).
    /// 판정은 사람이 로그로 한다 — [Soak] 줄 + NoSolutionFound/전멸 검색 (run-soak.ps1).
    ///
    /// ⚠️ 체감 항목(겨울이 긴장인가 등)은 이 소크가 대체하지 못한다 — 수지·리듬·오류만 잰다.
    /// </summary>
    public static class M28PlaySoak
    {
        private const string ACTIVE = "M28Soak.Active"; // SessionState — 플레이 진입 도메인 리로드 생존용

        public static void Run()
        {
            SessionState.SetBool(ACTIVE, true);
            EditorSceneManager.OpenScene("Assets/Scenes/M0Scene.unity");
            EditorApplication.isPlaying = true; // 리로드 후 Boot()가 이어받는다
        }

        [InitializeOnLoadMethod]
        private static void Boot()
        {
            if (SessionState.GetBool(ACTIVE, false))
                EditorApplication.update += Tick;
        }

        private static M0SimulationLoop _sim;
        private static double _t0 = -1;
        private static int _lastQ = -1;
        private static bool _fast;
        private static double _day1RealSec = -1;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (_sim == null)
            {
                _sim = Object.FindObjectOfType<M0SimulationLoop>();
                if (_sim == null) return;
                _t0 = EditorApplication.timeSinceStartup;
                Debug.Log("[Soak] 시작 — 1배속으로 Day 1까지 (하루 600초 검증), 이후 6배속");
            }

            double real = EditorApplication.timeSinceStartup - _t0;
            float day = _sim.GameTime;

            int q = (int)(day * 4f); // 0.25일(실시간 150초/배속 25초)마다 한 줄
            if (q != _lastQ)
            {
                _lastQ = q;
                Debug.Log($"[Soak] real={real:F0}s day={day:F2} scale={Time.timeScale} " +
                          $"사망={_sim.DeathCount} 정착={_sim.SettleCount}");
            }

            if (!_fast && day >= 1f)
            {
                _fast = true;
                _day1RealSec = real;
                Debug.Log($"[Soak] Day1 도달 — 실측 {real:F1}초 → 6배속 전환");
            }
            // 배속 유지 — 게임의 배속 창구(PlayerInputController.SetSpeed)에 "자동 실시간 복귀"가
            // 있어 이벤트마다 1배속으로 되돌린다 (1차 소크에서 Day 2.75에 풀림 실측). 창구를
            // 통해 되걸어 준다 (Time.timeScale 직접 쓰기는 그 창구 주석이 금지).
            if (_fast && Time.timeScale != 6f)
            {
                var input = Object.FindObjectOfType<PlayerInputController>();
                if (input != null) input.RequestSpeed(6f);
                else Time.timeScale = 6f; // 창구가 없는 씬이면 직접 (소크 전용 폴백)
            }

            if (day >= 13f || real > 4500)
            {
                Debug.Log($"[Soak] 종료 — day={day:F2} real={real:F0}s day1실측={_day1RealSec:F1}s " +
                          $"사망={_sim.DeathCount} 정착={_sim.SettleCount} " +
                          $"최고인구={_sim.PeakPopulation}");
                SessionState.SetBool(ACTIVE, false);
                EditorApplication.Exit(day >= 13f ? 0 : 3); // 3 = 시간 초과 (Day 13 미달)
            }
        }
    }
}
