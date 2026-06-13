using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 商人の信用・為替手形・レバレッジ・破産（#1077・狼と香辛料）を固定する：取引成功で信用が積み失敗で速く崩れ、
    /// 信用と担保が与信枠を生み、信用ある商人の手形は額面に近く・信用なき手形は割り引かれ、てこは儲けも損も倍にし
    /// （両刃）、過大なレバレッジ×変動が一撃破産を招く（信用取引の崖）。既定Paramsで期待値を固定。
    /// </summary>
    public class MerchantCreditRulesTests
    {
        private static readonly MerchantCreditParams P = MerchantCreditParams.Default;
        // 信用構築0.1/失墜0.2/与信1.0/割引0.5/時間割引0.05/安全てこ2.0/破滅てこ5.0

        [Test]
        public void CreditworthinessTick_SuccessBuildsFailureErodesFaster()
        {
            // 成功：0.5＋0.1×1＝0.6（実績が信用を作る）
            Assert.AreEqual(0.6f, MerchantCreditRules.CreditworthinessTick(0.5f, true, 1f, P), 1e-4f);
            // 失敗：0.5−0.2×1＝0.3（崩れる方が速い＝erode＞build）
            Assert.AreEqual(0.3f, MerchantCreditRules.CreditworthinessTick(0.5f, false, 1f, P), 1e-4f);
            // 信用は0..1にクランプ（満点を超えない・地を割らない）
            Assert.AreEqual(1f, MerchantCreditRules.CreditworthinessTick(0.95f, true, 1f, P), 1e-4f);
            Assert.AreEqual(0f, MerchantCreditRules.CreditworthinessTick(0.1f, false, 1f, P), 1e-4f);
        }

        [Test]
        public void CreditLimit_NeedsBothCreditAndCollateral()
        {
            // 信用0.8×担保1000×倍率1.0＝800
            Assert.AreEqual(800f, MerchantCreditRules.CreditLimit(0.8f, 1000f, P), 1e-3f);
            // 信用が高いほど同じ担保で多く借りられる＝信用は元手
            Assert.Greater(MerchantCreditRules.CreditLimit(0.9f, 1000f, P), MerchantCreditRules.CreditLimit(0.3f, 1000f, P));
            // 信用ゼロでは借りられない（積＝両方が要る）
            Assert.AreEqual(0f, MerchantCreditRules.CreditLimit(0f, 1000f, P), 1e-4f);
            // 担保ゼロでも借りられない
            Assert.AreEqual(0f, MerchantCreditRules.CreditLimit(0.8f, 0f, P), 1e-4f);
        }

        [Test]
        public void BillOfExchangeValue_CreditWorthyNearFaceUnworthyDiscounted()
        {
            // 信用1.0・満期0：額面1000がそのまま通る（信用ある手形は額面に近い）
            Assert.AreEqual(1000f, MerchantCreditRules.BillOfExchangeValue(1000f, 1f, 0f, P), 1e-3f);
            // 信用0・満期0：割引0.5ぶん削られ500（信用なき手形は買い叩かれる）
            Assert.AreEqual(500f, MerchantCreditRules.BillOfExchangeValue(1000f, 0f, 0f, P), 1e-3f);
            // 信用が高いほど手形価値が高い＝信用割引
            Assert.Greater(
                MerchantCreditRules.BillOfExchangeValue(1000f, 0.9f, 0f, P),
                MerchantCreditRules.BillOfExchangeValue(1000f, 0.3f, 0f, P));
        }

        [Test]
        public void BillOfExchangeValue_FurtherMaturityWorthLess()
        {
            // 信用1.0・満期10：時間割引0.05×10＝0.5ぶん削られ1000×0.5＝500（先の手形ほど現在価値が下がる）
            Assert.AreEqual(500f, MerchantCreditRules.BillOfExchangeValue(1000f, 1f, 10f, P), 1e-3f);
            // 満期が近いほど価値が高い
            Assert.Greater(
                MerchantCreditRules.BillOfExchangeValue(1000f, 1f, 2f, P),
                MerchantCreditRules.BillOfExchangeValue(1000f, 1f, 8f, P));
        }

        [Test]
        public void Leverage_BorrowingMultipliesExposure()
        {
            // 借入0・自己資本1000＝てこなし1.0
            Assert.AreEqual(1f, MerchantCreditRules.Leverage(0f, 1000f), 1e-4f);
            // 借入1000・自己資本1000＝(1000+1000)/1000＝2.0
            Assert.AreEqual(2f, MerchantCreditRules.Leverage(1000f, 1000f), 1e-4f);
            // 借入4000・自己資本1000＝5.0（深いてこ）
            Assert.AreEqual(5f, MerchantCreditRules.Leverage(4000f, 1000f), 1e-4f);
            // 自己資本ゼロで借入ありは破滅的なてこ（∞）
            Assert.IsTrue(float.IsPositiveInfinity(MerchantCreditRules.Leverage(1000f, 0f)));
        }

        [Test]
        public void LeveragedReturn_DoubleEdged()
        {
            // てこ2.0×素のリターン+0.1＝+0.2（儲けが倍増）
            Assert.AreEqual(0.2f, MerchantCreditRules.LeveragedReturn(2f, 0.1f), 1e-4f);
            // てこ2.0×素の損−0.1＝−0.2（損も倍＝両刃）
            Assert.AreEqual(-0.2f, MerchantCreditRules.LeveragedReturn(2f, -0.1f), 1e-4f);
            // てこが深いほど損益が拡大する
            Assert.Greater(MerchantCreditRules.LeveragedReturn(5f, 0.1f), MerchantCreditRules.LeveragedReturn(2f, 0.1f));
            Assert.Less(MerchantCreditRules.LeveragedReturn(5f, -0.1f), MerchantCreditRules.LeveragedReturn(2f, -0.1f));
        }

        [Test]
        public void BankruptcyRisk_OverLeverageWithVolatilityRuins()
        {
            // 安全てこ2.0以下は変動があっても破産しない（崖の手前）
            Assert.AreEqual(0f, MerchantCreditRules.BankruptcyRisk(2f, 1f, P), 1e-4f);
            // てこ3.5（安全2/破滅5の中間）×変動1.0＝0.5
            Assert.AreEqual(0.5f, MerchantCreditRules.BankruptcyRisk(3.5f, 1f, P), 1e-4f);
            // 破滅てこ5.0×変動1.0＝1.0（一撃破産）
            Assert.AreEqual(1f, MerchantCreditRules.BankruptcyRisk(5f, 1f, P), 1e-4f);
            // 高てこでも変動ゼロ（穏やかな取引）なら破産しない＝積＝両方が要る
            Assert.AreEqual(0f, MerchantCreditRules.BankruptcyRisk(5f, 0f, P), 1e-4f);
            // てこが深いほど破産しやすい
            Assert.Greater(MerchantCreditRules.BankruptcyRisk(4.5f, 0.8f, P), MerchantCreditRules.BankruptcyRisk(2.5f, 0.8f, P));
        }

        [Test]
        public void IsBankrupt_DeterministicByRoll()
        {
            // リスク0.6＞roll0.5＝破産
            Assert.IsTrue(MerchantCreditRules.IsBankrupt(0.6f, 0.5f));
            // リスク0.4＜roll0.5＝持ちこたえる
            Assert.IsFalse(MerchantCreditRules.IsBankrupt(0.4f, 0.5f));
            // リスク0で破産せず
            Assert.IsFalse(MerchantCreditRules.IsBankrupt(0f, 0f));
        }
    }
}
