using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 攻城の強襲 vs 兵糧攻め（SiegeAssaultRules）の純ロジックテスト。
    /// 既定 Params(0.10/0.50/0.20/0.10/0.02/0.05) で期待値を固定。
    /// </summary>
    public class SiegeAssaultRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 強襲損害は防御施設が固いほど大きい()
        {
            // 100兵 × 0.10 × (1 + fort×0.5)
            float hard = SiegeAssaultRules.AssaultCasualties(100f, 1f); // 100×0.1×1.5
            float soft = SiegeAssaultRules.AssaultCasualties(100f, 0f); // 100×0.1×1.0
            Assert.AreEqual(15f, hard, Eps);
            Assert.AreEqual(10f, soft, Eps);
            Assert.Greater(hard, soft);
        }

        [Test]
        public void 強襲速度は無防備で最大_完全防御で停滞()
        {
            // 100 × 0.20 × (1 − fort)
            Assert.AreEqual(20f, SiegeAssaultRules.AssaultSpeed(100f, 0f), Eps);
            Assert.AreEqual(0f, SiegeAssaultRules.AssaultSpeed(100f, 1f), Eps);
        }

        [Test]
        public void 兵糧攻めは残量を超えて削らない()
        {
            // 封鎖50 × 0.10 × dt=1 = 5
            Assert.AreEqual(5f, SiegeAssaultRules.StarvationProgress(50f, 100f, 1f), Eps);
            // 残量3しか無ければ3でクランプ
            Assert.AreEqual(3f, SiegeAssaultRules.StarvationProgress(50f, 3f, 1f), Eps);
            // dt<=0 は進まない
            Assert.AreEqual(0f, SiegeAssaultRules.StarvationProgress(50f, 100f, 0f), Eps);
        }

        [Test]
        public void 包囲消耗は時間で増え1で頭打ち()
        {
            Assert.AreEqual(0.2f, SiegeAssaultRules.BlockadeAttrition(10f), Eps);  // 10×0.02
            Assert.AreEqual(1f, SiegeAssaultRules.BlockadeAttrition(100f), Eps);   // clamp01(2)
            Assert.AreEqual(0f, SiegeAssaultRules.BlockadeAttrition(-5f), Eps);    // 負はクランプ
        }

        [Test]
        public void 速攻と損害のトレードオフは中立で0_両極で正負1()
        {
            Assert.AreEqual(1f, SiegeAssaultRules.AssaultVsSiegeTradeoff(1f, 1f), Eps);    // 急ぐ＋損害許容＝強襲
            Assert.AreEqual(-1f, SiegeAssaultRules.AssaultVsSiegeTradeoff(0f, 0f), Eps);  // 余裕＋損害惜しむ＝包囲
            Assert.AreEqual(0f, SiegeAssaultRules.AssaultVsSiegeTradeoff(0.5f, 0.5f), Eps);
        }

        [Test]
        public void 出撃逆襲リスクは長期と守備兵力で上がる()
        {
            // 10日 × 0.05 × (1 + clamp01(100/100)) = 1.0
            Assert.AreEqual(1f, SiegeAssaultRules.SallyRisk(10f, 100f), Eps);
            // 1日 × 0.05 × (1 + 0) = 0.05
            Assert.AreEqual(0.05f, SiegeAssaultRules.SallyRisk(1f, 0f), Eps);
        }

        [Test]
        public void 突破口拡張は予備兵力で広がり突破口無しでは0()
        {
            // 0.5 × clamp01(100/100) × 1.5 = 0.75
            Assert.AreEqual(0.75f, SiegeAssaultRules.BreachExploitation(0.5f, 100f), Eps);
            // 突破口が無ければ広げられない
            Assert.AreEqual(0f, SiegeAssaultRules.BreachExploitation(0f, 100f), Eps);
        }

        [Test]
        public void 要塞は補給枯渇か士気崩壊で持たなくなる()
        {
            Assert.IsTrue(SiegeAssaultRules.IsFortressUntenable(0f, 0.5f, 0.2f));   // 補給尽きた
            Assert.IsTrue(SiegeAssaultRules.IsFortressUntenable(50f, 0.1f, 0.2f));  // 士気が閾値割れ
            Assert.IsFalse(SiegeAssaultRules.IsFortressUntenable(50f, 0.5f, 0.2f)); // どちらも余裕
        }

        [Test]
        public void 物語_強襲は速いが大損害_兵糧攻めは遅いが損害小だが長期で逆襲リスク()
        {
            // 同じ堅城(fort=0.8)・同じ攻め手(兵力100)で、強襲と包囲を比べる。
            float fort = 0.8f;
            float assaultSpeed = SiegeAssaultRules.AssaultSpeed(100f, fort);     // 速いが…
            float assaultCost  = SiegeAssaultRules.AssaultCasualties(100f, fort); // 大損害
            // 包囲＝封鎖で干上がるのを待つ＝即時の攻略進捗(強襲速度)はない。
            // 一定日数の包囲で攻め手が被る消耗は強襲の損害より小さい。
            float blockadeCost = SiegeAssaultRules.BlockadeAttrition(5f) * 100f;  // 5日 → 0.1×100 = 10

            Assert.Greater(assaultSpeed, 0f, "強襲は即座に攻略を進める");
            Assert.Greater(assaultCost, blockadeCost, "強襲の損害は短期包囲の消耗より大きい");

            // ただし包囲を長引かせると敵が打って出る逆襲リスクが高まる（速攻の誘因）。
            float earlyRisk = SiegeAssaultRules.SallyRisk(2f, 80f);
            float lateRisk  = SiegeAssaultRules.SallyRisk(12f, 80f);
            Assert.Greater(lateRisk, earlyRisk, "長期包囲ほど出撃逆襲リスクが上がる");

            // 急がず損害を惜しむ将は包囲(−)を、急ぎ損害を許容する将は強襲(+)を選ぶ。
            Assert.Less(SiegeAssaultRules.AssaultVsSiegeTradeoff(0.1f, 0.1f), 0f);
            Assert.Greater(SiegeAssaultRules.AssaultVsSiegeTradeoff(0.9f, 0.9f), 0f);
        }
    }
}
