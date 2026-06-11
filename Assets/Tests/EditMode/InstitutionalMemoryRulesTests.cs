using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 歴史の教訓と制度的記憶（POLY-5 #1454・ポリュビオス型）の純ロジック検証。
    /// 危機学習・記憶の蓄積・記憶の忘却・過ち反復リスク・備えボーナス・成文化の価値・
    /// 歴史の知恵・学習する制度判定を既定 Params の具体値で固定する。
    /// </summary>
    public class InstitutionalMemoryRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>危機学習＝危機の深刻さ×省察能力。痛い危機を省察するほど学びが深い（積）。</summary>
        [Test]
        public void CrisisLearning_痛い危機を省察するほど深く学ぶ()
        {
            // 0.8×0.5×1.0 = 0.4
            Assert.AreEqual(0.4f, InstitutionalMemoryRules.CrisisLearning(0.8f, 0.5f), Eps);
            // 省察ゼロなら学ばない
            Assert.AreEqual(0f, InstitutionalMemoryRules.CrisisLearning(1f, 0f), Eps);
            // 危機が深いほど学びが深い
            Assert.Greater(
                InstitutionalMemoryRules.CrisisLearning(0.9f, 0.6f),
                InstitutionalMemoryRules.CrisisLearning(0.3f, 0.6f));
        }

        /// <summary>記憶の蓄積＝教訓が制度知として積もる（経験が制度知になる）。</summary>
        [Test]
        public void MemoryAccumulationTick_教訓が制度知として積もる()
        {
            // 0.2 + 0.5×0.1×2 = 0.3
            Assert.AreEqual(0.3f, InstitutionalMemoryRules.MemoryAccumulationTick(0.2f, 0.5f, 2f), Eps);
            // 学習ゼロなら据え置き
            Assert.AreEqual(0.4f, InstitutionalMemoryRules.MemoryAccumulationTick(0.4f, 0f, 5f), Eps);
            // 1で頭打ち
            Assert.AreEqual(1f, InstitutionalMemoryRules.MemoryAccumulationTick(0.95f, 1f, 10f), Eps);
        }

        /// <summary>記憶の忘却＝記録を怠ると制度的記憶が薄れる（記録なしは世代で消える）。</summary>
        [Test]
        public void MemoryDecay_記録を怠ると記憶が薄れる()
        {
            // 0.8 − (1−0.4)×0.05×4 = 0.8 − 0.12 = 0.68
            Assert.AreEqual(0.68f, InstitutionalMemoryRules.MemoryDecay(0.8f, 0.4f, 4f), Eps);
            // 記録完璧なら薄れない
            Assert.AreEqual(0.8f, InstitutionalMemoryRules.MemoryDecay(0.8f, 1f, 10f), Eps);
            // 記録なしは速く薄れる
            Assert.Less(
                InstitutionalMemoryRules.MemoryDecay(0.8f, 0f, 4f),
                InstitutionalMemoryRules.MemoryDecay(0.8f, 0.5f, 4f));
        }

        /// <summary>過ち反復リスク＝記憶が薄いと似た危機で同じ過ちを繰り返す（歴史を忘れた者は繰り返す）。</summary>
        [Test]
        public void RepeatedMistakeRisk_記憶が薄いと過ちを繰り返す()
        {
            // (1−0.3)×0.8 = 0.56
            Assert.AreEqual(0.56f, InstitutionalMemoryRules.RepeatedMistakeRisk(0.3f, 0.8f), Eps);
            // 記憶が満ちていればリスク0
            Assert.AreEqual(0f, InstitutionalMemoryRules.RepeatedMistakeRisk(1f, 1f), Eps);
            // 記憶が薄いほどリスクが高い
            Assert.Greater(
                InstitutionalMemoryRules.RepeatedMistakeRisk(0.1f, 0.7f),
                InstitutionalMemoryRules.RepeatedMistakeRisk(0.9f, 0.7f));
        }

        /// <summary>備えボーナス＝蓄積知が類似危機への備えを高める（過去に学んだ対処）。</summary>
        [Test]
        public void PreparednessBonus_蓄積知が類似危機への備えを高める()
        {
            // 0.5×0.8×0.6 = 0.24
            Assert.AreEqual(0.24f, InstitutionalMemoryRules.PreparednessBonus(0.5f, 0.8f), Eps);
            // 知ゼロなら備えなし
            Assert.AreEqual(0f, InstitutionalMemoryRules.PreparednessBonus(0f, 1f), Eps);
            // 類似度が高いほど備えが効く
            Assert.Greater(
                InstitutionalMemoryRules.PreparednessBonus(0.7f, 0.9f),
                InstitutionalMemoryRules.PreparednessBonus(0.7f, 0.2f));
        }

        /// <summary>成文化の価値＝教訓を文書・制度に成文化すると記憶が永続する（暗黙知→形式知）。</summary>
        [Test]
        public void CodificationValue_成文化で記憶が永続する()
        {
            // 0.6×0.5×0.7 = 0.21
            Assert.AreEqual(0.21f, InstitutionalMemoryRules.CodificationValue(0.6f, 0.5f), Eps);
            // 文書化ゼロなら残らない
            Assert.AreEqual(0f, InstitutionalMemoryRules.CodificationValue(1f, 0f), Eps);
            // 学んだ教訓を文書化するほど永続する
            Assert.Greater(
                InstitutionalMemoryRules.CodificationValue(0.9f, 0.9f),
                InstitutionalMemoryRules.CodificationValue(0.3f, 0.9f));
        }

        /// <summary>歴史の知恵＝蓄積知が意思決定の質を高める（歴史は実践的教師・基準非破壊）。</summary>
        [Test]
        public void WisdomFromHistory_歴史が意思決定の質を高める()
        {
            // 0.5 + 0.6×0.5 = 0.8
            Assert.AreEqual(0.8f, InstitutionalMemoryRules.WisdomFromHistory(0.6f, 0.5f), Eps);
            // 知ゼロなら基準のまま（非破壊）
            Assert.AreEqual(0.5f, InstitutionalMemoryRules.WisdomFromHistory(0f, 0.5f), Eps);
            // 1で頭打ち
            Assert.AreEqual(1f, InstitutionalMemoryRules.WisdomFromHistory(1f, 0.9f), Eps);
        }

        /// <summary>学習する制度判定＝制度知識が閾値以上なら学習する制度（忘却なら反復する制度）。</summary>
        [Test]
        public void IsLearningOrganization_閾値で学習する制度を判定()
        {
            // 既定閾値 0.5
            Assert.IsTrue(InstitutionalMemoryRules.IsLearningOrganization(0.5f));
            Assert.IsTrue(InstitutionalMemoryRules.IsLearningOrganization(0.8f));
            Assert.IsFalse(InstitutionalMemoryRules.IsLearningOrganization(0.49f));
            // 明示閾値
            Assert.IsTrue(InstitutionalMemoryRules.IsLearningOrganization(0.7f, 0.6f));
            Assert.IsFalse(InstitutionalMemoryRules.IsLearningOrganization(0.5f, 0.6f));
        }
    }
}
