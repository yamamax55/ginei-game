using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 発注・生産オーダー＝PO/PrO の割付（#985）を固定する：作るか買うかの種別判定（素材＝購買PO／部品＝生産PrO）、
    /// 内製か外注かの判断（能力＋コスト）、同種設備のうち最も空いた所への割付（負荷分散）、
    /// 納期×重要度の優先度、発注のまとめ（ロット集約）、設備能力の引き当て（占有率）。
    /// 既定Params具体値で期待値を固定する。
    /// </summary>
    public class ProductionOrderRulesTests
    {
        // 種別判定：素材は購買PO・部品/製品は生産PrO＝作るか買うか
        [Test]
        public void DecideOrderType_RawMaterialIsPurchase_PartIsProduction()
        {
            Assert.AreEqual(OrderType.購買オーダーPO, ProductionOrderRules.DecideOrderType(true));
            Assert.AreEqual(OrderType.生産オーダーPrO, ProductionOrderRules.DecideOrderType(false));
        }

        // 内製/外注：能力があり同コストまでは内製、割高なら外注、能力なしは外注
        [Test]
        public void MakeOrBuyDecision_RespectsCapacityAndCost()
        {
            // 能力あり・内製が安い→内製
            Assert.AreEqual(OrderType.生産オーダーPrO,
                ProductionOrderRules.MakeOrBuyDecision(80f, 100f, true));
            // 能力あり・内製が割高（許容比1.0超）→外注
            Assert.AreEqual(OrderType.購買オーダーPO,
                ProductionOrderRules.MakeOrBuyDecision(120f, 100f, true));
            // 能力なし→たとえ安くても外注
            Assert.AreEqual(OrderType.購買オーダーPO,
                ProductionOrderRules.MakeOrBuyDecision(50f, 100f, false));
        }

        // 同コストちょうどは内製（許容比1.0＝外注と同額まで作る）
        [Test]
        public void MakeOrBuyDecision_EqualCostMakesInHouse()
        {
            Assert.AreEqual(OrderType.生産オーダーPrO,
                ProductionOrderRules.MakeOrBuyDecision(100f, 100f, true));
        }

        // 設備割付：同種設備のうち最も負荷の低い所（負荷分散）。同値は先着
        [Test]
        public void AssignFacility_PicksLeastLoaded()
        {
            var loads = new[] { 0.8f, 0.3f, 0.5f };
            Assert.AreEqual(1, ProductionOrderRules.AssignFacility(loads));

            // 空配列は割付先なし
            Assert.AreEqual(-1, ProductionOrderRules.AssignFacility(new float[0]));
            Assert.AreEqual(-1, ProductionOrderRules.AssignFacility(null));
        }

        // 優先度：納期が近いほど・重要度が高いほど高い。遅延は最優先
        [Test]
        public void OrderPriority_NearDueAndCriticalRanksHigher()
        {
            // 遅延（dueDate<currentTime）＝緊急度1。重要度1→優先度1
            Assert.AreEqual(1f, ProductionOrderRules.OrderPriority(50f, 60f, 1f), 1e-4f);

            // 残り50秒（urgencyHorizon100）＝緊急度0.5、重要度0.5：(0.5×0.6+0.5×0.4)/1.0=0.5
            float p = ProductionOrderRules.OrderPriority(50f, 0f, 0.5f);
            Assert.AreEqual(0.5f, p, 1e-4f);

            // 納期が近い方が遠い方より優先度が高い（重要度同一）
            float near = ProductionOrderRules.OrderPriority(20f, 0f, 0.3f);
            float far = ProductionOrderRules.OrderPriority(90f, 0f, 0.3f);
            Assert.Greater(near, far);
        }

        // 発注まとめ：窓内の数量を束ねる（ロット集約）。窓0以下は全合算
        [Test]
        public void ConsolidateOrders_SumsWithinWindow()
        {
            var qty = new[] { 10, 20, 30, 40 };
            Assert.AreEqual(30, ProductionOrderRules.ConsolidateOrders(qty, 2)); // 10+20
            Assert.AreEqual(100, ProductionOrderRules.ConsolidateOrders(qty, 0)); // 全件
            Assert.AreEqual(100, ProductionOrderRules.ConsolidateOrders(qty, 10)); // 窓>件数
            Assert.AreEqual(0, ProductionOrderRules.ConsolidateOrders(null, 2));
        }

        // 能力引き当て：占有率＝数量/能力（超過は飽和1・能力0は飽和・数量0は0）
        [Test]
        public void CapacityReservation_ComputesOccupancy()
        {
            Assert.AreEqual(0.5f, ProductionOrderRules.CapacityReservation(50, 100f), 1e-4f);
            Assert.AreEqual(1f, ProductionOrderRules.CapacityReservation(150, 100f), 1e-4f); // 超過＝飽和
            Assert.AreEqual(1f, ProductionOrderRules.CapacityReservation(10, 0f), 1e-4f); // 容量なし
            Assert.AreEqual(0f, ProductionOrderRules.CapacityReservation(0, 100f), 1e-4f); // 数量なし
        }

        // 既定Params値の固定
        [Test]
        public void DefaultParams_HasExpectedValues()
        {
            var p = ProductionOrderParams.Default;
            Assert.AreEqual(1.0f, p.inHouseCostTolerance, 1e-4f);
            Assert.AreEqual(0.6f, p.urgencyWeight, 1e-4f);
            Assert.AreEqual(0.4f, p.criticalityWeight, 1e-4f);
            Assert.AreEqual(100f, p.urgencyHorizon, 1e-4f);
        }
    }
}
