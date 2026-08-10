using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 벌목 나뭇잎 한 장 (2026-08-10) — 떨어지며 흔들리다 사라진다.
    ///
    /// 🔑 왜 파티클 시스템이 아니라 스프라이트 한 장인가: 팩이 준 것은 16×16 잎 그림
    /// (Oak/Birch_Leaf_Particle) 한 장이고, 우리가 필요한 것은 "지금 저 나무를 패고 있다"는
    /// 신호 하나다. ParticleSystem 을 들이면 에셋·머티리얼·프리팹이 딸려 오는데, 그 무게는
    /// 신호 하나의 값을 넘는다. 수명이 끝나면 스스로 사라져 관리할 상태도 남지 않는다.
    ///
    /// 판정과 무관한 순수 표현이다 — 자원량·점유·플랜 어느 것도 읽지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FallingLeaf : MonoBehaviour
    {
        private const float LIFE_SEC = 1.3f;
        private const float FALL_SPEED = 0.85f;   // 유닛/초 — 나뭇잎이라 느리게
        private const float SWAY_AMPLITUDE = 0.22f;
        private const float SWAY_SPEED = 3.2f;
        private const float SPIN_DEG = 90f;       // 초당 회전 (뒤집히며 떨어지는 느낌)

        private SpriteRenderer _sr;
        private float _age;
        private float _startX;
        private float _phase;
        private Color _baseColor;

        /// <summary>잎 한 장을 띄운다. sprite 가 없으면 아무것도 만들지 않는다 (중립).</summary>
        public static void Spawn(Transform parent, Vector3 worldPos, Sprite sprite, float scale)
        {
            if (sprite == null) return;
            var go = new GameObject("Leaf");
            go.transform.SetParent(parent, worldPositionStays: true);
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * scale;

            var leaf = go.AddComponent<FallingLeaf>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            leaf._sr = sr;
            leaf._startX = worldPos.x;
            leaf._phase = Random.value * Mathf.PI * 2f;   // 잎마다 다른 흔들림 (같이 흔들리면 기계처럼 보인다)
            leaf._baseColor = sr.color;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= LIFE_SEC) { Destroy(gameObject); return; }

            Vector3 p = transform.position;
            p.y -= FALL_SPEED * Time.deltaTime;
            p.x = _startX + Mathf.Sin(_phase + _age * SWAY_SPEED) * SWAY_AMPLITUDE;
            transform.position = p;
            transform.Rotate(0f, 0f, SPIN_DEG * Time.deltaTime);

            // 깊이는 매 프레임 — 떨어지면서 y 가 변하니 정렬도 따라와야 한다.
            _sr.sortingOrder = WorldSort.Order(p.y, WorldSort.Particle);

            // 마지막 1/3 구간에서 사라진다 (툭 꺼지면 삭제된 것처럼 보인다).
            float t = _age / LIFE_SEC;
            Color c = _baseColor;
            c.a = _baseColor.a * Mathf.Clamp01((1f - t) * 3f);
            _sr.color = c;
        }
    }
}
