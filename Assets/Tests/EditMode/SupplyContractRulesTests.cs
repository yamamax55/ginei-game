using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>供給契約ロジック（#1006）の純ロジック担保。長期契約の安定vs柔軟性喪失・破棄の外交余波を固定する。</summary>
    public class SupplyContractRulesTests
    {
        const float Tol = 1e-4f;

        /// <summary>長期契約の被覆率が高いほど価格が安定する（被覆率1.0で stabilityBase=0.9）。</summary>
        [Test]
        public void 被覆率が高いほど価格が安定する()
        {
            Assert.AreEqual(0f, SupplyContractRules.ContractPriceStability(0f), Tol);
            Assert.AreEqual(0.45f, SupplyContractRules.ContractPriceStability(0.5f), Tol);
            Assert.AreEqual(0.9f, SupplyContractRules.ContractPriceStability(1f), Tol);
        }

        /// <summary>契約していない分だけスポット価格変動に晒される（被覆0.6×ボラ0.5→0.2）。</summary>
        [Test]
        public void 未契約分がスポット価格に晒される()
        {
            Assert.AreEqual(0.5f, SupplyContractRules.SpotExposure(0f, 0.5f), Tol);   // 全量スポット＝ボラそのもの
            Assert.AreEqual(0.2f, SupplyContractRules.SpotExposure(0.6f, 0.5f), Tol); // (1-0.6)*0.5
            Assert.AreEqual(0f, SupplyContractRules.SpotExposure(1f, 0.5f), Tol);     // 全量長期契約＝曝露なし
        }

        /// <summary>長期契約は柔軟性を売る＝被覆率が高いほど需要変動に追従できない（被覆1.0で rigidityWeight=0.8）。</summary>
        [Test]
        public void 長期契約は柔軟性を失う()
        {
            Assert.AreEqual(0f, SupplyContractRules.FlexibilityLoss(0f), Tol);
            Assert.AreEqual(0.8f, SupplyContractRules.FlexibilityLoss(1f), Tol);
            // 安定と柔軟性喪失は同じ被覆率で逆方向のトレードオフ：固めるほど安定するが硬直する
            Assert.Greater(SupplyContractRules.ContractPriceStability(1f), SupplyContractRules.ContractPriceStability(0f));
            Assert.Greater(SupplyContractRules.FlexibilityLoss(1f), SupplyContractRules.FlexibilityLoss(0f));
        }

        /// <summary>破棄ペナルティは残存期間×価値（残りが長いほど高くつく）。価値100×残10×単価1.0=1000。</summary>
        [Test]
        public void 破棄は残存期間が長いほど高くつく()
        {
            Assert.AreEqual(1000f, SupplyContractRules.BreachPenalty(100f, 10f), Tol);
            Assert.AreEqual(100f, SupplyContractRules.BreachPenalty(100f, 1f), Tol);
            Assert.AreEqual(0f, SupplyContractRules.BreachPenalty(100f, 0f), Tol); // 満了直前は実質ノーコスト
        }

        /// <summary>大国との契約破棄は外交余波が大きい（価値100×国力1.0×重み1.5=150）／弱小相手はほぼゼロ。</summary>
        [Test]
        public void 破棄の外交余波は相手の国力に比例する()
        {
            Assert.AreEqual(150f, SupplyContractRules.BreachDiplomaticFallout(100f, 1f), Tol); // 大国＝制裁/戦争リスク
            Assert.AreEqual(0f, SupplyContractRules.BreachDiplomaticFallout(100f, 0f), Tol);   // 弱小＝商売の範囲
            Assert.Greater(
                SupplyContractRules.BreachDiplomaticFallout(100f, 1f),
                SupplyContractRules.BreachDiplomaticFallout(100f, 0.2f));
        }

        /// <summary>最適比率＝需要安定×価格変動なら長期契約・需要変動が大きいならスポット。</summary>
        [Test]
        public void 需要安定価格変動なら長期契約が最適()
        {
            // 需要安定(0)×価格荒れ(0.9)→0.9＝長期契約を厚く
            Assert.AreEqual(0.9f, SupplyContractRules.OptimalContractMix(0f, 0.9f), Tol);
            // 需要が荒れる(0.8)→価格が荒れても比率は下がる(0.9*0.2=0.18)＝スポット寄り
            Assert.AreEqual(0.18f, SupplyContractRules.OptimalContractMix(0.8f, 0.9f), Tol);
            // 価格が安定(0)なら固める意味がない
            Assert.AreEqual(0f, SupplyContractRules.OptimalContractMix(0f, 0f), Tol);
        }

        /// <summary>推奨様式は最適比率から離散化される（厚い→長期契約／薄い→スポット／中庸→混合）。</summary>
        [Test]
        public void 推奨様式が最適比率から離散化される()
        {
            Assert.AreEqual(ProcurementMode.長期契約, SupplyContractRules.RecommendMode(0f, 0.9f));   // mix=0.9
            Assert.AreEqual(ProcurementMode.スポット, SupplyContractRules.RecommendMode(0.8f, 0.9f)); // mix=0.18
            Assert.AreEqual(ProcurementMode.混合, SupplyContractRules.RecommendMode(0f, 0.5f));       // mix=0.5
        }

        /// <summary>全入力はクランプされ範囲外でも破綻しない。</summary>
        [Test]
        public void 入力はクランプされる()
        {
            Assert.AreEqual(0.9f, SupplyContractRules.ContractPriceStability(5f), Tol);   // 被覆率>1→1扱い
            Assert.AreEqual(1f, SupplyContractRules.SpotExposure(-1f, 2f), Tol);          // 被覆-1→0, ボラ2→1
            Assert.AreEqual(0f, SupplyContractRules.BreachPenalty(-100f, 10f), Tol);      // 負の価値→0
            Assert.AreEqual(0f, SupplyContractRules.OptimalContractMix(-1f, -1f), Tol);
        }
    }
}
