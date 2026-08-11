using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 건물 마커 프레임 재생 (표현 전용) — BuildingSO.MarkerFrames/AltFrames를 순환시킨다.
    /// AgentAnimator의 건물판이다: Animator Controller 없이 에셋 데이터만으로 돈다.
    /// 어떤 그림이 얼마나 빨리 도는지는 **에셋이 정한다** (ADR-M23-1 정신 — 코드 매핑 금지).
    ///
    /// **평상/대체 두 벌만** 안다. 무엇이 대체 상태인지는 이 컴포넌트가 모른다 —
    /// 뷰가 정한다 (CampfireCookView = 조리 중, GateLockView = 잠김). 여기는 SetAlt로 받은
    /// 상태만 그린다. 그래서 새 상태 표현이 늘어도 이 파일은 안 는다. 시뮬 쓰기 0.
    /// </summary>
    public sealed class BuildingFlipbook : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Sprite[] _idle;
        private Sprite[] _alt;
        private float _fps = 6f;
        private float _clock;
        private bool _altNow;

        /// <summary>스폰 직후 1회 배선. idle이 비면 아무것도 하지 않는다 (중립).</summary>
        public void Setup(SpriteRenderer sr, Sprite[] idle, Sprite[] alt, float fps)
        {
            _sr = sr;
            _idle = idle;
            _alt = alt;
            _fps = Mathf.Max(0.1f, fps);
            // 평상·대체 어느 쪽이라도 프레임이 있으면 돈다 — idle만 보고 끄면 상태 전환이 죽는다
            enabled = _sr != null
                      && ((_idle != null && _idle.Length > 0) || (_alt != null && _alt.Length > 0));
        }

        /// <summary>대체 상태 전환. AltFrames 미배선이면 무시 — 평상 그림 유지 (중립).</summary>
        public void SetAlt(bool on)
        {
            bool want = on && _alt != null && _alt.Length > 0;
            if (want == _altNow) return;
            _altNow = want;
            _clock = 0f; // 갈아입을 때 첫 프레임부터 — 솥이 중간 프레임에서 튀어나오지 않게
        }

        private Sprite[] Frames
        {
            get
            {
                Sprite[] f = _altNow ? _alt : _idle;
                return f != null && f.Length > 0 ? f : null;
            }
        }

        private void Update()
        {
            Sprite[] frames = Frames;
            if (_sr == null || frames == null) return;
            _clock += Time.deltaTime * _fps;
            _sr.sprite = frames[(int)_clock % frames.Length];
        }
    }
}
