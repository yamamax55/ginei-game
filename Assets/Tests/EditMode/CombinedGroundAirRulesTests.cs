using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 航空地上協同（近接航空支援・制空権と地上の協同）の純ロジック検証。既定 Params で期待値固定。
    /// </summary>
    public class CombinedGroundAirRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void AirSuperiority_RatioAndTie()
        {
            // 70 対 30 防空 ＝ 0.7。
            Assert.AreEqual(0.7f, CombinedGroundAirRules.AirSuperiority(70f, 30f), Eps);
            // 双方0は拮抗 0.5。
            Assert.AreEqual(0.5f, CombinedGroundAirRules.AirSuperiority(0f, 0f), Eps);
            // 負入力はクランプ（防空のみ＝制空0）。
            Assert.AreEqual(0f, CombinedGroundAirRules.AirSuperiority(-5f, 10f), Eps);
        }

        [Test]
        public void CloseAirSupport_SortieScalesSupport()
        {
            // 制空0.7・出撃満点：0.7*Lerp(0.5,1,1)=0.7。
            Assert.AreEqual(0.7f, CombinedGroundAirRules.CloseAirSupport(0.7f, 1f), Eps);
            // 出撃0でも制空ぶんは出る：0.7*0.5=0.35。
            Assert.AreEqual(0.35f, CombinedGroundAirRules.CloseAirSupport(0.7f, 0f), Eps);
        }

        [Test]
        public void GroundAirCoordination_ControllersBoostComms()
        {
            // 通信0.8・FAC満点：0.8*Lerp(0.5,1,1)=0.8。
            Assert.AreEqual(0.8f, CombinedGroundAirRules.GroundAirCoordination(0.8f, 1f), Eps);
            // FAC無し：0.8*0.5=0.4。
            Assert.AreEqual(0.4f, CombinedGroundAirRules.GroundAirCoordination(0.8f, 0f), Eps);
        }

        [Test]
        public void CasEffectiveness_CoordinationGates()
        {
            // 近接0.7・連携0.8：0.7*Lerp(0.5,1,0.8)=0.7*0.9=0.63。
            Assert.AreEqual(0.63f, CombinedGroundAirRules.CasEffectiveness(0.7f, 0.8f), Eps);
            // 連携0なら半減：0.7*0.5=0.35。
            Assert.AreEqual(0.35f, CombinedGroundAirRules.CasEffectiveness(0.7f, 0f), Eps);
        }

        [Test]
        public void AntiAirAttrition_FallsWithAirSuperiority()
        {
            // 防空満点・制空0：1*1*0.6=0.6。
            Assert.AreEqual(0.6f, CombinedGroundAirRules.AntiAirAttrition(1f, 0f), Eps);
            // 制空0.7で損耗低下：1*0.3*0.6=0.18。
            Assert.AreEqual(0.18f, CombinedGroundAirRules.AntiAirAttrition(1f, 0.7f), Eps);
        }

        [Test]
        public void WeatherImpact_AllWeatherCancels()
        {
            // 悪天候満点・全天候能力0：1*1*0.8=0.8。
            Assert.AreEqual(0.8f, CombinedGroundAirRules.WeatherImpact(1f, 0f), Eps);
            // 全天候0.5で半減：1*0.5*0.8=0.4。
            Assert.AreEqual(0.4f, CombinedGroundAirRules.WeatherImpact(1f, 0.5f), Eps);
            // 晴天は影響なし。
            Assert.AreEqual(0f, CombinedGroundAirRules.WeatherImpact(0f, 0f), Eps);
        }

        [Test]
        public void InterdictionEffect_ScalesWithAirAndSupply()
        {
            // 制空0.7・補給線露出満点：0.7*1*0.7=0.49。
            Assert.AreEqual(0.49f, CombinedGroundAirRules.InterdictionEffect(0.7f, 1f), Eps);
            // 制空なしなら阻止できない。
            Assert.AreEqual(0f, CombinedGroundAirRules.InterdictionEffect(0f, 1f), Eps);
        }

        [Test]
        public void CombinedArmsAdvantage_BlendsAndWeatherDampens()
        {
            // 近接0.63・阻止0.49・好天：0.63*0.5+0.49*0.5=0.56。
            Assert.AreEqual(0.56f, CombinedGroundAirRules.CombinedArmsAdvantage(0.63f, 0.49f, 0f), Eps);
            // 悪天候0.5で半減：0.56*0.5=0.28。
            Assert.AreEqual(0.28f, CombinedGroundAirRules.CombinedArmsAdvantage(0.63f, 0.49f, 0.5f), Eps);
        }

        [Test]
        public void Story_AirSupremacyTurnsGroundBattle()
        {
            var p = CombinedGroundAirParams.Default;

            // 制空を握る側：航空戦力80・敵防空20。
            float airUp = CombinedGroundAirRules.AirSuperiority(80f, 20f, p);   // 0.8
            // 制空を失う側：航空戦力20・敵防空80。
            float airDown = CombinedGroundAirRules.AirSuperiority(20f, 80f, p); // 0.2
            Assert.Greater(airUp, airDown);

            // 高い出撃頻度＋良好な通信＋FAC で近接支援が実効化。
            float casUp = CombinedGroundAirRules.CloseAirSupport(airUp, 0.9f, p);
            float coordUp = CombinedGroundAirRules.GroundAirCoordination(0.9f, 0.9f, p);
            float effUp = CombinedGroundAirRules.CasEffectiveness(casUp, coordUp, p);

            // 制空劣勢側は出撃も連携も乏しい。
            float casDown = CombinedGroundAirRules.CloseAirSupport(airDown, 0.3f, p);
            float coordDown = CombinedGroundAirRules.GroundAirCoordination(0.4f, 0.2f, p);
            float effDown = CombinedGroundAirRules.CasEffectiveness(casDown, coordDown, p);
            Assert.Greater(effUp, effDown);

            // 制空側は敵補給線を断つ。
            float interUp = CombinedGroundAirRules.InterdictionEffect(airUp, 0.8f, p);
            float interDown = CombinedGroundAirRules.InterdictionEffect(airDown, 0.8f, p);
            Assert.Greater(interUp, interDown);

            // 損耗は制空側が小さい。
            float lossUp = CombinedGroundAirRules.AntiAirAttrition(0.5f, airUp, p);
            float lossDown = CombinedGroundAirRules.AntiAirAttrition(0.5f, airDown, p);
            Assert.Less(lossUp, lossDown);

            // 好天では航空優勢側が地上戦で優位。
            float clear = 0f;
            float advUp = CombinedGroundAirRules.CombinedArmsAdvantage(effUp, interUp, clear, p);
            float advDown = CombinedGroundAirRules.CombinedArmsAdvantage(effDown, interDown, clear, p);
            Assert.Greater(advUp, advDown);

            // 悪天候は優位を割り引く（全天候能力なし）。
            float storm = CombinedGroundAirRules.WeatherImpact(0.9f, 0.1f, p);
            float advStorm = CombinedGroundAirRules.CombinedArmsAdvantage(effUp, interUp, storm, p);
            Assert.Less(advStorm, advUp);
            Assert.GreaterOrEqual(advStorm, 0f);
        }
    }
}
