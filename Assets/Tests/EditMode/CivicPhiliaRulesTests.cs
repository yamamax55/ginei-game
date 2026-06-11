using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>市民的友愛と審議崩壊（ARIS-4 #1503）の純ロジックテスト。</summary>
    public class CivicPhiliaRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>市民的信頼＝共有価値×互恵性。どちらか欠ければ友愛は成り立たない。</summary>
        [Test]
        public void CivicTrust_共有価値と互恵性の積()
        {
            Assert.AreEqual(0.5f, CivicPhiliaRules.CivicTrust(1f, 0.5f), Eps);
            Assert.AreEqual(0.42f, CivicPhiliaRules.CivicTrust(0.6f, 0.7f), Eps);
            // 互恵性ゼロなら友愛は成り立たない
            Assert.AreEqual(0f, CivicPhiliaRules.CivicTrust(1f, 0f), Eps);
            // クランプ
            Assert.AreEqual(1f, CivicPhiliaRules.CivicTrust(2f, 2f), Eps);
        }

        /// <summary>熟議の機能度＝市民的信頼が高いほど審議が機能する。</summary>
        [Test]
        public void DeliberativeCapacity_信頼に比例して審議が機能()
        {
            Assert.AreEqual(0.8f, CivicPhiliaRules.DeliberativeCapacity(0.8f), Eps);
            Assert.AreEqual(0f, CivicPhiliaRules.DeliberativeCapacity(0f), Eps);
            Assert.AreEqual(1f, CivicPhiliaRules.DeliberativeCapacity(1.5f), Eps);
        }

        /// <summary>不平等と僭主圧力が時間で市民的友愛を蝕む（既定 不平等0.15/秒・僭主0.2/秒）。</summary>
        [Test]
        public void TrustErosion_不平等と僭主圧力で侵食()
        {
            // (0.15*1 + 0.2*1) * 1 = 0.35
            Assert.AreEqual(0.35f, CivicPhiliaRules.TrustErosion(1f, 1f, 1f), Eps);
            // (0.15*0.4 + 0.2*0.5) * 2 = (0.06+0.1)*2 = 0.32
            Assert.AreEqual(0.32f, CivicPhiliaRules.TrustErosion(0.4f, 0.5f, 2f), Eps);
            // dt<=0 は侵食なし
            Assert.AreEqual(0f, CivicPhiliaRules.TrustErosion(1f, 1f, 0f), Eps);
        }

        /// <summary>審議の崩壊＝信頼が失われ二極化すると崩壊する。信頼が高ければ二極化に耐える。</summary>
        [Test]
        public void DeliberativeCollapse_低信頼と二極化で崩壊()
        {
            // (1-0.2)*1.0 = 0.8 ＝低信頼×強い二極化で審議は崩壊
            Assert.AreEqual(0.8f, CivicPhiliaRules.DeliberativeCollapse(0.2f, 1f), Eps);
            // 信頼が高ければ二極化しても崩壊は小さい (1-0.9)*1 = 0.1
            Assert.AreEqual(0.1f, CivicPhiliaRules.DeliberativeCollapse(0.9f, 1f), Eps);
            // 二極化ゼロなら崩壊しない
            Assert.AreEqual(0f, CivicPhiliaRules.DeliberativeCollapse(0.2f, 0f), Eps);
        }

        /// <summary>審議の崩壊が膠着を増幅する（既定 増幅率0.5）。</summary>
        [Test]
        public void GridlockAmplification_崩壊が膠着を増幅()
        {
            // 崩壊0で増幅なし
            Assert.AreEqual(1f, CivicPhiliaRules.GridlockAmplification(0f), Eps);
            // 崩壊1で 1 + 0.5*1 = 1.5
            Assert.AreEqual(1.5f, CivicPhiliaRules.GridlockAmplification(1f), Eps);
            // 崩壊0.6で 1 + 0.5*0.6 = 1.3
            Assert.AreEqual(1.3f, CivicPhiliaRules.GridlockAmplification(0.6f), Eps);
        }

        /// <summary>共通目標による結束＝友愛と共通善の志向でポリスがまとまる。</summary>
        [Test]
        public void CommonPurposeStrength_友愛と共通目標で結束()
        {
            Assert.AreEqual(0.56f, CivicPhiliaRules.CommonPurposeStrength(0.7f, 0.8f), Eps);
            // 共通目標がなければ結束しない
            Assert.AreEqual(0f, CivicPhiliaRules.CommonPurposeStrength(0.9f, 0f), Eps);
        }

        /// <summary>派閥的敵意＝不平等と僭主圧力が市民を敵対派閥に分断する（友愛の反対）。</summary>
        [Test]
        public void FactionalEnmity_不平等と僭主圧力で分断()
        {
            // 1 - (1-0.5)(1-0.5) = 1 - 0.25 = 0.75
            Assert.AreEqual(0.75f, CivicPhiliaRules.FactionalEnmity(0.5f, 0.5f), Eps);
            // どちらもゼロなら敵意なし
            Assert.AreEqual(0f, CivicPhiliaRules.FactionalEnmity(0f, 0f), Eps);
            // 片方が満杯なら敵意も満杯
            Assert.AreEqual(1f, CivicPhiliaRules.FactionalEnmity(1f, 0.3f), Eps);
        }

        /// <summary>市民的崩壊判定＝信頼が閾値（既定0.3）割れで政治機能不全。</summary>
        [Test]
        public void IsCivicBreakdown_信頼閾値割れで崩壊()
        {
            Assert.IsTrue(CivicPhiliaRules.IsCivicBreakdown(0.2f));
            Assert.IsFalse(CivicPhiliaRules.IsCivicBreakdown(0.5f));
            // 境界（0.3）は未満でないので崩壊しない
            Assert.IsFalse(CivicPhiliaRules.IsCivicBreakdown(0.3f));
            // カスタム閾値
            Assert.IsTrue(CivicPhiliaRules.IsCivicBreakdown(0.4f, 0.5f));
        }
    }
}
