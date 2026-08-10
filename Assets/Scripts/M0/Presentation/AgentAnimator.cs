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
        private AnimKind _lastActing = AnimKind.None; // 몸짓 전환 감지 — 휘두름은 첫 프레임부터 보여야 한다
        private bool _horizontal;                     // 지금 보고 있는 축 (true = 측면). 기본 = 정면

        /// <summary>축을 바꾸려면 새 축이 이만큼 우세해야 한다 (2026-08-10).
        ///
        /// 🔴 없을 때 무슨 일이 났나: 경로가 8방향(JPS)이라 대각선 구간이 흔한데, 45°에서는
        /// |dx| 와 |dy| 가 명목상 같다. 그래서 `|dx| > |dy|` 한 줄이 **부동소수점 잡음으로 매
        /// 프레임 뒤집혔고**, 측면 그림과 정면 그림이 초당 수십 번 교대했다 — 사용자가 오래
        /// 거슬려 하던 "주민·적 부들부들"의 정체다.
        ///
        /// 🔑 고칠 자리가 여기인 이유: 축을 고르는 지점이 이 함수 하나라, 주민이든 위협이든
        /// 한 번 고치면 같이 낫는다. 부르는 쪽(러너·개체)에서 방향을 다듬으면 통로가 갈린다.
        /// 1.2 = 대각선(비 1.0)은 안 바꾸고, 3:4 정도로 기울면(1.33) 바꾼다.</summary>
        private const float AXIS_SWITCH_MARGIN = 1.2f;

        /// <summary>다음 프레임에 볼 축 (순수 — 게이트 M24-T9). 같으면 **안 바꾼다**가 핵심이다:
        /// 대각선에서 두 성분이 엎치락뒤치락해도 보던 축을 지킨다.</summary>
        public static bool NextAxisHorizontal(bool prevHorizontal, Vector2 dir,
                                              float margin = AXIS_SWITCH_MARGIN)
        {
            float ax = Mathf.Abs(dir.x), ay = Mathf.Abs(dir.y);
            if (ax <= 1e-5f && ay <= 1e-5f) return prevHorizontal; // 정지 — 보던 쪽을 계속 본다
            if (prevHorizontal) return !(ay > ax * margin);
            return ax > ay * margin;
        }

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
            // 몸짓이 바뀌면 시계를 0으로 (2026-08-10 Play 판정의 곁가지): 시계가 이어지면
            // 공격이 중간 프레임 — 이미 칼을 내린 자세 — 에서 시작해 "휘둘렀다"가 안 보인다.
            // 걷기↔대기 전환에는 걸지 않는다 (매 프레임 토글되면 0번 프레임에 굳는다).
            if (acting != _lastActing) { _lastActing = acting; _clock = 0f; }

            // 축은 프레임마다 새로 고르지 않고 **넘어갈 때만** 넘어간다 (AXIS_SWITCH_MARGIN 참조).
            _horizontal = NextAxisHorizontal(_horizontal, dir);

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

        /// <summary>이 몸짓 1회분의 길이(초) = 프레임 수 ÷ FPS. 등록이 없으면 fallback.
        /// 🔑 몸짓을 **몇 초 보여줄지 정하는 쪽이 그림에게 물어보게** 하는 창구다 — 그 수치를
        /// 호출자 코드에 상수로 두면 그림이 바뀌는 순간 반드시 어긋난다 (M23 관통 교훈).</summary>
        public float ActionCycleSec(AnimKind kind, float fallbackSec)
        {
            if (_actions == null || !_actions.TryGetValue(kind, out AgentSpriteSetSO.ActionAnim a))
                return fallbackSec;
            int n = Mathf.Max(a.Down != null ? a.Down.Length : 0,
                    Mathf.Max(a.Side != null ? a.Side.Length : 0,
                              a.Up != null ? a.Up.Length : 0));
            return n > 0 ? n / Mathf.Max(1f, _set.FramesPerSecond) : fallbackSec;
        }

        private Sprite[] PickActionFrames(in AgentSpriteSetSO.ActionAnim a, Vector2 dir, out bool flipX)
        {
            flipX = false;
            if (_horizontal)
            {
                flipX = (dir.x < 0f) == _set.SideFacesRight;
                return a.Side;
            }
            return dir.y > 0f ? a.Up : a.Down;
        }

        private Sprite[] PickFrames(bool walking, Vector2 dir, out bool flipX)
        {
            flipX = false;

            if (_horizontal)
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
