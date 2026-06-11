using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 普遍史の因果波及（POLY-4 #1451・ポリュビオス）の純ロジックを既定Paramsの具体値で固定する。
    /// 事件の波及・連関度・因果カスケード・波及の到達・系全体の大事件・歴史の収束・距離減衰・
    /// 一体化した世界判定・空配列安全を担保する。
    /// </summary>
    public class UniversalHistoryRulesTests
    {
        private const float Eps = 0.001f;

        /// <summary>事件の波及＝連関した世界ほど遠くまで届く（同距離で連関度が高いほど波及が強い）。</summary>
        [Test]
        public void EventPropagation_連関度が高いほど遠方へ強く波及する()
        {
            float isolated = UniversalHistoryRules.EventPropagation(1f, 0.5f, 0f);
            float connected = UniversalHistoryRules.EventPropagation(1f, 0.5f, 1f);
            Assert.Greater(connected, isolated, "一体化した世界ほど同じ距離へ強く波及する");
            // 連関0の具体値：effectiveDecay=1.5・reach=e^-0.75≈0.4724・Lerp(0.05,1,0.4724)≈0.4988。
            Assert.AreEqual(0.4988f, isolated, 0.01f);
            // 距離0なら連関度に依らず満額。
            Assert.AreEqual(1f, UniversalHistoryRules.EventPropagation(1f, 0f, 0.5f), Eps);
        }

        /// <summary>連関度＝交易0.4・政治0.35・通信0.25の加重平均。</summary>
        [Test]
        public void Interconnectedness_結びつきの加重平均になる()
        {
            Assert.AreEqual(1f, UniversalHistoryRules.Interconnectedness(1f, 1f, 1f), Eps);
            Assert.AreEqual(0.5f, UniversalHistoryRules.Interconnectedness(0.5f, 0.5f, 0.5f), Eps);
            Assert.AreEqual(0.4f, UniversalHistoryRules.Interconnectedness(1f, 0f, 0f), Eps, "交易の重みは0.4");
            Assert.AreEqual(0.35f, UniversalHistoryRules.Interconnectedness(0f, 1f, 0f), Eps, "政治の重みは0.35");
        }

        /// <summary>因果カスケード＝鎖が長く・各ホップ減衰が大きいほど末端に届く力は薄まる。</summary>
        [Test]
        public void CausalCascade_鎖を伝うほど減衰する()
        {
            // 鎖長0＝伝播せず満額。
            Assert.AreEqual(1f, UniversalHistoryRules.CausalCascade(1f, 0f, 0f), Eps);
            // 鎖長1・ホップ減衰1＝hop=1.0・5ホップで完全消失。
            Assert.AreEqual(0f, UniversalHistoryRules.CausalCascade(1f, 1f, 1f), Eps);
            // 鎖が長いほど減衰する（単調）。
            float shortChain = UniversalHistoryRules.CausalCascade(1f, 0.3f, 0.5f);
            float longChain = UniversalHistoryRules.CausalCascade(1f, 0.8f, 0.5f);
            Assert.Greater(shortChain, longChain, "風が吹けば桶屋＝鎖が長いほど薄まる");
        }

        /// <summary>波及の到達＝大事件×連関した世界で広範囲。</summary>
        [Test]
        public void RippleReach_大事件と連関で広範囲になる()
        {
            Assert.AreEqual(1f, UniversalHistoryRules.RippleReach(1f, 1f), Eps);
            Assert.AreEqual(0.25f, UniversalHistoryRules.RippleReach(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, UniversalHistoryRules.RippleReach(1f, 0f), Eps, "連関ゼロなら届かない");
        }

        /// <summary>系全体の大事件＝局所事件が連関して一つの大事件にまとまる。</summary>
        [Test]
        public void SystemicEvent_連関で局所事件が系全体の大事件になる()
        {
            float[] locals = { 0.5f, 0.5f };
            // 連関0＝最大の局所事件（独立）。
            Assert.AreEqual(0.5f, UniversalHistoryRules.SystemicEvent(locals, 0f), Eps);
            // 連関1＝飽和和 1-(0.5*0.5)=0.75。
            Assert.AreEqual(0.75f, UniversalHistoryRules.SystemicEvent(locals, 1f), Eps);
            // 連関が高いほど系全体規模は大きい（単調）。
            Assert.Greater(
                UniversalHistoryRules.SystemicEvent(locals, 1f),
                UniversalHistoryRules.SystemicEvent(locals, 0f),
                "有機的な全体は局所の最大を超える");
        }

        /// <summary>系全体の大事件＝null/空配列は0で安全。</summary>
        [Test]
        public void SystemicEvent_空配列は安全に0()
        {
            Assert.AreEqual(0f, UniversalHistoryRules.SystemicEvent(null, 1f), Eps);
            Assert.AreEqual(0f, UniversalHistoryRules.SystemicEvent(new float[0], 1f), Eps);
        }

        /// <summary>歴史の収束＝連関度が時間で一体化へ向かう（ローマ型統一）。</summary>
        [Test]
        public void HistoricalConvergence_連関度が時間で高まる()
        {
            // 0.5+(1-0.5)*0.1=0.55。
            Assert.AreEqual(0.55f, UniversalHistoryRules.HistoricalConvergence(0.5f, 1f), Eps);
            // 進めば必ず上がる（一方向のドリフト）。
            Assert.Greater(
                UniversalHistoryRules.HistoricalConvergence(0.5f, 1f),
                0.5f,
                "孤立した歴史が普遍史へ収束する");
            // dt=0なら不変。
            Assert.AreEqual(0.5f, UniversalHistoryRules.HistoricalConvergence(0.5f, 0f), Eps);
        }

        /// <summary>距離減衰＝指数減衰。距離0で1、遠いほど薄まる。</summary>
        [Test]
        public void DistanceDecay_指数的に減衰する()
        {
            Assert.AreEqual(1f, UniversalHistoryRules.DistanceDecay(0f), Eps);
            // e^-1.5≈0.2231。
            Assert.AreEqual(0.2231f, UniversalHistoryRules.DistanceDecay(1f, 1.5f), 0.01f);
            Assert.Greater(
                UniversalHistoryRules.DistanceDecay(0.3f),
                UniversalHistoryRules.DistanceDecay(0.8f),
                "遠いほど波及は薄まる");
        }

        /// <summary>一体化した世界判定＝連関度が閾値以上で普遍史の段階。</summary>
        [Test]
        public void IsInterconnectedWorld_閾値で普遍史の段階を判定する()
        {
            // 既定閾値0.6。
            Assert.IsTrue(UniversalHistoryRules.IsInterconnectedWorld(0.7f), "連関した世界＝事件が波及する");
            Assert.IsFalse(UniversalHistoryRules.IsInterconnectedWorld(0.5f), "バラバラな世界＝事件は孤立する");
            Assert.IsTrue(UniversalHistoryRules.IsInterconnectedWorld(0.6f), "閾値ちょうどは普遍史");
        }
    }
}
