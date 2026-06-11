using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 制海権てこ作用（SeaControlLeverageRules・SKUN-4 #1434）の純ロジックを既定Paramsで担保する。
    /// 制宙権の確実さ・攻城ボーナス・補給の保証・敵補給の遮断・上陸の実現性・海陸協調・係争ペナルティ・確立判定。
    /// </summary>
    public class SeaControlLeverageRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>制宙権の確実さ＝優越×(1−係争)。敵艦隊が残ると制宙権は不確実。</summary>
        [Test]
        public void CommandOfSpace_艦隊優越と係争度から確実さを出す()
        {
            // 完全優越・係争なし＝確実な制宙権1.0
            Assert.AreEqual(1f, SeaControlLeverageRules.CommandOfSpace(1f, 0f), Eps);
            // 優越0.8・係争0.5＝0.8×0.5＝0.4（敵艦隊が残ると不確実）
            Assert.AreEqual(0.4f, SeaControlLeverageRules.CommandOfSpace(0.8f, 0.5f), Eps);
            // 係争完全＝制宙権ゼロ
            Assert.AreEqual(0f, SeaControlLeverageRules.CommandOfSpace(1f, 1f), Eps);
        }

        /// <summary>攻城ボーナス＝制宙権で上陸・砲撃が自由になり実効倍率が1.0以上に。</summary>
        [Test]
        public void SiegeBonus_制宙権が攻城を有利にする()
        {
            // 制宙権ゼロ＝ボーナスなし1.0
            Assert.AreEqual(1f, SeaControlLeverageRules.SiegeBonus(0f), Eps);
            // 完全制宙権＝1+0.5＝1.5（既定 siegeBonusMax=0.5）
            Assert.AreEqual(1.5f, SeaControlLeverageRules.SiegeBonus(1f), Eps);
            // 半分＝1.25
            Assert.AreEqual(1.25f, SeaControlLeverageRules.SiegeBonus(0.5f), Eps);
        }

        /// <summary>補給の保証＝制宙権があれば露出した補給線も通る／なければ断たれる。</summary>
        [Test]
        public void SupplyAssurance_制宙権が補給線を保証する()
        {
            // 露出ゼロ＝制宙権に依らず満額保証
            Assert.AreEqual(1f, SeaControlLeverageRules.SupplyAssurance(0f, 0f), Eps);
            // 完全制宙権＝露出していても全保証
            Assert.AreEqual(1f, SeaControlLeverageRules.SupplyAssurance(1f, 1f), Eps);
            // 制宙権ゼロ・全露出＝1−(1×1×0.8)＝0.2（通商破壊で断たれる、既定 weight=0.8）
            Assert.AreEqual(0.2f, SeaControlLeverageRules.SupplyAssurance(0f, 1f), Eps);
        }

        /// <summary>敵補給の遮断＝敵が補給依存なら制宙権で兵糧攻めが効く。</summary>
        [Test]
        public void EnemySupplyInterdiction_制宙権で敵補給を断つ()
        {
            // 完全制宙権・敵が完全依存＝0.9（既定 interdictionMax=0.9）
            Assert.AreEqual(0.9f, SeaControlLeverageRules.EnemySupplyInterdiction(1f, 1f), Eps);
            // 敵が自給自足（依存0）＝制宙権を握っても遮断は効かない
            Assert.AreEqual(0f, SeaControlLeverageRules.EnemySupplyInterdiction(1f, 0f), Eps);
            // 制宙権ゼロ＝遮断できない
            Assert.AreEqual(0f, SeaControlLeverageRules.EnemySupplyInterdiction(0f, 1f), Eps);
        }

        /// <summary>上陸の実現性＝制宙権なき上陸は床まで落ちる（不可能に近い）。</summary>
        [Test]
        public void AmphibiousFeasibility_制宙権が上陸を可能にする()
        {
            // 制宙権ゼロ・抵抗最大＝床0.1（既定 amphibiousFloor=0.1＝制宙権なき上陸は不可能に近い）
            Assert.AreEqual(0.1f, SeaControlLeverageRules.AmphibiousFeasibility(0f, 1f), Eps);
            // 完全制宙権＝抵抗を制圧して上陸可能1.0
            Assert.AreEqual(1f, SeaControlLeverageRules.AmphibiousFeasibility(1f, 1f), Eps);
            // 抵抗ゼロ＝制宙権なしでも上陸可能1.0
            Assert.AreEqual(1f, SeaControlLeverageRules.AmphibiousFeasibility(0f, 0f), Eps);
        }

        /// <summary>海陸協調＝海と陸が揃ってはじめて相乗（積）。どちらか欠けると効かない。</summary>
        [Test]
        public void CombinedArmsSynergy_海陸一体で相乗する()
        {
            Assert.AreEqual(0.5f, SeaControlLeverageRules.CombinedArmsSynergy(1f, 0.5f), Eps);
            // 陸がゼロ＝制宙権だけでは陸上作戦は成らない
            Assert.AreEqual(0f, SeaControlLeverageRules.CombinedArmsSynergy(1f, 0f), Eps);
            Assert.AreEqual(0.49f, SeaControlLeverageRules.CombinedArmsSynergy(0.7f, 0.7f), Eps);
        }

        /// <summary>係争のペナルティ＝制宙権が争われると陸上作戦が不安定になる。</summary>
        [Test]
        public void ContestedSpacePenalty_係争で不安定になる()
        {
            Assert.AreEqual(1f, SeaControlLeverageRules.ContestedSpacePenalty(0f), Eps);
            Assert.AreEqual(0.4f, SeaControlLeverageRules.ContestedSpacePenalty(0.6f), Eps);
            Assert.AreEqual(0f, SeaControlLeverageRules.ContestedSpacePenalty(1f), Eps);
        }

        /// <summary>制宙権確立判定＝既定閾値0.7以上で確立。</summary>
        [Test]
        public void IsSeaControlEstablished_閾値で確立を判定する()
        {
            // 既定閾値0.7
            Assert.IsTrue(SeaControlLeverageRules.IsSeaControlEstablished(0.7f));
            Assert.IsTrue(SeaControlLeverageRules.IsSeaControlEstablished(0.9f));
            Assert.IsFalse(SeaControlLeverageRules.IsSeaControlEstablished(0.6f));
            // 明示閾値版
            Assert.IsTrue(SeaControlLeverageRules.IsSeaControlEstablished(0.5f, 0.5f));
            Assert.IsFalse(SeaControlLeverageRules.IsSeaControlEstablished(0.49f, 0.5f));
        }
    }
}
