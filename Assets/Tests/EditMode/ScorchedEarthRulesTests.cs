using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 焦土戦術を固定する：拒否価値は徹底度の分だけ（範囲×0.8）・侵攻減速は範囲比例・自国民の恨みは
    /// 退避で軽減（ただしゼロにはならない）・生じる荒廃は ReconstructionRules の devastation 入力・
    /// 損益は「すぐ取り返すなら焼くだけ損・長期失陥なら焼き得」＝分岐点75（既定Params）。
    /// クランプと徹底度ゼロの無限大ケースを担保。
    /// </summary>
    public class ScorchedEarthRulesTests
    {
        private static readonly ScorchedEarthParams P = ScorchedEarthParams.Default; // 徹底0.8/減速0.5/恨み0.6/退避軽減0.7/荒廃1.0/復興費比0.6/飽和100

        [Test]
        public void DeniedValue_ScopeTimesEfficiency_Clamped()
        {
            // 資産1000を半分焼く → 1000×0.5×0.8＝400 が敵に渡らない
            Assert.AreEqual(400f, ScorchedEarthRules.DeniedValue(1000f, 0.5f, P), 1e-3f);
            // 全面焼却でも徹底度の分だけ＝800（燃え残りは敵が拾う）
            Assert.AreEqual(800f, ScorchedEarthRules.DeniedValue(1000f, 1f, P), 1e-3f);
            // 範囲過大・資産負はクランプ
            Assert.AreEqual(800f, ScorchedEarthRules.DeniedValue(1000f, 1.5f, P), 1e-3f);
            Assert.AreEqual(0f, ScorchedEarthRules.DeniedValue(-100f, 1f, P), 1e-5f);
        }

        [Test]
        public void InvaderSlowdown_ProportionalToScope()
        {
            // 全面焼却で減速量0.5（敵速度は半分）、範囲0.4で0.2
            Assert.AreEqual(0.5f, ScorchedEarthRules.InvaderSlowdown(1f, P), 1e-4f);
            Assert.AreEqual(0.2f, ScorchedEarthRules.InvaderSlowdown(0.4f, P), 1e-4f);
            // 焼かなければ鈍らない・過大入力はクランプ
            Assert.AreEqual(0f, ScorchedEarthRules.InvaderSlowdown(0f, P), 1e-5f);
            Assert.AreEqual(0.5f, ScorchedEarthRules.InvaderSlowdown(2f, P), 1e-4f);
        }

        [Test]
        public void PopulationResentment_EvacuationMitigates_ButNeverZero()
        {
            // 住民ごと焼く（退避ゼロ）＝恨み最大 0.6
            Assert.AreEqual(0.6f, ScorchedEarthRules.PopulationResentment(1f, 0f, P), 1e-4f);
            // 全力退避なら 0.6×(1−0.7)＝0.18 まで軽い
            Assert.AreEqual(0.18f, ScorchedEarthRules.PopulationResentment(1f, 1f, P), 1e-4f);
            // 範囲0.5×全力退避＝0.09
            Assert.AreEqual(0.09f, ScorchedEarthRules.PopulationResentment(0.5f, 1f, P), 1e-4f);
            // 退避しても財産は焼ける＝恨みはゼロにならない
            Assert.Greater(ScorchedEarthRules.PopulationResentment(1f, 1f, P), 0f);
        }

        [Test]
        public void DevastationCreated_FeedsReconstruction()
        {
            // 既定（変換係数1.0）＝焼いた範囲がそのまま荒廃に
            Assert.AreEqual(0.7f, ScorchedEarthRules.DevastationCreated(0.7f, P), 1e-4f);
            Assert.AreEqual(1f, ScorchedEarthRules.DevastationCreated(1.5f, P), 1e-5f); // クランプ
            // ReconstructionRules の devastation 入力としてそのまま流せる＝産出は 1−荒廃
            float dev = ScorchedEarthRules.DevastationCreated(0.7f, P);
            Assert.AreEqual(0.3f, ReconstructionRules.OutputFactor(dev), 1e-4f);
        }

        [Test]
        public void NetValue_QuickRecaptureIsPureLoss()
        {
            // 即時奪還（t=0）＝敵に使わせる時間が無い＝利得ゼロで復興費だけ残る：−1000×1×0.6＝−600
            Assert.AreEqual(-600f, ScorchedEarthRules.NetValue(1000f, 1f, 0f, P), 1e-3f);
            // 資産ゼロ以下は損益なし
            Assert.AreEqual(0f, ScorchedEarthRules.NetValue(0f, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void NetValue_LongLossMakesBurningPay()
        {
            // 長期失陥（t=飽和100）＝拒否価値満額800−復興費600＝＋200 の焼き得
            Assert.AreEqual(200f, ScorchedEarthRules.NetValue(1000f, 1f, 100f, P), 1e-3f);
            // 飽和を超えても利得は伸びない（敵の活用は飽和済み）
            Assert.AreEqual(200f, ScorchedEarthRules.NetValue(1000f, 1f, 500f, P), 1e-3f);
            // 損益分岐 t=75 でちょうどゼロ：800×0.75−600＝0
            Assert.AreEqual(0f, ScorchedEarthRules.NetValue(1000f, 1f, 75f, P), 1e-3f);
            // 「時間を買って未来を売る」＝失陥が長いほど損益は単調に改善
            Assert.Less(ScorchedEarthRules.NetValue(1000f, 1f, 30f, P), ScorchedEarthRules.NetValue(1000f, 1f, 60f, P));
        }

        [Test]
        public void BreakEvenTime_DefaultIs75_NoEfficiencyIsForever()
        {
            // 既定＝100×0.6/0.8＝75（これより長く奪われる見込みなら焼け）
            Assert.AreEqual(75f, ScorchedEarthRules.BreakEvenTime(P), 1e-3f);
            // 徹底度ゼロ（何も拒否できない）は永遠に元が取れない
            var noDenial = new ScorchedEarthParams(0f, 0.5f, 0.6f, 0.7f, 1f, 0.6f, 100f);
            Assert.IsTrue(float.IsPositiveInfinity(ScorchedEarthRules.BreakEvenTime(noDenial)));
            // 復興費比が徹底度を上回る世界でも焼き得は来ない
            var costly = new ScorchedEarthParams(0.5f, 0.5f, 0.6f, 0.7f, 1f, 0.9f, 100f);
            Assert.IsTrue(float.IsPositiveInfinity(ScorchedEarthRules.BreakEvenTime(costly)));
        }
    }
}
