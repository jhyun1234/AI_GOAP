using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 코드 주도 스프라이트 애니메이터 — 방향(정면/측면/후면) × 상태(대기/걷기/행동) 프레임 재생.
    /// Animator Controller 에셋 없이 AgentSpriteSetSO 데이터만으로 동작한다.
    /// 행동 몸짓(M23-W1)은 acting 인자가 정한다 — 어느 액션이 어떤 몸짓인지는 ActionSO.Anim
    /// 에셋 필드의 몫 (ADR-M23-1). 미등록 Kind·빈 배열은 대기/걷기 폴백 (중립).
    /// </summary>
    public sealed class AgentAnimator
    {
        private readonly SpriteRenderer _sr;
        private readonly AgentSpriteSetSO _set;
        private readonly Dictionary<AnimKind, AgentSpriteSetSO.ActionAnim> _actions; // 생성 시 1회 구축
        private float _clock;

        public AgentAnimator(SpriteRenderer sr, AgentSpriteSetSO set, Color agentTint)
        {
            _sr = sr;
            _set = set;
            _sr.color = Color.Lerp(Color.white, agentTint, set.TintStrength);
            if (set.Actions != null && set.Actions.Length > 0)
            {
                _actions = new Dictionary<AnimKind, AgentSpriteSetSO.ActionAnim>(set.Actions.Length);
                foreach (AgentSpriteSetSO.ActionAnim a in set.Actions)
                    if (a.Kind != AnimKind.None) _actions[a.Kind] = a; // 중복 Kind는 뒤가 이긴다
            }
        }

        /// <summary>매 프레임 호출. dir은 마지막 이동 방향(월드) — 대기 시에도 방향 유지.
        /// acting != None이면 해당 몸짓이 대기를 대체한다 (걷기와 경쟁하지 않는다 — Acting은 제자리).</summary>
        public void Tick(float dt, bool walking, Vector2 dir, AnimKind acting = AnimKind.None)
        {
            Sprite[] frames = null;
            bool flipX = false;
            if (!walking && acting != AnimKind.None && _actions != null
                && _actions.TryGetValue(acting, out AgentSpriteSetSO.ActionAnim anim))
                frames = PickActionFrames(anim, dir, out flipX);
            if (frames == null || frames.Length == 0)
                frames = PickFrames(walking, dir, out flipX);
            if (frames == null || frames.Length == 0) return;

            _clock += dt * Mathf.Max(1f, _set.FramesPerSecond);
            int idx = (int)_clock % frames.Length;

            _sr.sprite = frames[idx];
            _sr.flipX = flipX;
        }

        private Sprite[] PickActionFrames(in AgentSpriteSetSO.ActionAnim a, Vector2 dir, out bool flipX)
        {
            flipX = false;
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                flipX = (dir.x < 0f) == _set.SideFacesRight;
                return a.Side;
            }
            return dir.y > 0f ? a.Up : a.Down;
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
