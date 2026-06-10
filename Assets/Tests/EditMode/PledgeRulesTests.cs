using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 個人結盟・盟誓（#1105・桃園結義型）を固定する：公的に立てた誓い（証人あり）ほど拘束力が重く、
    /// 共に潜った修羅場が拘束力を固め、誓いを破れば天下の信を失う（名声失墜＝裏切り者の烙印）、守れば義に
    /// 厚い者として慕われる。盟友の危機には駆けつける義務が生じ、義兄弟は盟主と運命を共にする（忠誠連鎖）。
    /// 既定Paramsの具体値で期待値を固定。
    /// </summary>
    public class PledgeRulesTests
    {
        private static readonly PledgeParams P = PledgeParams.Default;

        [Test]
        public void PledgeStrength_PublicVowBindsHarder()
        {
            // 義兄弟・誠意満点・私的(口約束)＝1.0×1=1.0
            Assert.AreEqual(1f, PledgeRules.PledgeStrength(PledgeType.義兄弟, 1f, false, P), 1e-5f);
            // 共闘の誓約・誠意満点・私的＝1.0×0.5=0.5（軽い契り）
            Assert.AreEqual(0.5f, PledgeRules.PledgeStrength(PledgeType.共闘の誓約, 1f, false, P), 1e-5f);
            // 公的に立てた誓い＝天地に誓った重み：誠意0.5×重み0.5×証人1.5=0.375
            Assert.AreEqual(0.375f, PledgeRules.PledgeStrength(PledgeType.共闘の誓約, 0.5f, true, P), 1e-5f);
            // 証人ありで上限1にクランプ（義兄弟・誠意満点×1.5は天井）
            Assert.AreEqual(1f, PledgeRules.PledgeStrength(PledgeType.義兄弟, 1f, true, P), 1e-5f);
        }

        [Test]
        public void BindingForce_StrengthenedBySharedAdversity()
        {
            // 修羅場なし＝拘束力そのまま
            Assert.AreEqual(0.5f, PledgeRules.BindingForce(0.5f, 0f, P), 1e-5f);
            // 苦楽を共にした誓いは固い：0.5×(1+0.5×1)=0.75
            Assert.AreEqual(0.75f, PledgeRules.BindingForce(0.5f, 1f, P), 1e-5f);
            // 上限1にクランプ
            Assert.AreEqual(1f, PledgeRules.BindingForce(1f, 1f, P), 1e-5f);
        }

        [Test]
        public void BetrayalPenalty_HeavyVowCostsMore()
        {
            // 裏切り者の烙印：義兄弟の誓い破棄＝1.0×重み1×0.8=0.8
            Assert.AreEqual(0.8f, PledgeRules.BetrayalPenalty(1f, PledgeType.義兄弟, P), 1e-5f);
            // 共闘の誓約破棄＝1.0×重み0.5×0.8=0.4（軽い契りの破棄は代償も軽い）
            Assert.AreEqual(0.4f, PledgeRules.BetrayalPenalty(1f, PledgeType.共闘の誓約, P), 1e-5f);
            // 義兄弟の代償は共闘の2倍
            Assert.AreEqual(2f,
                PledgeRules.BetrayalPenalty(1f, PledgeType.義兄弟, P) /
                PledgeRules.BetrayalPenalty(1f, PledgeType.共闘の誓約, P), 1e-4f);
        }

        [Test]
        public void HonorBonus_OnlyForUpholdingTheVow()
        {
            // 関羽の義：守れば義に厚い者として名声＝1.0×0.4=0.4
            Assert.AreEqual(0.4f, PledgeRules.HonorBonus(1f, true, P), 1e-5f);
            // 破った者に名声ボーナスは無い
            Assert.AreEqual(0f, PledgeRules.HonorBonus(1f, false, P), 1e-5f);
        }

        [Test]
        public void PledgeObligation_DutyArisesWhenAllyInPeril()
        {
            // 盟友が危機なら駆けつける義務（義兄弟＝重み1）
            Assert.AreEqual(1f, PledgeRules.PledgeObligation(PledgeType.義兄弟, true), 1e-5f);
            // 危機でなければ義務は生じない
            Assert.AreEqual(0f, PledgeRules.PledgeObligation(PledgeType.義兄弟, false), 1e-5f);
            // 軽い契りは義務も軽い（共闘＝0.5）
            Assert.AreEqual(0.5f, PledgeRules.PledgeObligation(PledgeType.共闘の誓約, true), 1e-5f);
        }

        [Test]
        public void CascadingLoyalty_GroupSharesLeadersFate()
        {
            // 義兄弟は離れない：盟主が義を貫けば(act=1) 連鎖最大＝1.0×0.6×1=0.6
            Assert.AreEqual(0.6f, PledgeRules.CascadingLoyalty(1f, 1f, P), 1e-5f);
            // 盟主が不義に走れば(act=0) 連鎖は消える
            Assert.AreEqual(0f, PledgeRules.CascadingLoyalty(1f, 0f, P), 1e-5f);
            // 半端な行い＝0.5×0.6×0.5=0.15
            Assert.AreEqual(0.15f, PledgeRules.CascadingLoyalty(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void Params_CtorClampsToValidRange()
        {
            // 証人倍率は1未満不可（公的な誓いが拘束力を下げてはならない）
            var p = new PledgeParams(0.5f, -1f, -2f, -0.5f, -0.1f);
            Assert.AreEqual(1f, p.witnessMultiplier, 1e-5f);
            Assert.AreEqual(0f, p.adversityScale, 1e-5f);
            Assert.AreEqual(0f, p.betrayalPenaltyScale, 1e-5f);
            Assert.AreEqual(0f, p.honorBonusScale, 1e-5f);
            Assert.AreEqual(0f, p.cascadeScale, 1e-5f);
        }
    }
}
