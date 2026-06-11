using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>間合いドクトリン（最適交戦距離）の純ロジック検証（#1384・五輪書の間合い）。</summary>
    public class EngagementDistanceRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>最適交戦距離＝射程は遠間へ・近接型は near へ寄る（射程重み0.7・機動重み0.3）。</summary>
        [Test]
        public void OptimalDistance_射程の長い側は遠間_近接型は近間()
        {
            // 長射程・高機動＝遠間（1*0.7+1*0.3=1.0）。
            Assert.AreEqual(1.0f, EngagementDistanceRules.OptimalDistance(1f, 1f), Eps);
            // 短射程・低機動＝近間（0）。
            Assert.AreEqual(0f, EngagementDistanceRules.OptimalDistance(0f, 0f), Eps);
            // 中庸（0.5*0.7+0.5*0.3=0.5）。
            Assert.AreEqual(0.5f, EngagementDistanceRules.OptimalDistance(0.5f, 0.5f), Eps);
            // 射程重みが効く：高射程・低機動(1*0.7+0*0.3=0.7) > 低射程・高機動(0*0.7+1*0.3=0.3)。
            Assert.Greater(EngagementDistanceRules.OptimalDistance(1f, 0f), EngagementDistanceRules.OptimalDistance(0f, 1f));
        }

        /// <summary>距離の優位＝自分の最適に近く敵の最適から遠いほど高い（自分の間合いで戦う）。</summary>
        [Test]
        public void DistanceAdvantage_自分の間合いで戦うほど優位()
        {
            // current=自分の最適0.8・敵の最適0.2：自分への近さ1.0＋敵からの遠さ0.6 → (1+0.6)/2=0.8。
            Assert.AreEqual(0.8f, EngagementDistanceRules.DistanceAdvantage(0.8f, 0.8f, 0.2f), Eps);
            // current=敵の最適0.2・自分の最適0.8：自分への近さ1-0.6=0.4＋敵からの遠さ0 → 0.2（敵の間合い）。
            Assert.AreEqual(0.2f, EngagementDistanceRules.DistanceAdvantage(0.2f, 0.8f, 0.2f), Eps);
            // 自分の間合いの方が敵の間合いより優位が高い。
            Assert.Greater(
                EngagementDistanceRules.DistanceAdvantage(0.8f, 0.8f, 0.2f),
                EngagementDistanceRules.DistanceAdvantage(0.2f, 0.8f, 0.2f));
        }

        /// <summary>間合いを制した効果＝優位が高いほど戦闘力が上がる（1〜1.3倍・実効値・基準非破壊）。</summary>
        [Test]
        public void RangeControlEffect_最適距離で戦うと戦闘力上昇()
        {
            // 優位0＝据え置き1.0（不利は下げない）。
            Assert.AreEqual(1.0f, EngagementDistanceRules.RangeControlEffect(0f), Eps);
            // 優位1＝最大ボーナス 1+0.3=1.3。
            Assert.AreEqual(1.3f, EngagementDistanceRules.RangeControlEffect(1f), Eps);
            // 優位0.5＝1+0.5*0.3=1.15。
            Assert.AreEqual(1.15f, EngagementDistanceRules.RangeControlEffect(0.5f), Eps);
        }

        /// <summary>間合いを詰めるか開くか＝自分の最適へ動く（遠すぎれば詰め・近すぎれば開く）。</summary>
        [Test]
        public void ClosingOrOpening_自分の最適へ詰めるか開くか()
        {
            // current0.8 > own0.3：遠すぎ＝詰める(正)。gap0.5×機動1.0=0.5。
            Assert.AreEqual(0.5f, EngagementDistanceRules.ClosingOrOpening(0.8f, 0.3f, 1f), Eps);
            // current0.2 < own0.7：近すぎ＝開く(負)。gap-0.5×機動1.0=-0.5。
            Assert.AreEqual(-0.5f, EngagementDistanceRules.ClosingOrOpening(0.2f, 0.7f, 1f), Eps);
            // 既に最適＝0。
            Assert.AreEqual(0f, EngagementDistanceRules.ClosingOrOpening(0.5f, 0.5f, 1f), Eps);
            // 機動が低いと動きが鈍い（同じgapでも小さい）。
            Assert.AreEqual(0.25f, EngagementDistanceRules.ClosingOrOpening(0.8f, 0.3f, 0.5f), Eps);
        }

        /// <summary>不得意な間合いのペナルティ＝自分の最適から外れて戦う不利（最大0.4）。</summary>
        [Test]
        public void MismatchPenalty_不得意な間合いで戦う不利()
        {
            // 最適ぴったり＝罰ゼロ。
            Assert.AreEqual(0f, EngagementDistanceRules.MismatchPenalty(0.5f, 0.5f), Eps);
            // 最大に外される（遠間型own1.0が近接0で戦う）：gap1.0×0.4=0.4。
            Assert.AreEqual(0.4f, EngagementDistanceRules.MismatchPenalty(0f, 1f), Eps);
            // 半分外す：gap0.5×0.4=0.2。
            Assert.AreEqual(0.2f, EngagementDistanceRules.MismatchPenalty(0.3f, 0.8f), Eps);
        }

        /// <summary>交戦距離の強要＝機動で優る側が間合いの主導権を握る（速い側が距離を決める）。</summary>
        [Test]
        public void ForcingEngagement_機動で優る側が間合いを強要()
        {
            // 自軍が速い＝強要できる(正)。0.9-0.3=0.6。
            Assert.AreEqual(0.6f, EngagementDistanceRules.ForcingEngagement(0.9f, 0.3f), Eps);
            // 敵が速い＝強要される(負)。0.3-0.9=-0.6。
            Assert.AreEqual(-0.6f, EngagementDistanceRules.ForcingEngagement(0.3f, 0.9f), Eps);
            // 互角＝主導権なし。
            Assert.AreEqual(0f, EngagementDistanceRules.ForcingEngagement(0.5f, 0.5f), Eps);
        }

        /// <summary>接近戦vs遠距離＝得意間合いの押し付け合い（近間型は接近・遠間型は遠距離）。</summary>
        [Test]
        public void InterceptVsStandoff_得意間合いの押し付け合い()
        {
            // 自軍が近間型(own0.2)・敵が遠間型(enemy0.8)＝接近戦を挑む(正)。0.8-0.2=0.6。
            Assert.AreEqual(0.6f, EngagementDistanceRules.InterceptVsStandoff(0.2f, 0.8f), Eps);
            // 自軍が遠間型(own0.8)・敵が近間型(enemy0.2)＝遠距離で撃つ(負)。0.2-0.8=-0.6。
            Assert.AreEqual(-0.6f, EngagementDistanceRules.InterceptVsStandoff(0.8f, 0.2f), Eps);
            // 同じ間合い＝真っ向勝負(0)。
            Assert.AreEqual(0f, EngagementDistanceRules.InterceptVsStandoff(0.5f, 0.5f), Eps);
        }

        /// <summary>間合い支配判定＝距離の優位が閾値を超えれば敵の間合いを外して有利に戦える（既定0.3）。</summary>
        [Test]
        public void IsRangeDominant_間合いを支配して有利()
        {
            // 既定閾値0.3超＝支配。
            Assert.IsTrue(EngagementDistanceRules.IsRangeDominant(0.8f));
            Assert.IsFalse(EngagementDistanceRules.IsRangeDominant(0.2f));
            Assert.IsFalse(EngagementDistanceRules.IsRangeDominant(0.3f)); // 閾値ちょうどは非支配。
            // 明示閾値：高い閾値0.9なら0.8でも非支配。
            Assert.IsFalse(EngagementDistanceRules.IsRangeDominant(0.8f, 0.9f));
        }
    }
}
