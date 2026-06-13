using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// EconomicFreedomRules（HAYK-5 #1553・経済的自由と政治的自由の連動）の純ロジック検証。
    /// 既定 EconomicFreedomParams（協力床0.1／依存倍率0.8／逆説率0.2／隷従閾値0.3）で期待値を固定する。
    /// </summary>
    public class EconomicFreedomRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>経済的自由＝私有財産×市場の自由×(1−統制)＝いずれか欠ければ崩れる積。</summary>
        [Test]
        public void EconomicFreedom_積で三要素を掛ける()
        {
            // 0.8 * 0.5 * (1-0.5) = 0.2
            Assert.AreEqual(0.2f, EconomicFreedomRules.EconomicFreedom(0.8f, 0.5f, 0.5f), Eps);
            // 統制が最大なら自由は消える
            Assert.AreEqual(0f, EconomicFreedomRules.EconomicFreedom(1f, 1f, 1f), Eps);
            // 私有財産がゼロでも全体ゼロ
            Assert.AreEqual(0f, EconomicFreedomRules.EconomicFreedom(0f, 1f, 0f), Eps);
        }

        /// <summary>政治的自由は経済的自由に支えられる＝連動の単調写像。</summary>
        [Test]
        public void PoliticalFreedomLink_経済的自由が政治的自由を支える()
        {
            Assert.AreEqual(0.7f, EconomicFreedomRules.PoliticalFreedomLink(0.7f), Eps);
            // 経済を握られる（経済的自由ゼロ）と政治的自由の支えもゼロ
            Assert.AreEqual(0f, EconomicFreedomRules.PoliticalFreedomLink(0f), Eps);
        }

        /// <summary>協力係数＝経済的自由が高いほど自発的協力、強制が強いほど協力が強制に変わる。</summary>
        [Test]
        public void CooperationCoefficient_自由で自発的協力_強制で蝕む()
        {
            // 自由1・強制0 → voluntary=1 → 0.1 + 0.9*1 = 1.0
            Assert.AreEqual(1.0f, EconomicFreedomRules.CooperationCoefficient(1f, 0f), Eps);
            // 自由1・強制1 → voluntary=0 → 床0.1（協力が強制に置き換わる）
            Assert.AreEqual(0.1f, EconomicFreedomRules.CooperationCoefficient(1f, 1f), Eps);
            // 自由0.5・強制0.5 → voluntary=0.25 → 0.1 + 0.9*0.25 = 0.325
            Assert.AreEqual(0.325f, EconomicFreedomRules.CooperationCoefficient(0.5f, 0.5f), Eps);
        }

        /// <summary>国家依存＝経済統制が人々を国家に依存させる（パンを握る者が自由を握る）。</summary>
        [Test]
        public void DependencyOnState_統制が依存を生む()
        {
            // 統制1 * 依存倍率0.8 = 0.8
            Assert.AreEqual(0.8f, EconomicFreedomRules.DependencyOnState(1f), Eps);
            // 統制0.5 → 0.4
            Assert.AreEqual(0.4f, EconomicFreedomRules.DependencyOnState(0.5f), Eps);
            // 統制なしなら依存なし
            Assert.AreEqual(0f, EconomicFreedomRules.DependencyOnState(0f), Eps);
        }

        /// <summary>自由に基づく安定は自発的協力に支えられる＝協力係数を安定度へ写す。</summary>
        [Test]
        public void StabilityFromFreedom_協力に基づく安定()
        {
            Assert.AreEqual(0.6f, EconomicFreedomRules.StabilityFromFreedom(0.6f), Eps);
            Assert.AreEqual(0f, EconomicFreedomRules.StabilityFromFreedom(0f), Eps);
        }

        /// <summary>統制の逆説＝過度な統制が時間をかけて協力を蝕む。統制0なら逆説なし。</summary>
        [Test]
        public void ControlBackfire_過度な統制が協力を失わせる()
        {
            // 統制1 * 逆説率0.2 * dt1 = 0.2
            Assert.AreEqual(0.2f, EconomicFreedomRules.ControlBackfire(1f, 1f), Eps);
            // 統制0.5 * 0.2 * 2 = 0.2
            Assert.AreEqual(0.2f, EconomicFreedomRules.ControlBackfire(0.5f, 2f), Eps);
            // 統制ゼロなら逆説は起きない
            Assert.AreEqual(0f, EconomicFreedomRules.ControlBackfire(0f, 1f), Eps);
            // dt<=0 は0
            Assert.AreEqual(0f, EconomicFreedomRules.ControlBackfire(1f, 0f), Eps);
        }

        /// <summary>二つの自由は補完的＝積＝片方だけでは保てない。</summary>
        [Test]
        public void FreedomComplementarity_二つの自由は補完的()
        {
            // 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, EconomicFreedomRules.FreedomComplementarity(0.8f, 0.5f), Eps);
            // 政治的自由がゼロなら全体ゼロ（経済的自由だけでは保てない）
            Assert.AreEqual(0f, EconomicFreedomRules.FreedomComplementarity(1f, 0f), Eps);
        }

        /// <summary>隷従ドリフト＝高統制かつ低協力で隷従へ向かう判定（既定閾値0.3）。</summary>
        [Test]
        public void IsServitudeDrift_統制で自発性を失い隷従へ()
        {
            var p = EconomicFreedomParams.Default; // 閾値0.3 → control>0.7 かつ cooperation<0.3
            // 高統制0.9・低協力0.2 → 隷従ドリフト
            Assert.IsTrue(EconomicFreedomRules.IsServitudeDrift(0.9f, 0.2f, p));
            // 高統制でも協力が十分なら隷従ではない
            Assert.IsFalse(EconomicFreedomRules.IsServitudeDrift(0.9f, 0.5f, p));
            // 統制が低ければ隷従ではない（協力が低くても）
            Assert.IsFalse(EconomicFreedomRules.IsServitudeDrift(0.5f, 0.2f, p));
        }
    }
}
