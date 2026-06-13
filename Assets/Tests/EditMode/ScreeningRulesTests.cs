using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 偵察幕（スクリーニング）を固定する：軽快な前衛が本隊を隠し、敵索敵を遅らせ、早期警戒を与えるが、
    /// 接触で消耗して破れる。幕と貫通のせめぎ合い・隠蔽による過小評価・境界・クランプを担保。
    /// </summary>
    public class ScreeningRulesTests
    {
        private static readonly ScreeningParams P = ScreeningParams.Default; // 重み1 / 消耗0.5 / 警戒1 / 最大遅延10 / 半減点20

        [Test]
        public void ScreenStrength_ScalesWithMobility()
        {
            Assert.AreEqual(20f, ScreeningRules.ScreenStrength(20f, 50f, P), 1e-4f);  // 機動50＝等倍
            Assert.AreEqual(26f, ScreeningRules.ScreenStrength(20f, 80f, P), 1e-4f);  // 機動80＝1.3倍
            Assert.AreEqual(10f, ScreeningRules.ScreenStrength(20f, 0f, P), 1e-4f);   // 機動0＝0.5倍
            Assert.AreEqual(30f, ScreeningRules.ScreenStrength(20f, 100f, P), 1e-4f); // 機動100＝1.5倍
        }

        [Test]
        public void Concealment_HardterForLargerMainForce()
        {
            Assert.AreEqual(0.5f, ScreeningRules.Concealment(40f, 40f, P), 1e-4f);  // 拮抗＝半分隠す
            Assert.AreEqual(0.75f, ScreeningRules.Concealment(60f, 20f, P), 1e-4f); // 小部隊は濃い幕で隠れる
            Assert.AreEqual(0f, ScreeningRules.Concealment(0f, 40f, P), 1e-4f);     // 幕ゼロ＝隠せない
        }

        [Test]
        public void PenetrationChance_ContestedBetweenReconAndScreen()
        {
            Assert.AreEqual(0.75f, ScreeningRules.PenetrationChance(30f, 10f), 1e-4f); // 強い偵察は薄い幕を貫く
            Assert.AreEqual(0.5f, ScreeningRules.PenetrationChance(10f, 10f), 1e-4f);  // 拮抗＝五分
            Assert.AreEqual(0f, ScreeningRules.PenetrationChance(0f, 0f), 1e-4f);      // 両ゼロ＝0
        }

        [Test]
        public void RevealedSize_UndersizedByConcealment_AccurateWhenPenetrated()
        {
            Assert.AreEqual(40f, ScreeningRules.RevealedSize(100f, 0.6f, 0f), 1e-4f);   // 未貫通＝隠蔽ぶん過小
            Assert.AreEqual(100f, ScreeningRules.RevealedSize(100f, 0.6f, 1f), 1e-4f);  // 完全貫通＝真値
            Assert.AreEqual(70f, ScreeningRules.RevealedSize(100f, 0.6f, 0.5f), 1e-4f); // 中間は線形
        }

        [Test]
        public void ScreenAttrition_LightScreenErodesAndCaps()
        {
            Assert.AreEqual(20f, ScreeningRules.ScreenAttrition(40f, 1f, P), 1e-4f); // 接触圧1＝消耗率0.5で半分
            // 過大な圧でも残量を超えて失わない（キャップ）
            Assert.AreEqual(40f, ScreeningRules.ScreenAttrition(40f, 5f, P), 1e-4f);
        }

        [Test]
        public void EarlyWarning_DenserScreenWarnsSooner_FasterApproachShrinks()
        {
            Assert.AreEqual(4f, ScreeningRules.EarlyWarning(40f, 10f, P), 1e-4f); // 濃さ40÷速度10
            Assert.AreEqual(2f, ScreeningRules.EarlyWarning(40f, 20f, P), 1e-4f); // 速い接近は警戒を縮める
        }

        [Test]
        public void DelayImposed_ApproachesMaxWithDenserScreen()
        {
            Assert.AreEqual(0f, ScreeningRules.DelayImposed(0f, P), 1e-4f);   // 幕ゼロ＝遅延なし
            Assert.AreEqual(5f, ScreeningRules.DelayImposed(20f, P), 1e-4f);  // 半減点20＝最大の半分
            Assert.AreEqual(7.5f, ScreeningRules.DelayImposed(60f, P), 1e-4f);// 濃いほど上限へ漸近
        }

        [Test]
        public void IsScreenBroken_AtThreshold()
        {
            Assert.IsTrue(ScreeningRules.IsScreenBroken(5f, 10f));   // 薄れて露呈
            Assert.IsFalse(ScreeningRules.IsScreenBroken(15f, 10f)); // まだ張れている
        }

        [Test]
        public void Story_DenseScreenHidesAndDelays_ThenBreaksUnderContact()
        {
            // 軽快な前衛30隊・高機動(80)が濃い幕を張る
            float screen = ScreeningRules.ScreenStrength(30f, 80f, P); // 30×1.3 = 39
            Assert.AreEqual(39f, screen, 1e-4f);

            // 本隊40を隠す＝相手に見える規模は真値より過小
            float conceal = ScreeningRules.Concealment(screen, 40f, P);
            float pen = ScreeningRules.PenetrationChance(10f, screen); // 弱い偵察はほぼ貫けない
            float revealed = ScreeningRules.RevealedSize(100f, conceal, pen);
            Assert.Less(revealed, 100f); // 過小評価される

            // 敵索敵を遅らせ、接近を早期警戒する
            Assert.Greater(ScreeningRules.DelayImposed(screen, P), 0f);
            Assert.Greater(ScreeningRules.EarlyWarning(screen, 10f, P), 0f);

            // だが激しい接触で軽快な幕は消耗して破れ、本隊が露呈する
            float loss = ScreeningRules.ScreenAttrition(screen, 5f, P);
            float remaining = screen - loss;
            Assert.IsTrue(ScreeningRules.IsScreenBroken(remaining, 10f)); // 幕が薄れ露呈
        }
    }
}
