using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// CorneringRules（買い占め・投機・バブル＝商品コーナリング動学・#1076）の純ロジックテスト。
    /// 支配率での価格吊り上げ（非線形）・買い占めコストのスリッページ・実需なきバブルの脆さ・崩壊の大損を担保する。
    /// </summary>
    public class CorneringRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>市場支配率＝買い占めた割合（在庫/総供給・クランプ）。</summary>
        [Test]
        public void MarketControl_保有を総供給で割った支配率()
        {
            Assert.AreEqual(0.25f, CorneringRules.MarketControl(25f, 100f), Eps);
            Assert.AreEqual(1f, CorneringRules.MarketControl(150f, 100f), Eps); // 超過は1にクランプ
            Assert.AreEqual(0f, CorneringRules.MarketControl(10f, 0f), Eps);    // 供給0は支配0
        }

        /// <summary>価格吊り上げ＝支配率が高いほど非線形に跳ねる（指数2・支配後に一気に）。</summary>
        [Test]
        public void PriceManipulation_支配率で非線形に吊り上げ()
        {
            // 既定 maxManipulation=2, exponent=2 ＝ 2×control^2
            Assert.AreEqual(0f, CorneringRules.PriceManipulation(0f), Eps);
            Assert.AreEqual(0.5f, CorneringRules.PriceManipulation(0.5f), Eps);  // 2×0.25
            Assert.AreEqual(2f, CorneringRules.PriceManipulation(1f), Eps);      // 完全買い占め＝+200%
            // 非線形＝支配率が倍でも吊り上げは4倍
            Assert.Greater(CorneringRules.PriceManipulation(0.8f), CorneringRules.PriceManipulation(0.4f) * 2f);
        }

        /// <summary>買い占めコスト＝支配を強めるほど自分の買いが値を上げて割高（凸）。</summary>
        [Test]
        public void CornerCost_支配を強めるほど割高なスリッページ()
        {
            // 既定 slippageScale=1, slippageExponent=2 ＝ qty×price×(1+control^2)
            // control=0.5, price=10, supply=100 → qty=50, factor=1.25 → 50×10×1.25=625
            Assert.AreEqual(625f, CorneringRules.CornerCost(0.5f, 10f, 100f), Eps);
            // control=1.0 → qty=100, factor=2 → 100×10×2=2000
            Assert.AreEqual(2000f, CorneringRules.CornerCost(1f, 10f, 100f), Eps);
            // 単価あたりコスト（割高度）は支配を強めるほど上がる
            float per50 = CorneringRules.CornerCost(0.5f, 10f, 100f) / (0.5f * 100f);
            float per100 = CorneringRules.CornerCost(1f, 10f, 100f) / (1f * 100f);
            Assert.Greater(per100, per50);
        }

        /// <summary>バブルの脆さ＝実需の裏付けが無いほど脆い（砂上の楼閣）。</summary>
        [Test]
        public void BubbleFragility_実需なきほど脆い()
        {
            // control=1 → manipulation=2、fundamental=0 → 2×(1−0)=2 → クランプ1
            Assert.AreEqual(1f, CorneringRules.BubbleFragility(1f, 0f), Eps);
            // 実需が満点なら脆さ0＝裏付けある価格は崩れない
            Assert.AreEqual(0f, CorneringRules.BubbleFragility(1f, 1f), Eps);
            // 同じ吊り上げでも実需が薄いほど脆い
            Assert.Greater(
                CorneringRules.BubbleFragility(0.6f, 0.1f),
                CorneringRules.BubbleFragility(0.6f, 0.8f));
        }

        /// <summary>バブル崩壊＝脆さに応じ roll で判定・引き金で崩れやすくなる。</summary>
        [Test]
        public void Burst_脆さと引き金で崩壊()
        {
            // fragility=0.4、引き金なし＝chance0.4：roll0.3で崩壊・roll0.5で持ちこたえる
            Assert.IsTrue(CorneringRules.Burst(0.4f, false, 0.3f));
            Assert.IsFalse(CorneringRules.Burst(0.4f, false, 0.5f));
            // 引き金あり＝chance0.8（triggerBoost=2）：roll0.5でも崩壊
            Assert.IsTrue(CorneringRules.Burst(0.4f, true, 0.5f));
            // 脆さ0なら何があっても崩れない
            Assert.IsFalse(CorneringRules.Burst(0f, true, 0f));
        }

        /// <summary>手仕舞いの損失＝天井で買った在庫が暴落で大損（投げ売り罰込み）。</summary>
        [Test]
        public void UnwindLoss_崩壊で売り抜けられず大損()
        {
            // holdings=100, peak=30, crash=10 → drop=20 → 100×20×(1+0.3)=2600
            Assert.AreEqual(2600f, CorneringRules.UnwindLoss(100f, 30f, 10f), Eps);
            // 投げ売り罰ぶん単純損失より大きい（コーナリングの失敗は破産）
            Assert.Greater(CorneringRules.UnwindLoss(100f, 30f, 10f), 100f * 20f);
            // 値上がり中（crash≥peak）なら損失0
            Assert.AreEqual(0f, CorneringRules.UnwindLoss(100f, 10f, 30f), Eps);
        }

        /// <summary>既定Paramsの境界クランプ（指数≥1・ブースト≥1）。</summary>
        [Test]
        public void Params_境界クランプ()
        {
            var p = new CorneringParams(-1f, 0.2f, -2f, 0.5f, -1f, 0.3f, -1f);
            Assert.AreEqual(0f, p.maxManipulation, Eps);
            Assert.AreEqual(1f, p.manipulationExponent, Eps); // 線形未満にしない
            Assert.AreEqual(0f, p.slippageScale, Eps);
            Assert.AreEqual(1f, p.slippageExponent, Eps);
            Assert.AreEqual(0f, p.fragilityScale, Eps);
            Assert.AreEqual(1f, p.triggerBoost, Eps);         // 引き金は崩壊を弱めない
            Assert.AreEqual(0f, p.liquidationPenalty, Eps);
        }

        /// <summary>一連の動学＝買い占めて吊り上げ、実需なきバブルが崩壊して在庫もろとも損する。</summary>
        [Test]
        public void シナリオ_買い占めから崩壊までの一貫した動学()
        {
            float control = CorneringRules.MarketControl(80f, 100f); // 0.8 支配
            float manip = CorneringRules.PriceManipulation(control);
            Assert.Greater(manip, 1f); // 大幅に吊り上げ（+128%）
            float fragility = CorneringRules.BubbleFragility(control, 0.1f); // 実需薄＝脆い
            Assert.Greater(fragility, 0.5f);
            Assert.IsTrue(CorneringRules.Burst(fragility, true, 0.4f)); // 引き金で崩壊
            float loss = CorneringRules.UnwindLoss(80f, 30f, 5f);
            Assert.Greater(loss, 0f); // 崩壊で大損
        }
    }
}
