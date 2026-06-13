using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人質外交を固定する：価値は階級tier×血縁、譲歩は非情さで満額（温厚は下限まで逓減）、
    /// 処刑は外聞コスト＋交渉力全喪失＝保持し続けるより常に損（生かしてこそ価値）、
    /// 救出は精鋭×警備の決定論roll、価値は時間で風化。境界とクランプを担保。
    /// </summary>
    public class HostageRulesTests
    {
        private static readonly HostageParams P = HostageParams.Default;
        // tier段差+50%/基準1/血縁倍率2/非情さ下限0.5/処刑コスト1.5/風化0.1

        [Test]
        public void HostageValue_RankAndBloodTie()
        {
            Assert.AreEqual(1f, HostageRules.HostageValue(0, 0f, P), 1e-5f);   // 無位・無縁
            Assert.AreEqual(6f, HostageRules.HostageValue(10, 0f, P), 1e-5f);  // 元帥＝1+10×0.5
            Assert.AreEqual(3f, HostageRules.HostageValue(0, 1f, P), 1e-5f);   // 君主の実子＝1×(1+2)
            Assert.AreEqual(7f, HostageRules.HostageValue(5, 0.5f, P), 1e-5f); // 准将×血縁0.5＝3.5×2
            // クランプ：負tierは0扱い・血縁は0..1
            Assert.AreEqual(1f, HostageRules.HostageValue(-3, 0f, P), 1e-5f);
            Assert.AreEqual(3f, HostageRules.HostageValue(0, 2f, P), 1e-5f);
        }

        [Test]
        public void ConcessionPressure_RuthlessnessCredibility()
        {
            Assert.AreEqual(10f, HostageRules.ConcessionPressure(10f, 1f, P), 1e-5f); // 非情＝満額
            Assert.AreEqual(5f, HostageRules.ConcessionPressure(10f, 0f, P), 1e-5f);  // 温厚＝下限0.5まで
            Assert.AreEqual(7.5f, HostageRules.ConcessionPressure(10f, 0.5f, P), 1e-5f);
            Assert.AreEqual(0f, HostageRules.ConcessionPressure(-5f, 1f, P), 1e-5f);  // 負価値は0
        }

        [Test]
        public void ExecutionCost_HigherValueLooksMoreBarbaric()
        {
            Assert.AreEqual(15f, HostageRules.ExecutionCost(10f, P), 1e-5f); // 価値×1.5
            Assert.AreEqual(1.5f, HostageRules.ExecutionCost(1f, P), 1e-5f);
            Assert.AreEqual(0f, HostageRules.ExecutionCost(-1f, P), 1e-5f);
        }

        [Test]
        public void Execution_AlwaysWorseThanKeeping()
        {
            // 殺せば材料（価値全額）＋外聞を失う＝総損失は保持価値を常に上回る＝生かしてこそ価値
            float value = HostageRules.HostageValue(8, 1f, P); // 大将×実子＝5×3=15
            Assert.AreEqual(15f, value, 1e-4f);
            float totalLoss = HostageRules.ExecutionCost(value, P) + HostageRules.ExecutionLeverageLoss(value);
            Assert.AreEqual(37.5f, totalLoss, 1e-4f);
            Assert.Greater(totalLoss, value);
        }

        [Test]
        public void RescueChance_SecurityVsElite()
        {
            Assert.AreEqual(0.4f, HostageRules.RescueChance(0.5f, 0.8f, P), 1e-5f); // 0.8×(1-0.5)
            Assert.AreEqual(0f, HostageRules.RescueChance(1f, 1f, P), 1e-5f);       // 完全警備は破れない
            Assert.AreEqual(0f, HostageRules.RescueChance(0f, 0f, P), 1e-5f);       // 部隊なしは届かない
            Assert.AreEqual(1f, HostageRules.RescueChance(-1f, 2f, P), 1e-5f);      // クランプ
        }

        [Test]
        public void RescueSucceeds_DeterministicRoll()
        {
            // 成功率0.4：roll が下回れば成功
            Assert.IsTrue(HostageRules.RescueSucceeds(0.5f, 0.8f, 0.39f, P));
            Assert.IsFalse(HostageRules.RescueSucceeds(0.5f, 0.8f, 0.4f, P));
            // 成功率0は roll=0 でも失敗
            Assert.IsFalse(HostageRules.RescueSucceeds(1f, 1f, 0f, P));
        }

        [Test]
        public void ValueDecay_PublicForgets()
        {
            Assert.AreEqual(5f, HostageRules.ValueDecay(10f, 10f, P), 1e-4f); // 10秒で半減（0.1/秒）
            Assert.AreEqual(10f, HostageRules.ValueDecay(10f, 0f, P), 1e-5f); // 経過なしは不変
            Assert.AreEqual(10f, HostageRules.ValueDecay(10f, -5f, P), 1e-5f); // 負dtは変化なし
            Assert.Greater(HostageRules.ValueDecay(10f, 1000f, P), 0f);        // 0へ漸近・負にならない
        }
    }
}
