using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 未知宙域探査を固定する：探査進捗は能力で速く・難所で遅い（能力ゼロでも下限速度で白地は埋まる）・
    /// 閾値で探索済み（＝<see cref="ColonizationRules.CanColonize"/> の explored 供給源）・発見イベントは
    /// 進捗×豊かさで増え roll で決定論判定・難所×低能力は帰らぬ探査艦（喪失リスク）・古い地図は猶予後に
    /// 陳腐化して再探査が要る・探索済み比率の戦略価値は√の逓減＝最初の地図ほど価値が大きい。
    /// クランプを担保。<see cref="ReconRules"/>（戦術の霧）とは別系統の戦略版。
    /// </summary>
    public class ExplorationRulesTests
    {
        private static readonly ExplorationParams P = ExplorationParams.Default; // 速度0.1/能力下限0.2/難所減速0.8/閾値1.0/基礎発見0.05/豊かさ重み0.45/最悪喪失0.3/陳腐化0.005/猶予1年

        [Test]
        public void SurveyTick_CapabilitySpeedsUp_DifficultySlowsDown()
        {
            // 熟練×平易：0.2 + 0.1×1.0×1.0×1 = 0.3
            Assert.AreEqual(0.3f, ExplorationRules.SurveyTick(0.2f, 1f, 0f, 1f, P), 1e-4f);
            // 能力0.5（係数0.6）×難所0.5（係数0.6）：0.2 + 0.1×0.6×0.6 = 0.236
            Assert.AreEqual(0.236f, ExplorationRules.SurveyTick(0.2f, 0.5f, 0.5f, 1f, P), 1e-4f);
            // 能力0でも下限0.2×最難所係数0.2＝0.004 進む＝白地はいつか埋まる
            Assert.AreEqual(0.204f, ExplorationRules.SurveyTick(0.2f, 0f, 1f, 1f, P), 1e-4f);
            // 上限1でクランプ（進みすぎない）・過大入力もクランプ
            Assert.AreEqual(1f, ExplorationRules.SurveyTick(0.95f, 2f, -1f, 10f, P), 1e-5f);
            // dt 負は進まない＝据え置き
            Assert.AreEqual(0.2f, ExplorationRules.SurveyTick(0.2f, 1f, 0f, -1f, P), 1e-5f);
        }

        [Test]
        public void IsSurveyed_ThresholdBoundary_FeedsCanColonize()
        {
            Assert.IsTrue(ExplorationRules.IsSurveyed(1f, P));      // 閾値ちょうどで探索済み
            Assert.IsFalse(ExplorationRules.IsSurveyed(0.99f, P));  // 未満は未探索
            Assert.IsTrue(ExplorationRules.IsSurveyed(1.5f, P));    // 過大入力はクランプして判定
            // 探索済み＝入植可の前提が通る（CanColonize の explored 引数の供給源）
            var target = new StarSystem(1, "辺境", UnityEngine.Vector2.zero) { habitable = true, isColonized = false };
            Assert.IsTrue(ColonizationRules.CanColonize(target, ExplorationRules.IsSurveyed(1f, P)));
            Assert.IsFalse(ColonizationRules.CanColonize(target, ExplorationRules.IsSurveyed(0.5f, P)));
        }

        [Test]
        public void DiscoveryChance_ScalesWithProgressAndRichness()
        {
            // まだ見ていない宙域からは何も出ない
            Assert.AreEqual(0f, ExplorationRules.DiscoveryChance(0f, 1f, P), 1e-5f);
            // 完全踏査×最豊：0.05 + 0.45×1 = 0.5
            Assert.AreEqual(0.5f, ExplorationRules.DiscoveryChance(1f, 1f, P), 1e-4f);
            // 不毛でも基礎率は残る：0.05
            Assert.AreEqual(0.05f, ExplorationRules.DiscoveryChance(1f, 0f, P), 1e-4f);
            // 半分しか調べていなければ半分：0.25
            Assert.AreEqual(0.25f, ExplorationRules.DiscoveryChance(0.5f, 1f, P), 1e-4f);
        }

        [Test]
        public void Discovers_DeterministicRoll()
        {
            Assert.IsTrue(ExplorationRules.Discovers(0.5f, 0.49f));  // roll < chance で発見
            Assert.IsFalse(ExplorationRules.Discovers(0.5f, 0.5f));  // 境界は不発
            Assert.IsFalse(ExplorationRules.Discovers(0f, 0f));      // 率ゼロは何も出ない
            Assert.IsTrue(ExplorationRules.Discovers(2f, 0.99f));    // 過大な率はクランプ＝確実
        }

        [Test]
        public void HazardChance_HardSectorTimesLowCapability()
        {
            // 最難所×能力0＝最悪0.3＝帰らぬ探査艦
            Assert.AreEqual(0.3f, ExplorationRules.HazardChance(1f, 0f, P), 1e-4f);
            // 熟練（能力1）はどんな難所でも喪失ゼロ
            Assert.AreEqual(0f, ExplorationRules.HazardChance(1f, 1f, P), 1e-5f);
            // 平易な宙域は喪失ゼロ
            Assert.AreEqual(0f, ExplorationRules.HazardChance(0f, 0f, P), 1e-5f);
            // 中間：0.3×0.5×0.5 = 0.075
            Assert.AreEqual(0.075f, ExplorationRules.HazardChance(0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void SurveyDecayTick_GracePeriodThenStaleDecay()
        {
            // 猶予（1年）以内の新鮮なデータは劣化しない
            Assert.AreEqual(1f, ExplorationRules.SurveyDecayTick(1f, 0.5f, 10f, P), 1e-5f);
            // 3年放置（超過2年）×dt10：1.0 − 0.005×2×10 = 0.9 ＝探索済みが未探索へ戻る＝再探査の必要
            float decayed = ExplorationRules.SurveyDecayTick(1f, 3f, 10f, P);
            Assert.AreEqual(0.9f, decayed, 1e-4f);
            Assert.IsFalse(ExplorationRules.IsSurveyed(decayed, P));
            // 下限0でクランプ（マイナスに沈まない）
            Assert.AreEqual(0f, ExplorationRules.SurveyDecayTick(0.01f, 100f, 100f, P), 1e-5f);
        }

        [Test]
        public void FrontierValue_DiminishingReturns_EarlyMapsWorthMore()
        {
            Assert.AreEqual(0f, ExplorationRules.FrontierValue(0f), 1e-5f);   // 白地だけでは価値ゼロ
            Assert.AreEqual(0.5f, ExplorationRules.FrontierValue(0.25f), 1e-4f); // √0.25＝最初の四半分で半分の価値
            Assert.AreEqual(1f, ExplorationRules.FrontierValue(1f), 1e-5f);
            // 逓減＝序盤の探査ほど価値が大きい（share を上回る）＝埋めた者が先に選べる
            Assert.Greater(ExplorationRules.FrontierValue(0.25f), 0.25f);
            // クランプ
            Assert.AreEqual(1f, ExplorationRules.FrontierValue(2f), 1e-5f);
            Assert.AreEqual(0f, ExplorationRules.FrontierValue(-1f), 1e-5f);
        }
    }
}
