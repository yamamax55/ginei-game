using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// CasualtyEvacuationRules（傷病兵後送＝負傷者の救護と搬送）の EditMode テスト。
    /// 既定 Params で期待値を固定（手計算で検算）。
    /// </summary>
    public class CasualtyEvacuationRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void GoldenHourFactor_DecaysWithTime()
        {
            // t=0 → 1/(1+0) = 1
            Assert.AreEqual(1f, CasualtyEvacuationRules.GoldenHourFactor(0f), Eps);
            // t=1,k=1 → 1/2 = 0.5
            Assert.AreEqual(0.5f, CasualtyEvacuationRules.GoldenHourFactor(1f), Eps);
            // t=3 → 1/4 = 0.25
            Assert.AreEqual(0.25f, CasualtyEvacuationRules.GoldenHourFactor(3f), Eps);
            // 負の時間はクランプして t=0 扱い
            Assert.AreEqual(1f, CasualtyEvacuationRules.GoldenHourFactor(-5f), Eps);
        }

        [Test]
        public void EvacuationRouteSafety_ControlTimesInterdiction()
        {
            // 0.8 × (1-0.5) = 0.4
            Assert.AreEqual(0.4f, CasualtyEvacuationRules.EvacuationRouteSafety(0.8f, 0.5f), Eps);
            // 完全支配・阻止なし → 1.0
            Assert.AreEqual(1f, CasualtyEvacuationRules.EvacuationRouteSafety(1f, 0f), Eps);
            // 完全阻止 → 0
            Assert.AreEqual(0f, CasualtyEvacuationRules.EvacuationRouteSafety(0.9f, 1f), Eps);
        }

        [Test]
        public void TransportCapacity_UnitsOverDemand()
        {
            // 100/(100+300) = 0.25
            Assert.AreEqual(0.25f, CasualtyEvacuationRules.TransportCapacity(100f, 300f), Eps);
            // 傷病者ゼロ・手段ゼロ → 逼迫なし 1.0
            Assert.AreEqual(1f, CasualtyEvacuationRules.TransportCapacity(0f, 0f), Eps);
        }

        [Test]
        public void EvacuationDelay_LowCapAndSafetyMeansDelay()
        {
            // (1-0.25)×(1-0.4) = 0.75×0.6 = 0.45
            Assert.AreEqual(0.45f, CasualtyEvacuationRules.EvacuationDelay(0.25f, 0.4f), Eps);
            // 能力も安全も満点 → 遅延なし
            Assert.AreEqual(0f, CasualtyEvacuationRules.EvacuationDelay(1f, 1f), Eps);
        }

        [Test]
        public void PreventableDeaths_DelayTimesMortality()
        {
            // 0.45 × (1-0.5) = 0.225
            Assert.AreEqual(0.225f, CasualtyEvacuationRules.PreventableDeaths(0.45f, 0.5f), Eps);
            // 救命率満点 → 防げた死ゼロ
            Assert.AreEqual(0f, CasualtyEvacuationRules.PreventableDeaths(0.9f, 1f), Eps);
        }

        [Test]
        public void MedevacAttrition_UnsafeRouteCostsRescuers()
        {
            // (1-0.4)×0.5 = 0.3
            Assert.AreEqual(0.3f, CasualtyEvacuationRules.MedevacAttrition(0.4f, 0.5f), Eps);
            // (1-0.1)×1.0 = 0.9 だが maxAttrition(0.8) でクランプ
            Assert.AreEqual(0.8f, CasualtyEvacuationRules.MedevacAttrition(0.1f, 1f), Eps);
        }

        [Test]
        public void FrontlineTradeoff_DivertedTimesNeed()
        {
            // 0.4 × 0.7 = 0.28
            Assert.AreEqual(0.28f, CasualtyEvacuationRules.FrontlineTradeoff(0.4f, 0.7f), Eps);
            // 後送に人を割かなければジレンマなし
            Assert.AreEqual(0f, CasualtyEvacuationRules.FrontlineTradeoff(0f, 1f), Eps);
        }

        [Test]
        public void SurvivalRate_RescueTimesTransportMinusDeaths()
        {
            // 0.8 × 0.6 - 0.1 = 0.48 - 0.1 = 0.38
            Assert.AreEqual(0.38f, CasualtyEvacuationRules.SurvivalRate(0.8f, 0.6f, 0.1f), Eps);
            // 防げた死が大きいと 0 にクランプ
            Assert.AreEqual(0f, CasualtyEvacuationRules.SurvivalRate(0.3f, 0.3f, 0.5f), Eps);
            // しきい値判定
            Assert.IsTrue(CasualtyEvacuationRules.EvacuationSucceeded(0.6f));
            Assert.IsFalse(CasualtyEvacuationRules.EvacuationSucceeded(0.3f));
        }

        /// <summary>
        /// 物語テスト：制空権を握り近接した野戦病院へ素早く運べた部隊（治療まで0.25時・経路支配0.9/
        /// 阻止0.1・後送手段200に傷病者100）は高い生存率で後送に成功する。一方、敵の阻止下で遠い
        /// 後方へ手段不足のまま運ぶ部隊（治療まで4時・支配0.4/阻止0.7・手段50に傷病者300）は遅延が
        /// 嵩んで防げたはずの死が膨らみ、後送は破綻する。
        /// </summary>
        [Test]
        public void Story_FastSafeEvacSucceedsContestedEvacFails()
        {
            // --- 速く安全な後送 ---
            float ghfGood = CasualtyEvacuationRules.GoldenHourFactor(0.25f);          // 1/1.25 = 0.8
            Assert.AreEqual(0.8f, ghfGood, Eps);
            float safeGood = CasualtyEvacuationRules.EvacuationRouteSafety(0.9f, 0.1f); // 0.9×0.9 = 0.81
            Assert.AreEqual(0.81f, safeGood, Eps);
            float capGood = CasualtyEvacuationRules.TransportCapacity(200f, 100f);     // 200/300 ≈ 0.6667
            float delayGood = CasualtyEvacuationRules.EvacuationDelay(capGood, safeGood);
            float preventGood = CasualtyEvacuationRules.PreventableDeaths(delayGood, ghfGood);
            float survGood = CasualtyEvacuationRules.SurvivalRate(ghfGood, capGood, preventGood);
            Assert.IsTrue(CasualtyEvacuationRules.EvacuationSucceeded(survGood),
                "速く安全に運べば後送は成功するはず");

            // --- 阻止下の遅い後送 ---
            float ghfBad = CasualtyEvacuationRules.GoldenHourFactor(4f);              // 1/5 = 0.2
            Assert.AreEqual(0.2f, ghfBad, Eps);
            float safeBad = CasualtyEvacuationRules.EvacuationRouteSafety(0.4f, 0.7f); // 0.4×0.3 = 0.12
            Assert.AreEqual(0.12f, safeBad, Eps);
            float capBad = CasualtyEvacuationRules.TransportCapacity(50f, 300f);       // 50/350 ≈ 0.1429
            float delayBad = CasualtyEvacuationRules.EvacuationDelay(capBad, safeBad);
            float preventBad = CasualtyEvacuationRules.PreventableDeaths(delayBad, ghfBad);
            float survBad = CasualtyEvacuationRules.SurvivalRate(ghfBad, capBad, preventBad);
            Assert.IsFalse(CasualtyEvacuationRules.EvacuationSucceeded(survBad),
                "阻止下で遅く運べば防げた死が嵩み後送は破綻するはず");

            // 救護隊も不安全な経路で損耗する（成功側より大きい）
            float attrBad = CasualtyEvacuationRules.MedevacAttrition(safeBad, 0.8f);
            float attrGood = CasualtyEvacuationRules.MedevacAttrition(safeGood, 0.8f);
            Assert.Greater(attrBad, attrGood, "不安全な経路ほど救護隊の損耗は大きい");
        }
    }
}
