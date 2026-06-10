using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// クーデター純ロジック（#215-219）を固定する：忠誠/支持が低いほど発生確率が上がること（主体ごとの主因の重み）、
    /// roll による帰結（成功/粛清/内戦）の決定論的境界、事後正統性が帰結ごとに妥当な向きへ動くこと。全て 0..1 にクランプ。
    /// </summary>
    public class CoupRulesTests
    {
        // --- CoupSuccessChance ---

        [Test]
        public void CoupSuccessChance_LowLoyaltyAndSupport_HighChance()
        {
            // 忠誠も支持も低い＝不満最大＝確率≒1
            float c = CoupRules.CoupSuccessChance(0f, 0f, CoupType.軍部);
            Assert.AreEqual(1f, c, 1e-4f);
        }

        [Test]
        public void CoupSuccessChance_FullLoyaltyAndSupport_ZeroChance()
        {
            // 忠誠も支持も満点＝不満なし＝確率0
            float c = CoupRules.CoupSuccessChance(1f, 1f, CoupType.宮廷);
            Assert.AreEqual(0f, c, 1e-4f);
        }

        [Test]
        public void CoupSuccessChance_MilitaryCoup_LoyaltyDriven()
        {
            // 軍部クーデターは軍の忠誠が主因＝忠誠だけ落ちると支持だけ落とすより高い
            float byLoyalty = CoupRules.CoupSuccessChance(0f, 1f, CoupType.軍部);
            float bySupport = CoupRules.CoupSuccessChance(1f, 0f, CoupType.軍部);
            Assert.Greater(byLoyalty, bySupport);
        }

        [Test]
        public void CoupSuccessChance_Revolution_SupportDriven()
        {
            // 革命は支持崩壊が主因＝支持だけ落とす方が高い
            float byLoyalty = CoupRules.CoupSuccessChance(0f, 1f, CoupType.革命);
            float bySupport = CoupRules.CoupSuccessChance(1f, 0f, CoupType.革命);
            Assert.Greater(bySupport, byLoyalty);
        }

        [Test]
        public void CoupSuccessChance_ClampsOutOfRangeInputs()
        {
            // 範囲外入力でも 0..1 に収まる
            float lo = CoupRules.CoupSuccessChance(5f, 5f, CoupType.宮廷);   // 過大忠誠/支持→0
            float hi = CoupRules.CoupSuccessChance(-5f, -5f, CoupType.宮廷); // 過小→1
            Assert.AreEqual(0f, lo, 1e-4f);
            Assert.AreEqual(1f, hi, 1e-4f);
        }

        // --- Resolve ---

        [Test]
        public void Resolve_RollBelowChance_Success()
        {
            // roll < chance ＝成功
            Assert.AreEqual(CoupOutcome.成功, CoupRules.Resolve(0.6f, 0.3f));
        }

        [Test]
        public void Resolve_RollAtChance_NotSuccess()
        {
            // roll == chance は成功域に含まれない（< 判定）＝未遂側
            Assert.AreNotEqual(CoupOutcome.成功, CoupRules.Resolve(0.5f, 0.5f));
        }

        [Test]
        public void Resolve_TopOfFailBand_CivilWar()
        {
            // chance=0.5・既定civilWarShare=0.4 → 未遂帯[0.5,1]の上端0.2が内戦＝roll=0.95は内戦
            Assert.AreEqual(CoupOutcome.内戦, CoupRules.Resolve(0.5f, 0.95f));
        }

        [Test]
        public void Resolve_MiddleOfFailBand_Purge()
        {
            // 未遂帯の下寄り＝粛清（rollがちょうどchanceでも粛清側）
            Assert.AreEqual(CoupOutcome.粛清, CoupRules.Resolve(0.5f, 0.6f));
            Assert.AreEqual(CoupOutcome.粛清, CoupRules.Resolve(0.5f, 0.5f));
        }

        [Test]
        public void Resolve_ChanceOne_AlwaysSuccess()
        {
            // 確率1なら roll に関わらず成功（roll<1 は常に成立、1は境界でも成功優先）
            Assert.AreEqual(CoupOutcome.成功, CoupRules.Resolve(1f, 0.99f));
            Assert.AreEqual(CoupOutcome.成功, CoupRules.Resolve(1f, 0f));
        }

        [Test]
        public void Resolve_ClampsRollAndChance()
        {
            // 範囲外でもクランプして決定論的（chance>1→成功、chance<0かつroll>0→未遂）
            Assert.AreEqual(CoupOutcome.成功, CoupRules.Resolve(2f, 0.5f));
            Assert.AreEqual(CoupOutcome.内戦, CoupRules.Resolve(-1f, 5f)); // chance0・roll1→内戦帯上端
        }

        // --- PostCoupLegitimacy ---

        [Test]
        public void PostCoupLegitimacy_Success_AboveBase()
        {
            // 成功＝基礎正統性以上（支持があるほど上振れ）
            float low = CoupRules.PostCoupLegitimacy(CoupOutcome.成功, 0f);
            float high = CoupRules.PostCoupLegitimacy(CoupOutcome.成功, 1f);
            Assert.AreEqual(CoupRules.CoupParams.Default.postCoupBaseLegitimacy, low, 1e-4f);
            Assert.AreEqual(1f, high, 1e-4f);
            Assert.Greater(high, low);
        }

        [Test]
        public void PostCoupLegitimacy_CivilWar_LowestForGivenSupport()
        {
            // 内戦は分裂＝同じ支持なら最も低い
            float support = 0.6f;
            float civil = CoupRules.PostCoupLegitimacy(CoupOutcome.内戦, support);
            float purge = CoupRules.PostCoupLegitimacy(CoupOutcome.粛清, support);
            float success = CoupRules.PostCoupLegitimacy(CoupOutcome.成功, support);
            Assert.Less(civil, purge);
            Assert.Less(civil, success);
        }

        [Test]
        public void PostCoupLegitimacy_Purge_NotBelowExistingSupport()
        {
            // 粛清＝引き締めで既存支持を割らない
            float support = 0.4f;
            float purge = CoupRules.PostCoupLegitimacy(CoupOutcome.粛清, support);
            Assert.GreaterOrEqual(purge, support);
        }

        [Test]
        public void PostCoupLegitimacy_ClampsSupport()
        {
            // 範囲外支持でも 0..1 に収まる
            float r = CoupRules.PostCoupLegitimacy(CoupOutcome.内戦, 9f);
            Assert.GreaterOrEqual(r, 0f);
            Assert.LessOrEqual(r, 1f);
        }
    }
}
