using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 重心分析（クラウゼヴィッツ Schwerpunkt・#1136）を固定する：力の源泉が集中する一点の重み・
    /// 重心判定・崩壊の波及・AIの攻撃優先度（重要×脆弱×到達可能）・決定的弱点・間接アプローチ・
    /// 戦力集中・自軍重心の防護。既定 Params（強さ0.6/結節0.4・重心閾値0.6・崩壊冪1.5・間接閾値0.6）。
    /// </summary>
    public class CenterOfGravityRulesTests
    {
        [Test]
        public void GravityWeight_IsWeightedBlendOfStrengthAndConnectivity()
        {
            // 既定 強さ0.6/結節0.4：強さ1×結節1=1.0、強さ1×結節0=0.6、強さ0×結節1=0.4。
            Assert.AreEqual(1f, CenterOfGravityRules.GravityWeight(CoGType.主力艦隊, 1f, 1f), 1e-4f);
            Assert.AreEqual(0.6f, CenterOfGravityRules.GravityWeight(CoGType.首都星系, 1f, 0f), 1e-4f);
            Assert.AreEqual(0.4f, CenterOfGravityRules.GravityWeight(CoGType.同盟関係, 0f, 1f), 1e-4f);
            // 入力はクランプされる。
            Assert.AreEqual(0f, CenterOfGravityRules.GravityWeight(CoGType.補給拠点, -1f, -1f), 1e-4f);
        }

        [Test]
        public void IsCenterOfGravity_ThresholdAtSixTenths()
        {
            // 既定閾値0.6：以上なら重心、未満なら重心でない。
            Assert.IsTrue(CenterOfGravityRules.IsCenterOfGravity(0.6f));
            Assert.IsTrue(CenterOfGravityRules.IsCenterOfGravity(0.8f));
            Assert.IsFalse(CenterOfGravityRules.IsCenterOfGravity(0.59f));
        }

        [Test]
        public void CollapseImpact_DeepDependencyAmplifiesCollapse()
        {
            // 依存度1なら重みがそのまま波及（冪1.5でも1^1.5=1）。
            Assert.AreEqual(0.8f, CenterOfGravityRules.CollapseImpact(0.8f, 1f), 1e-4f);
            // 依存ゼロなら波及なし＝そこを失っても全体は崩れない。
            Assert.AreEqual(0f, CenterOfGravityRules.CollapseImpact(0.8f, 0f), 1e-4f);
            // 依存0.5：0.8 × 0.5^1.5 = 0.8 × 0.353553 ≈ 0.28284。
            Assert.AreEqual(0.28284f, CenterOfGravityRules.CollapseImpact(0.8f, 0.5f), 1e-3f);
        }

        [Test]
        public void AttackPriority_RequiresImportantVulnerableReachable()
        {
            // 重要1×脆弱1×到達1 ＝最優先1.0。
            Assert.AreEqual(1f, CenterOfGravityRules.AttackPriority(1f, 1f, 1f), 1e-4f);
            // どれか一つでも0なら優先度0（届かない／崩せない重心は後回し）。
            Assert.AreEqual(0f, CenterOfGravityRules.AttackPriority(1f, 1f, 0f), 1e-4f);
            Assert.AreEqual(0f, CenterOfGravityRules.AttackPriority(1f, 0f, 1f), 1e-4f);
            // 0.8×0.5×0.5 = 0.2。
            Assert.AreEqual(0.2f, CenterOfGravityRules.AttackPriority(0.8f, 0.5f, 0.5f), 1e-4f);
            // 叩ける重心（脆弱×到達が高い）が、固く遠い重心より優先される。
            Assert.Greater(
                CenterOfGravityRules.AttackPriority(0.7f, 0.8f, 0.8f),
                CenterOfGravityRules.AttackPriority(0.9f, 0.2f, 0.2f));
        }

        [Test]
        public void CriticalVulnerability_HeavyButUndefended_IsTheWeakSpot()
        {
            // 重い重心が無防備なら急所最大：重み0.9×(1-0)=0.9。
            Assert.AreEqual(0.9f, CenterOfGravityRules.CriticalVulnerability(0.9f, 0f), 1e-4f);
            // 守りが固ければ急所にならない：0.9×(1-1)=0。
            Assert.AreEqual(0f, CenterOfGravityRules.CriticalVulnerability(0.9f, 1f), 1e-4f);
            // 重い重心の守りの穴 ＞ 軽い重心の同じ穴。
            Assert.Greater(
                CenterOfGravityRules.CriticalVulnerability(0.9f, 0.3f),
                CenterOfGravityRules.CriticalVulnerability(0.4f, 0.3f));
        }

        [Test]
        public void IndirectApproach_HardFrontWithFlankingOption_IsRecommended()
        {
            // 正面が固く(0.6超)・迂回路があるほど間接アプローチ：正面1.0×迂回1.0で最大。
            // 既定閾値0.6：excess=(1-0.6)/(1-0.6)=1、×flank1 = 1.0。
            Assert.AreEqual(1f, CenterOfGravityRules.IndirectApproach(1f, 1f), 1e-4f);
            // 正面が薄ければ（閾値以下）正面突破で足りる＝間接アプローチ不要(0)。
            Assert.AreEqual(0f, CenterOfGravityRules.IndirectApproach(0.5f, 1f), 1e-4f);
            // 迂回路が無ければ間接アプローチは取れない(0)。
            Assert.AreEqual(0f, CenterOfGravityRules.IndirectApproach(1f, 0f), 1e-4f);
            // 正面0.8：excess=(0.8-0.6)/0.4=0.5、×迂回1.0 = 0.5。
            Assert.AreEqual(0.5f, CenterOfGravityRules.IndirectApproach(0.8f, 1f), 1e-4f);
        }

        [Test]
        public void ConcentrationOfForce_OnePointFocusBeatsDispersion()
        {
            // 一点集中（分散0）＝総戦力がそのまま効く。
            Assert.AreEqual(1f, CenterOfGravityRules.ConcentrationOfForce(1f, 0f), 1e-4f);
            // 分散1.0＝総戦力1.0が 1/(1+1)=0.5 に薄まる（兵力分散の戒め）。
            Assert.AreEqual(0.5f, CenterOfGravityRules.ConcentrationOfForce(1f, 1f), 1e-4f);
            // 一点集中 ＞ 分散投入（同じ総戦力）。
            Assert.Greater(
                CenterOfGravityRules.ConcentrationOfForce(0.8f, 0.1f),
                CenterOfGravityRules.ConcentrationOfForce(0.8f, 0.9f));
        }

        [Test]
        public void ProtectOwnCoG_MirrorsEnemyCriticalVulnerability()
        {
            // 自軍の重い重心が手薄なら守りの優先度最大：0.9×(1-0)=0.9。
            Assert.AreEqual(0.9f, CenterOfGravityRules.ProtectOwnCoG(0.9f, 0f), 1e-4f);
            // 既に固く守られていれば優先度は下がる：0.9×(1-1)=0。
            Assert.AreEqual(0f, CenterOfGravityRules.ProtectOwnCoG(0.9f, 1f), 1e-4f);
            // 敵の決定的弱点の式と一致（守る側の鏡像）。
            Assert.AreEqual(
                CenterOfGravityRules.CriticalVulnerability(0.7f, 0.4f),
                CenterOfGravityRules.ProtectOwnCoG(0.7f, 0.4f), 1e-4f);
        }

        [Test]
        public void Params_ClampInvalidValues_GravityWeightZeroWhenNoWeights()
        {
            // ctor クランプ：負の重み→0、閾値→0..1、冪→下限1。
            var p = new CenterOfGravityParams(-1f, -1f, 2f, -5f, 1.5f);
            Assert.AreEqual(0f, p.strengthWeight, 1e-4f);
            Assert.AreEqual(0f, p.connectivityWeight, 1e-4f);
            Assert.AreEqual(1f, p.gravityThreshold, 1e-4f);
            Assert.AreEqual(1f, p.collapseExponent, 1e-4f);
            Assert.AreEqual(1f, p.indirectThreshold, 1e-4f);
            // 重み合計0なら重みは0（ゼロ除算しない）。
            Assert.AreEqual(0f, CenterOfGravityRules.GravityWeight(CoGType.主力艦隊, 1f, 1f, p), 1e-4f);
        }
    }
}
