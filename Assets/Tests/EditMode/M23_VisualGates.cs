using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M23 비주얼 게이트 (Docs/M23_비주얼_실행명세서.md). W1 = 행동 애니메이션 배선 검산 —
    /// fileID 손배선이 한 자리라도 틀리면 스프라이트가 null로 로드되므로 여기서 잡힌다.
    /// </summary>
    public class M23_VisualGates
    {
        private static AgentSpriteSetSO Sprites()
            => AssetDatabase.LoadAssetAtPath<AgentSpriteSetSO>("Assets/M0Config/VillagerSprites.asset");

        [Test]
        public void M23_T1_ActionAnims_WiredAndComplete()
        {
            // 배포 액션 전수 — Anim != None이면 스프라이트 세트에 그 몸짓 칸이 있고 3방 전부 차 있다
            // (침묵 배선 방지: 몸짓을 선언하고 그림이 없으면 "일하는데 가만히 서 있는" 거짓말이 된다)
            AgentSpriteSetSO set = Sprites();
            Assert.IsNotNull(set, "VillagerSprites 에셋 없음");
            Assert.IsTrue(set.Actions != null && set.Actions.Length > 0, "행동 칸(Actions)이 비어 있다");

            var kinds = new System.Collections.Generic.HashSet<AnimKind>();
            foreach (AgentSpriteSetSO.ActionAnim a in set.Actions)
            {
                kinds.Add(a.Kind);
                Assert.AreNotEqual(AnimKind.None, a.Kind, "None 칸은 의미가 없다 (폴백은 미등록으로 표현)");
                foreach ((string label, Sprite[] frames) in
                         new[] { ("Down", a.Down), ("Side", a.Side), ("Up", a.Up) })
                {
                    Assert.IsTrue(frames != null && frames.Length > 0, $"{a.Kind}/{label}: 프레임 0장");
                    for (int i = 0; i < frames.Length; i++)
                        Assert.IsNotNull(frames[i],
                            $"{a.Kind}/{label}[{i}]: 스프라이트 null — fileID 손배선 오류 (meta internalID 재확인)");
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ActionSO", new[] { "Assets/M0Config/Actions" }))
            {
                var action = AssetDatabase.LoadAssetAtPath<ActionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (action == null || action.Anim == AnimKind.None) continue;
                Assert.IsTrue(kinds.Contains(action.Anim),
                    $"{action.name}: Anim={action.Anim}인데 스프라이트 세트에 칸이 없다");
            }
        }

        [Test]
        public void M23_T2_FenceAutotile_MaskMappingAndWiring()
        {
            // 조각 선택 순수 함수 (ADR-M23-2) — 시트 판독표를 박제 (실수로 표를 바꾸면 red).
            // 인덱스: 0=세로상단 1=가로좌끝 2=가로중간 3=가로우끝 4=세로중간 5=┌ 6=┬ 7=┐
            //         8=세로하단 9=├ 10=┼ 11=┤ 12=단독 13=└ 14=┴ 15=┘
            Assert.AreEqual(12, DefenseFenceView.PieceOf(false, false, false, false), "고립 = 단독 기둥");
            Assert.AreEqual(10, DefenseFenceView.PieceOf(true, true, true, true), "사방 = 십자");
            Assert.AreEqual(2, DefenseFenceView.PieceOf(false, true, false, true), "동서 = 가로 중간");
            Assert.AreEqual(4, DefenseFenceView.PieceOf(true, false, true, false), "남북 = 세로 중간");
            Assert.AreEqual(1, DefenseFenceView.PieceOf(false, true, false, false), "동만 = 가로 왼끝");
            Assert.AreEqual(3, DefenseFenceView.PieceOf(false, false, false, true), "서만 = 가로 오른끝");
            Assert.AreEqual(0, DefenseFenceView.PieceOf(false, false, true, false), "남만 = 세로 위끝");
            Assert.AreEqual(8, DefenseFenceView.PieceOf(true, false, false, false), "북만 = 세로 아래끝");
            Assert.AreEqual(5, DefenseFenceView.PieceOf(false, true, true, false), "동남 = ┌");
            Assert.AreEqual(7, DefenseFenceView.PieceOf(false, false, true, true), "남서 = ┐");
            Assert.AreEqual(13, DefenseFenceView.PieceOf(true, true, false, false), "북동 = └");
            Assert.AreEqual(15, DefenseFenceView.PieceOf(true, false, false, true), "북서 = ┘");
            Assert.AreEqual(6, DefenseFenceView.PieceOf(false, true, true, true), "동남서 = ┬");
            Assert.AreEqual(14, DefenseFenceView.PieceOf(true, true, false, true), "북동서 = ┴");
            Assert.AreEqual(9, DefenseFenceView.PieceOf(true, true, true, false), "북동남 = ├");
            Assert.AreEqual(11, DefenseFenceView.PieceOf(true, false, true, true), "북남서 = ┤");

            // 거울 대칭 (판독 오류 탐지기): E↔W 반전이면 좌우 조각도 짝으로 뒤집힌다
            Assert.AreEqual(
                DefenseFenceView.PieceOf(false, true, false, false) == 1 ? 3 : -1,
                DefenseFenceView.PieceOf(false, false, false, true), "좌우 거울쌍 1↔3");

            // 에셋 배선 — 조각 16장 전부 실 스프라이트 (fileID 손배선 검증), 문 그림 실재
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            var gate = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Gate.asset");
            Assert.IsTrue(fence.TileSprites != null && fence.TileSprites.Length == 16, "울타리 조각 16장");
            for (int i = 0; i < 16; i++)
                Assert.IsNotNull(fence.TileSprites[i], $"울타리 조각[{i}] null — fileID 손배선 오류");
            Assert.IsNotNull(fence.MarkerSprite, "울타리 대표(폴백) 스프라이트");
            Assert.IsNotNull(gate.MarkerSprite, "문 스프라이트 (Fence_Big_Gate 닫힌 문)");
        }

        [Test]
        public void M23_T1b_CoreActions_AnimMapping()
        {
            // 핵심 매핑 박제 (합의 — 명세 §W1. 괭이(Hoe)는 시트에 없어 심기 = 물뿌리개로 정정):
            // 벌목=Chop · 채석=Mine · 수리=Hammer · 심기=Water · 교전=Attack
            foreach ((string name, AnimKind want) in new[]
            {
                ("ChopWood", AnimKind.Chop), ("MineStone", AnimKind.Mine),
                ("RepairDefense", AnimKind.Hammer), ("BuildFence", AnimKind.Hammer),
                ("PlantCrop", AnimKind.Water), ("Action_Fight", AnimKind.Attack),
            })
            {
                var a = AssetDatabase.LoadAssetAtPath<ActionSO>($"Assets/M0Config/Actions/{name}.asset");
                Assert.IsNotNull(a, $"{name} 에셋 없음");
                Assert.AreEqual(want, a.Anim, $"{name}: 몸짓 매핑이 명세와 다르다");
            }
            // 식사·휴식은 시트에 동작이 없다 — None 유지 (중립: 배선 전과 완전 동일)
            var eat = AssetDatabase.LoadAssetAtPath<ActionSO>("Assets/M0Config/Actions/EatRawFood.asset");
            Assert.AreEqual(AnimKind.None, eat.Anim, "식사는 None (시트에 동작 없음 — 스코프 가드)");
        }
    }
}
