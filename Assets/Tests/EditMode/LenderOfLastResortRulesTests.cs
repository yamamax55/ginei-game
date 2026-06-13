using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>最後の貸し手＝Bagehot原則（KNDB-2 #1613）の純ロジックを既定Paramsの具体値で固定する。</summary>
    public class LenderOfLastResortRulesTests
    {
        private LenderOfLastResortParams P => LenderOfLastResortParams.Default;

        /// <summary>必要流動性＝取付けの深さ×非流動資産。0.8×0.5=0.4。</summary>
        [Test]
        public void LiquidityNeeded_取付けの深さと非流動資産の積()
        {
            Assert.AreEqual(0.4f, LenderOfLastResortRules.LiquidityNeeded(0.8f, 0.5f), 1e-4f);
            // 非流動資産が無ければ流動化できる＝緊急流動性は不要。
            Assert.AreEqual(0f, LenderOfLastResortRules.LiquidityNeeded(0.9f, 0f), 1e-4f);
        }

        /// <summary>無制限供給が必要量を満たせば取付けは沈静（=1）、不足なら供給比だけ残る。</summary>
        [Test]
        public void PanicArrest_必要量を満たせば沈静_不足なら連鎖()
        {
            // 必要0.4に対し0.4供給＝充足率1.0で完全沈静。
            Assert.AreEqual(1f, LenderOfLastResortRules.PanicArrest(0.4f, 0.4f, P), 1e-4f);
            // 必要0.4に対し0.2供給＝半分しか止まらない。
            Assert.AreEqual(0.5f, LenderOfLastResortRules.PanicArrest(0.2f, 0.4f, P), 1e-4f);
            // 危機が無ければ完全沈静。
            Assert.AreEqual(1f, LenderOfLastResortRules.PanicArrest(0f, 0f, P), 1e-4f);
        }

        /// <summary>Bagehotの罰則金利＝基準＋危機の深さ×上乗せ。基準2%＋深さ1.0×5%=7%。</summary>
        [Test]
        public void PenaltyRate_危機が深いほど高金利()
        {
            Assert.AreEqual(0.07f, LenderOfLastResortRules.PenaltyRate(0.02f, 0.05f, 1.0f), 1e-4f);
            Assert.AreEqual(0.045f, LenderOfLastResortRules.PenaltyRate(0.02f, 0.05f, 0.5f), 1e-4f);
            // 平時（深さ0）は基準金利のまま。
            Assert.AreEqual(0.02f, LenderOfLastResortRules.PenaltyRate(0.02f, 0.05f, 0f), 1e-4f);
        }

        /// <summary>優良担保ほど低い掛け目＝(1−質)。質1.0は最低掛け目5%まで、質0.3は0.7。</summary>
        [Test]
        public void CollateralHaircut_劣悪担保ほど割り引く()
        {
            Assert.AreEqual(0.7f, LenderOfLastResortRules.CollateralHaircut(0.3f, P), 1e-4f);
            // 最良担保でも最低掛け目までは割る（過信防止）。
            Assert.AreEqual(0.05f, LenderOfLastResortRules.CollateralHaircut(1.0f, P), 1e-4f);
        }

        /// <summary>救済の常態化×リスク選好がモラルハザードを育てる。0.8×0.5×0.5×1.0=0.2。</summary>
        [Test]
        public void MoralHazardBuildup_どうせ助かるが蓄積する()
        {
            Assert.AreEqual(0.2f, LenderOfLastResortRules.MoralHazardBuildup(0.8f, 0.5f, 1.0f, P), 1e-4f);
            // 救済が稀なら蓄積しない。
            Assert.AreEqual(0f, LenderOfLastResortRules.MoralHazardBuildup(0f, 0.9f, 1.0f, P), 1e-4f);
        }

        /// <summary>罰則金利と優良担保が毒消し＝高金利×良担保ほど抑制が効く（どちらか欠けると0）。</summary>
        [Test]
        public void MoralHazardMitigation_罰則金利と優良担保が抑える()
        {
            float strong = LenderOfLastResortRules.MoralHazardMitigation(0.1f, 1.0f); // 高金利×良担保
            float weak = LenderOfLastResortRules.MoralHazardMitigation(0.0f, 1.0f);   // 罰則なし
            Assert.Greater(strong, weak);
            // 罰則0.1×良担保1.0＝0.1/(0.1+0.1)×1.0=0.5。
            Assert.AreEqual(0.5f, strong, 1e-4f);
            // 担保が劣悪なら抑制は効かない。
            Assert.AreEqual(0f, LenderOfLastResortRules.MoralHazardMitigation(0.1f, 0f), 1e-4f);
        }

        /// <summary>流動性不足（救うべき）か支払不能（ゾンビ＝救うべきでない）かの弁別。</summary>
        [Test]
        public void IlliquidVsInsolvent_救うべきとゾンビを分ける()
        {
            // 優良担保で負債を覆える＝流動性不足（救える）。
            Assert.IsTrue(LenderOfLastResortRules.IlliquidVsInsolvent(0.8f, 0.5f, P));
            // 担保が劣悪かつ過負債＝支払不能（救済は損失の付け替え）。
            Assert.IsFalse(LenderOfLastResortRules.IlliquidVsInsolvent(0.2f, 0.9f, P));
            // 担保の質が閾値0.4以上なら過負債でも救済対象。
            Assert.IsTrue(LenderOfLastResortRules.IlliquidVsInsolvent(0.4f, 0.9f, P));
        }

        /// <summary>Bagehotの三原則＝高金利×優良担保×無制限の全充足。</summary>
        [Test]
        public void IsBagehotCompliant_三原則の全充足()
        {
            // 罰則金利7%＞基準2%・掛け目0.7あり・無制限＝充足。
            Assert.IsTrue(LenderOfLastResortRules.IsBagehotCompliant(0.07f, 0.02f, 0.7f, true));
            // 罰則金利なし（基準と同じ）＝選別が効かず非充足。
            Assert.IsFalse(LenderOfLastResortRules.IsBagehotCompliant(0.02f, 0.02f, 0.7f, true));
            // 無制限でない＝取付けを止めきれず非充足。
            Assert.IsFalse(LenderOfLastResortRules.IsBagehotCompliant(0.07f, 0.02f, 0.7f, false));
        }
    }
}
