using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政体駆動（C1 Tier A・軍政#145→政変#215→政体#117）の純ロジックを固定する：
    /// クーデター発火（統制/支持が弱いと起きる）／主体選択（革命/軍部/宮廷）／成功で政体転換／粛清・内戦の帰結。
    /// </summary>
    public class PoliticalUpheavalRulesTests
    {
        private static CoupContext Ctx(CivilianControlType ctrl, float strength, float support, float incl = 0.5f, bool defeat = false)
            => new CoupContext(ctrl, strength, support, defeat, incl);

        // ===== 主体選択 =====

        [Test]
        public void ChooseCoupType_BySupportAndControl()
        {
            Assert.AreEqual(CoupType.革命, PoliticalUpheavalRules.ChooseCoupType(Ctx(CivilianControlType.君主統帥, 0.5f, 0.2f)));   // 支持崩壊
            Assert.AreEqual(CoupType.軍部, PoliticalUpheavalRules.ChooseCoupType(Ctx(CivilianControlType.軍部優位, 0.5f, 0.5f)));   // 軍優位
            Assert.AreEqual(CoupType.軍部, PoliticalUpheavalRules.ChooseCoupType(Ctx(CivilianControlType.未分化, 0.5f, 0.5f)));     // 首長制
            Assert.AreEqual(CoupType.宮廷, PoliticalUpheavalRules.ChooseCoupType(Ctx(CivilianControlType.文民統制, 0.5f, 0.5f)));   // ほか
        }

        // ===== 政変後の形態 =====

        [Test]
        public void FormAfterCoup_ByType()
        {
            Assert.AreEqual(GovernmentForm.指導者独裁, PoliticalUpheavalRules.FormAfterCoup(GovernmentForm.首長制, CoupType.軍部, 0.5f));
            Assert.AreEqual(GovernmentForm.共産主義, PoliticalUpheavalRules.FormAfterCoup(GovernmentForm.君主制, CoupType.革命, 0.3f)); // 収奪的→共産
            Assert.AreEqual(GovernmentForm.共和制, PoliticalUpheavalRules.FormAfterCoup(GovernmentForm.君主制, CoupType.革命, 0.7f));   // 包摂的→共和
            Assert.AreEqual(GovernmentForm.立憲君主制, PoliticalUpheavalRules.FormAfterCoup(GovernmentForm.立憲君主制, CoupType.宮廷, 0.5f)); // 宮廷＝形態不変
        }

        // ===== 発火しない（強い統制） =====

        [Test]
        public void StableRegime_NoCoup()
        {
            var r = PoliticalUpheavalRules.ResolveUpheaval(GovernmentForm.共和制, Ctx(CivilianControlType.文民統制, 0.9f, 0.9f), 0f);
            Assert.IsFalse(r.attempted);
            Assert.IsFalse(r.formChanged);
            Assert.AreEqual(GovernmentForm.共和制, r.newForm);
        }

        // ===== 軍部クーデター成功→指導者独裁 =====

        [Test]
        public void MilitaryCoupSuccess_BecomesStrongmanRule()
        {
            var r = PoliticalUpheavalRules.ResolveUpheaval(GovernmentForm.君主制, Ctx(CivilianControlType.軍部優位, 0f, 0.3f), roll: 0f);
            Assert.IsTrue(r.attempted);
            Assert.AreEqual(CoupType.軍部, r.type);
            Assert.AreEqual(CoupOutcome.成功, r.outcome);
            Assert.IsTrue(r.formChanged);
            Assert.AreEqual(GovernmentForm.指導者独裁, r.newForm);
            Assert.AreEqual(0.58f, r.newLegitimacy, 1e-2f); // PostCoupLegitimacy(成功,0.3)=0.4+0.3×0.6
        }

        // ===== 革命成功→（収奪なら）共産主義 =====

        [Test]
        public void RevolutionSuccess_ExtractiveBecomesCommunism()
        {
            var r = PoliticalUpheavalRules.ResolveUpheaval(GovernmentForm.君主制, Ctx(CivilianControlType.軍部優位, 0f, 0.2f, incl: 0.3f), roll: 0f);
            Assert.IsTrue(r.attempted);
            Assert.AreEqual(CoupType.革命, r.type); // 支持0.2<0.3
            Assert.AreEqual(GovernmentForm.共産主義, r.newForm);
        }

        // ===== 未遂（粛清）＝体制存続・正統性引き締め =====

        [Test]
        public void FailedCoup_Suppressed_RegimeSurvives()
        {
            var r = PoliticalUpheavalRules.ResolveUpheaval(GovernmentForm.君主制, Ctx(CivilianControlType.軍部優位, 0f, 0.3f), roll: 0.95f);
            Assert.IsTrue(r.attempted);
            Assert.AreEqual(CoupOutcome.粛清, r.outcome);
            Assert.IsFalse(r.formChanged);
            Assert.AreEqual(GovernmentForm.君主制, r.newForm);
            Assert.AreEqual(0.65f, r.newLegitimacy, 1e-2f); // 0.3+0.7×0.5
        }
    }
}
