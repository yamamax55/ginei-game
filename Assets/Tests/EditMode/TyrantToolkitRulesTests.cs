using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>僭主維持術カタログ（ARIS-5 #1504・アリストテレス）の純ロジック検証。</summary>
    public class TyrantToolkitRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>短期維持＝手法ごとの重み×強度×shortTermScale(0.6)。傑物排除(重み1.0)は最も即効。</summary>
        [Test]
        public void ShortTermControl_傑物排除は最も即効()
        {
            // 傑物排除：1.0×0.5×0.6=0.3 ／ 大型事業：0.7×0.5×0.6=0.21
            Assert.AreEqual(0.3f, TyrantToolkitRules.ShortTermControl(TyrantTactic.傑物排除, 0.5f), Eps);
            Assert.AreEqual(0.21f, TyrantToolkitRules.ShortTermControl(TyrantTactic.大型事業, 0.5f), Eps);
            Assert.Greater(
                TyrantToolkitRules.ShortTermControl(TyrantTactic.傑物排除, 0.5f),
                TyrantToolkitRules.ShortTermControl(TyrantTactic.大型事業, 0.5f));
        }

        /// <summary>長期疲弊＝手法ごとの重み×強度×longTermScale(0.7)。傑物排除(重み1.0)が最も深く国を空洞化させる。</summary>
        [Test]
        public void LongTermDecay_傑物排除が最も国を蝕む()
        {
            // 傑物排除：1.0×1.0×0.7=0.7 ／ 分断統治：0.6×1.0×0.7=0.42
            Assert.AreEqual(0.7f, TyrantToolkitRules.LongTermDecay(TyrantTactic.傑物排除, 1f), Eps);
            Assert.AreEqual(0.42f, TyrantToolkitRules.LongTermDecay(TyrantTactic.分断統治, 1f), Eps);
            Assert.Greater(
                TyrantToolkitRules.LongTermDecay(TyrantTactic.傑物排除, 1f),
                TyrantToolkitRules.LongTermDecay(TyrantTactic.分断統治, 1f));
        }

        /// <summary>傑物排除＝脅威は消えるが人材を失う（出る杭を切る代償）。</summary>
        [Test]
        public void TallPoppyElimination_脅威を消すが人材を失う()
        {
            // 脅威0.8×強度0.5=0.4が抑制／人材毀損=0.5×talentLossScale(0.5)=0.25
            float talentLoss = TyrantToolkitRules.TallPoppyElimination(0.8f, 0.5f, out float suppressed);
            Assert.AreEqual(0.4f, suppressed, Eps);
            Assert.AreEqual(0.25f, talentLoss, Eps);
        }

        /// <summary>貧困化＝重税で富を削り反抗の余力を奪う。富が0なら奪う余地なし。</summary>
        [Test]
        public void ImpoverishmentControl_重税が反抗の余力を奪う()
        {
            // 富0.8×税0.5=0.4 ×重み(0.9)×shortTermScale(0.6)=0.216
            Assert.AreEqual(0.216f, TyrantToolkitRules.ImpoverishmentControl(0.8f, 0.5f), Eps);
            // 富がなければ奪う余地もない
            Assert.AreEqual(0f, TyrantToolkitRules.ImpoverishmentControl(0f, 1f), Eps);
        }

        /// <summary>大型事業＝壮麗さと財政浪費の相乗で気を逸らし疲弊させる（暴政版パンとサーカス）。</summary>
        [Test]
        public void GrandProjectDistraction_壮麗さと浪費の相乗()
        {
            // (壮麗0.6+浪費0.4)/2=0.5 ×重み(0.7)×shortTermScale(0.6)=0.21
            Assert.AreEqual(0.21f, TyrantToolkitRules.GrandProjectDistraction(0.6f, 0.4f), Eps);
        }

        /// <summary>密告網＝報酬に比例して相互不信を生み団結を防ぐ。</summary>
        [Test]
        public void InformantNetwork_相互不信が団結を防ぐ()
        {
            // 報酬0.5×重み(0.8)×shortTermScale(0.6)=0.24
            Assert.AreEqual(0.24f, TyrantToolkitRules.InformantNetwork(0.5f), Eps);
        }

        /// <summary>分断統治＝派閥分断に比例して連帯を妨げる。</summary>
        [Test]
        public void DivideAndRule_分断が連帯を妨げる()
        {
            // 分断0.5×重み(0.8)×shortTermScale(0.6)=0.24
            Assert.AreEqual(0.24f, TyrantToolkitRules.DivideAndRule(0.5f), Eps);
        }

        /// <summary>純持続力＝短期維持−長期疲弊。疲弊が積もれば自国を食い潰し負へ転じる。</summary>
        [Test]
        public void NetTyrannyDurability_疲弊が積もれば負へ転じる()
        {
            Assert.AreEqual(0.2f, TyrantToolkitRules.NetTyrannyDurability(0.5f, 0.3f), Eps);
            // 長期疲弊が短期維持を上回ると純持続力は負＝国を食い潰す
            Assert.Less(TyrantToolkitRules.NetTyrannyDurability(0.3f, 0.7f), 0f);
        }

        /// <summary>空洞化判定＝累積した長期疲弊が閾値(0.5)を超えると国が空洞化したとみなす。</summary>
        [Test]
        public void IsHollowingState_疲弊が閾値を超えると空洞化()
        {
            Assert.IsFalse(TyrantToolkitRules.IsHollowingState(0.4f));
            Assert.IsTrue(TyrantToolkitRules.IsHollowingState(0.5f));
            Assert.IsTrue(TyrantToolkitRules.IsHollowingState(0.8f));
        }
    }
}
