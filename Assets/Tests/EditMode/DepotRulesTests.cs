using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 前進補給基地（補給デポ・CRV-1 #1363）の純ロジック検証。デポが補給の基点を前方へ移し作戦到達限界を
    /// 延伸する＝倉庫システム。既定 <see cref="DepotParams.Default"/>（延伸0.6/緩衝0.5/前進脆弱0.8/設置0.2/
    /// 敵地0.5/終末点遅延0.5/リレー0.6/閾値0.4）で期待値を固定。
    /// </summary>
    public class DepotRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>到達限界の延伸＝前進×処理能力に比例し、満点で延伸上限0.6。どちらかが0なら延伸しない。</summary>
        [Test]
        public void ReachExtension_前進と処理能力の積で延伸する()
        {
            // 前進1・処理能力1 → 0.6×1×1=0.6（上限）
            Assert.AreEqual(0.6f, DepotRules.ReachExtension(1f, 1f), Eps);
            // 前進0.5・処理能力0.5 → 0.6×0.5×0.5=0.15
            Assert.AreEqual(0.15f, DepotRules.ReachExtension(0.5f, 0.5f), Eps);
            // 前進だけ・倉庫だけでは伸びない
            Assert.AreEqual(0f, DepotRules.ReachExtension(1f, 0f), Eps);
            Assert.AreEqual(0f, DepotRules.ReachExtension(0f, 1f), Eps);
        }

        /// <summary>実効補給範囲＝本国基礎範囲＋未到達域を延伸で埋める。デポが届く先まで補給が伸びる。</summary>
        [Test]
        public void EffectiveSupplyRange_基礎範囲に延伸を足す()
        {
            // 基礎0.4・延伸0.5 → 0.4 + (1-0.4)×0.5 = 0.7
            Assert.AreEqual(0.7f, DepotRules.EffectiveSupplyRange(0.4f, 0.5f), Eps);
            // 延伸0なら基礎のまま
            Assert.AreEqual(0.4f, DepotRules.EffectiveSupplyRange(0.4f, 0f), Eps);
            // 延伸1なら全域へ到達（上限1）
            Assert.AreEqual(1f, DepotRules.EffectiveSupplyRange(0.4f, 1f), Eps);
        }

        /// <summary>備蓄が前線需要の振れを吸収する＝在庫が高いほど実効需要が平準化（在庫が補給を安定させる）。</summary>
        [Test]
        public void StockpileBuffer_備蓄が需要変動を吸収する()
        {
            // 備蓄1・需要1・dt=1 → absorb=0.5×1×1=0.5 → 1×(1-0.5)=0.5
            Assert.AreEqual(0.5f, DepotRules.StockpileBuffer(1f, 1f, 1f), Eps);
            // 備蓄0なら緩衝なし＝需要がそのまま
            Assert.AreEqual(1f, DepotRules.StockpileBuffer(0f, 1f, 1f), Eps);
            // 備蓄が高いほど実効需要が下がる（単調）
            Assert.Less(DepotRules.StockpileBuffer(0.8f, 1f, 1f), DepotRules.StockpileBuffer(0.3f, 1f, 1f));
        }

        /// <summary>前進したデポは敵に近く狙われやすい＝前進と防御のトレードオフ。本国の倉庫は安全。</summary>
        [Test]
        public void DepotVulnerability_前進で上がり防御で下がる()
        {
            // 前進1・防御0 → 0.8×1×(1-0)=0.8
            Assert.AreEqual(0.8f, DepotRules.DepotVulnerability(1f, 0f), Eps);
            // 前進1・防御0.5 → 0.8×(1-0.5)=0.4
            Assert.AreEqual(0.4f, DepotRules.DepotVulnerability(1f, 0.5f), Eps);
            // 本国（前進0）は安全
            Assert.AreEqual(0f, DepotRules.DepotVulnerability(0f, 0f), Eps);
        }

        /// <summary>補給リレー＝デポ数×間隔で補給線が段階的に伸びる。1基だけ・偏った配置ではリレー不成立。</summary>
        [Test]
        public void SupplyRelayChain_中継で補給線が伸びる()
        {
            // デポ数1・間隔1 → 0.6×1×1=0.6（上限）
            Assert.AreEqual(0.6f, DepotRules.SupplyRelayChain(1f, 1f), Eps);
            // デポ数0.5・間隔0.5 → 0.6×0.5×0.5=0.15
            Assert.AreEqual(0.15f, DepotRules.SupplyRelayChain(0.5f, 0.5f), Eps);
            // 偏った配置（間隔0）ではリレー成立せず
            Assert.AreEqual(0f, DepotRules.SupplyRelayChain(1f, 0f), Eps);
        }

        /// <summary>前進したデポを敵地に築くほどコストが高い＝敵の眼前の倉庫は費用がかさむ。本国近傍は基礎のみ。</summary>
        [Test]
        public void DepotEstablishmentCost_前進と敵地で割増される()
        {
            // 前進1・敵地1 → 0.2 + 0.5×1×1 = 0.7
            Assert.AreEqual(0.7f, DepotRules.DepotEstablishmentCost(1f, 1f), Eps);
            // 本国近傍（前進0・敵対0）は基礎コストのみ
            Assert.AreEqual(0.2f, DepotRules.DepotEstablishmentCost(0f, 0f), Eps);
            // 前進1・敵地0.5 → 0.2 + 0.5×1×0.5 = 0.45
            Assert.AreEqual(0.45f, DepotRules.DepotEstablishmentCost(1f, 0.5f), Eps);
        }

        /// <summary>デポが攻勢終末点の到来を遅らせる＝延伸が大きく進撃が深いほど効く（補給が続けば進める）。</summary>
        [Test]
        public void AdvanceCulminationDelay_延伸と進撃深度で終末点が遠のく()
        {
            // 延伸1・進撃深度1 → 0.5×1×1=0.5（上限）
            Assert.AreEqual(0.5f, DepotRules.AdvanceCulminationDelay(1f, 1f), Eps);
            // 延伸0.6・進撃深度0.5 → 0.5×0.6×0.5=0.15
            Assert.AreEqual(0.15f, DepotRules.AdvanceCulminationDelay(0.6f, 0.5f), Eps);
            // 進撃が浅い（手前）なら恩恵小
            Assert.AreEqual(0f, DepotRules.AdvanceCulminationDelay(0.6f, 0f), Eps);
        }

        /// <summary>前進補給確立＝延伸×備蓄が閾値以上。射程を伸ばしても在庫が無ければ確立しない。</summary>
        [Test]
        public void IsForwardSupplyEstablished_延伸と備蓄の両立で確立する()
        {
            // 延伸0.8×備蓄0.6=0.48 ≥ 0.4 → 確立
            Assert.IsTrue(DepotRules.IsForwardSupplyEstablished(0.8f, 0.6f));
            // 延伸0.9でも備蓄0.1 → 0.09 < 0.4 → 未確立（在庫が無ければ枯れる）
            Assert.IsFalse(DepotRules.IsForwardSupplyEstablished(0.9f, 0.1f));
            // 明示閾値も尊重
            Assert.IsTrue(DepotRules.IsForwardSupplyEstablished(0.5f, 0.5f, 0.2f));
            Assert.IsFalse(DepotRules.IsForwardSupplyEstablished(0.5f, 0.5f, 0.3f));
        }
    }
}
