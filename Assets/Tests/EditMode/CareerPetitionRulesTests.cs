using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>立身出世の追加具申：増援/恩賞/減税/名誉回復の effectKey・量。</summary>
    public class CareerPetitionRulesTests
    {
        [Test]
        public void Reinforce_EncodeDecode_AndRecommend()
        {
            Assert.AreEqual(3000, CareerPetitionRules.RecommendReinforce(12000)); // 12000×0.25
            Assert.AreEqual(0, CareerPetitionRules.RecommendReinforce(-5));
            string key = CareerPetitionRules.EncodeReinforce(3000);
            Assert.AreEqual("fleet.reinforce:3000", key);
            Assert.IsTrue(CareerPetitionRules.IsReinforce(key));
            Assert.IsTrue(CareerPetitionRules.TryDecodeReinforce(key, out int amt));
            Assert.AreEqual(3000, amt);
            Assert.IsFalse(CareerPetitionRules.TryDecodeReinforce("hr.honor", out _));
        }

        [Test]
        public void Honor_TaxCut_Clear_KeysAndAmounts()
        {
            Assert.IsTrue(CareerPetitionRules.IsHonor("hr.honor"));
            Assert.IsFalse(CareerPetitionRules.IsHonor("gov.taxcut"));
            Assert.Greater(CareerPetitionRules.HonorFameReward, 0);

            Assert.IsTrue(CareerPetitionRules.IsTaxCut("gov.taxcut"));
            Assert.AreEqual(0.02f, CareerPetitionRules.TaxCutAmount, 1e-4f);
            Assert.IsTrue(CareerPetitionRules.IsTaxHike("gov.taxhike"));
            Assert.IsFalse(CareerPetitionRules.IsTaxHike("gov.taxcut"));
            Assert.AreEqual(0.02f, CareerPetitionRules.TaxHikeAmount, 1e-4f);

            Assert.IsTrue(CareerPetitionRules.IsClear("self.clear"));
            Assert.AreEqual(20f, CareerPetitionRules.ClearGrievanceAmount, 1e-4f);
        }

        [Test]
        public void Keys_AreDistinct_AndNotConfusedWithBudgetBase()
        {
            Assert.IsFalse(CareerPetitionRules.IsReinforce("fleet.budget:0:5000"));
            Assert.IsFalse(CareerPetitionRules.IsClear(null));
            Assert.AreEqual(PetitionEffect.なし, PetitionEffectRules.Classify(CareerPetitionRules.HonorKey)); // 予算/根拠地ではない
        }
    }
}
