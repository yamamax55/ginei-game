using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>制宙権（宙域の支配権）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class SpaceSuperiorityRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow を経る箇所のみ緩める

        [Test]
        public void 拮抗した哨戒は係争中で支配度ゼロ()
        {
            // 100:100 → share0.5 → signed0 → 0
            Assert.AreEqual(0f, SpaceSuperiorityRules.ControlLevel(100f, 100f), Eps);
        }

        [Test]
        public void 哨戒優勢で支配度がプラスに振れる()
        {
            // 300:100 → share0.75 → signed0.5 → 指数1.0で0.5
            Assert.AreEqual(0.5f, SpaceSuperiorityRules.ControlLevel(300f, 100f), PowEps);
        }

        [Test]
        public void 敵不在で完全制宙_自不在で敵に完全制宙()
        {
            Assert.AreEqual(1f, SpaceSuperiorityRules.ControlLevel(100f, 0f), Eps);
            Assert.AreEqual(-1f, SpaceSuperiorityRules.ControlLevel(0f, 100f), Eps);
        }

        [Test]
        public void 双方不在の真空は係争中ゼロ()
        {
            Assert.AreEqual(0f, SpaceSuperiorityRules.ControlLevel(0f, 0f), Eps);
        }

        [Test]
        public void 支配側のみ移動偵察補給の自由を得て劣勢側はゼロ()
        {
            // controlLevel 0.5 ＝ advantageScale1.0 で 0.5
            Assert.AreEqual(0.5f, SpaceSuperiorityRules.FreedomOfMovement(0.5f), Eps);
            Assert.AreEqual(0.5f, SpaceSuperiorityRules.ReconAdvantage(0.5f), Eps);
            Assert.AreEqual(0.5f, SpaceSuperiorityRules.SupplyProtection(0.5f), Eps);
            Assert.AreEqual(0.5f, SpaceSuperiorityRules.EnemyMovementDenial(0.5f), Eps);
            // 劣勢側（負の支配度）は一切の自由を持たない
            Assert.AreEqual(0f, SpaceSuperiorityRules.FreedomOfMovement(-0.5f), Eps);
            Assert.AreEqual(0f, SpaceSuperiorityRules.SupplyProtection(-0.5f), Eps);
        }

        [Test]
        public void 係争中ほど消耗が大きく完全制宙ではゼロ()
        {
            // 拮抗(0)で最大0.3、半支配(0.5)で0.15、完全制宙(1)で0
            Assert.AreEqual(0.3f, SpaceSuperiorityRules.ContestedZonePenalty(0f), Eps);
            Assert.AreEqual(0.15f, SpaceSuperiorityRules.ContestedZonePenalty(0.5f), Eps);
            Assert.AreEqual(0f, SpaceSuperiorityRules.ContestedZonePenalty(1f), Eps);
            // 符号に依らず大きさで効く＝敵の完全制宙(-1)でも消耗ゼロ
            Assert.AreEqual(0f, SpaceSuperiorityRules.ContestedZonePenalty(-1f), Eps);
        }

        [Test]
        public void 哨戒を引くと支配が中立へ薄れ_維持すれば不変()
        {
            // 引く：decayRate0.5/秒、dt1.0 → 0.5から0へ到達
            Assert.AreEqual(0f, SpaceSuperiorityRules.ControlDecay(0.5f, true, 1.0f), Eps);
            // 引く：dt0.5 → 0.25ぶん減衰して0.25
            Assert.AreEqual(0.25f, SpaceSuperiorityRules.ControlDecay(0.5f, true, 0.5f), Eps);
            // 維持（哨戒残置）：不変
            Assert.AreEqual(0.8f, SpaceSuperiorityRules.ControlDecay(0.8f, false, 1.0f), Eps);
        }

        [Test]
        public void 閾値で制宙権の有無を判定する()
        {
            Assert.IsTrue(SpaceSuperiorityRules.HasSpaceSuperiority(0.5f, 0.3f));
            Assert.IsTrue(SpaceSuperiorityRules.HasSpaceSuperiority(0.3f, 0.3f)); // 閾値ちょうどは握る
            Assert.IsFalse(SpaceSuperiorityRules.HasSpaceSuperiority(0.2f, 0.3f));
        }

        [Test]
        public void 物語_優勢な哨戒で制宙権を握り自由を得るが哨戒を引くと薄れる()
        {
            // 優勢な哨戒（300:100）で宙域を支配する。
            float control = SpaceSuperiorityRules.ControlLevel(300f, 100f);
            Assert.AreEqual(0.5f, control, PowEps);

            // 制宙権を握り（閾値0.3）、移動・偵察・補給の自由を得て、敵の移動を妨げる。
            Assert.IsTrue(SpaceSuperiorityRules.HasSpaceSuperiority(control, 0.3f));
            Assert.Greater(SpaceSuperiorityRules.FreedomOfMovement(control), 0f);
            Assert.Greater(SpaceSuperiorityRules.SupplyProtection(control), 0f);
            Assert.Greater(SpaceSuperiorityRules.EnemyMovementDenial(control), 0f);

            // だが哨戒を前線から引くと、支配は中立へ薄れていく。
            float faded = SpaceSuperiorityRules.ControlDecay(control, true, 0.6f);
            Assert.Less(faded, control);
            // 薄れた結果、もはや制宙権を握れていない（0.5→0.2）。
            Assert.AreEqual(0.2f, faded, Eps);
            Assert.IsFalse(SpaceSuperiorityRules.HasSpaceSuperiority(faded, 0.3f));
            // 補給の安全も目減りする＝守りが薄くなる。
            Assert.Less(SpaceSuperiorityRules.SupplyProtection(faded),
                        SpaceSuperiorityRules.SupplyProtection(control));
        }
    }
}
