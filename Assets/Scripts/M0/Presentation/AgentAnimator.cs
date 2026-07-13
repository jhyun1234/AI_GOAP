using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 코드 주도 스프라이트 애니메이터 — 방향(정면/측면/후면) × 상태(대기/걷기) 프레임 재생.
    /// Animator Controller 에셋 없이 AgentSpriteSetSO 데이터만으로 동작한다.
    /// </summary>
    public sealed class AgentAnimator
    {
        private readonly SpriteRenderer _sr;
        private readonly AgentSpriteSetSO _set;
        private float _clock;

        public AgentAnimator(SpriteRenderer sr, AgentSpriteSetSO set, Color agentTint)
        {
            _sr = sr;
            _set = set;
            _sr.color = Color.Lerp(Color.white, agentTint, set.TintStrength);
        }

        /// <summary>매 프레임 호출. dir은 마지막 이동 방향(월드) — 대기 시에도 방향 유지.</summary>
        public void Tick(float dt, bool walking, Vector2 dir)
        {
            Sprite[] frames = PickFrames(walking, dir, out bool flipX);
            if (frames == null || frames.Length == 0) return;

            _clock += dt * Mathf.Max(1f, _set.FramesPerSecond);
            int idx = (int)_clock % frames.Length;

            _sr.sprite = frames[idx];
            _sr.flipX = flipX;
        }

        private Sprite[] PickFrames(bool walking, Vector2 dir, out bool flipX)
        {
            flipX = false;

            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                flipX = (dir.x < 0f) == _set.SideFacesRight;
                return walking ? _set.WalkSide : _set.IdleSide;
            }
            if (dir.y > 0f)
                return walking ? _set.WalkUp : _set.IdleUp;
            return walking ? _set.WalkDown : _set.IdleDown;
        }
    }
}
