using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>三位一体の緊張（#1135・クラウゼヴィッツ）の純ロジックテスト。既定 Params で期待値を固定。</summary>
    public class TrinitarianTensionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>均衡＝三者が揃うほど高く偏ると最弱要素が引き下げる。</summary>
        [Test]
        public void TrinityBalance_均衡は揃うほど高く偏ると最弱が効く()
        {
            // 完全均衡＝平均0.8・最弱0.8 → 0.8。
            Assert.AreEqual(0.8f, TrinitarianTensionRules.TrinityBalance(0.8f, 0.8f, 0.8f), Eps);

            // 偏り＝平均(0.9+0.9+0.3)/3=0.7・最弱0.3 → 0.7×0.4＋0.3×0.6=0.28＋0.18=0.46。
            float skewed = TrinitarianTensionRules.TrinityBalance(0.9f, 0.9f, 0.3f);
            Assert.AreEqual(0.46f, skewed, Eps);

            // 同じ平均でも均衡な方が高い（均衡＞偏り）。
            float even = TrinitarianTensionRules.TrinityBalance(0.7f, 0.7f, 0.7f);
            Assert.Greater(even, skewed);
        }

        /// <summary>最弱要素＝破綻の起点。同値は 政府＞軍＞国民 の先勝ち。</summary>
        [Test]
        public void WeakestPillar_最弱要素を返し同値は政府優先()
        {
            Assert.AreEqual(TrinitarianTensionRules.TrinityPillar.国民,
                TrinitarianTensionRules.WeakestPillar(0.9f, 0.9f, 0.3f));
            Assert.AreEqual(TrinitarianTensionRules.TrinityPillar.軍,
                TrinitarianTensionRules.WeakestPillar(0.9f, 0.2f, 0.9f));
            // 全同値 → 政府が先勝ち。
            Assert.AreEqual(TrinitarianTensionRules.TrinityPillar.政府,
                TrinitarianTensionRules.WeakestPillar(0.5f, 0.5f, 0.5f));
        }

        /// <summary>戦争遂行能力＝均衡が下限未満なら0、下限以上で線形に立つ。</summary>
        [Test]
        public void WarSustainability_下限未満は0で以上は線形()
        {
            // 下限0.25未満 → 0。
            Assert.AreEqual(0f, TrinitarianTensionRules.WarSustainability(0.2f), Eps);
            // 均衡1.0 → 満額。
            Assert.AreEqual(1f, TrinitarianTensionRules.WarSustainability(1f), Eps);
            // 均衡0.625 → (0.625-0.25)/0.75 = 0.5。
            Assert.AreEqual(0.5f, TrinitarianTensionRules.WarSustainability(0.625f), Eps);
        }

        /// <summary>政軍の乖離＝意志−軍事力（正=軍不足/負=軍暴走）。</summary>
        [Test]
        public void PoliticalMilitaryGap_意志と軍事力の乖離()
        {
            Assert.AreEqual(0.5f, TrinitarianTensionRules.PoliticalMilitaryGap(0.9f, 0.4f), Eps);   // 意志先行
            Assert.AreEqual(-0.6f, TrinitarianTensionRules.PoliticalMilitaryGap(0.3f, 0.9f), Eps);  // 軍暴走
            Assert.AreEqual(0f, TrinitarianTensionRules.PoliticalMilitaryGap(0.5f, 0.5f), Eps);     // 釣り合い
        }

        /// <summary>民衆の厭戦＝戦死者が支持を削り、損耗0なら不変。</summary>
        [Test]
        public void PopularWarWeariness_戦死者が支持を冷ます()
        {
            // 支持0.8・損耗0.5・dt1 → 0.8-0.5×0.5×1=0.55。
            Assert.AreEqual(0.55f, TrinitarianTensionRules.PopularWarWeariness(0.8f, 0.5f, 1f), Eps);
            // 損耗0 → 不変。
            Assert.AreEqual(0.8f, TrinitarianTensionRules.PopularWarWeariness(0.8f, 0f, 1f), Eps);
            // 下限クランプ。
            Assert.AreEqual(0f, TrinitarianTensionRules.PopularWarWeariness(0.1f, 1f, 1f), Eps);
        }

        /// <summary>崩壊検知＝均衡が閾値（既定0.3）未満で破綻。</summary>
        [Test]
        public void CollapseDetection_閾値未満で破綻検知()
        {
            Assert.IsTrue(TrinitarianTensionRules.CollapseDetection(0.2f));
            Assert.IsFalse(TrinitarianTensionRules.CollapseDetection(0.3f)); // 境界は破綻でない
            Assert.IsFalse(TrinitarianTensionRules.CollapseDetection(0.6f));
        }

        /// <summary>情念と理性の緊張＝支持−意志（正=情念暴走/負=理性が支持を欠く）。</summary>
        [Test]
        public void PassionRationalityTension_情念と理性の緊張()
        {
            Assert.AreEqual(0.6f, TrinitarianTensionRules.PassionRationalityTension(0.9f, 0.3f), Eps);  // 情念が政策を超える
            Assert.AreEqual(-0.5f, TrinitarianTensionRules.PassionRationalityTension(0.3f, 0.8f), Eps); // 理性が情念を欠く
            Assert.AreEqual(0f, TrinitarianTensionRules.PassionRationalityTension(0.5f, 0.5f), Eps);    // 調和
        }

        /// <summary>補強の必要＝最弱要素を補う（WeakestPillar への委譲）。</summary>
        [Test]
        public void RebalancingNeed_最弱要素を補強対象に返す()
        {
            Assert.AreEqual(TrinitarianTensionRules.TrinityPillar.軍,
                TrinitarianTensionRules.RebalancingNeed(0.8f, 0.2f, 0.7f));
            Assert.AreEqual(TrinitarianTensionRules.TrinityPillar.国民,
                TrinitarianTensionRules.RebalancingNeed(0.7f, 0.6f, 0.1f));
        }
    }
}
