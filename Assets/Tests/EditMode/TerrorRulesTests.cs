using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// テロ（地球教型）を固定する：直接被害は小さく恐怖は増幅される（報じられなければ半減）、
    /// 過剰反応のコストは脅威超過分のみ、弾圧×不満が細胞を生む自己増殖、報復の自滅判定。境界を担保。
    /// </summary>
    public class TerrorRulesTests
    {
        private static readonly TerrorParams P = TerrorParams.Default;
        // 直接0.01/増幅10/過剰反応0.3/急進化0.05/対テロ0.1

        [Test]
        public void DirectDamage_IsSmall()
        {
            Assert.AreEqual(0.01f, TerrorRules.DirectDamage(1f, P), 1e-6f);
            Assert.AreEqual(0.005f, TerrorRules.DirectDamage(0.5f, P), 1e-6f);
        }

        [Test]
        public void FearSpread_AmplifiedByMedia()
        {
            // 最大攻撃×全国報道＝0.01×10×1=0.1（被害の10倍の恐怖）
            Assert.AreEqual(0.1f, TerrorRules.FearSpread(1f, 1f, P), 1e-5f);
            // 報じられなければ恐怖は伝播しない（テロは劇場）
            Assert.AreEqual(0f, TerrorRules.FearSpread(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void OverreactionCost_OnlyExcessCrackdown()
        {
            // 脅威0.2 に弾圧0.8＝超過0.6×0.3=0.18
            Assert.AreEqual(0.18f, TerrorRules.OverreactionCost(0.8f, 0.2f, P), 1e-5f);
            // 脅威に見合った対処はコストなし
            Assert.AreEqual(0f, TerrorRules.OverreactionCost(0.2f, 0.2f, P), 1e-5f);
            Assert.AreEqual(0f, TerrorRules.OverreactionCost(0.1f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void CellsTick_CrackdownBreedsCells()
        {
            // 全力弾圧×高不満・対テロなし＝0.05 増える（弾圧が敵を作る）
            Assert.AreEqual(0.15f, TerrorRules.CellsTick(0.1f, 1f, 1f, 0f, 1f, P), 1e-5f);
            // 精密対テロのみ（弾圧なし）＝0.1×0.1=0.01 減る
            Assert.AreEqual(0.09f, TerrorRules.CellsTick(0.1f, 0f, 0f, 1f, 1f, P), 1e-5f);
            // 下限0・上限1
            Assert.AreEqual(0f, TerrorRules.CellsTick(0.001f, 0f, 0f, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void IsSelfDefeating_TheTerroristsTrap()
        {
            // 過剰弾圧×高不満×対テロ薄＝思う壺
            Assert.IsTrue(TerrorRules.IsSelfDefeating(1f, 0.1f, 1f, 0.1f, P));
            // 脅威に見合う精密対処＝罠にはまらない
            Assert.IsFalse(TerrorRules.IsSelfDefeating(0.1f, 0.1f, 1f, 1f, P));
            // 過剰でも対テロが急進化を上回れば自滅ではない
            Assert.IsFalse(TerrorRules.IsSelfDefeating(0.5f, 0.1f, 0.1f, 1f, P));
        }
    }
}
