using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>誇示的浪費と正統性（VEBL-3 #1601）の純ロジックを既定Paramsの具体値で固定。</summary>
    public class OstentationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>正統性上昇は規模×聴衆を逓減カーブに通す（既定で spending0.5×聴衆1.0＝約0.0857）。</summary>
        [Test]
        public void LegitimacyGain_既定の具体値()
        {
            // raw=0.5, diminished=0.5/(1+1.5*0.5)=0.285714, ×0.3
            Assert.AreEqual(0.0857143f, OstentationRules.LegitimacyGain(0.5f, 1.0f), Eps);
        }

        /// <summary>規模を倍にしても正統性は倍にならない＝逓減（盛大なほど効くが頭打ち）。</summary>
        [Test]
        public void LegitimacyGain_逓減する()
        {
            float half = OstentationRules.LegitimacyGain(0.5f, 1.0f); // 0.0857
            float full = OstentationRules.LegitimacyGain(1.0f, 1.0f); // 0.12
            Assert.AreEqual(0.12f, full, Eps);
            Assert.Less(full, half * 2f, "規模倍でも効果は倍未満＝逓減");
        }

        /// <summary>聴衆ゼロ（誰も見ない）なら誇示は無意味＝正統性0。</summary>
        [Test]
        public void LegitimacyGain_聴衆ゼロは無効()
        {
            Assert.AreEqual(0f, OstentationRules.LegitimacyGain(1.0f, 0f), Eps);
        }

        /// <summary>財政圧迫は国庫容量を超えた分の二乗で急増し、身の丈内なら0。</summary>
        [Test]
        public void FiscalStrain_身の丈超過で急増()
        {
            // over=0.3, 0.09*2.0=0.18
            Assert.AreEqual(0.18f, OstentationRules.FiscalStrain(0.8f, 0.5f), Eps);
            // 容量内（0.4 ≤ 0.5）は圧迫なし
            Assert.AreEqual(0f, OstentationRules.FiscalStrain(0.4f, 0.5f), Eps);
        }

        /// <summary>純効果＝威信から財政圧迫を引く＝身の丈超過でマイナスへ転じる両刃。</summary>
        [Test]
        public void NetLegitimacy_両刃()
        {
            // gain=0.12, strain=0.18 → -0.06
            Assert.AreEqual(-0.06f, OstentationRules.NetLegitimacy(0.12f, 0.18f), Eps);
            // 圧迫が無ければ丸ごとプラス
            Assert.AreEqual(0.12f, OstentationRules.NetLegitimacy(0.12f, 0f), Eps);
        }

        /// <summary>浪費を重ねるほど一回の効果が薄れる（累計0.75で倍率0.25）。</summary>
        [Test]
        public void DiminishingReturns_慣れ()
        {
            Assert.AreEqual(0.25f, OstentationRules.DiminishingReturns(0.75f), Eps);
            Assert.AreEqual(1.0f, OstentationRules.DiminishingReturns(0f), Eps);
        }

        /// <summary>長期崩壊リスクは蓄積×財政の弱さ×時間で育つが、健全な国庫なら育たない。</summary>
        [Test]
        public void LongTermCollapseRisk_健全なら育たない()
        {
            // accum=2.0, weakness=1.0, rate0.05, dt1.0 → 0.1
            Assert.AreEqual(0.1f, OstentationRules.LongTermCollapseRisk(2.0f, 0f, 1.0f), Eps);
            // 国庫健全（fiscalHealth=1）なら蓄積しても0
            Assert.AreEqual(0f, OstentationRules.LongTermCollapseRisk(2.0f, 1.0f, 1.0f), Eps);
        }

        /// <summary>適正浪費は身の丈いっぱい（聴衆ぶん）＝聴衆ゼロなら0、破滅的奢侈は容量1.5倍超で成立。</summary>
        [Test]
        public void OptimalSpending_と_破滅的奢侈()
        {
            Assert.AreEqual(0.6f, OstentationRules.OptimalSpending(0.6f, 1.0f), Eps);
            Assert.AreEqual(0f, OstentationRules.OptimalSpending(0.6f, 0f), Eps);
            // 容量0.5×1.5=0.75。0.9>0.75=破滅、0.6<0.75=健全
            Assert.IsTrue(OstentationRules.IsRuinousLuxury(0.9f, 0.5f));
            Assert.IsFalse(OstentationRules.IsRuinousLuxury(0.6f, 0.5f));
        }
    }
}
