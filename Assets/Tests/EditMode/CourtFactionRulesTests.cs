using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>宮廷派閥（朝廷内の党派抗争と勢力均衡）の純ロジック検証。</summary>
    public class CourtFactionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>派閥勢力＝構成員/要職/君寵の重み付き平均。頭数だけでなく要職・寵を握るほど強い。</summary>
        [Test]
        public void FactionPower_構成員と要職と君寵で勢力()
        {
            // members=5/10=0.5, positions=2.5/5=0.5, favor=0.5 → (0.5+0.5+0.5)/3=0.5
            Assert.AreEqual(0.5f, CourtFactionRules.FactionPower(5f, 2.5f, 0.5f), Eps);
            // 全て満たせば1
            Assert.AreEqual(1f, CourtFactionRules.FactionPower(10f, 5f, 1f), Eps);
            // 正規化を超える人数・要職は飽和（過大にならない）
            Assert.AreEqual(1f, CourtFactionRules.FactionPower(20f, 10f, 1f), Eps);
            // 何も無ければ勢力なし
            Assert.AreEqual(0f, CourtFactionRules.FactionPower(0f, 0f, 0f), Eps);
        }

        /// <summary>抗争の激しさ＝両派の拮抗×争点。伯仲し争点が大きいほど激しい。</summary>
        [Test]
        public void RivalryHeat_拮抗と争点で抗争が燃える()
        {
            // parity=1-|0.6-0.5|=0.9, stake=0.8 → 0.72
            Assert.AreEqual(0.72f, CourtFactionRules.RivalryHeat(0.6f, 0.5f, 0.8f), Eps);
            // 一強が圧倒すれば（差最大）抗争は鎮まる
            Assert.AreEqual(0f, CourtFactionRules.RivalryHeat(1f, 0f, 1f), Eps);
            // 争点が無ければ抗争も無い
            Assert.AreEqual(0f, CourtFactionRules.RivalryHeat(0.5f, 0.5f, 0f), Eps);
        }

        /// <summary>勢力均衡度＝1-|勢力差|。拮抗で1・一強で0。</summary>
        [Test]
        public void BalanceOfPower_拮抗で高く一強で低い()
        {
            // 1-|0.6-0.5|=0.9
            Assert.AreEqual(0.9f, CourtFactionRules.BalanceOfPower(0.6f, 0.5f), Eps);
            // 互角で1
            Assert.AreEqual(1f, CourtFactionRules.BalanceOfPower(0.4f, 0.4f), Eps);
            // 一強で0
            Assert.AreEqual(0f, CourtFactionRules.BalanceOfPower(1f, 0f), Eps);
        }

        /// <summary>一強の危険＝一強勢力×(1-均衡)。均衡が崩れ強い派が突出するほど危険。</summary>
        [Test]
        public void HegemonyRisk_一強と不均衡で危険()
        {
            // 0.9*(1-0.2)=0.72
            Assert.AreEqual(0.72f, CourtFactionRules.HegemonyRisk(0.9f, 0.2f), Eps);
            // 均衡が完全なら危険なし
            Assert.AreEqual(0f, CourtFactionRules.HegemonyRisk(0.9f, 1f), Eps);
            // 突出した派が居なければ危険なし
            Assert.AreEqual(0f, CourtFactionRules.HegemonyRisk(0f, 0f), Eps);
        }

        /// <summary>中間派の決定力＝規模×接戦度。接戦ほど中立ブロックが情勢を左右。</summary>
        [Test]
        public void SwingFactionLeverage_接戦で中間派が決める()
        {
            // 0.3*0.8=0.24
            Assert.AreEqual(0.24f, CourtFactionRules.SwingFactionLeverage(0.3f, 0.8f), Eps);
            // 接戦でなければ（一方の圧勝）中間派は無力
            Assert.AreEqual(0f, CourtFactionRules.SwingFactionLeverage(0.5f, 0f), Eps);
            // 中間派が居なければ決定力なし
            Assert.AreEqual(0f, CourtFactionRules.SwingFactionLeverage(0f, 1f), Eps);
        }

        /// <summary>政務の停滞＝抗争×拮抗。激しい抗争が互角のまま膠着するほど止まる。</summary>
        [Test]
        public void GovernanceParalysis_抗争が膠着すると政務が止まる()
        {
            // 0.8*0.9=0.72
            Assert.AreEqual(0.72f, CourtFactionRules.GovernanceParalysis(0.8f, 0.9f), Eps);
            // 抗争が無ければ停滞しない
            Assert.AreEqual(0f, CourtFactionRules.GovernanceParalysis(0f, 1f), Eps);
            // 一強で決着がつけば（拮抗0）停滞しない
            Assert.AreEqual(0f, CourtFactionRules.GovernanceParalysis(0.9f, 0f), Eps);
        }

        /// <summary>君主の派閥操縦＝手腕×均衡。諸派が拮抗するほど名君は漁夫の利を得る。</summary>
        [Test]
        public void SovereignManipulation_拮抗で君主が漁夫の利()
        {
            // 0.7*0.8=0.56
            Assert.AreEqual(0.56f, CourtFactionRules.SovereignManipulation(0.7f, 0.8f), Eps);
            // 一強に呑まれれば（均衡0）操縦できない
            Assert.AreEqual(0f, CourtFactionRules.SovereignManipulation(0.9f, 0f), Eps);
            // 暗君なら操縦できない
            Assert.AreEqual(0f, CourtFactionRules.SovereignManipulation(0f, 1f), Eps);
        }

        /// <summary>宮廷の安定判定＝均衡が停滞を上回るか。既定閾値0。</summary>
        [Test]
        public void IsCourtStable_均衡が停滞を上回れば安定()
        {
            // 均衡0.8 > 停滞0.3 → 安定
            Assert.IsTrue(CourtFactionRules.IsCourtStable(0.8f, 0.3f));
            // 停滞0.7 > 均衡0.4 → 不安定
            Assert.IsFalse(CourtFactionRules.IsCourtStable(0.4f, 0.7f));
            // 境界＝既定閾値0で安定
            Assert.IsTrue(CourtFactionRules.IsCourtStable(0.5f, 0.5f));
            // 明示閾値版（高い余裕を要求すれば不安定）
            Assert.IsFalse(CourtFactionRules.IsCourtStable(0.8f, 0.3f, 0.6f));
            Assert.IsTrue(CourtFactionRules.IsCourtStable(0.9f, 0.2f, 0.6f));
        }

        /// <summary>
        /// 物語：帝国宮廷に二大派閥（門閥派と新興派）。両派が伯仲し争点が大きいうちは抗争が燃え、
        /// 互角ゆえ政務は膠着するが、均衡が君主の操縦を許し宮廷は辛うじて安定する。
        /// やがて一派が要職と君寵を独占し突出すると、均衡が崩れて一強の危険が高まり、
        /// 君主は操縦力を失い、抗争が鎮まっても宮廷は専横の危機に傾く。
        /// </summary>
        [Test]
        public void Story_均衡の宮廷から一強の専横へ()
        {
            // 二大派閥が伯仲（要職・君寵も拮抗）
            float aPower = CourtFactionRules.FactionPower(8f, 3f, 0.5f);
            float bPower = CourtFactionRules.FactionPower(7f, 2f, 0.5f);
            float balance = CourtFactionRules.BalanceOfPower(aPower, bPower);
            Assert.Greater(balance, 0.8f); // よく拮抗

            // 争点が大きく抗争が燃える
            float heat = CourtFactionRules.RivalryHeat(aPower, bPower, 0.9f);
            Assert.Greater(heat, 0.6f);

            // 互角ゆえ政務は膠着
            float paralysis = CourtFactionRules.GovernanceParalysis(heat, balance);
            Assert.Greater(paralysis, 0.5f);

            // だが均衡が名君の操縦を許す
            float manip = CourtFactionRules.SovereignManipulation(0.8f, balance);
            Assert.Greater(manip, 0.6f);

            // 一強の危険は低い
            float riskBalanced = CourtFactionRules.HegemonyRisk(aPower, balance);
            Assert.Less(riskBalanced, 0.2f);

            // ── やがて門閥派が要職と君寵を独占し突出 ──
            float aDominant = CourtFactionRules.FactionPower(12f, 5f, 1f); // 飽和＝最強
            float bWeak = CourtFactionRules.FactionPower(3f, 0f, 0.1f);
            float balanceImbalanced = CourtFactionRules.BalanceOfPower(aDominant, bWeak);
            Assert.Less(balanceImbalanced, balance); // 均衡が崩れる

            // 一強の危険が跳ね上がる
            float riskImbalanced = CourtFactionRules.HegemonyRisk(aDominant, balanceImbalanced);
            Assert.Greater(riskImbalanced, riskBalanced);
            Assert.Greater(riskImbalanced, 0.6f);

            // 君主は操縦力を失う
            float manipImbalanced = CourtFactionRules.SovereignManipulation(0.8f, balanceImbalanced);
            Assert.Less(manipImbalanced, manip);

            // 一強の宮廷は専横へ傾く（均衡が低く不安定）
            float heatImbalanced = CourtFactionRules.RivalryHeat(aDominant, bWeak, 0.9f);
            float paralysisImbalanced = CourtFactionRules.GovernanceParalysis(heatImbalanced, balanceImbalanced);
            Assert.IsTrue(CourtFactionRules.IsCourtStable(balance, paralysis));               // 均衡期は安定
            Assert.IsFalse(CourtFactionRules.IsCourtStable(balanceImbalanced, riskImbalanced)); // 一強期は不安定
        }
    }
}
