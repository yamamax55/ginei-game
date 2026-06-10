using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 辺境気質 <see cref="FrontierRules"/> の純ロジック検証（既定 <see cref="FrontierParams.Default"/> 具体値で期待固定）。
    /// 「距離は文化を作る＝遠さ×冷遇が独立志向を生む」を担保する。
    /// </summary>
    public class FrontierRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>遠く通信途絶なら辺境度は最大1.0、近く通信即時ならゼロ＝中央の手の届き方を表す。</summary>
        [Test]
        public void FrontierIndex_DistanceAndCommLag()
        {
            Assert.AreEqual(1.0f, FrontierRules.FrontierIndex(100f, 0f), Eps);   // 遠い×途絶＝最辺境
            Assert.AreEqual(0.3f, FrontierRules.FrontierIndex(50f, 1f), Eps);    // 中距離×即時＝距離0.6×0.5
            Assert.AreEqual(0.0f, FrontierRules.FrontierIndex(0f, 1f), Eps);     // 中枢＝辺境ゼロ
        }

        /// <summary>自立の気風は辺境度に比例（最辺境で1.0）、中枢でも下限0.1は残る＝開拓者精神。</summary>
        [Test]
        public void SelfReliance_ScalesWithFrontier()
        {
            Assert.AreEqual(1.0f, FrontierRules.SelfReliance(1.0f), Eps);
            Assert.AreEqual(FrontierRules.SelfRelianceFloor, FrontierRules.SelfReliance(0f), Eps);
            Assert.Greater(FrontierRules.SelfReliance(0.8f), FrontierRules.SelfReliance(0.2f));
        }

        /// <summary>距離は統制を薄め、駐留がそれを補う＝駐留で統制が回復する。</summary>
        [Test]
        public void ControlPenetration_GarrisonOffsetsDistance()
        {
            // 最辺境・無駐留＝統制は下限0.1まで崩れる
            Assert.AreEqual(FrontierRules.MinControl, FrontierRules.ControlPenetration(1.0f, 0f), Eps);
            // 最辺境でも満駐留なら erosion=1×(1−0.7)=0.3 → 統制0.7
            Assert.AreEqual(0.7f, FrontierRules.ControlPenetration(1.0f, 1.0f), Eps);
            // 駐留が手厚いほど統制は厚い
            Assert.Greater(FrontierRules.ControlPenetration(1.0f, 1.0f),
                           FrontierRules.ControlPenetration(1.0f, 0f));
        }

        /// <summary>独立志向＝辺境度×中央の冷遇。遠さだけでは離れず、冷遇されて初めて分離の土壌になる。</summary>
        [Test]
        public void IndependenceSentiment_NeedsNeglect()
        {
            Assert.AreEqual(1.0f, FrontierRules.IndependenceSentiment(1.0f, 1.0f), Eps); // 辺境×冷遇＝最大
            Assert.AreEqual(0.0f, FrontierRules.IndependenceSentiment(1.0f, 0f), Eps);  // 厚遇なら離れない
            Assert.AreEqual(0.4f, FrontierRules.IndependenceSentiment(0.5f, 0.8f), Eps);
        }

        /// <summary>厚遇でも辺境度ゼロでも独立志向は生まれない＝両方そろって初めて土壌になる（分離の鏡像）。</summary>
        [Test]
        public void IndependenceSentiment_BothFactorsRequired()
        {
            Assert.AreEqual(0.0f, FrontierRules.IndependenceSentiment(0f, 1.0f), Eps);   // 中枢は冷遇でも離れない
            Assert.Greater(FrontierRules.IndependenceSentiment(1.0f, 1.0f),
                           FrontierRules.IndependenceSentiment(0.4f, 1.0f));             // 辺境ほど強い
        }

        /// <summary>辺境の自衛力＝自立×人口×係数。中央が守らない辺境は自ら武装する（戦力にも反乱基盤にも）。</summary>
        [Test]
        public void FrontierMilitia_FromSelfRelianceAndPopulation()
        {
            Assert.AreEqual(50f, FrontierRules.FrontierMilitia(1.0f, 1000f), Eps); // 1×1000×0.05
            Assert.AreEqual(0f, FrontierRules.FrontierMilitia(1.0f, 0f), Eps);     // 無人＝武装なし
            Assert.Greater(FrontierRules.FrontierMilitia(1.0f, 1000f),
                           FrontierRules.FrontierMilitia(0.3f, 1000f));            // 自立が高いほど厚い
        }

        /// <summary>辺境の自由は辺境度に比例（中央の目が届かない＝実験場）、中枢でも下限0.2は残る。</summary>
        [Test]
        public void InnovationFreedom_HighOnFrontier()
        {
            Assert.AreEqual(1.0f, FrontierRules.InnovationFreedom(1.0f), Eps);
            Assert.AreEqual(FrontierRules.InnovationFloor, FrontierRules.InnovationFreedom(0f), Eps);
            Assert.Greater(FrontierRules.InnovationFreedom(0.9f), FrontierRules.InnovationFreedom(0.1f));
        }
    }
}
