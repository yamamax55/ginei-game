using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>軍事技術拡散（#1377）の純ロジックを検証する。</summary>
    public class TechDiffusionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>拡散速度＝格差×接触×（1−封鎖遮断）。既定で具体値を固定する。</summary>
        [Test]
        public void DiffusionRate_格差と接触に比例し封鎖が絞る()
        {
            // base0.12 × gap0.5 × contact0.8 × (1 - 0.7×0.5=0.65) = 0.12×0.5×0.8×0.65 = 0.0312
            float rate = TechDiffusionRules.DiffusionRate(0.5f, 0.8f, 0.5f);
            Assert.AreEqual(0.0312f, rate, Eps);
        }

        /// <summary>格差ゼロまたは接触ゼロなら拡散しない＝漏れる元・経路が無い。</summary>
        [Test]
        public void DiffusionRate_格差ゼロか接触ゼロで停止()
        {
            Assert.AreEqual(0f, TechDiffusionRules.DiffusionRate(0f, 1f, 0f), Eps);
            Assert.AreEqual(0f, TechDiffusionRules.DiffusionRate(1f, 0f, 0f), Eps);
        }

        /// <summary>封鎖全力でも接触と格差があれば拡散は正＝独占は時限（完全には止められない）。</summary>
        [Test]
        public void DiffusionRate_封鎖全力でも正で独占は時限()
        {
            // base0.12 × 1 × 1 × (1 - 0.7×1 = 0.3) = 0.036 > 0
            float rate = TechDiffusionRules.DiffusionRate(1f, 1f, 1f);
            Assert.AreEqual(0.036f, rate, Eps);
            Assert.Greater(rate, 0f);
        }

        /// <summary>スパイ窃取＝スパイ網×標的価値×係数。網が無ければ奪えない。</summary>
        [Test]
        public void EspionageTransfer_浸透と価値の積に比例()
        {
            // 0.6 × 0.5 × espionage0.5 = 0.15
            Assert.AreEqual(0.15f, TechDiffusionRules.EspionageTransfer(0.6f, 0.5f), Eps);
            Assert.AreEqual(0f, TechDiffusionRules.EspionageTransfer(0f, 1f), Eps);
        }

        /// <summary>リバースエンジニアリング＝捕獲量×自前基盤×係数。基盤ゼロなら宝の持ち腐れ。</summary>
        [Test]
        public void ReverseEngineering_基盤ゼロで解析不能()
        {
            // 0.8 × 0.5 × reverse0.6 = 0.24
            Assert.AreEqual(0.24f, TechDiffusionRules.ReverseEngineering(0.8f, 0.5f), Eps);
            Assert.AreEqual(0f, TechDiffusionRules.ReverseEngineering(1f, 0f), Eps);
        }

        /// <summary>亡命技術者＝規模×専門性×係数。どちらかゼロなら技術は来ない。</summary>
        [Test]
        public void DefectorTransfer_規模と専門性の積()
        {
            // 0.5 × 0.4 × defector0.7 = 0.14
            Assert.AreEqual(0.14f, TechDiffusionRules.DefectorTransfer(0.5f, 0.4f), Eps);
            Assert.AreEqual(0f, TechDiffusionRules.DefectorTransfer(1f, 0f), Eps);
        }

        /// <summary>同盟移転＝同盟強度×共有意思×係数。共有意思が無ければ渡らない。</summary>
        [Test]
        public void AllyTransfer_同盟強度と共有意思の積()
        {
            // 0.7 × 0.5 × ally0.8 = 0.28
            Assert.AreEqual(0.28f, TechDiffusionRules.AllyTransfer(0.7f, 0.5f), Eps);
            Assert.AreEqual(0f, TechDiffusionRules.AllyTransfer(1f, 0f), Eps);
        }

        /// <summary>技術封鎖の遮断率は二経路を合成し上限 maxBlockade でクランプ＝完全遮断しない。</summary>
        [Test]
        public void TechBlockadeEffect_合成され上限で頭打ち()
        {
            // 1 - (1-0.5)(1-0.4) = 1 - 0.3 = 0.7 → min(0.7, 0.7) = 0.7
            Assert.AreEqual(0.7f, TechDiffusionRules.TechBlockadeEffect(0.5f, 0.4f), Eps);
            // 両方全力でも上限 0.7 で頭打ち（完全には止められない）
            Assert.AreEqual(0.7f, TechDiffusionRules.TechBlockadeEffect(1f, 1f), Eps);
        }

        /// <summary>後発の追い上げ＝格差×拡散速度×dt。格差が大きいほど一歩が大きい。</summary>
        [Test]
        public void CatchUpAcceleration_格差に比例し格差を超えない()
        {
            // gap0.6 × rate0.5 × dt1 = 0.3
            Assert.AreEqual(0.3f, TechDiffusionRules.CatchUpAcceleration(0.6f, 0.5f, 1f), Eps);
            // 大きな rate でも格差を超えて縮められない（並走で止まる）
            Assert.AreEqual(0.6f, TechDiffusionRules.CatchUpAcceleration(0.6f, 5f, 1f), Eps);
        }

        /// <summary>技術独占の崩壊判定＝拡散速度が閾値以上なら漏出で崩れつつある。</summary>
        [Test]
        public void IsTechMonopolyEroding_閾値超で崩壊判定()
        {
            Assert.IsTrue(TechDiffusionRules.IsTechMonopolyEroding(0.05f, 0.03f));
            Assert.IsFalse(TechDiffusionRules.IsTechMonopolyEroding(0.01f, 0.03f));
        }
    }
}
