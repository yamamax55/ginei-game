using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>一般意志汚染指標（ROUS-1 #1462・ルソー）の純ロジックを既定 Params の具体値で固定する EditMode テスト。</summary>
    public class GeneralWillRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>一般意志の純度＝公益志向−派閥捕獲（汚染されていない度）。</summary>
        [Test]
        public void GeneralWillPurity_PublicMinusFactional()
        {
            // 公益志向0.8・派閥捕獲0.3 → 0.5
            Assert.AreEqual(0.5f, GeneralWillRules.GeneralWillPurity(0.8f, 0.3f), Eps);
            // 派閥が公益を上回れば純度0（汚染しきっている）
            Assert.AreEqual(0f, GeneralWillRules.GeneralWillPurity(0.3f, 0.9f), Eps);
        }

        /// <summary>全体意志＝特殊意志（私益）の総和の平均。null/空は0。</summary>
        [Test]
        public void WillOfAll_AveragesParticularInterests_NullSafe()
        {
            // (0.2+0.4+0.9)/3 = 0.5
            Assert.AreEqual(0.5f, GeneralWillRules.WillOfAll(new[] { 0.2f, 0.4f, 0.9f }), Eps);
            Assert.AreEqual(0f, GeneralWillRules.WillOfAll(null), Eps);
            Assert.AreEqual(0f, GeneralWillRules.WillOfAll(new float[0]), Eps);
        }

        /// <summary>派閥の汚染＝数×強さ×感度。どちらかが0なら汚染0。</summary>
        [Test]
        public void FactionalContamination_CountTimesStrength()
        {
            // 既定感度1：0.6×0.5×1 = 0.3
            Assert.AreEqual(0.3f, GeneralWillRules.FactionalContamination(0.6f, 0.5f), Eps);
            // 派閥が無ければ汚染も無い
            Assert.AreEqual(0f, GeneralWillRules.FactionalContamination(0f, 1f), Eps);
            Assert.AreEqual(0f, GeneralWillRules.FactionalContamination(1f, 0f), Eps);
        }

        /// <summary>公共善との整合＝純度×政策の公共性。</summary>
        [Test]
        public void PublicGoodAlignment_PurityTimesPublicness()
        {
            // 0.5×0.8 = 0.4
            Assert.AreEqual(0.4f, GeneralWillRules.PublicGoodAlignment(0.5f, 0.8f), Eps);
            // 政策が私的なら整合0
            Assert.AreEqual(0f, GeneralWillRules.PublicGoodAlignment(1f, 0f), Eps);
        }

        /// <summary>参加の正統性＝直接参加×0.6＋直接審議×0.4（既定）。</summary>
        [Test]
        public void ParticipationLegitimacy_WeightsDirectParticipation()
        {
            // 0.8×0.6 + 0.5×0.4 = 0.48 + 0.20 = 0.68
            Assert.AreEqual(0.68f, GeneralWillRules.ParticipationLegitimacy(0.8f, 0.5f), Eps);
        }

        /// <summary>特殊意志の腐敗＝汚染×速度×dt。dt≤0で0。</summary>
        [Test]
        public void CorruptionByParticularWill_AccruesOverTime()
        {
            // 既定速度0.1：0.4×0.1×2 = 0.08
            Assert.AreEqual(0.08f, GeneralWillRules.CorruptionByParticularWill(0.4f, 2f), Eps);
            Assert.AreEqual(0f, GeneralWillRules.CorruptionByParticularWill(0.4f, 0f), Eps);
            Assert.AreEqual(0f, GeneralWillRules.CorruptionByParticularWill(0.4f, -1f), Eps);
        }

        /// <summary>自由の強制＝純度×個人の逸脱（ルソーの逆説）。</summary>
        [Test]
        public void ForcedToBeFree_PurityTimesDeviation()
        {
            // 0.8×0.5 = 0.4
            Assert.AreEqual(0.4f, GeneralWillRules.ForcedToBeFree(0.8f, 0.5f), Eps);
            // 一般意志が汚染（純度0）なら強制は正当化されない
            Assert.AreEqual(0f, GeneralWillRules.ForcedToBeFree(0f, 1f), Eps);
        }

        /// <summary>一般意志の汚染判定＝純度が既定閾値0.3未満で true。</summary>
        [Test]
        public void IsGeneralWillCorrupted_BelowThreshold()
        {
            Assert.IsTrue(GeneralWillRules.IsGeneralWillCorrupted(0.2f));   // 0.3未満＝公共善が失われた
            Assert.IsFalse(GeneralWillRules.IsGeneralWillCorrupted(0.3f));  // 閾値ちょうどは未汚染
            Assert.IsFalse(GeneralWillRules.IsGeneralWillCorrupted(0.7f));
        }
    }
}
