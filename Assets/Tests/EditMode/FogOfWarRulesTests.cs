using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦場の霧の純ロジックの EditMode テスト（既定 Params で期待値固定）。</summary>
    public class FogOfWarRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 可視性は近くほど高く遠くで下がる()
        {
            // 既定：half=20, base=0.15。distFactor=1/(1+d/20) を floor..1 へ写す
            Assert.AreEqual(1f, FogOfWarRules.Visibility(0f, 0f), Eps);        // 至近＝完全可視
            Assert.AreEqual(0.575f, FogOfWarRules.Visibility(20f, 0f), Eps);  // 半減距離
            Assert.AreEqual(0.3625f, FogOfWarRules.Visibility(60f, 0f), Eps); // 遠方で霧が深い
        }

        [Test]
        public void 偵察が効くと遠くても霧が晴れる()
        {
            // recon=1 で floor=1 → distFactor によらず常に完全可視
            Assert.AreEqual(1f, FogOfWarRules.Visibility(20f, 1f), Eps);
            Assert.AreEqual(1f, FogOfWarRules.Visibility(60f, 1f), Eps);
        }

        [Test]
        public void 推定誤差は可視性が低いほど大きい()
        {
            Assert.AreEqual(0f, FogOfWarRules.EstimationError(1f), Eps);        // 見えれば誤差なし
            Assert.AreEqual(0.7f, FogOfWarRules.EstimationError(0f), Eps);      // 見えなければ最大
            Assert.AreEqual(0.2975f, FogOfWarRules.EstimationError(0.575f), Eps);
        }

        [Test]
        public void 推定戦力はロールで真値の上下にぶれる()
        {
            // roll=0.5 で真値・roll=1 で過大評価・roll=0 で過小評価（誤差0.3）
            Assert.AreEqual(100f, FogOfWarRules.PerceivedStrength(100f, 0.3f, 0.5f), Eps);
            Assert.AreEqual(130f, FogOfWarRules.PerceivedStrength(100f, 0.3f, 1f), Eps);
            Assert.AreEqual(70f, FogOfWarRules.PerceivedStrength(100f, 0.3f, 0f), Eps);
            // 負にはならない
            Assert.AreEqual(0f, FogOfWarRules.PerceivedStrength(100f, 2f, 0f), Eps);
        }

        [Test]
        public void 隠蔽は地形遮蔽と電波管制の相補積()
        {
            Assert.AreEqual(0f, FogOfWarRules.ConcealmentBonus(0f, 0f), Eps);     // 何もなければ素通し
            Assert.AreEqual(0.75f, FogOfWarRules.ConcealmentBonus(0.5f, 0.5f), Eps);
            Assert.AreEqual(1f, FogOfWarRules.ConcealmentBonus(1f, 0f), Eps);     // 完全遮蔽
        }

        [Test]
        public void 探知距離は隠蔽で縮む()
        {
            Assert.AreEqual(100f, FogOfWarRules.DetectionRange(100f, 0f), Eps);
            Assert.AreEqual(25f, FogOfWarRules.DetectionRange(100f, 0.75f), Eps);
            Assert.AreEqual(0f, FogOfWarRules.DetectionRange(100f, 1f), Eps);
        }

        [Test]
        public void 情報は古いほど信頼度が落ちる()
        {
            // 既定 decay=0.1 → 1/(1+0.1×age)
            Assert.AreEqual(1f, FogOfWarRules.IntelDecay(0f), Eps);
            Assert.AreEqual(0.5f, FogOfWarRules.IntelDecay(10f), Eps);
            Assert.AreEqual(0.2f, FogOfWarRules.IntelDecay(40f), Eps);
        }

        [Test]
        public void 霧中判定は閾値未満でtrue()
        {
            Assert.IsTrue(FogOfWarRules.IsFogged(0.3f, 0.5f));
            Assert.IsFalse(FogOfWarRules.IsFogged(0.6f, 0.5f));
        }

        // 物語：偵察すれば霧が晴れて推定が正確になり、こちらだけ見えていれば奇襲が成立する。
        [Test]
        public void 物語_偵察で霧が晴れ推定が正確になり一方的視認で奇襲が成る()
        {
            var p = FogOfWarParams.Default;
            float dist = 40f;

            // 偵察なし＝遠方で霧が深く、推定が大きく外れうる
            float visBlind = FogOfWarRules.Visibility(dist, 0f, p);
            float errBlind = FogOfWarRules.EstimationError(visBlind, p);

            // 偵察を effortfull に焚く＝可視性が上がり推定誤差が縮む
            float visScout = FogOfWarRules.Visibility(dist, 0.9f, p);
            float errScout = FogOfWarRules.EstimationError(visScout, p);

            Assert.Greater(visScout, visBlind, "偵察で可視性が上がる");
            Assert.Less(errScout, errBlind, "偵察で推定誤差が縮む");

            // 同じ過大評価ロールでも、偵察済みのほうが真値に近い推定になる
            float trueStr = 100f;
            float guessBlind = FogOfWarRules.PerceivedStrength(trueStr, errBlind, 1f);
            float guessScout = FogOfWarRules.PerceivedStrength(trueStr, errScout, 1f);
            Assert.Less(Mathf.Abs(guessScout - trueStr), Mathf.Abs(guessBlind - trueStr), "偵察済みの推定は真値に近い");

            // こちらは敵がよく見え（高可視）、敵からこちらは見えない（低可視）＝奇襲が成立
            float surprise = FogOfWarRules.SurpriseFactor(visScout, 0.05f);
            float noSurprise = FogOfWarRules.SurpriseFactor(visScout, visScout);
            Assert.Greater(surprise, noSurprise, "一方的視認のほうが奇襲有利が大きい");
            Assert.Greater(surprise, 0.5f, "明確な奇襲有利");
        }
    }
}
