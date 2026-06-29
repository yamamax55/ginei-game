using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>建艦競争（軍拡レース）の純ロジック検証。既定 Params の具体値で期待値を固定。</summary>
    public class NavalArmsRaceRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void BuildupResponse_RivalRateTimesThreat()
        {
            // 0.6 * 1.0 * (1 + 0.4*0.5) = 0.6 * 1.2 = 0.72
            Assert.AreEqual(0.72f, NavalArmsRaceRules.BuildupResponse(0.6f, 0.4f), Eps);
            // 脅威0なら相手の建艦速度そのまま
            Assert.AreEqual(0.6f, NavalArmsRaceRules.BuildupResponse(0.6f, 0f), Eps);
            // 入力 clamp（範囲外でも 0..1）
            Assert.AreEqual(1f, NavalArmsRaceRules.BuildupResponse(2f, 2f), Eps);
            Assert.AreEqual(0f, NavalArmsRaceRules.BuildupResponse(-1f, 0.5f), Eps);
        }

        [Test]
        public void QualityVsQuantity_RatioOfTech()
        {
            // 0.3 / (0.3+0.7) = 0.3
            Assert.AreEqual(0.3f, NavalArmsRaceRules.QualityVsQuantity(0.3f, 0.7f), Eps);
            // 量だけ＝0（量で押す）
            Assert.AreEqual(0f, NavalArmsRaceRules.QualityVsQuantity(0f, 0.8f), Eps);
            // 技術だけ＝1（質を磨く）
            Assert.AreEqual(1f, NavalArmsRaceRules.QualityVsQuantity(0.5f, 0f), Eps);
            // 両0は均衡 0.5
            Assert.AreEqual(0.5f, NavalArmsRaceRules.QualityVsQuantity(0f, 0f), Eps);
        }

        [Test]
        public void EconomicBurden_BudgetTimesGdpShare()
        {
            // 0.5 * 0.4 * 1.0 = 0.2
            Assert.AreEqual(0.2f, NavalArmsRaceRules.EconomicBurden(0.5f, 0.4f), Eps);
            // 予算0なら圧迫なし
            Assert.AreEqual(0f, NavalArmsRaceRules.EconomicBurden(0f, 1f), Eps);
            // 全振りで最大
            Assert.AreEqual(1f, NavalArmsRaceRules.EconomicBurden(1f, 1f), Eps);
        }

        [Test]
        public void TechnologicalEdge_RdShareWithParity()
        {
            // 0.6 / (0.6+0.2) = 0.75（技術優越）
            Assert.AreEqual(0.75f, NavalArmsRaceRules.TechnologicalEdge(0.6f, 0.2f), Eps);
            // 拮抗は 0.5
            Assert.AreEqual(0.5f, NavalArmsRaceRules.TechnologicalEdge(0.4f, 0.4f), Eps);
            // 両0も 0.5（中立）
            Assert.AreEqual(0.5f, NavalArmsRaceRules.TechnologicalEdge(0f, 0f), Eps);
            // 敵が上回れば 0.5 未満
            Assert.AreEqual(0.25f, NavalArmsRaceRules.TechnologicalEdge(0.2f, 0.6f), Eps);
        }

        [Test]
        public void EscalationSpiral_ProductOfBothResponses()
        {
            // 0.8 * 0.6 * 1.0 = 0.48
            Assert.AreEqual(0.48f, NavalArmsRaceRules.EscalationSpiral(0.8f, 0.6f), Eps);
            // 片方が建艦をやめれば暴走しない
            Assert.AreEqual(0f, NavalArmsRaceRules.EscalationSpiral(0.9f, 0f), Eps);
            // 双方全開で最大
            Assert.AreEqual(1f, NavalArmsRaceRules.EscalationSpiral(1f, 1f), Eps);
        }

        [Test]
        public void DisarmamentIncentive_BurdenTimesExhaustion()
        {
            // 0.6 * 0.5 * 1.0 = 0.3
            Assert.AreEqual(0.3f, NavalArmsRaceRules.DisarmamentIncentive(0.6f, 0.5f), Eps);
            // 疲弊なしなら誘因なし
            Assert.AreEqual(0f, NavalArmsRaceRules.DisarmamentIncentive(1f, 0f), Eps);
            // 圧迫なしなら誘因なし
            Assert.AreEqual(0f, NavalArmsRaceRules.DisarmamentIncentive(0f, 1f), Eps);
        }

        [Test]
        public void RaceStability_SignedStableVsUnstable()
        {
            // raw = (1-0.48) + 0.3*0.5 = 0.52 + 0.15 = 0.67 → 0.67*2-1 = 0.34
            Assert.AreEqual(0.34f, NavalArmsRaceRules.RaceStability(0.48f, 0.3f), Eps);
            // 暴走なし・軍縮なし＝完全安定 (raw=1 → +1)
            Assert.AreEqual(1f, NavalArmsRaceRules.RaceStability(0f, 0f), Eps);
            // 暴走最大・軍縮なし＝不安定 (raw=0 → -1)
            Assert.AreEqual(-1f, NavalArmsRaceRules.RaceStability(1f, 0f), Eps);
        }

        [Test]
        public void ArmsRaceOutcome_EdgeMinusBurdenMinusEscalation()
        {
            // edge=(0.75-0.5)*2=0.5; 0.5*1 - 0.2 - 0.48 = -0.18
            Assert.AreEqual(-0.18f, NavalArmsRaceRules.ArmsRaceOutcome(0.75f, 0.2f, 0.48f), Eps);
            // 技術優越のみ＝優勢
            Assert.AreEqual(1f, NavalArmsRaceRules.ArmsRaceOutcome(1f, 0f, 0f), Eps);
            // 圧迫と暴走に飲まれる＝疲弊（下限 -1 に clamp）
            Assert.AreEqual(-1f, NavalArmsRaceRules.ArmsRaceOutcome(0.5f, 1f, 1f), Eps);
            // 拮抗・圧迫なし・暴走なし＝中立 0
            Assert.AreEqual(0f, NavalArmsRaceRules.ArmsRaceOutcome(0.5f, 0f, 0f), Eps);
        }

        [Test]
        public void Narrative_RunawayArmsRaceEndsInMutualExhaustion()
        {
            // 物語：両大国が脅威に駆られ建艦を加速し、暴走して経済を蝕み、最後は相互疲弊で軍縮へ。
            var p = NavalArmsRaceParams.Default;

            // 第1幕：相手の急速な建艦＋高い脅威認識 → 強い対抗建艦
            float mine = NavalArmsRaceRules.BuildupResponse(0.8f, 0.9f, p);
            float theirs = NavalArmsRaceRules.BuildupResponse(0.85f, 0.85f, p);
            Assert.Greater(mine, 0.8f);
            Assert.Greater(theirs, 0.8f);

            // 第2幕：双方の増強が掛け合わさり暴走（高エスカレーション）
            float esc = NavalArmsRaceRules.EscalationSpiral(mine, theirs, p);
            Assert.Greater(esc, 0.6f);

            // 第3幕：莫大な建艦予算が経済を圧迫
            float burden = NavalArmsRaceRules.EconomicBurden(0.9f, 0.8f, p);
            Assert.Greater(burden, 0.6f);

            // 暴走下では帰結はマイナス（疲弊側）に振れる
            float edge = NavalArmsRaceRules.TechnologicalEdge(0.55f, 0.5f, p); // ほぼ拮抗
            float outcome = NavalArmsRaceRules.ArmsRaceOutcome(edge, burden, esc, p);
            Assert.Less(outcome, 0f);

            // 暴走中＝レースは不安定（負）
            float stableHot = NavalArmsRaceRules.RaceStability(esc, 0.1f, p);
            Assert.Less(stableHot, 0f);

            // 終幕：経済圧迫×相互疲弊が軍縮の誘因を生み、軍縮が進めば安定が回復（正へ）
            float disarm = NavalArmsRaceRules.DisarmamentIncentive(burden, 0.8f, p);
            Assert.Greater(disarm, 0.4f);
            float stableCooled = NavalArmsRaceRules.RaceStability(0.1f, disarm, p);
            Assert.Greater(stableCooled, stableHot);
            Assert.Greater(stableCooled, 0f);
        }
    }
}
