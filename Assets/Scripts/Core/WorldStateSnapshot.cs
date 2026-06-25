/// <summary>
/// WorldStateSnapshot.cs - GOAP 플래너용 읽기 전용 월드 스테이트 스냅샷
///
/// 역할(Role): AuthoritativeWorldState의 현재 값을 NativeArray<int>로 직렬화한 복사본.
///             C# Job System의 Burst Compiler가 처리할 수 있도록 Blittable 구조로 변환한다.
///             스냅샷은 플래닝 시작 시 한 번 생성되며, 플래닝 완료 후 반드시 Dispose() 해야 한다.
/// 사용법(Usage):
///   1. 메인 스레드에서: var snapshot = WorldStateSnapshot.CreateFrom(AuthoritativeWorldState.Instance);
///   2. Job에 snapshot.Data를 [ReadOnly] 파라미터로 전달
///   3. Job 완료(Complete()) 후: snapshot.Dispose();
/// 의존성(Dependencies): AuthoritativeWorldState.cs, WorldStateIndices.cs
///
/// float 직렬화 방식:
///   float을 손실 없이 int에 저장하기 위해 IEEE 754 비트 재해석을 사용한다.
///   저장: BitConverter.SingleToInt32Bits(floatValue)
///   읽기: BitConverter.Int32BitsToSingle(intValue)
///
/// 메모리 관리:
///   NativeArray는 관리되지 않는 메모리(unmanaged memory)를 사용하므로
///   반드시 Dispose()를 호출해야 메모리 누수가 발생하지 않는다.
///   using 구문 사용을 권장한다: using var snapshot = WorldStateSnapshot.CreateFrom(state);
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

using System;
using Unity.Collections;
using UnityEngine;
using AIVillage.Core;

namespace AIVillage.Core
{
    /// <summary>
    /// GOAP Job System에 전달되는 읽기 전용 월드 스테이트 스냅샷.
    /// IDisposable을 구현하므로 using 구문으로 자동 해제하거나 수동으로 Dispose()를 호출한다.
    /// </summary>
    public class WorldStateSnapshot : IDisposable
    {
        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private bool _isDisposed = false;

        // ── 공개 데이터 ───────────────────────────────────────────────────────

        /// <summary>
        /// 직렬화된 월드 스테이트 데이터.
        /// 인덱스는 WorldStateIndices 상수를 사용한다.
        /// float 값은 BitConverter.Int32BitsToSingle()로 역직렬화한다.
        /// bool 값은 0(false) / 1(true)로 저장되어 있다.
        /// </summary>
        public NativeArray<int> Data { get; private set; }

        // ── 생성자 (private: CreateFrom 팩토리 메서드를 통해서만 생성) ─────────

        private WorldStateSnapshot() { }

        // ── 팩토리 메서드 ─────────────────────────────────────────────────────

        // [PR Fix]: CreateFrom() XML 주석에 TempJob 4프레임 수명 제약 및 Dispose 필수 경고를 문서화 (F-003)
        /// <summary>
        /// AuthoritativeWorldState의 현재 값을 NativeArray에 복사하여 스냅샷을 생성한다.
        /// 메인 스레드에서만 호출해야 한다 (AuthoritativeWorldState 접근이 메인 스레드 전용이므로).
        ///
        /// [TempJob 제약]
        ///   - 이 메서드는 Allocator.TempJob으로 NativeArray를 할당한다.
        ///   - TempJob 할당은 반드시 4프레임 이내에 해제해야 한다. 초과 시 Unity가 오류를 발생시킨다.
        ///   - Job 완료(JobHandle.Complete()) 직후 즉시 Dispose()를 호출해야 한다.
        ///   - 클래스 필드나 정적 변수에 장기 보관 금지. using 구문 또는 try-finally 패턴을 권장한다.
        ///   - 예시: using var snapshot = WorldStateSnapshot.CreateFrom(state);
        /// </summary>
        /// <param name="state">현재 실행 중인 월드 스테이트. null이면 빈 스냅샷 반환.</param>
        /// <returns>Allocator.TempJob으로 할당된 스냅샷. Job 완료 후 4프레임 이내 Dispose() 필수.</returns>
        public static WorldStateSnapshot CreateFrom(AuthoritativeWorldState state)
        {
            if (state == null)
            {
                Debug.LogError("[WorldStateSnapshot] CreateFrom: state가 null입니다. 빈 스냅샷을 반환합니다.");
                var empty = new WorldStateSnapshot();
                empty.Data = new NativeArray<int>(WorldStateIndices.TOTAL_COUNT, Allocator.TempJob);
                return empty;
            }

            var snapshot = new WorldStateSnapshot();
            // Allocator.TempJob: Job System에 전달하기 위한 임시 할당.
            // 4프레임 이내에 Dispose하지 않으면 Unity가 오류를 발생시킨다.
            snapshot.Data = new NativeArray<int>(WorldStateIndices.TOTAL_COUNT, Allocator.TempJob);

            var data = snapshot.Data;

            // ── 자원 재고 직렬화 (float → int 비트 재해석) ──────────────────
            // IEEE 754 비트 재해석: float의 정밀도를 손실 없이 int에 보존한다.
            data[WorldStateIndices.RawFoodStock]    = BitConverter.SingleToInt32Bits(state.RawFoodStock);
            data[WorldStateIndices.CookedFoodStock] = BitConverter.SingleToInt32Bits(state.CookedFoodStock);
            data[WorldStateIndices.WoodStock]       = BitConverter.SingleToInt32Bits(state.WoodStock);
            data[WorldStateIndices.StoneStock]      = BitConverter.SingleToInt32Bits(state.StoneStock);
            data[WorldStateIndices.IronStock]       = BitConverter.SingleToInt32Bits(state.IronStock);
            data[WorldStateIndices.CopperStock]     = BitConverter.SingleToInt32Bits(state.CopperStock);
            data[WorldStateIndices.SilverStock]     = BitConverter.SingleToInt32Bits(state.SilverStock);

            // ── 예약량 직렬화 ────────────────────────────────────────────────
            data[WorldStateIndices.RawFoodReserved]    = BitConverter.SingleToInt32Bits(state.RawFoodReserved);
            data[WorldStateIndices.CookedFoodReserved] = BitConverter.SingleToInt32Bits(state.CookedFoodReserved);
            data[WorldStateIndices.WoodReserved]       = BitConverter.SingleToInt32Bits(state.WoodReserved);
            data[WorldStateIndices.StoneReserved]      = BitConverter.SingleToInt32Bits(state.StoneReserved);
            data[WorldStateIndices.IronReserved]       = BitConverter.SingleToInt32Bits(state.IronReserved);
            data[WorldStateIndices.CopperReserved]     = BitConverter.SingleToInt32Bits(state.CopperReserved);
            data[WorldStateIndices.SilverReserved]     = BitConverter.SingleToInt32Bits(state.SilverReserved);

            // ── 월드 플래그 직렬화 (bool → 0/1) ─────────────────────────────
            data[WorldStateIndices.EnemyNearby]     = state.EnemyNearby     ? 1 : 0;
            data[WorldStateIndices.BuildingQueued]  = state.BuildingQueued  ? 1 : 0;
            data[WorldStateIndices.TownHallBuilt]   = state.TownHallBuilt   ? 1 : 0;
            data[WorldStateIndices.ForgeBuilt]      = state.ForgeBuilt      ? 1 : 0;
            data[WorldStateIndices.StorehouseBuilt] = state.StorehouseBuilt ? 1 : 0;

            // ── 게임 시간 직렬화 (int 직접 저장) ─────────────────────────────
            data[WorldStateIndices.GameDay] = state.GameDay;
            data[WorldStateIndices.Season]  = (int)state.Season;

            return snapshot;
        }

        // ── 편의 읽기 메서드 ──────────────────────────────────────────────────

        /// <summary>
        /// 특정 자원의 가용량(stock - reserved)을 float으로 반환한다.
        /// Job 외부(메인 스레드)에서 스냅샷 기반 판단이 필요할 때 사용한다.
        /// </summary>
        public float GetAvailableStock(ResourceType type)
        {
            // [PR Fix]: Dispose 상태 외에 Data.IsCreated도 함께 확인하여 상태 불일치 방지 (F-006)
            if (_isDisposed || !Data.IsCreated)
            {
                Debug.LogError("[WorldStateSnapshot] GetAvailableStock: 이미 Dispose된 스냅샷에 접근했습니다.");
                return 0f;
            }

            int stockIdx    = WorldStateIndices.StockIndexOf(type);
            int reservedIdx = WorldStateIndices.ReservedIndexOf(type);

            float stock    = BitConverter.Int32BitsToSingle(Data[stockIdx]);
            float reserved = BitConverter.Int32BitsToSingle(Data[reservedIdx]);

            // 예약량이 재고를 초과하는 경우는 무결성 오류이지만 음수를 반환하지 않도록 보호
            return Mathf.Max(0f, stock - reserved);
        }

        /// <summary>
        /// 월드 플래그(bool)를 반환한다. 인덱스는 WorldStateIndices의 플래그 상수를 사용한다.
        /// </summary>
        public bool GetFlag(int flagIndex)
        {
            // [PR Fix]: Dispose 상태 외에 Data.IsCreated도 함께 확인하여 상태 불일치 방지 (F-006)
            if (_isDisposed || !Data.IsCreated)
            {
                Debug.LogError("[WorldStateSnapshot] GetFlag: 이미 Dispose된 스냅샷에 접근했습니다.");
                return false;
            }

            return Data[flagIndex] == 1;
        }

        // ── IDisposable 구현 ──────────────────────────────────────────────────

        /// <summary>
        /// NativeArray 메모리를 해제한다. Job.Complete() 호출 이후 반드시 호출해야 한다.
        /// using 구문을 사용하면 자동으로 호출된다.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            if (Data.IsCreated)
            {
                Data.Dispose();
            }

            _isDisposed = true;
        }
    }
}
