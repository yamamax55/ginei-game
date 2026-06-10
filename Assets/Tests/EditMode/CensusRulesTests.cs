using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国勢調査・統計精度を固定する：可視性は投資で上がり社会変化で陳腐化（統計は生もの）、粗い統計は人口推定・徴税・
    /// 政策を外し（見えない国は治められない）、高すぎる可視性は監視への反発を生む（見えすぎる国は息が詰まる）＝両刃を担保。
    /// 推定は roll で決定論・全入力クランプ。<see cref="DemographicsRules"/>（真値の人口動態）とは別＝政府の認識のテスト。
    /// </summary>
    public class CensusRulesTests
    {
        private static readonly CensusParams P = CensusParams.Default; // 投資0.2/陳腐化0.1/誤差±0.5/最低徴税0.3/失敗上限0.8/闇人口0.4/監視閾値0.6

        [Test]
        public void LegibilityTick_InvestmentRaises_SocialChangeErodes()
        {
            // 全力投資・社会静止：0.5 + 0.2*1 = 0.7
            Assert.AreEqual(0.7f, CensusRules.LegibilityTick(0.5f, 1f, 0f, 1f, P), 1e-5f);
            // 投資ゼロ・激動の社会：0.5 - 0.1*1 = 0.4（統計は放置で古びる）
            Assert.AreEqual(0.4f, CensusRules.LegibilityTick(0.5f, 0f, 1f, 1f, P), 1e-5f);
            // 拮抗：投資0.5(+0.1) と変化1.0(-0.1) で現状維持
            Assert.AreEqual(0.5f, CensusRules.LegibilityTick(0.5f, 0.5f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void LegibilityTick_ClampedToUnitRange()
        {
            // 上限1.0・下限0.0 を超えない
            Assert.AreEqual(1f, CensusRules.LegibilityTick(0.95f, 1f, 0f, 10f, P), 1e-5f);
            Assert.AreEqual(0f, CensusRules.LegibilityTick(0.05f, 0f, 1f, 10f, P), 1e-5f);
            // 負の dt は進めない（時間は巻き戻らない）
            Assert.AreEqual(0.5f, CensusRules.LegibilityTick(0.5f, 1f, 0f, -1f, P), 1e-5f);
        }

        [Test]
        public void PerceivedPopulation_BiasedByRoll_Deterministic()
        {
            // legibility=0.5 → 誤差0.25。roll=0 で真値、+1 で+25%、-1 で-25%
            Assert.AreEqual(1000f, CensusRules.PerceivedPopulation(1000f, 0.5f, 0f, P), 1e-3f);
            Assert.AreEqual(1250f, CensusRules.PerceivedPopulation(1000f, 0.5f, 1f, P), 1e-3f);
            Assert.AreEqual(750f, CensusRules.PerceivedPopulation(1000f, 0.5f, -1f, P), 1e-3f);
            // 完全可視なら認識＝真値（バイアスが効かない）
            Assert.AreEqual(1000f, CensusRules.PerceivedPopulation(1000f, 1f, 1f, P), 1e-3f);
        }

        [Test]
        public void PerceivedPopulation_NeverNegative_RollClamped()
        {
            // 極端な負バイアスでも 0 未満にならない
            Assert.GreaterOrEqual(CensusRules.PerceivedPopulation(100f, 0f, -5f, P), 0f);
            // roll は [-1,1] にクランプ＝roll=5 は roll=1 と同じ
            Assert.AreEqual(CensusRules.PerceivedPopulation(1000f, 0f, 1f, P),
                            CensusRules.PerceivedPopulation(1000f, 0f, 5f, P), 1e-3f);
        }

        [Test]
        public void TaxAndShadow_BlindStateCannotReach()
        {
            // 徴税効率：盲目でも最低0.3（関所ぶん）・完全可視で1.0・中間は線形
            Assert.AreEqual(0.3f, CensusRules.TaxCollectionEfficiency(0f, P), 1e-5f);
            Assert.AreEqual(1f, CensusRules.TaxCollectionEfficiency(1f, P), 1e-5f);
            Assert.AreEqual(0.65f, CensusRules.TaxCollectionEfficiency(0.5f, P), 1e-5f);
            // 闇人口：盲目で最大0.4・完全可視で0（動員も課税も届かない層が消える）
            Assert.AreEqual(0.4f, CensusRules.ShadowPopulation(0f, P), 1e-5f);
            Assert.AreEqual(0f, CensusRules.ShadowPopulation(1f, P), 1e-5f);
        }

        [Test]
        public void PolicyMisfire_RiskShrinksWithLegibility_DeterministicByRoll()
        {
            // 盲目＝最大0.8・完全可視＝0・中間は線形
            Assert.AreEqual(0.8f, CensusRules.PolicyMisfireRisk(0f, P), 1e-5f);
            Assert.AreEqual(0f, CensusRules.PolicyMisfireRisk(1f, P), 1e-5f);
            Assert.AreEqual(0.4f, CensusRules.PolicyMisfireRisk(0.5f, P), 1e-5f);
            // 判定は roll で決定論
            Assert.IsTrue(CensusRules.PolicyMisfires(0.5f, 0.39f, P));
            Assert.IsFalse(CensusRules.PolicyMisfires(0.5f, 0.41f, P));
        }

        [Test]
        public void SurveillanceResentment_DoubleEdgedSword()
        {
            // 閾値0.6以下なら反発なし（ほどほどの統計は嫌われない）
            Assert.AreEqual(0f, CensusRules.SurveillanceResentment(0.6f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, CensusRules.SurveillanceResentment(0.3f, 1f, P), 1e-5f);
            // 超過幅×自由気質：legibility=0.8 → 超過半分 → 自由社会(1.0)で0.5・統制社会(0.0)で0
            Assert.AreEqual(0.5f, CensusRules.SurveillanceResentment(0.8f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, CensusRules.SurveillanceResentment(0.8f, 0f, P), 1e-5f);
            // 完全監視×完全自由＝最大反発1.0
            Assert.AreEqual(1f, CensusRules.SurveillanceResentment(1f, 1f, P), 1e-5f);
            // 両刃の担保：盲目国家は政策が外れるが反発はゼロ／全可視国家は政策が当たるが反発が最大
            Assert.Greater(CensusRules.PolicyMisfireRisk(0f, P), 0f);
            Assert.AreEqual(0f, CensusRules.SurveillanceResentment(0f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, CensusRules.PolicyMisfireRisk(1f, P), 1e-5f);
            Assert.Greater(CensusRules.SurveillanceResentment(1f, 1f, P), 0f);
        }
    }
}
