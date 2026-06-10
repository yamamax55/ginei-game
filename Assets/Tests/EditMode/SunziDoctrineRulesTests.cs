using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 謀攻優先ドクトリン（孫子「謀攻篇」・#1130）を固定する：選好は謀＞交＞兵＞攻城、
    /// 戦わずして勝つ（謀略・外交）が最善に選ばれ、攻城は下策として罰を受け、敵の聡明さが謀略を阻む。
    /// </summary>
    public class SunziDoctrineRulesTests
    {
        private static SunziDoctrineParams P => SunziDoctrineParams.Default;

        // --- Preference：上策→下策の序列 ---

        [Test]
        public void Preference_StratagemBeatsDiplomacyBeatsBattleBeatsSiege()
        {
            // 孫子＝謀＞交＞兵＞攻城
            Assert.AreEqual(1.0f, SunziDoctrineRules.Preference(StrategicMeans.謀略, P), 1e-4f);
            Assert.AreEqual(0.8f, SunziDoctrineRules.Preference(StrategicMeans.外交, P), 1e-4f);
            Assert.AreEqual(0.5f, SunziDoctrineRules.Preference(StrategicMeans.野戦, P), 1e-4f);
            Assert.AreEqual(0.25f, SunziDoctrineRules.Preference(StrategicMeans.攻城, P), 1e-4f);
            Assert.Greater(SunziDoctrineRules.Preference(StrategicMeans.謀略, P), SunziDoctrineRules.Preference(StrategicMeans.攻城, P));
        }

        // --- MeansScore ---

        [Test]
        public void MeansScore_NetGainAfterCost()
        {
            // 謀略：feas=1, cost=0.4, gain=0.8 → net=0.8-0.5*0.4=0.6 → 1.0*1*0.6=0.6
            float s = SunziDoctrineRules.MeansScore(StrategicMeans.謀略, 1f, 0.4f, 0.8f, P);
            Assert.AreEqual(0.6f, s, 1e-4f);
        }

        [Test]
        public void MeansScore_InfeasibleIsZero()
        {
            // 実行不能なら高利得でもスコア0
            float s = SunziDoctrineRules.MeansScore(StrategicMeans.謀略, 0f, 0f, 1f, P);
            Assert.AreEqual(0f, s, 1e-4f);
        }

        // --- BestMeans ---

        [Test]
        public void BestMeans_AllEqualConditions_PicksStratagem()
        {
            // 全手段で実行可能性・コスト・利得が同条件なら選好の高い謀略を選ぶ＝戦わずして勝つ
            var feas = new[] { 1f, 1f, 1f, 1f };
            var cost = new[] { 0.3f, 0.3f, 0.3f, 0.3f };
            var gain = new[] { 0.7f, 0.7f, 0.7f, 0.7f };
            Assert.AreEqual(StrategicMeans.謀略, SunziDoctrineRules.BestMeans(feas, cost, gain, P));
        }

        [Test]
        public void BestMeans_StratagemInfeasible_FallsToDiplomacy()
        {
            // 謀略が不可能なら順に下って外交を選ぶ（さらに不可なら野戦→攻城）
            var feas = new[] { 0f, 1f, 1f, 1f };
            var cost = new[] { 0.3f, 0.3f, 0.3f, 0.3f };
            var gain = new[] { 0.7f, 0.7f, 0.7f, 0.7f };
            Assert.AreEqual(StrategicMeans.外交, SunziDoctrineRules.BestMeans(feas, cost, gain, P));
        }

        [Test]
        public void BestMeans_OnlySiegeFeasible_PicksSiege()
        {
            // 上策がすべて不可能なら最後の手段＝攻城に下る
            var feas = new[] { 0f, 0f, 0f, 1f };
            var cost = new[] { 0.3f, 0.3f, 0.3f, 0.3f };
            var gain = new[] { 0.7f, 0.7f, 0.7f, 0.7f };
            Assert.AreEqual(StrategicMeans.攻城, SunziDoctrineRules.BestMeans(feas, cost, gain, P));
        }

        // --- BloodlessVictoryValue：戦わずして勝つは最上 ---

        [Test]
        public void BloodlessVictory_FullDisruption_ExceedsBattleVictory()
        {
            // 完全瓦解の無血勝利(1.5)は野戦の通常勝利(選好0.5)を超える＝善の善なる者
            float full = SunziDoctrineRules.BloodlessVictoryValue(1f, P);
            Assert.AreEqual(1.5f, full, 1e-4f);
            Assert.AreEqual(0.5f, SunziDoctrineRules.BloodlessVictoryValue(0f, P), 1e-4f);
            Assert.Greater(full, SunziDoctrineRules.Preference(StrategicMeans.野戦, P));
        }

        // --- SiegePenalty：攻城は下策＝最も損なう ---

        [Test]
        public void SiegePenalty_GrowsWithTimeAndStrength()
        {
            // 時間10・兵力5 → 0.1*10 + 0.2*5 = 2.0
            float pen = SunziDoctrineRules.SiegePenalty(10f, 5f, P);
            Assert.AreEqual(2.0f, pen, 1e-4f);
            // 長期化ほど罰が重い（孫子が戒めた最後の手段）
            Assert.Greater(SunziDoctrineRules.SiegePenalty(20f, 5f, P), pen);
        }

        // --- DiplomaticIsolation：伐交 ---

        [Test]
        public void DiplomaticIsolation_NoAlliancesIsZero_MorePowerIsolatesDeeper()
        {
            Assert.AreEqual(0f, SunziDoctrineRules.DiplomaticIsolation(0, 1f, P), 1e-4f);
            float weak = SunziDoctrineRules.DiplomaticIsolation(3, 0.3f, P);
            float strong = SunziDoctrineRules.DiplomaticIsolation(3, 0.9f, P);
            Assert.Greater(strong, weak); // 外交力が高いほど深く孤立させる
        }

        // --- StratagemSuccess：敵の聡明さが防壁・決定論 ---

        [Test]
        public void StratagemSuccess_SmartEnemyResists_Deterministic()
        {
            // 巧拙0.8・敵聡明0 → 成功率0.8。roll=0.5<0.8 成功、roll=0.9 失敗
            Assert.IsTrue(SunziDoctrineRules.StratagemSuccess(0.8f, 0f, 0.5f));
            Assert.IsFalse(SunziDoctrineRules.StratagemSuccess(0.8f, 0f, 0.9f));
            // 聡明な敵(0.9)は同じ巧拙でも成功率0.08まで落ちる＝計を見破る
            Assert.IsFalse(SunziDoctrineRules.StratagemSuccess(0.8f, 0.9f, 0.5f));
        }
    }
}
