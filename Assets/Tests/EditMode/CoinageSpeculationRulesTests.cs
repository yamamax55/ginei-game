using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 改鋳投機（#1073・戦わぬ経済戦は情報戦）を固定する：信じられた噂ほど相場が動き、品位上昇の噂は
    /// 旧貨を買い・低下の噂は売り抜け、噂どおりなら儲かり外れれば損（情報の精度が利益を決める）、
    /// 改鋳を事前に知る内部者が一番儲け、噂が噂を呼ぶと信認が改鋳前から揺らぎ、低下の噂で良貨が退蔵される。
    /// </summary>
    public class CoinageSpeculationRulesTests
    {
        private static readonly CoinageSpeculationParams P = CoinageSpeculationParams.Default;
        // 噂強度1.0/ポジション1.0/利益傾き1.0/内部情報2.0/混乱0.2

        [Test]
        public void RumorImpact_CredibleRumorMovesMarket()
        {
            // 信憑性0.8×品位上昇方向+1.0×強度1.0＝+0.8（買い圧）
            Assert.AreEqual(0.8f, CoinageSpeculationRules.RumorImpact(0.8f, 1f, P), 1e-4f);
            // 品位低下方向−1.0なら負（売り圧）
            Assert.AreEqual(-0.8f, CoinageSpeculationRules.RumorImpact(0.8f, -1f, P), 1e-4f);
            // 信じられない噂（信憑性0）は相場を動かさない
            Assert.AreEqual(0f, CoinageSpeculationRules.RumorImpact(0f, 1f, P), 1e-4f);
            // 信憑性が高いほど相場が大きく動く＝噂で値が動く
            Assert.Greater(CoinageSpeculationRules.RumorImpact(0.9f, 1f, P), CoinageSpeculationRules.RumorImpact(0.3f, 1f, P));
        }

        [Test]
        public void SpeculativePosition_RiseBuysFallSells()
        {
            // 上昇インパクト+0.8×資本1000×リスク選好0.5＝+400（旧貨を買う＝ロング）
            Assert.AreEqual(400f, CoinageSpeculationRules.SpeculativePosition(0.8f, 1000f, 0.5f, P), 1e-3f);
            // 低下インパクト−0.8なら負（売り抜け＝ショート）
            Assert.AreEqual(-400f, CoinageSpeculationRules.SpeculativePosition(-0.8f, 1000f, 0.5f, P), 1e-3f);
        }

        [Test]
        public void SpeculativeProfit_RightRumorWinsWrongRumorLoses()
        {
            // 買い建玉+400・噂どおり品位上昇+0.5が実現＝+400×0.5＝+200の儲け
            Assert.AreEqual(200f, CoinageSpeculationRules.SpeculativeProfit(400f, 0.5f, 0.5f, P), 1e-3f);
            // 噂が外れて逆（品位低下−0.5）に動けば＝+400×(−0.5)＝−200の損
            Assert.AreEqual(-200f, CoinageSpeculationRules.SpeculativeProfit(400f, -0.5f, 0.5f, P), 1e-3f);
            // 売り建玉−400で品位が下がれば（−0.5）＝+200の儲け
            Assert.AreEqual(200f, CoinageSpeculationRules.SpeculativeProfit(-400f, -0.5f, -0.5f, P), 1e-3f);
        }

        [Test]
        public void InsiderAdvantage_EarliestInfoWinsMost()
        {
            // 情報の早さ1.0×倍率2.0＝最大優位2.0（事前に知る者が一番儲ける）
            Assert.AreEqual(2f, CoinageSpeculationRules.InsiderAdvantage(1f, P), 1e-4f);
            // 情報なし（0）は優位なし
            Assert.AreEqual(0f, CoinageSpeculationRules.InsiderAdvantage(0f, P), 1e-4f);
            // 早い情報ほど優位が大きい＝情報の非対称
            Assert.Greater(CoinageSpeculationRules.InsiderAdvantage(0.8f, P), CoinageSpeculationRules.InsiderAdvantage(0.2f, P));
        }

        [Test]
        public void MarketPanic_RumorsBegetRumors()
        {
            // インパクト0.5×累積噂4×強度0.2＝0.4の混乱
            Assert.AreEqual(0.4f, CoinageSpeculationRules.MarketPanic(0.5f, 4, P), 1e-4f);
            // 累積が増えるほど信認が揺らぐ（改鋳前から実体が動く）
            Assert.Greater(CoinageSpeculationRules.MarketPanic(0.5f, 8, P), CoinageSpeculationRules.MarketPanic(0.5f, 2, P));
            // 噂が無ければ混乱なし
            Assert.AreEqual(0f, CoinageSpeculationRules.MarketPanic(0.5f, 0, P), 1e-4f);
            // 多数の噂でも0..1にクランプ（信認崩壊寸前で天井）
            Assert.AreEqual(1f, CoinageSpeculationRules.MarketPanic(1f, 100, P), 1e-4f);
        }

        [Test]
        public void HoardingPressure_DebasementRumorHoardsGoodCoin()
        {
            // 品位低下方向−1.0×退蔵性向0.7＝0.7ぶん良貨が退蔵（グレシャムの前倒し）
            Assert.AreEqual(0.7f, CoinageSpeculationRules.HoardingPressure(-1f, 0.7f), 1e-4f);
            // 品位上昇の噂（正方向）では退蔵は起きない
            Assert.AreEqual(0f, CoinageSpeculationRules.HoardingPressure(1f, 0.7f), 1e-4f);
            // 退蔵性向が低ければ抱え込みも弱い
            Assert.Greater(CoinageSpeculationRules.HoardingPressure(-1f, 0.9f), CoinageSpeculationRules.HoardingPressure(-1f, 0.2f));
        }
    }
}
