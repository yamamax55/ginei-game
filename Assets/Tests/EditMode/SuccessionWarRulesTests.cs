using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>継承戦争ロジック（#1095）の純ロジックテスト。後継明確で危機ゼロ・請求者拮抗で無政府化を担保。</summary>
    public class SuccessionWarRulesTests
    {
        /// <summary>明確な世継ぎ1人なら継承危機はゼロ（戦争が起きない）。</summary>
        [Test]
        public void CrisisSeverity_ClearSingleHeir_IsZero()
        {
            Assert.AreEqual(0f, SuccessionWarRules.CrisisSeverity(1, 1f), 1e-5f);
            Assert.AreEqual(0f, SuccessionWarRules.CrisisSeverity(1, 0f), 1e-5f); // 1人なら明確さ無関係に0
        }

        /// <summary>請求者が多く後継が曖昧なほど危機が深まる（指名明白なら抑えられる）。</summary>
        [Test]
        public void CrisisSeverity_RisesWithClaimantsAndAmbiguity()
        {
            // 3請求者・完全曖昧：rivalry=1-1/3=2/3、(1-clarity)=1 → 0.6667
            float ambiguous = SuccessionWarRules.CrisisSeverity(3, 0f);
            Assert.AreEqual(2f / 3f, ambiguous, 1e-4f);
            // 指名が明白(clarity=1)なら同じ請求者数でも危機ゼロ
            Assert.AreEqual(0f, SuccessionWarRules.CrisisSeverity(3, 1f), 1e-5f);
            // 請求者が増えるほど（曖昧時）深刻化
            Assert.Greater(SuccessionWarRules.CrisisSeverity(5, 0f), ambiguous);
        }

        /// <summary>請求者総合力＝既定重みの加重和（正統性0.4/武力0.35/諸侯支持0.25）。</summary>
        [Test]
        public void ClaimantStrength_WeightedSum_Default()
        {
            // 全て1.0 → 重み合計で割って 1.0
            Assert.AreEqual(1f, SuccessionWarRules.ClaimantStrength(1f, 1f, 1f), 1e-5f);
            // 正統性のみ1.0 → 0.4/(0.4+0.35+0.25)=0.4
            Assert.AreEqual(0.4f, SuccessionWarRules.ClaimantStrength(1f, 0f, 0f), 1e-5f);
            // 武力のみ → 0.35
            Assert.AreEqual(0.35f, SuccessionWarRules.ClaimantStrength(0f, 1f, 0f), 1e-5f);
        }

        /// <summary>諸侯は roll に応じて決定論的に請求者を選ぶ（強者へ偏る）。</summary>
        [Test]
        public void NobleAllegiance_Deterministic_AndBandwagonsToStrong()
        {
            float[] claimants = { 0.9f, 0.1f }; // 圧倒的な強者と弱小
            // roll 低＝累積先頭の強者に付く
            Assert.AreEqual(0, SuccessionWarRules.NobleAllegiance(claimants, 0f, 0.05f));
            // 私心ゼロ・強者圧倒なら roll が高めでもまず強者（重みが偏る）
            Assert.AreEqual(0, SuccessionWarRules.NobleAllegiance(claimants, 0f, 0.5f));
            // 同じ入力は同じ結果（決定論）
            Assert.AreEqual(
                SuccessionWarRules.NobleAllegiance(claimants, 0f, 0.5f),
                SuccessionWarRules.NobleAllegiance(claimants, 0f, 0.5f));
            // 空配列は-1
            Assert.AreEqual(-1, SuccessionWarRules.NobleAllegiance(new float[0], 0f, 0.5f));
        }

        /// <summary>突出した1人がいれば無政府度は低く、拮抗並立では高くなる。</summary>
        [Test]
        public void AnarchyLevel_DominantLow_ContestedHigh()
        {
            float dominant = SuccessionWarRules.AnarchyLevel(new[] { 0.95f, 0.05f });
            float contested = SuccessionWarRules.AnarchyLevel(new[] { 0.5f, 0.5f });
            Assert.Less(dominant, 0.2f, "突出した1人がいれば無政府度は低い");
            Assert.Greater(contested, 0.9f, "拮抗並立は無政府化");
            Assert.Greater(contested, dominant);
            // 請求者1人以下は無政府ゼロ
            Assert.AreEqual(0f, SuccessionWarRules.AnarchyLevel(new[] { 0.8f }), 1e-5f);
        }

        /// <summary>中央権威崩壊＝無政府度×崩壊倍率0.9、安定度倍率はその裏返し。</summary>
        [Test]
        public void CentralAuthorityCollapse_ScalesAnarchy()
        {
            Assert.AreEqual(0.9f, SuccessionWarRules.CentralAuthorityCollapse(1f), 1e-5f);
            Assert.AreEqual(0f, SuccessionWarRules.CentralAuthorityCollapse(0f), 1e-5f);
            // 安定度倍率＝1−崩壊度（無政府最大で0.1へ）
            Assert.AreEqual(0.1f, SuccessionWarRules.StabilityFactor(1f), 1e-5f);
            Assert.AreEqual(1f, SuccessionWarRules.StabilityFactor(0f), 1e-5f);
        }

        /// <summary>決着＝実効兵力（総合力）最大の請求者が勝者。全員無力なら断絶(-1)。</summary>
        [Test]
        public void WarResolution_StrongestWins_AllZeroIsExtinction()
        {
            Assert.AreEqual(2, SuccessionWarRules.WarResolution(new[] { 0.3f, 0.5f, 0.7f }));
            Assert.AreEqual(0, SuccessionWarRules.WarResolution(new[] { 0.6f, 0.2f }));
            Assert.AreEqual(-1, SuccessionWarRules.WarResolution(new[] { 0f, 0f }), "全員無力＝王朝断絶");
            Assert.AreEqual(-1, SuccessionWarRules.WarResolution(null));
        }
    }
}
