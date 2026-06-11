using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>膠着戦況（StalemateRules・塹壕戦・RMK-3 #1408）の純ロジックテスト。</summary>
    public class StalemateRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>膠着尤度＝拮抗（forceBalance=0.5）かつ防御有利なほど高い。両端は拮抗0。</summary>
        [Test]
        public void StalemateLikelihood_拮抗と防御有利()
        {
            // 互角（0.5）かつ防御有利満点→最大1。
            Assert.AreEqual(1f, StalemateRules.StalemateLikelihood(0.5f, 1f), Eps);
            // 互角でも防御有利ゼロ→膠着しない。
            Assert.AreEqual(0f, StalemateRules.StalemateLikelihood(0.5f, 0f), Eps);
            // 一方的（forceBalance=1.0）は拮抗0→防御有利でも膠着しない。
            Assert.AreEqual(0f, StalemateRules.StalemateLikelihood(1f, 1f), Eps);
            // forceBalance=0.75→拮抗0.5、防御有利1.0→0.5。
            Assert.AreEqual(0.5f, StalemateRules.StalemateLikelihood(0.75f, 1f), Eps);
        }

        /// <summary>会戦の三択＝決定的差で勝利／敗北、拮抗なら膠着。これが核。</summary>
        [Test]
        public void ResolveBattle_勝利敗北膠着の三択()
        {
            // 既定 decisiveMargin=0.2、防御有利0。攻撃1.0 vs 防御0.5→差0.5>0.2→勝利。
            Assert.AreEqual(BattleResult.勝利, StalemateRules.ResolveBattle(1f, 0.5f, 0f));
            // 攻撃0.4 vs 防御0.9→差−0.5<−0.2→敗北。
            Assert.AreEqual(BattleResult.敗北, StalemateRules.ResolveBattle(0.4f, 0.9f, 0f));
            // 攻撃0.6 vs 防御0.5→差0.1（<0.2）→膠着（拮抗で決着せず）。
            Assert.AreEqual(BattleResult.膠着, StalemateRules.ResolveBattle(0.6f, 0.5f, 0f));
        }

        /// <summary>防御有利が膠着を生む＝攻撃優位でも守りが堅いと決着しない。</summary>
        [Test]
        public void ResolveBattle_防御有利で膠着へ()
        {
            // 防御有利0なら攻撃0.8 vs 防御0.5は差0.3>0.2→勝利。
            Assert.AreEqual(BattleResult.勝利, StalemateRules.ResolveBattle(0.8f, 0.5f, 0f));
            // 同じ戦力でも防御有利1.0で防御側が0.5→1.0に底上げ、差−0.2（≦margin）→膠着。
            Assert.AreEqual(BattleResult.膠着, StalemateRules.ResolveBattle(0.8f, 0.5f, 1f));
        }

        /// <summary>相互消耗＝膠着強度×消耗速度×dt（決着なき血の代償）。</summary>
        [Test]
        public void MutualAttrition_膠着で消耗継続()
        {
            // 既定 attritionRate=0.15。強度1・dt2→0.3。
            Assert.AreEqual(0.3f, StalemateRules.MutualAttrition(1f, 2f), Eps);
            // 強度半分なら消耗も半分。
            Assert.AreEqual(0.075f, StalemateRules.MutualAttrition(0.5f, 1f), Eps);
            // 膠着なし（強度0）は消耗なし。
            Assert.AreEqual(0f, StalemateRules.MutualAttrition(0f, 10f), Eps);
        }

        /// <summary>戦線の固着＝膠着尤度×(0.5＋要塞化×0.5)。塹壕・要塞化が固着を深める。</summary>
        [Test]
        public void FrontStagnation_塹壕で固着()
        {
            // 尤度1・要塞化1→1×1.0=1。
            Assert.AreEqual(1f, StalemateRules.FrontStagnation(1f, 1f), Eps);
            // 尤度1・要塞化0→1×0.5=0.5（膠着でも半分は動かない）。
            Assert.AreEqual(0.5f, StalemateRules.FrontStagnation(1f, 0f), Eps);
            // 尤度0なら固着なし。
            Assert.AreEqual(0f, StalemateRules.FrontStagnation(0f, 1f), Eps);
        }

        /// <summary>膠着打開＝新技術・側面機動・新鋭予備のどれかで破る（OR合成）。</summary>
        [Test]
        public void StalemateBreaker_打開要素のOR合成()
        {
            // どれも無ければ打開できない。
            Assert.AreEqual(0f, StalemateRules.StalemateBreaker(0f, 0f, 0f), Eps);
            // 三つ揃えば確実に打開。
            Assert.AreEqual(1f, StalemateRules.StalemateBreaker(1f, 1f, 1f), Eps);
            // 新技術単独でも打開へ働く。
            Assert.AreEqual(1f, StalemateRules.StalemateBreaker(1f, 0f, 0f), Eps);
            // 各0.5→1−0.5×0.5×0.5=0.875。
            Assert.AreEqual(0.875f, StalemateRules.StalemateBreaker(0.5f, 0.5f, 0.5f), Eps);
        }

        /// <summary>消耗戦のコスト＝膠着の長さ×コスト速度×dt（勝者なき疲弊）。</summary>
        [Test]
        public void WarOfAttritionCost_長期膠着で国力疲弊()
        {
            // 既定 warCostRate=0.1。長さ1・dt3→0.3。
            Assert.AreEqual(0.3f, StalemateRules.WarOfAttritionCost(1f, 3f), Eps);
            // 長さゼロなら疲弊なし。
            Assert.AreEqual(0f, StalemateRules.WarOfAttritionCost(0f, 10f), Eps);
        }

        /// <summary>膠着下の士気＝膠着の長さ×侵食×dt 分だけ低下（厭戦）。下限0。</summary>
        [Test]
        public void MoraleUnderStalemate_厭戦で士気低下()
        {
            // 既定 moraleErosionRate=0.2。長さ1・dt1→0.2低下。0.8→0.6。
            Assert.AreEqual(0.6f, StalemateRules.MoraleUnderStalemate(0.8f, 1f, 1f), Eps);
            // 膠着なし（長さ0）なら士気不変。
            Assert.AreEqual(0.8f, StalemateRules.MoraleUnderStalemate(0.8f, 0f, 5f), Eps);
            // 下限0でクランプ。
            Assert.AreEqual(0f, StalemateRules.MoraleUnderStalemate(0.1f, 1f, 5f), Eps);
        }

        /// <summary>デッドロック判定＝膠着尤度が閾値以上で戦線が動かない。</summary>
        [Test]
        public void IsDeadlock_閾値判定()
        {
            // 既定閾値0.6。
            Assert.IsTrue(StalemateRules.IsDeadlock(0.7f));
            Assert.IsFalse(StalemateRules.IsDeadlock(0.5f));
            Assert.IsTrue(StalemateRules.IsDeadlock(0.6f));
            // 明示閾値。
            Assert.IsFalse(StalemateRules.IsDeadlock(0.5f, 0.8f));
        }
    }
}
