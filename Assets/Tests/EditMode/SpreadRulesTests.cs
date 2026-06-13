using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>SpreadRules（スプレッド＝出力−入力・原料高で採算消失・#1111）の純ロジック検証。</summary>
    public class SpreadRulesTests
    {
        private SpreadRules.SpreadParams P => SpreadRules.SpreadParams.Default;

        /// <summary>粗マージン＝出力価値−投入コスト。</summary>
        [Test]
        public void GrossSpread_出力マイナス入力()
        {
            Assert.AreEqual(4f, SpreadRules.GrossSpread(10f, 6f), 1e-4f);
            // 原料高＝入力が出力を超えると粗マージンは負（採算割れ）。
            Assert.AreEqual(-2f, SpreadRules.GrossSpread(6f, 8f), 1e-4f);
        }

        /// <summary>純マージン＝粗マージン−加工費。採算判定は純マージンの正負。</summary>
        [Test]
        public void NetMargin_加工費を引く_採算判定()
        {
            float gross = SpreadRules.GrossSpread(10f, 6f); // 4
            float net = SpreadRules.NetMargin(gross, 1.5f);  // 2.5
            Assert.AreEqual(2.5f, net, 1e-4f);
            Assert.IsTrue(SpreadRules.IsProfitable(net));
            // 加工費が粗マージンを食い尽くせば停止。
            Assert.IsFalse(SpreadRules.IsProfitable(SpreadRules.NetMargin(gross, 5f)));
        }

        /// <summary>操業意欲＝マージンが薄いほど縮小・損益分岐(既定1.0)割れで停止。</summary>
        [Test]
        public void ProductionIncentive_損益分岐割れで停止()
        {
            // breakEven=1.0, slope=0.5。net=3 → (3-1)*0.5=1.0 で飽和。
            Assert.AreEqual(1f, SpreadRules.ProductionIncentive(3f, 1f, P), 1e-4f);
            // net=2 → (2-1)*0.5=0.5＝薄いマージンで操業縮小。
            Assert.AreEqual(0.5f, SpreadRules.ProductionIncentive(2f, 1f, P), 1e-4f);
            // net=1.0（=breakEven）→ 0＝損益分岐ちょうどで止まる。
            Assert.AreEqual(0f, SpreadRules.ProductionIncentive(1f, 1f, P), 1e-4f);
            // 採算割れ（負）→ 0。
            Assert.AreEqual(0f, SpreadRules.ProductionIncentive(-2f, 1f, P), 1e-4f);
        }

        /// <summary>原料高×製品安の挟み撃ちでマージンが消える（#1111 の核）。</summary>
        [Test]
        public void MarginSqueezeTick_原料高と製品安でマージン消失()
        {
            // sensitivity=1.0。原料高+2かつ製品安-2 → pressure=4 → margin 3 - 4*1*1 = -1（採算割れへ沈む）。
            float squeezed = SpreadRules.MarginSqueezeTick(3f, +2f, -2f, 1f, P);
            Assert.AreEqual(-1f, squeezed, 1e-4f);
            Assert.IsFalse(SpreadRules.IsProfitable(squeezed));
        }

        /// <summary>原料高だけ・製品安だけより、両方同時の方が削りが大きい（挟み撃ち）。</summary>
        [Test]
        public void MarginSqueezeTick_両方同時が最も削る()
        {
            float inputOnly = SpreadRules.MarginSqueezeTick(5f, +2f, 0f, 1f, P);   // 5-2=3
            float outputOnly = SpreadRules.MarginSqueezeTick(5f, 0f, -2f, 1f, P);  // 5-2=3
            float both = SpreadRules.MarginSqueezeTick(5f, +2f, -2f, 1f, P);       // 5-4=1
            Assert.AreEqual(3f, inputOnly, 1e-4f);
            Assert.AreEqual(3f, outputOnly, 1e-4f);
            Assert.AreEqual(1f, both, 1e-4f);
            Assert.Less(both, inputOnly);
            Assert.Less(both, outputOnly);
        }

        /// <summary>dt=0 はマージン不変（時間が進まないと圧迫もない）。</summary>
        [Test]
        public void MarginSqueezeTick_dtゼロは不変()
        {
            Assert.AreEqual(3f, SpreadRules.MarginSqueezeTick(3f, +5f, -5f, 0f, P), 1e-4f);
        }

        /// <summary>操業停止点＝可変費（固定費は短期では埋没＝止めても掛かるので閾値に含めない）。</summary>
        [Test]
        public void ShutdownThreshold_可変費が閾値()
        {
            Assert.AreEqual(3f, SpreadRules.ShutdownThreshold(10f, 3f), 1e-4f);
            // 負入力はクランプ。
            Assert.AreEqual(0f, SpreadRules.ShutdownThreshold(10f, -1f), 1e-4f);
        }
    }
}
