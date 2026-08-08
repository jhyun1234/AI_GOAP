using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AIVillage.M0.Capture
{
    /// <summary>
    /// 촬영 컷 리스트 (롱폼 트랙 W3) — 사건이 일어날 때마다 한 줄씩 쌓아
    /// cuts.json 으로 쓴다. 클립 선별(clips.mjs)의 유일한 입력이다.
    ///
    /// recSec = (사건 프레임 − 녹화 시작 프레임) / 30. Recorder 가 CapFPS 로 돌면
    /// 영상 시간축은 벽시계가 아니라 **프레임 수**다 — 렌더가 느려져 벽시계가 늘어도
    /// 영상 안에서의 시각은 이 값이 맞는다 (검증: W3 DoD 표본 3건).
    ///
    /// 사건마다 즉시 파일을 다시 쓴다 — 세션이 도중에 죽어도 그때까지의 컷은 남는다.
    /// 파일이 작아(수십 건 × 한 줄) 통짜 재작성이 append 관리보다 단순하다.
    /// </summary>
    public sealed class CutLog
    {
        public struct Cut
        {
            public float RecSec;     // 녹화 기준 시각 (초)
            public string Kind;      // death · siege-hit · built …
            public string Detail;    // 사람 읽는 한 줄
            public Vector2 Pos;      // 사건 좌표 (월드 = 타일, ADR-M0-9)
            public bool Attended;    // 카메라가 이 사건으로 이동했는가
            public float CamDist;    // 사건 순간 카메라와의 거리 (선별 참고)
            public float Speed;      // 사건 순간 배속 (순항 중이면 그 앞 구간은 빨리 감긴 화면)
        }

        private readonly List<Cut> _cuts = new List<Cut>(64);
        private readonly string _file;

        public CutLog(string sessionDir)
        {
            Directory.CreateDirectory(sessionDir);
            _file = Path.Combine(sessionDir, "cuts.json");
        }

        public int Count => _cuts.Count;

        public void Add(Cut c)
        {
            _cuts.Add(c);
            Flush();
        }

        private void Flush()
        {
            var sb = new StringBuilder(_cuts.Count * 128 + 64);
            sb.Append("{\n  \"fps\": 30,\n  \"cuts\": [\n");
            for (int i = 0; i < _cuts.Count; i++)
            {
                Cut c = _cuts[i];
                sb.Append("    {\"recSec\": ").Append(c.RecSec.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(", \"kind\": \"").Append(Escape(c.Kind))
                  .Append("\", \"detail\": \"").Append(Escape(c.Detail))
                  .Append("\", \"x\": ").Append(c.Pos.x.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(", \"y\": ").Append(c.Pos.y.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(", \"attended\": ").Append(c.Attended ? "true" : "false")
                  .Append(", \"camDist\": ").Append(c.CamDist.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(", \"spd\": ").Append(c.Speed.ToString("F1", CultureInfo.InvariantCulture))
                  .Append('}').Append(i < _cuts.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(_file, sb.ToString());
        }

        private static string Escape(string s)
            => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
