using NUnit.Framework;
using UnityEngine;
using Ginei;
using MonoParams = Ginei.MonopolyRules.MonopolyParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 独占・財閥＝市場の失敗（MonopolyRules）を固定する：価格吊り上げ（競争域0・非線形跳ね・クランプ）、
    /// 生活水準への害、政治買収（シェア×富）、競争圧なき停滞、解体反発（早い介入は安い）、
    /// シェアドリフト（放置で寡占・規模の自己強化・競争性で侵食）。既定 Params の具体値で期待値固定。
    /// </summary>
    public class MonopolyRulesTests
    {
        // 吊り上げ：競争域（share≤0.4）は0、share0.7で0.25、完全独占で最大1（既定＝指数2・maxMarkup1）
        [Test]
        public void PriceMarkup_DefaultParams_FixedValues()
        {
            Assert.AreEqual(0f, MonopolyRules.PriceMarkup(0.4f), 1e-4f);   // 競争域＝吊り上げなし
            Assert.AreEqual(0f, MonopolyRules.PriceMarkup(0.1f), 1e-4f);
            Assert.AreEqual(0.25f, MonopolyRules.PriceMarkup(0.7f), 1e-4f); // t=0.5 → 0.5^2 = 0.25
            Assert.AreEqual(1f, MonopolyRules.PriceMarkup(1f), 1e-4f);      // 完全独占＝maxMarkup
            Assert.AreEqual(1f, MonopolyRules.PriceMarkup(5f), 1e-4f);      // 入力クランプ
        }

        // 非線形：中間シェアの吊り上げは線形より小さい＝支配的になってから一気に跳ねる
        [Test]
        public void PriceMarkup_IsConvex_JumpsWhenDominant()
        {
            float mid = MonopolyRules.PriceMarkup(0.7f);  // t=0.5
            Assert.Less(mid, 0.5f); // 線形なら0.5のところ0.25＝凸
            // 跳ね：後半の伸び（0.7→1.0）が前半（0.4→0.7）より大きい
            Assert.Greater(MonopolyRules.PriceMarkup(1f) - mid, mid - MonopolyRules.PriceMarkup(0.4f));
        }

        // 害：markup0で0・markup1で0.8（既定 harmScale0.8）・負はクランプ
        [Test]
        public void ConsumerHarm_DefaultParams_FixedValues()
        {
            Assert.AreEqual(0f, MonopolyRules.ConsumerHarm(0f), 1e-4f);
            Assert.AreEqual(0.4f, MonopolyRules.ConsumerHarm(0.5f), 1e-4f);
            Assert.AreEqual(0.8f, MonopolyRules.ConsumerHarm(1f), 1e-4f);
            Assert.AreEqual(0f, MonopolyRules.ConsumerHarm(-1f), 1e-4f); // 負はクランプ
        }

        // 政治買収：シェア×富（既定 captureScale1）。片方0なら買えない・両方1で完全買収
        [Test]
        public void PoliticalCapture_RequiresBothShareAndWealth()
        {
            Assert.AreEqual(0.25f, MonopolyRules.PoliticalCapture(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, MonopolyRules.PoliticalCapture(1f, 0f), 1e-4f); // 富なし＝買収不可
            Assert.AreEqual(0f, MonopolyRules.PoliticalCapture(0f, 1f), 1e-4f); // 支配なし＝買収不可
            Assert.AreEqual(1f, MonopolyRules.PoliticalCapture(1f, 1f), 1e-4f);
        }

        // 停滞：競争域は0・share0.7で0.5・完全独占で1（既定 stagnationScale1）
        [Test]
        public void InnovationStagnation_DefaultParams_FixedValues()
        {
            Assert.AreEqual(0f, MonopolyRules.InnovationStagnation(0.4f), 1e-4f); // 競争圧あり＝停滞なし
            Assert.AreEqual(0.5f, MonopolyRules.InnovationStagnation(0.7f), 1e-4f);
            Assert.AreEqual(1f, MonopolyRules.InnovationStagnation(1f), 1e-4f);   // 競争圧ゼロ＝最大停滞
        }

        // 解体反発：シェアの2乗＝早い介入は安い（0.3→0.09・0.9→0.81）
        [Test]
        public void BreakupBacklash_EarlyInterventionIsCheap()
        {
            float early = MonopolyRules.BreakupBacklash(0.3f);
            float late = MonopolyRules.BreakupBacklash(0.9f);
            Assert.AreEqual(0.09f, early, 1e-4f);
            Assert.AreEqual(0.81f, late, 1e-4f);
            Assert.Greater(late, early * 3f); // 育ててからでは3倍以上高くつく（超線形）
        }

        // ドリフト：競争性0で寡占へ漂い（大シェアほど速い＝自己強化）、競争性1で競争域0.4へ侵食、dt0は無変化
        [Test]
        public void ShareTick_DriftsToMonopoly_AndErodesUnderCompetition()
        {
            var p = MonoParams.Default; // driftRate0.1
            // 放置（競争性0）：0.5 → +0.1×(0.5+0.5) = 0.6
            Assert.AreEqual(0.6f, MonopolyRules.ShareTick(0.5f, 0f, 1f), 1e-4f);
            // 規模の自己強化：大きいシェアほど成長が速い（0.8→+0.13 > 0.5→+0.10）
            float stepBig = MonopolyRules.ShareTick(0.8f, 0f, 1f) - 0.8f;
            float stepSmall = MonopolyRules.ShareTick(0.5f, 0f, 1f) - 0.5f;
            Assert.Greater(stepBig, stepSmall);
            Assert.AreEqual(0.93f, MonopolyRules.ShareTick(0.8f, 0f, 1f), 1e-4f);
            // 競争性1：競争域0.4へ侵食（0.8 → 0.7）
            Assert.AreEqual(0.7f, MonopolyRules.ShareTick(0.8f, 1f, 1f), 1e-4f);
            // dt0/負は無変化・目標を行き過ぎない
            Assert.AreEqual(0.5f, MonopolyRules.ShareTick(0.5f, 0f, 0f), 1e-4f);
            Assert.AreEqual(0.5f, MonopolyRules.ShareTick(0.5f, 0f, -1f), 1e-4f);
            Assert.AreEqual(1f, MonopolyRules.ShareTick(0.99f, 0f, 10f, p), 1e-4f); // 行き過ぎない
        }
    }
}
