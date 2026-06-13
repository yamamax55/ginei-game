using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// MRP所要量計算（#984・MrpRules）の純ロジックテスト。正味所要（在庫で足りる分は発注しない・余剰は0）・
    /// 計画オーダーのロット丸め・発注時期のリードタイム遡り・安全在庫・発注点・在庫予測残高を既定Paramsの具体値で固定。
    /// </summary>
    public class MrpRulesTests
    {
        /// <summary>正味所要＝総所要−在庫−入荷予定（在庫＋入荷で手当てされる分は発注しない）。</summary>
        [Test]
        public void NetRequirement_在庫と入荷予定を差し引く()
        {
            // 総所要100・在庫30・入荷予定20 → 正味50。
            Assert.AreEqual(50f, MrpRules.NetRequirement(100f, 30f, 20f), 1e-4f);
        }

        /// <summary>手持＋入荷で総所要を上回る＝余剰なら正味所要は0（余っていても発注しない）。</summary>
        [Test]
        public void NetRequirement_余剰は0にクランプ()
        {
            // 総所要50・在庫40・入荷予定30 → −20 → 0。
            Assert.AreEqual(0f, MrpRules.NetRequirement(50f, 40f, 30f), 1e-4f);
        }

        /// <summary>計画オーダーはロットサイズの倍数へ切り上げ（端数も1ロット＝まとめ買い）。</summary>
        [Test]
        public void PlannedOrderQuantity_ロットサイズで切り上げる()
        {
            // 正味50・ロット20 → 切り上げ3ロット → 60。
            Assert.AreEqual(60f, MrpRules.PlannedOrderQuantity(50f, 20f), 1e-4f);
            // 正味0＝発注不要 → 0。
            Assert.AreEqual(0f, MrpRules.PlannedOrderQuantity(0f, 20f), 1e-4f);
            // ロット無し（<=0）＝ロットフォーロット＝必要なだけ。
            Assert.AreEqual(50f, MrpRules.PlannedOrderQuantity(50f, 0f), 1e-4f);
        }

        /// <summary>最小発注量を下回る計画オーダーは最小発注量へ引き上げる。</summary>
        [Test]
        public void PlannedOrderQuantity_最小発注量へ引き上げる()
        {
            // 正味5・ロット無し・最小25 → 25。
            Assert.AreEqual(25f, MrpRules.PlannedOrderQuantity(5f, 0f, 25f), 1e-4f);
        }

        /// <summary>発注時期＝必要日からリードタイムを遡る（間に合わせるには今出すか）。</summary>
        [Test]
        public void OrderReleaseTiming_リードタイムを遡る()
        {
            // 必要日10・リードタイム3 → 発注は7日目。
            Assert.AreEqual(7f, MrpRules.OrderReleaseTiming(10f, 3f), 1e-4f);
            // 必要日2・リードタイム5 → 既に手遅れ → 0（今すぐ発注）。
            Assert.AreEqual(0f, MrpRules.OrderReleaseTiming(2f, 5f), 1e-4f);
        }

        /// <summary>安全在庫＝ばらつき×√リードタイム×安全係数。ばらつき0なら0（余分な在庫を持たない）。</summary>
        [Test]
        public void SafetyStockRequirement_ばらつきとリードタイムで増える()
        {
            // 既定safetyFactor=1.5・ばらつき0.5・リードタイム4 → 1.5×0.5×√4=1.5。
            Assert.AreEqual(1.5f, MrpRules.SafetyStockRequirement(0.5f, 4f), 1e-4f);
            // ばらつき0＝需要確定 → 安全在庫0。
            Assert.AreEqual(0f, MrpRules.SafetyStockRequirement(0f, 4f), 1e-4f);
        }

        /// <summary>発注点＝平均需要×リードタイム＋安全在庫（割ったら次の入荷前に欠品）。</summary>
        [Test]
        public void ReorderPoint_リードタイム需要に安全在庫を足す()
        {
            // 平均需要10・リードタイム3・安全在庫5 → 10×3+5=35。
            Assert.AreEqual(35f, MrpRules.ReorderPoint(10f, 3f, 5f), 1e-4f);
        }

        /// <summary>在庫予測残高＝手持＋入荷予定−総所要（負＝不足を符号で示す）。</summary>
        [Test]
        public void InventoryProjection_不足を負で返す()
        {
            // 手持30・入荷20・総所要100 → −50（不足50）。
            Assert.AreEqual(-50f, MrpRules.InventoryProjection(30f, 20f, 100f), 1e-4f);
            // 手持60・入荷20・総所要50 → +30（余剰30）。
            Assert.AreEqual(30f, MrpRules.InventoryProjection(60f, 20f, 50f), 1e-4f);
        }
    }
}
