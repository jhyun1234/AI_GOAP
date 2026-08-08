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
