using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace AIVillage.M0.Capture.Editor
{
    /// <summary>
    /// 무인 촬영 세션 러너 (롱폼 트랙 W3, ADR-LF-4) — Editor 전용.
    ///
    /// 두 입구:
    ///   메뉴  AI GOAP → 촬영 세션 (사람이 시험할 때)
    ///   CLI   Unity.exe -projectPath . -executeMethod
    ///         AIVillage.M0.Capture.Editor.CaptureSessionRunner.RunCli [-captureMinutes 30]
    ///         (tools/scene-video/capture-session.cmd 가 이걸 부른다)
    ///   ⚠️ -batchmode 금지 — 그래픽스 없이는 GameView 캡처가 안 된다. 창이 뜬 채 돈다.
    ///
    /// Play 진입은 도메인 리로드를 지나므로 정적 상태가 다 날아간다 —
    /// 명령은 SessionState(리로드 생존·에디터 종료 소멸)에 적고,
    /// InitializeOnLoadMethod 가 리로드 뒤 다시 배선한다.
    ///
    /// 순서: Begin() → SessionState 기록 → Play 진입 → [도메인 리로드] →
    /// EnteredPlayMode: Recorder 시작(1920×1080·30fps·CapFPS) + CaptureDirector 생성 →
    /// 계획 시간 도달(Director 통지) → 녹화 정지 → Play 종료 → (CLI 면) 에디터 종료.
    /// </summary>
    public static class CaptureSessionRunner
    {
        private const string KeyPending = "aigoap.capture.pending";
        private const string KeyMinutes = "aigoap.capture.minutes";
        private const string KeyExit = "aigoap.capture.exitAfter";
        private const string KeyDir = "aigoap.capture.sessionDir";
        private const string ClipsRoot = @"D:\AI_GOAP-videos\clips";

        private static RecorderController _controller;

        [MenuItem("AI GOAP/촬영 세션 시작 (30분)")]
        public static void Menu30() => Begin(30f, exitAfter: false);

        [MenuItem("AI GOAP/촬영 세션 시작 (3분 · 연기 시험)")]
        public static void Menu3() => Begin(3f, exitAfter: false);

        /// <summary>capture-session.cmd 입구. -captureMinutes N (기본 30).</summary>
        public static void RunCli()
        {
            float minutes = 30f;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureMinutes" && float.TryParse(args[i + 1], out float m))
                    minutes = m;
            Begin(minutes, exitAfter: true);
        }

        private static void Begin(float minutes, bool exitAfter)
        {
            // CLI 로 새로 뜬 에디터는 빈 Untitled 씬에서 시작한다 — 게임 씬을 먼저 연다.
            // (실측 2026-08-09: 이 줄이 없으면 빈 씬을 3분 녹화하고, 시뮬이 없어
            //  종료 통지도 안 와 세션이 영원히 안 끝난다.)
            const string ScenePath = "Assets/Scenes/M0Scene.unity";
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);

            string dir = Path.Combine(ClipsRoot, DateTime.Now.ToString("yyMMdd-HHmm"));
            SessionState.SetBool(KeyPending, true);
            SessionState.SetFloat(KeyMinutes, minutes);
            SessionState.SetBool(KeyExit, exitAfter);
            SessionState.SetString(KeyDir, dir);
            Debug.Log($"[Capture] 촬영 예약 — {minutes:F0}분 · {dir}");
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Wire()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(KeyPending, false)) return;
            SessionState.SetBool(KeyPending, false);

            string dir = SessionState.GetString(KeyDir, Path.Combine(ClipsRoot, "unnamed"));
            float minutes = SessionState.GetFloat(KeyMinutes, 30f);
            Directory.CreateDirectory(dir);

            StartRecorder(dir);

            var go = new GameObject("CaptureDirector");
            var director = go.AddComponent<CaptureDirector>();
            director.SessionDir = dir;
            director.PlannedMinutes = minutes;
            director.RecordStartFrame = Time.frameCount;
            director.OnSessionComplete += EndSession;
        }

        private static void StartRecorder(string dir)
        {
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "raw";
            movie.Enabled = true;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = 1920,
                OutputHeight = 1080,
            };
            // 확장자는 Recorder 가 붙인다 → raw.mp4
            movie.OutputFile = Path.Combine(dir, "raw").Replace('\\', '/');
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            // CapFPS — 영상 시간축을 프레임 수로 못박는다. CutLog.recSec 이 이 전제를 쓴다.
            settings.FrameRate = 30f;
            settings.CapFrameRate = true;

            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            if (!_controller.StartRecording())
                Debug.LogError("[Capture] Recorder 시작 실패 — raw.mp4 가 없을 것이다");
            else
                Debug.Log("[Capture] 녹화 시작 (1920×1080 · 30fps · CapFPS)");
        }

        private static void EndSession()
        {
            try
            {
                if (_controller != null && _controller.IsRecording())
                    _controller.StopRecording();
                Debug.Log("[Capture] 녹화 정지");
            }
            finally
            {
                bool exitAfter = SessionState.GetBool(KeyExit, false);
                EditorApplication.isPlaying = false;
                if (exitAfter)
                    // Play 종료가 정리(도메인 리로드 포함)를 마친 다음 틱에 끝낸다
                    EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }
    }
}
