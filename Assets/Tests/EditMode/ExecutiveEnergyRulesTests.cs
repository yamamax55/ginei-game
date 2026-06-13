using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ExecutiveEnergyRules（FED-5 #1489・行政エネルギーと単一執政＝フェデラリスト第70篇）の純ロジック検証。
    /// 既定 ExecutiveEnergyParams の具体値で期待値を固定し、
    /// 単一執政の決断速度・責任の明確さ・複数執政の麻痺・エネルギーvs安全・集中のクーデターリスク・危機対応能力・活力判定を担保する。
    /// </summary>
    public class ExecutiveEnergyRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>執政の統一＝一人（合議度0）で最大1.0、合議制（1）で0、4分の1合議で0.75。</summary>
        [Test]
        public void ExecutiveUnity_単一ほど高く合議ほど低い()
        {
            Assert.AreEqual(1f, ExecutiveEnergyRules.ExecutiveUnity(0f), Eps);     // 完全な単一執政
            Assert.AreEqual(0f, ExecutiveEnergyRules.ExecutiveUnity(1f), Eps);     // 多人数の合議制
            Assert.AreEqual(0.75f, ExecutiveEnergyRules.ExecutiveUnity(0.25f), Eps);
        }

        /// <summary>決断速度＝統一が高いほど速い（既定下限0.3）。一人なら果断さがそのまま、合議は0.3倍に薄まる。</summary>
        [Test]
        public void DecisionSpeed_統一が高いほど速い()
        {
            // 統一1.0・決断力0.8 → 0.8 ×Lerp(0.3,1,1)=0.8
            Assert.AreEqual(0.8f, ExecutiveEnergyRules.DecisionSpeed(1f, 0.8f), Eps);
            // 統一0（合議制）→ 0.8 ×0.3 = 0.24（同じ果断さでも遅い）
            Assert.AreEqual(0.24f, ExecutiveEnergyRules.DecisionSpeed(0f, 0.8f), Eps);
        }

        /// <summary>責任の明確さ＝統一が高いほど一点に集中（既定下限0.2）。合議制は責任が拡散する。</summary>
        [Test]
        public void Accountability_単一は責任が明確で合議は拡散する()
        {
            Assert.AreEqual(1f, ExecutiveEnergyRules.Accountability(1f), Eps);     // 誰の責任か明確
            Assert.AreEqual(0.2f, ExecutiveEnergyRules.Accountability(0f), Eps);   // 誰の責任でもない
            Assert.AreEqual(0.6f, ExecutiveEnergyRules.Accountability(0.5f), Eps);
        }

        /// <summary>複数執政の麻痺＝合議度×内部不和。一人なら麻痺せず、多人数かつ対立で相互妨害が立つ。</summary>
        [Test]
        public void PluralityParalysis_複数執政は内部対立で麻痺する()
        {
            // 合議度0.8・不和0.5 → 0.8×0.5×1.0 = 0.4
            Assert.AreEqual(0.4f, ExecutiveEnergyRules.PluralityParalysis(0.8f, 0.5f), Eps);
            // 一人（合議度0）なら不和があっても麻痺しない
            Assert.AreEqual(0f, ExecutiveEnergyRules.PluralityParalysis(0f, 1f), Eps);
        }

        /// <summary>エネルギーvs安全＝統一の活力が生む危険を制度的制約が割り引く。制約満点なら危険ゼロ。</summary>
        [Test]
        public void EnergyVsSafety_制度的制約が集中の危険を抑える()
        {
            // 統一1・制約0 → 1×1×0.8 = 0.8（裸の権力集中）
            Assert.AreEqual(0.8f, ExecutiveEnergyRules.EnergyVsSafety(1f, 0f), Eps);
            // 統一1・制約1 → 危険ゼロ（憲法・議会・司法の歯止め）
            Assert.AreEqual(0f, ExecutiveEnergyRules.EnergyVsSafety(1f, 1f), Eps);
        }

        /// <summary>集中のクーデターリスク＝任期制限と責任の明確さが歯止め。両方満点で危険消失（マディソン的歯止め）。</summary>
        [Test]
        public void CoupRiskFromConcentration_任期制限と責任が危険を抑える()
        {
            // 統一1・任期制限0・責任0 → check0、raw=0.8 → 0.8
            Assert.AreEqual(0.8f, ExecutiveEnergyRules.CoupRiskFromConcentration(1f, 0f, 0f), Eps);
            // 統一1・任期制限1・責任1 → check1 → 0（いつか退き責任を負うなら専制に至らない）
            Assert.AreEqual(0f, ExecutiveEnergyRules.CoupRiskFromConcentration(1f, 1f, 1f), Eps);
            // 統一1・任期制限0.5・責任0.5 → check0.5、0.8×0.5 = 0.4
            Assert.AreEqual(0.4f, ExecutiveEnergyRules.CoupRiskFromConcentration(1f, 0.5f, 0.5f), Eps);
        }

        /// <summary>危機対応能力＝決断速度×活力（既定下限0.4）。決断が速くても活力がなければ実行が伴わない。</summary>
        [Test]
        public void CrisisResponseCapacity_単一執政の真価は非常時の決断()
        {
            // 速さ0.8・活力1 → 0.8×Lerp(0.4,1,1)=0.8
            Assert.AreEqual(0.8f, ExecutiveEnergyRules.CrisisResponseCapacity(0.8f, 1f), Eps);
            // 速さ0.8・活力0 → 0.8×0.4 = 0.32（決断しても行動が伴わない）
            Assert.AreEqual(0.32f, ExecutiveEnergyRules.CrisisResponseCapacity(0.8f, 0f), Eps);
        }

        /// <summary>活力ある執政の判定＝決断速度と責任の明確さの両方が閾値以上。片方だけでは良い執政でない。</summary>
        [Test]
        public void IsEnergeticExecutive_速さと責任の両立を求める()
        {
            Assert.IsTrue(ExecutiveEnergyRules.IsEnergeticExecutive(0.8f, 0.8f, 0.7f));  // 速く決め責任も明確
            Assert.IsFalse(ExecutiveEnergyRules.IsEnergeticExecutive(0.8f, 0.5f, 0.7f)); // 速いが責任が曖昧
            Assert.IsFalse(ExecutiveEnergyRules.IsEnergeticExecutive(0.5f, 0.8f, 0.7f)); // 責任は明確だが鈍い
        }
    }
}
