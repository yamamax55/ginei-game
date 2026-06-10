using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 大事業建造の工期×品質トレードオフ（#1091・Pillars of the Earth）を固定する：工期優先での進捗加速、
    /// 急造での構造品質低下（職人の腕が補完）、低品質×危険曝露での事故確率上昇、事故判定の決定論、
    /// 低品質ほど大きい進捗後退（数年分を失う）、急造の脆い長期耐久。
    /// 既定 Params（加速0.6/急造品質-0.5/名工品質+0.3/事故上限0.6/後退上限0.5/耐久下限0.4）で期待値固定。
    /// </summary>
    public class QualityScheduleRulesTests
    {
        // ===== 工期優先＝進捗加速 =====

        [Test]
        public void ScheduleSpeedup_FasterWhenRushing()
        {
            Assert.AreEqual(1.0f, QualityScheduleRules.ScheduleSpeedup(0f), 1e-4f);   // 品質優先＝等倍
            Assert.AreEqual(1.3f, QualityScheduleRules.ScheduleSpeedup(0.5f), 1e-4f);
            Assert.AreEqual(1.6f, QualityScheduleRules.ScheduleSpeedup(1f), 1e-4f);   // 工期全振り＝最速
        }

        // ===== 急造で品質低下・腕が補う（トレードオフ） =====

        [Test]
        public void StructuralQuality_RushLowersQuality_CraftRestores()
        {
            Assert.AreEqual(1.0f, QualityScheduleRules.StructuralQuality(0f, 0f), 1e-4f);  // 急がず＝満点
            Assert.AreEqual(0.5f, QualityScheduleRules.StructuralQuality(1f, 0f), 1e-4f);  // 急造＝品質-0.5
            Assert.AreEqual(0.75f, QualityScheduleRules.StructuralQuality(0.5f, 0f), 1e-4f);
            // 名工は急造を肩代わり：1-0.5+0.3=0.8
            Assert.AreEqual(0.8f, QualityScheduleRules.StructuralQuality(1f, 1f), 1e-4f);
            // 急がず名工＝上限1.0にクランプ（こぼれない）
            Assert.AreEqual(1.0f, QualityScheduleRules.StructuralQuality(0f, 1f), 1e-4f);
        }

        // ===== 低品質×危険曝露で事故確率が跳ねる =====

        [Test]
        public void AccidentChance_RisesWithLowQualityAndExposure()
        {
            Assert.AreEqual(0f, QualityScheduleRules.AccidentChance(1f, 1f), 1e-6f);    // 高品質＝曝露あっても安全
            Assert.AreEqual(0.3f, QualityScheduleRules.AccidentChance(0.5f, 1f), 1e-4f); // 低品質×高曝露
            Assert.AreEqual(0.15f, QualityScheduleRules.AccidentChance(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, QualityScheduleRules.AccidentChance(0.5f, 0f), 1e-6f);  // 曝露0＝事故なし
        }

        // ===== 事故判定は決定論（確率は提示せず roll で解決） =====

        [Test]
        public void AccidentOccurs_DeterministicByRoll()
        {
            // 事故確率0.3：roll<0.3 で発生、以上で回避
            Assert.IsTrue(QualityScheduleRules.AccidentOccurs(0.3f, 0.29f));
            Assert.IsFalse(QualityScheduleRules.AccidentOccurs(0.3f, 0.3f));
            Assert.IsFalse(QualityScheduleRules.AccidentOccurs(0.3f, 0.9f));
            Assert.IsFalse(QualityScheduleRules.AccidentOccurs(0f, 0f)); // 確率0は常に回避
        }

        // ===== 低品質ほど崩れて大きく戻る（数年分を失う） =====

        [Test]
        public void SetbackSeverity_LowQualityLosesMore()
        {
            Assert.AreEqual(0f, QualityScheduleRules.SetbackSeverity(1f, 0.8f), 1e-6f);    // 高品質＝崩れても局所
            Assert.AreEqual(0.2f, QualityScheduleRules.SetbackSeverity(0.5f, 0.8f), 1e-4f);
            // 粗悪×大きく進んだ建造ほど痛い：0.8×0.8×0.5=0.32
            Assert.AreEqual(0.32f, QualityScheduleRules.SetbackSeverity(0.2f, 0.8f), 1e-4f);
            // 同品質でも進捗が浅ければ後退も小さい
            Assert.Less(QualityScheduleRules.SetbackSeverity(0.2f, 0.2f),
                        QualityScheduleRules.SetbackSeverity(0.2f, 0.8f));
        }

        // ===== 急造の建物は完成後も脆い =====

        [Test]
        public void LongTermDurability_RushedIsFragile()
        {
            Assert.AreEqual(1.0f, QualityScheduleRules.LongTermDurability(1f), 1e-4f);  // 高品質＝最も頑丈
            Assert.AreEqual(0.7f, QualityScheduleRules.LongTermDurability(0.5f), 1e-4f);
            Assert.AreEqual(0.4f, QualityScheduleRules.LongTermDurability(0f), 1e-4f);  // 急造でも下限0.4は残る
        }

        // ===== トレードオフの統合：急造は速いが事故で帳消しになりうる =====

        [Test]
        public void Tradeoff_RushIsFasterButRiskier()
        {
            // 工期全振り（腕は普通0.5）と品質優先（腕は普通0.5）を比較
            float rushSpeed = QualityScheduleRules.ScheduleSpeedup(1f);
            float carefulSpeed = QualityScheduleRules.ScheduleSpeedup(0f);
            Assert.Greater(rushSpeed, carefulSpeed); // 急造は速い

            float rushQuality = QualityScheduleRules.StructuralQuality(1f, 0.5f);    // 1-0.5+0.15=0.65
            float carefulQuality = QualityScheduleRules.StructuralQuality(0f, 0.5f); // 1+0.15→clamp1.0
            Assert.Less(rushQuality, carefulQuality); // だが品質は低い

            // 低品質ほど事故確率も後退も大きい＝急造は崩れて数年分を失いうる
            float exposure = 1f;
            Assert.Greater(QualityScheduleRules.AccidentChance(rushQuality, exposure),
                           QualityScheduleRules.AccidentChance(carefulQuality, exposure));
            Assert.Greater(QualityScheduleRules.SetbackSeverity(rushQuality, 0.8f),
                           QualityScheduleRules.SetbackSeverity(carefulQuality, 0.8f));
        }
    }
}
