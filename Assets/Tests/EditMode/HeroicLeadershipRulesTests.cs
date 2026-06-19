using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>英雄的指揮（陣頭指揮）の純ロジック HeroicLeadershipRules の検証。</summary>
    public class HeroicLeadershipRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void FrontlineInspiration_ProductOfFactors()
        {
            // 0.8×1.0×0.75×1.0=0.6
            float v = HeroicLeadershipRules.FrontlineInspiration(0.8f, 1.0f, 0.75f);
            Assert.AreEqual(0.6f, v, Eps);
        }

        [Test]
        public void CharismaContagion_ConnectivityRaisesReach()
        {
            // 連結0.5：reach=Lerp(0.5,1,0.5)=0.75 / 0.6×0.75=0.45
            float v = HeroicLeadershipRules.CharismaContagion(0.6f, 0.5f);
            Assert.AreEqual(0.45f, v, Eps);
            // 孤立（連結0）でも床0.5は届く：0.6×0.5=0.3
            float lone = HeroicLeadershipRules.CharismaContagion(0.6f, 0f);
            Assert.AreEqual(0.3f, lone, Eps);
        }

        [Test]
        public void ExposureRisk_ProximityTimesEnemyFocus()
        {
            // 0.9×0.8=0.72
            float v = HeroicLeadershipRules.ExposureRisk(0.9f, 0.8f);
            Assert.AreEqual(0.72f, v, Eps);
        }

        [Test]
        public void RecklessnessFactor_BoldnessOverPrudence()
        {
            // 0.7/(0.7+0.3)=0.7（思慮が無謀を抑える）
            float v = HeroicLeadershipRules.RecklessnessFactor(0.7f, 0.3f);
            Assert.AreEqual(0.7f, v, Eps);
            // 両者0なら中庸0.5
            Assert.AreEqual(0.5f, HeroicLeadershipRules.RecklessnessFactor(0f, 0f), Eps);
        }

        [Test]
        public void HeroicSurge_PeaksAtIdealRecklessness()
        {
            // 最適点0.6で適合1.0：0.8×1.0=0.8
            float peak = HeroicLeadershipRules.HeroicSurge(0.8f, 0.6f);
            Assert.AreEqual(0.8f, peak, Eps);
            // 臆病すぎ（0.1）：dev=0.5→fitness=clamp01(1-2×0.5)=0→猛攻出ず
            float timid = HeroicLeadershipRules.HeroicSurge(0.8f, 0.1f);
            Assert.AreEqual(0f, timid, Eps);
            // 無謀すぎ（0.85）：dev=0.25→fitness=1-0.5=0.5→0.8×0.5=0.4（頂点より落ちる）
            float rash = HeroicLeadershipRules.HeroicSurge(0.8f, 0.85f);
            Assert.AreEqual(0.4f, rash, Eps);
            Assert.Less(rash, peak);
        }

        [Test]
        public void CommanderCasualtyChance_CappedAndGuardReduces()
        {
            // numer=0.72×0.7=0.504, denom=1 → 0.504 だが上限0.5で頭打ち
            float capped = HeroicLeadershipRules.CommanderCasualtyChance(0.72f, 0.7f, 0f);
            Assert.AreEqual(0.5f, capped, Eps);
            // 護衛が割り引く：numer=0.5×0.4=0.2, denom=1+1=2 → 0.1
            float guarded = HeroicLeadershipRules.CommanderCasualtyChance(0.5f, 0.4f, 1f);
            Assert.AreEqual(0.1f, guarded, Eps);
        }

        [Test]
        public void IsCommanderFallen_DeterministicRoll()
        {
            // 確率0.1：roll<0.1 で戦死、それ以上は生存
            Assert.IsTrue(HeroicLeadershipRules.IsCommanderFallen(0.5f, 0.4f, 1f, 0.05f));
            Assert.IsFalse(HeroicLeadershipRules.IsCommanderFallen(0.5f, 0.4f, 1f, 0.2f));
        }

        [Test]
        public void MoraleShockIfFallen_DevotionTimesSurge()
        {
            // 0.75×0.8=0.6（慕われた英雄ほど喪失の衝撃が大）
            float v = HeroicLeadershipRules.MoraleShockIfFallen(0.75f, 0.8f);
            Assert.AreEqual(0.6f, v, Eps);
        }

        [Test]
        public void IsHeroicLeadershipWorthwhile_SurgeMinusCasualtyVsThreshold()
        {
            // 0.8-0.1=0.7 ≥ 0.4 → 価値あり
            Assert.IsTrue(HeroicLeadershipRules.IsHeroicLeadershipWorthwhile(0.8f, 0.1f, 0.4f));
            // 0.4-0.1=0.3 < 0.4 → 割に合わない
            Assert.IsFalse(HeroicLeadershipRules.IsHeroicLeadershipWorthwhile(0.4f, 0.1f, 0.4f));
            // 既定閾値0.4委譲版
            Assert.IsTrue(HeroicLeadershipRules.IsHeroicLeadershipWorthwhile(0.8f, 0.1f));
            Assert.IsFalse(HeroicLeadershipRules.IsHeroicLeadershipWorthwhile(0.4f, 0.1f));
        }

        [Test]
        public void InputsAreClamped()
        {
            // 範囲外でも壊れず0..1に収まる
            float insp = HeroicLeadershipRules.FrontlineInspiration(9f, 9f, 9f);
            Assert.AreEqual(1f, insp, Eps);
            float cont = HeroicLeadershipRules.CharismaContagion(-9f, -9f);
            Assert.AreEqual(0f, cont, Eps);
            // 護衛は非負クランプ＝負でも denom=1 として扱う：numer=0.5×0.5=0.25/1=0.25
            float cas = HeroicLeadershipRules.CommanderCasualtyChance(0.5f, 0.5f, -9f);
            Assert.AreEqual(0.25f, cas, Eps);
        }

        [Test]
        public void Narrative_HeroAtFrontInspiresButRisksRuin()
        {
            // 物語：慕われた猛将が陣頭に立ち、適度な無謀さで猛攻する。士気は爆発的に高まるが、
            // 敵に狙われ護衛も薄く、英雄の戦死リスクが高い。倒れれば部隊の士気崩壊は甚大（諸刃）。
            float insp = HeroicLeadershipRules.FrontlineInspiration(0.9f, 1.0f, 0.9f);   // カリスマ将が最前線
            float cont = HeroicLeadershipRules.CharismaContagion(insp, 0.8f);            // 連結した部隊へ広く伝播
            float reckless = HeroicLeadershipRules.RecklessnessFactor(0.7f, 0.45f);      // 勇猛やや勝ち
            float exposure = HeroicLeadershipRules.ExposureRisk(1.0f, 0.9f);             // 最前線で集中砲火
            float surge = HeroicLeadershipRules.HeroicSurge(cont, reckless);
            float casualty = HeroicLeadershipRules.CommanderCasualtyChance(exposure, reckless, 1.0f); // 護衛つき
            float shock = HeroicLeadershipRules.MoraleShockIfFallen(0.9f, surge);

            Assert.Greater(surge, 0.5f, "陣頭の猛将は強烈な猛攻を生む");
            Assert.Greater(casualty, 0.2f, "前に出るほど将の戦死リスクは高い");
            Assert.Greater(shock, surge * 0.5f, "倒れれば士気崩壊は甚大（諸刃の剣）");
            Assert.IsTrue(HeroicLeadershipRules.IsHeroicLeadershipWorthwhile(surge, casualty),
                "それでも得る猛攻が危険を上回り陣頭指揮は割に合う");

            // 対比：臆病で後方に控える将は安全だが、猛攻は出ず陣頭指揮の価値もない。
            float contRear = HeroicLeadershipRules.CharismaContagion(
                HeroicLeadershipRules.FrontlineInspiration(0.9f, 0.2f, 0.9f), 0.8f);     // 後方＝前線近さ低
            float recklessTimid = HeroicLeadershipRules.RecklessnessFactor(0.2f, 0.8f);  // 思慮優勢で臆病
            float exposureRear = HeroicLeadershipRules.ExposureRisk(0.2f, 0.9f);         // 後方は晒されない
            float surgeRear = HeroicLeadershipRules.HeroicSurge(contRear, recklessTimid);
            float casualtyRear = HeroicLeadershipRules.CommanderCasualtyChance(exposureRear, recklessTimid, 1.0f);

            Assert.Less(casualtyRear, casualty, "後方の将は安全（戦死リスクは低い）");
            Assert.Less(surgeRear, surge, "だが猛攻は出ない");
            Assert.IsFalse(HeroicLeadershipRules.IsHeroicLeadershipWorthwhile(surgeRear, casualtyRear),
                "猛攻が乏しく陣頭に出る価値がない");
        }
    }
}
