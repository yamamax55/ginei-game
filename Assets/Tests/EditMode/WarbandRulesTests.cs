using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>武装集団（ウォーバンド）の純ロジックのテスト。既定 Params 期待値を手計算で固定。</summary>
    public class WarbandRulesTests
    {
        const float Eps = 1e-4f;
        const float EpsPow = 1e-3f; // 現状 Pow/Sqrt は未使用だが規約に従い予備

        [Test]
        public void ChieftainCharisma_WeightedBlend()
        {
            // (0.8*0.6 + 0.5*0.4)/1.0 = 0.68
            Assert.AreEqual(0.68f, WarbandRules.ChieftainCharisma(0.8f, 0.5f), Eps);
        }

        [Test]
        public void ChieftainCharisma_ClampsInputs()
        {
            // 武勇・気前ともに上限超過→clamp で 1
            Assert.AreEqual(1f, WarbandRules.ChieftainCharisma(5f, 9f), Eps);
            // 負入力→0
            Assert.AreEqual(0f, WarbandRules.ChieftainCharisma(-1f, -1f), Eps);
        }

        [Test]
        public void WarbandCohesion_VictoriesRaiseFromCharisma()
        {
            // 飽和勝利(5)：victoryFactor=1, Lerp(0.68, 1.0, 0.5)=0.84
            Assert.AreEqual(0.84f, WarbandRules.WarbandCohesion(0.68f, 5), Eps);
            // 勝利0：カリスマのまま
            Assert.AreEqual(0.6f, WarbandRules.WarbandCohesion(0.6f, 0), Eps);
        }

        [Test]
        public void PlunderShare_DividesSpoilsBySize()
        {
            // 8/10 = 0.8
            Assert.AreEqual(0.8f, WarbandRules.PlunderShare(8f, 10f), Eps);
            // 規模0は分け前0（ゼロ割回避）
            Assert.AreEqual(0f, WarbandRules.PlunderShare(5f, 0f), Eps);
        }

        [Test]
        public void FollowerRetention_WeightedBlend()
        {
            // (0.8*0.6 + 0.6*0.4)/1.0 = 0.72
            Assert.AreEqual(0.72f, WarbandRules.FollowerRetention(0.8f, 0.6f), Eps);
        }

        [Test]
        public void DefeatDispersal_CohesionDampens()
        {
            // guard=1-0.84*0.7=0.412, 0.9*0.412=0.3708
            Assert.AreEqual(0.3708f, WarbandRules.DefeatDispersal(0.9f, 0.84f), Eps);
            // 結束0なら離散は深刻さそのまま
            Assert.AreEqual(0.9f, WarbandRules.DefeatDispersal(0.9f, 0f), Eps);
        }

        [Test]
        public void RaidingPower_FerocityAmplifies()
        {
            // feroFactor=Lerp(0.5,1,0.6)=0.8, 0.84*0.8=0.672
            Assert.AreEqual(0.672f, WarbandRules.RaidingPower(0.84f, 0.6f), Eps);
        }

        [Test]
        public void UndisciplinedViolence_RisesWithSizeAndLackOfControl()
        {
            // sizeFactor=min(0.6*0.5,1)=0.3, 0.3*(1-0.7)=0.09
            Assert.AreEqual(0.09f, WarbandRules.UndisciplinedViolence(0.6f, 0.7f), Eps);
            // 完全統制なら暴力0
            Assert.AreEqual(0f, WarbandRules.UndisciplinedViolence(0.6f, 1f), Eps);
        }

        [Test]
        public void WarbandStrength_RaidTimesRetentionMinusViolence()
        {
            // 0.672*0.72 - 0.09 = 0.39384
            Assert.AreEqual(0.39384f, WarbandRules.WarbandStrength(0.672f, 0.72f, 0.09f), Eps);
            // 暴力が支配的なら戦力0（下限clamp）
            Assert.AreEqual(0f, WarbandRules.WarbandStrength(0.5f, 0.2f, 0.9f), Eps);
        }

        [Test]
        public void Narrative_VictoriousWarbandThrivesThenDefeatScatters()
        {
            var p = WarbandParams.Default;

            // 強く気前のよい首領（武勇0.8・気前0.5）
            float charisma = WarbandRules.ChieftainCharisma(0.8f, 0.5f, p);
            // 連勝で結束が固まる
            float cohesion = WarbandRules.WarbandCohesion(charisma, 5, p);
            // 戦利品が潤沢（8を10人で分配）→引き留め堅い
            float share = WarbandRules.PlunderShare(8f, 10f, p);
            float retention = WarbandRules.FollowerRetention(share, charisma, p);
            // 統制が利き暴力は低位、獰猛で略奪力高い
            float raiding = WarbandRules.RaidingPower(cohesion, 0.6f, p);
            float violence = WarbandRules.UndisciplinedViolence(0.6f, 0.7f, p);
            float strong = WarbandRules.WarbandStrength(raiding, retention, violence, p);

            // 結束した略奪集団は十分強い
            Assert.Greater(strong, 0.35f);

            // ところが大敗（深刻度0.9）に遭うと離散が進む
            float disperse = WarbandRules.DefeatDispersal(0.9f, cohesion, p);
            Assert.Greater(disperse, 0.3f);

            // 戦利品が枯れ統制も失われた集団（分け前薄・暴力高）は崩れる
            float poorShare = WarbandRules.PlunderShare(1f, 20f, p);
            float poorRetention = WarbandRules.FollowerRetention(poorShare, charisma, p);
            float chaos = WarbandRules.UndisciplinedViolence(2f, 0.1f, p);
            float weak = WarbandRules.WarbandStrength(raiding, poorRetention, chaos, p);

            // 内部崩壊した集団は実効戦力で繁栄期を大きく下回る
            Assert.Less(weak, strong);
        }
    }
}
