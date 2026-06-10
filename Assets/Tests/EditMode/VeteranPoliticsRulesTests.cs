using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 退役軍人の政治力を固定する：戦友会の組織率が圧力団体の実効規模を決め（未組織でも下限が残る）、
    /// 恩給は財政を食い、冷遇は傷痍軍人比で深まる不満になり、組織された不満が街頭動員力になる。
    /// 厚遇⇔冷遇のトレードオフ（財政負担と街頭不満は両立しない）と全クランプを担保。
    /// </summary>
    public class VeteranPoliticsRulesTests
    {
        private static readonly VeteranPoliticsParams P = VeteranPoliticsParams.Default; // 組織下限0.2/恩給スケール0.5/不満基礎0.5/傷痍加重0.5/忠誠配当上限0.2

        [Test]
        public void VeteranBlocSize_ScalesWithOrganization()
        {
            Assert.AreEqual(0.18f, VeteranPoliticsRules.VeteranBlocSize(0.3f, 0.5f, P), 1e-5f); // 0.3×(0.2+0.8×0.5)
            Assert.AreEqual(0.06f, VeteranPoliticsRules.VeteranBlocSize(0.3f, 0f, P), 1e-5f);   // 未組織でも下限の重みは残る
            Assert.AreEqual(0.3f, VeteranPoliticsRules.VeteranBlocSize(0.3f, 1f, P), 1e-5f);    // 完全組織＝人口比そのまま
            Assert.AreEqual(1f, VeteranPoliticsRules.VeteranBlocSize(2f, 5f, P), 1e-5f);        // 入力クランプ
        }

        [Test]
        public void PensionBurden_GenerosityCostsMoney()
        {
            Assert.AreEqual(0.12f, VeteranPoliticsRules.PensionBurden(0.3f, 0.8f, P), 1e-5f); // 0.3×0.8×0.5
            Assert.AreEqual(0f, VeteranPoliticsRules.PensionBurden(0.3f, 0f, P), 1e-5f);      // 無支給＝負担ゼロ
            Assert.AreEqual(0.5f, VeteranPoliticsRules.PensionBurden(1f, 1f, P), 1e-5f);      // 満額×全人口＝スケール上限
        }

        [Test]
        public void NeglectGrievance_DeepensWithWounded()
        {
            Assert.AreEqual(0.56f, VeteranPoliticsRules.NeglectGrievance(0.2f, 0.4f, P), 1e-5f); // (1-0.2)×(0.5+0.5×0.4)
            Assert.AreEqual(0f, VeteranPoliticsRules.NeglectGrievance(1f, 1f, P), 1e-5f);        // 満額支給＝不満なし
            Assert.AreEqual(1f, VeteranPoliticsRules.NeglectGrievance(0f, 1f, P), 1e-5f);        // 傷痍軍人を全面冷遇＝不満最大
            Assert.AreEqual(0.5f, VeteranPoliticsRules.NeglectGrievance(0f, 0f, P), 1e-5f);      // 健常でも冷遇は基礎分の不満
        }

        [Test]
        public void StreetPower_OrganizedGrievanceMobilizes()
        {
            Assert.AreEqual(0.1008f, VeteranPoliticsRules.StreetPower(0.18f, 0.56f), 1e-5f); // 規模×不満
            Assert.AreEqual(0f, VeteranPoliticsRules.StreetPower(0f, 1f), 1e-5f);            // 退役兵なし＝街頭もなし
            Assert.AreEqual(0f, VeteranPoliticsRules.StreetPower(0.5f, 0f), 1e-5f);          // 不満なし＝動員されない
        }

        [Test]
        public void MilitarismLobby_NostalgiaBecomesPressure()
        {
            Assert.AreEqual(0.18f, VeteranPoliticsRules.MilitarismLobby(0.3f, 0.6f), 1e-5f); // 規模×武勇の記憶
            Assert.AreEqual(0f, VeteranPoliticsRules.MilitarismLobby(0.3f, 0f), 1e-5f);      // 記憶が薄れれば圧力も消える
        }

        [Test]
        public void LoyaltyDividend_GenerosityPaysBackInLoyalty()
        {
            Assert.AreEqual(0.16f, VeteranPoliticsRules.LoyaltyDividend(0.8f, P), 1e-5f); // 0.8×0.2
            Assert.AreEqual(0.2f, VeteranPoliticsRules.LoyaltyDividend(1f, P), 1e-5f);    // 満額＝配当上限
            Assert.AreEqual(0f, VeteranPoliticsRules.LoyaltyDividend(0f, P), 1e-5f);      // 兵を捨てる国に配当なし
            Assert.AreEqual(0.2f, VeteranPoliticsRules.LoyaltyDividend(2f, P), 1e-5f);    // 入力クランプ
        }

        [Test]
        public void TradeOff_GenerousPaysInTreasury_StingyPaysInStreets()
        {
            // 同条件（人口比0.3・組織率0.8・傷痍0.5）で厚遇(0.9)と冷遇(0.1)を比較
            float bloc = VeteranPoliticsRules.VeteranBlocSize(0.3f, 0.8f, P);
            float burdenGenerous = VeteranPoliticsRules.PensionBurden(0.3f, 0.9f, P);
            float burdenStingy = VeteranPoliticsRules.PensionBurden(0.3f, 0.1f, P);
            float streetGenerous = VeteranPoliticsRules.StreetPower(bloc, VeteranPoliticsRules.NeglectGrievance(0.9f, 0.5f, P));
            float streetStingy = VeteranPoliticsRules.StreetPower(bloc, VeteranPoliticsRules.NeglectGrievance(0.1f, 0.5f, P));
            Assert.Greater(burdenGenerous, burdenStingy);   // 厚遇は財政を食う
            Assert.Less(streetGenerous, streetStingy);      // 冷遇は街頭に出る＝どちらかで必ず払う
            Assert.Greater(VeteranPoliticsRules.LoyaltyDividend(0.9f, P),
                VeteranPoliticsRules.LoyaltyDividend(0.1f, P)); // 厚遇は現役の忠誠で返ってくる
        }
    }
}
