using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 連立政権（PartyRules=誰が統べるか と対の、単独過半数なき政権がどう持ちこたえるか）を固定する：
    /// 過半数判定、党数×思想幅の政策希釈（最小公倍数化）、シェアでなく「抜けたら過半数割れ」が源泉の
    /// キングメーカー拒否権（足し算の議席と掛け算の拒否権）、結束の均衡収束、誘惑が掛かる崩壊リスク、
    /// ガムソンの法則のポスト配分。境界とクランプを担保。
    /// </summary>
    public class CoalitionRulesTests
    {
        private static readonly CoalitionParams P = CoalitionParams.Default;
        // 過半数0.5・希釈0.2/党×(0.5+幅)・余裕基準0.25・拒否権下限0.25・思想軋み0.6/外圧0.4・収束0.2・誘惑倍率1.0

        [Test]
        public void NeedsCoalition_ExactHalfIsNotMajority()
        {
            // ちょうど半分は過半数ではない＝連立が要る
            Assert.IsTrue(CoalitionRules.NeedsCoalition(0.5f, P));
            // 過半数を超えれば単独政権
            Assert.IsFalse(CoalitionRules.NeedsCoalition(0.51f, P));
            // 入力は0..1にクランプ
            Assert.IsFalse(CoalitionRules.NeedsCoalition(5f, P));
            Assert.IsTrue(CoalitionRules.NeedsCoalition(-1f, P));
        }

        [Test]
        public void PolicyDilution_PartnersTimesSpread_LowestCommonDenominator()
        {
            // 単独政権は薄まらない
            Assert.AreEqual(0f, CoalitionRules.PolicyDilution(1, 1f, P), 1e-5f);
            // 2党連立・思想幅0.5＝1×0.2×(0.5+0.5)=0.2
            Assert.AreEqual(0.2f, CoalitionRules.PolicyDilution(2, 0.5f, P), 1e-5f);
            // 思想が完全一致でも合意コストの下駄分は薄まる＝1×0.2×0.5=0.1
            Assert.AreEqual(0.1f, CoalitionRules.PolicyDilution(2, 0f, P), 1e-5f);
            // 4党×思想幅最大＝3×0.2×1.5=0.9 まで薄まる
            Assert.AreEqual(0.9f, CoalitionRules.PolicyDilution(4, 1f, P), 1e-5f);
            // 大連立の上限は1（政策は空文化）
            Assert.AreEqual(1f, CoalitionRules.PolicyDilution(10, 1f, P), 1e-5f);
        }

        [Test]
        public void KingmakerPower_PivotNotShare_IsTheSource()
        {
            // 非対称の核：3%の小党でも「抜けたら過半数割れ」（余裕0.02未満）なら強い拒否権＝1−0.02/0.25=0.92
            Assert.AreEqual(0.92f, CoalitionRules.KingmakerPower(0.03f, 0.02f, P), 1e-5f);
            // 30%の大党でも余裕0.35の大連立では抜けても保つ＝拒否権ゼロ（シェアは力ではない）
            Assert.AreEqual(0f, CoalitionRules.KingmakerPower(0.3f, 0.35f, P), 1e-5f);
            // 余裕ゼロのぎりぎり連立＝離脱カードは最強1.0
            Assert.AreEqual(1f, CoalitionRules.KingmakerPower(0.05f, 0f, P), 1e-5f);
            // シェア＝余裕（抜けてもちょうど過半数維持）は拒否権なし
            Assert.AreEqual(0f, CoalitionRules.KingmakerPower(0.05f, 0.05f, P), 1e-5f);
            // ピボタルである限り下限0.25は割らない（余裕0.2→1−0.8=0.2<下限）
            Assert.AreEqual(0.25f, CoalitionRules.KingmakerPower(0.3f, 0.2f, P), 1e-5f);
        }

        [Test]
        public void StabilityTick_ConvergesToStrainEquilibrium()
        {
            // 思想幅1×0.6＋外圧1×0.4＝均衡0へ、0.2/dt で軋む＝1.0→0.8
            Assert.AreEqual(0.8f, CoalitionRules.StabilityTick(1f, 1f, 1f, 1f, P), 1e-5f);
            // 要因が消えれば均衡1へ回復＝0.5→0.7
            Assert.AreEqual(0.7f, CoalitionRules.StabilityTick(0.5f, 0f, 0f, 1f, P), 1e-5f);
            // 長時間で均衡に到達（行き過ぎない）
            Assert.AreEqual(1f, CoalitionRules.StabilityTick(0.5f, 0f, 0f, 100f, P), 1e-5f);
            // dt負は動かない
            Assert.AreEqual(0.5f, CoalitionRules.StabilityTick(0.5f, 1f, 1f, -1f, P), 1e-5f);
        }

        [Test]
        public void CollapseRisk_TemptationMultiplies_NotAdds()
        {
            // 結束完全なら誘惑があっても倒れない＝掛け算の拒否権（足し算なら0にならない）
            Assert.AreEqual(0f, CoalitionRules.CollapseRisk(1f, 1f, P), 1e-5f);
            // 誘惑なし＝不安定さがそのままリスク
            Assert.AreEqual(0.5f, CoalitionRules.CollapseRisk(0.5f, 0f, P), 1e-5f);
            // 同じ亀裂が誘惑最大で倍に広がる＝0.5×(1+1)=1.0
            Assert.AreEqual(1f, CoalitionRules.CollapseRisk(0.5f, 1f, P), 1e-5f);
            // 中間＝0.4×(1+0.5)=0.6
            Assert.AreEqual(0.6f, CoalitionRules.CollapseRisk(0.6f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void PortfolioAllocation_GamsonsLaw_ProportionalToContribution()
        {
            // 連立0.55のうち0.1の貢献＝ポストの 0.1/0.55≒18.2%
            Assert.AreEqual(0.1f / 0.55f, CoalitionRules.PortfolioAllocation(0.1f, 0.55f), 1e-5f);
            // 連立の全議席を出す党は全ポスト
            Assert.AreEqual(1f, CoalitionRules.PortfolioAllocation(0.55f, 0.55f), 1e-5f);
            // 連立合計0は配分なし
            Assert.AreEqual(0f, CoalitionRules.PortfolioAllocation(0.5f, 0f), 1e-5f);
            // 範囲外入力もクランプ＝上限1
            Assert.AreEqual(1f, CoalitionRules.PortfolioAllocation(2f, 1f), 1e-5f);
        }

        [Test]
        public void DefaultParams_CtorClamps()
        {
            // 負値・範囲外はctorでクランプされる
            var p = new CoalitionParams(-1f, -1f, -1f, -1f, 2f, -1f, -1f, -1f, -1f);
            Assert.AreEqual(0f, p.majorityThreshold, 1e-6f);
            Assert.AreEqual(0f, p.dilutionPerPartner, 1e-6f);
            Assert.AreEqual(0.0001f, p.comfortMargin, 1e-7f); // ゼロ除算防止の下限
            Assert.AreEqual(1f, p.pivotFloor, 1e-6f);
            Assert.AreEqual(0f, p.defectionBoost, 1e-6f);
            // 既定値の固定
            Assert.AreEqual(0.5f, P.majorityThreshold, 1e-6f);
            Assert.AreEqual(0.2f, P.dilutionPerPartner, 1e-6f);
            Assert.AreEqual(0.25f, P.comfortMargin, 1e-6f);
            Assert.AreEqual(0.25f, P.pivotFloor, 1e-6f);
            Assert.AreEqual(1f, P.defectionBoost, 1e-6f);
        }
    }
}
