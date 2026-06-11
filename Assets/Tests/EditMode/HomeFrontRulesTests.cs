using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// HomeFrontRules（RMK-4 #1412・レマルク『西部戦線異状なし』＝前線-後方の情報非対称）の純ロジック検証。
    /// 認識ギャップ・プロパガンダ膨張・前線の幻滅・銃後の士気・乖離の崩壊・帰還兵の疎外・露呈の士気ショック・物語崩壊判定。
    /// </summary>
    public class HomeFrontRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>認識ギャップ＝前線の現実×銃後の信念。後方が美化した像を信じるほど開く。</summary>
        [Test]
        public void PerceptionGap_悲惨な現実と高い信念で大きい()
        {
            // 0.9 * 0.8 = 0.72
            Assert.AreEqual(0.72f, HomeFrontRules.PerceptionGap(0.9f, 0.8f), Eps);
            // 銃後も現実を悲惨と理解（信念0）ならギャップ無し
            Assert.AreEqual(0f, HomeFrontRules.PerceptionGap(0.9f, 0f), Eps);
        }

        /// <summary>プロパガンダ膨張＝公式の物語×検閲で銃後の幻想が膨らむ。</summary>
        [Test]
        public void PropagandaInflation_検閲が幻想を膨らませる()
        {
            // 0.6 * (1 + 0.8*0.5) = 0.6 * 1.4 = 0.84
            Assert.AreEqual(0.84f, HomeFrontRules.PropagandaInflation(0.6f, 0.8f), Eps);
            // 検閲ゼロでも物語の素地は届く
            Assert.AreEqual(0.6f, HomeFrontRules.PropagandaInflation(0.6f, 0f), Eps);
        }

        /// <summary>前線の幻滅＝認識ギャップ×被害目撃。落差と悲惨な死で深まる。</summary>
        [Test]
        public void FrontlineDisillusionment_落差と被害で深まる()
        {
            // 0.5 * 0.6 * (1 + 0.8) = 0.3 * 1.8 = 0.54
            Assert.AreEqual(0.54f, HomeFrontRules.FrontlineDisillusionment(0.5f, 0.6f), Eps);
            // ギャップ無しなら落差由来の幻滅は生じない
            Assert.AreEqual(0f, HomeFrontRules.FrontlineDisillusionment(0f, 0.6f), Eps);
        }

        /// <summary>銃後の士気＝信念×認識される勝利。勝っていると信じる間は高い。</summary>
        [Test]
        public void HomefrontMorale_勝利を信じる間は高い()
        {
            // 0.8 * 0.9 = 0.72
            Assert.AreEqual(0.72f, HomeFrontRules.HomefrontMorale(0.8f, 0.9f), Eps);
            // 勝利が信じられなくなると幻想が支えきれない
            Assert.AreEqual(0.16f, HomeFrontRules.HomefrontMorale(0.8f, 0.2f), Eps);
        }

        /// <summary>乖離の崩壊＝真実の漏れが大きなギャップを一気に崩す。</summary>
        [Test]
        public void GapCollapse_真実の漏れで一気に崩れる()
        {
            // 0.7 * 0.8 * (1 + 1.5) = 0.56 * 2.5 = 1.4 → clamp 1
            Assert.AreEqual(1f, HomeFrontRules.GapCollapse(0.7f, 0.8f), Eps);
            // 0.3 * 0.2 * 2.5 = 0.15
            Assert.AreEqual(0.15f, HomeFrontRules.GapCollapse(0.3f, 0.2f), Eps);
            // 漏れが無ければ崩壊しない
            Assert.AreEqual(0f, HomeFrontRules.GapCollapse(0.7f, 0f), Eps);
        }

        /// <summary>帰還兵の疎外＝前線の幻滅×銃後の無邪気さ（失われた世代）。</summary>
        [Test]
        public void ReturneeAlienation_無邪気な銃後に疎外を感じる()
        {
            // 0.6 * 0.7 * (1 + 0.7) = 0.42 * 1.7 = 0.714
            Assert.AreEqual(0.714f, HomeFrontRules.ReturneeAlienation(0.6f, 0.7f), Eps);
            // 銃後も現実を理解していれば疎外は薄い
            Assert.AreEqual(0f, HomeFrontRules.ReturneeAlienation(0.6f, 0f), Eps);
        }

        /// <summary>露呈時の士気ショック＝幻想が大きいほど露呈で大きく落ちる。</summary>
        [Test]
        public void MoraleShockOnRevelation_高く持ち上げた分だけ落ちる()
        {
            // 0.84 * 0.9 = 0.756
            Assert.AreEqual(0.756f, HomeFrontRules.MoraleShockOnRevelation(0.84f, 0.9f), Eps);
            // 膨張させていなければ落差は無い
            Assert.AreEqual(0f, HomeFrontRules.MoraleShockOnRevelation(0f, 0.9f), Eps);
        }

        /// <summary>物語崩壊判定＝乖離崩壊が閾値（既定0.6）を超えたか。</summary>
        [Test]
        public void IsNarrativeCollapse_閾値超えで成立()
        {
            Assert.IsTrue(HomeFrontRules.IsNarrativeCollapse(0.7f));   // 0.7 ≥ 0.6
            Assert.IsTrue(HomeFrontRules.IsNarrativeCollapse(0.6f));   // 境界も成立
            Assert.IsFalse(HomeFrontRules.IsNarrativeCollapse(0.5f));  // 0.5 < 0.6
        }
    }
}
