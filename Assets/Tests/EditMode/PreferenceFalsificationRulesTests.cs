using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>選好偽装（クーラン型）の純ロジック EditMode テスト（既定 Params の具体値で固定）。</summary>
    public class PreferenceFalsificationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>抑圧が高いほど表明支持が本音より上へ偽装される（抑圧0なら本音そのまま）。</summary>
        [Test]
        public void ExpressedSupport_盛られる()
        {
            // 抑圧0＝偽装なし
            Assert.AreEqual(0.3f, PreferenceFalsificationRules.ExpressedSupport(0.3f, 0f), Eps);
            // 本音0.3・抑圧0.8：0.3 + (1-0.3)*0.8*0.6 = 0.636
            Assert.AreEqual(0.636f, PreferenceFalsificationRules.ExpressedSupport(0.3f, 0.8f), Eps);
            // 偽装は常に上方向（表明 >= 本音）
            Assert.GreaterOrEqual(PreferenceFalsificationRules.ExpressedSupport(0.3f, 0.8f), 0.3f);
        }

        /// <summary>選好ギャップ＝表明と本音の乖離＝体制に見えていない不満の量。</summary>
        [Test]
        public void PreferenceGap_乖離量()
        {
            float exp = PreferenceFalsificationRules.ExpressedSupport(0.3f, 0.8f); // 0.636
            Assert.AreEqual(0.336f, PreferenceFalsificationRules.PreferenceGap(0.3f, exp), Eps);
        }

        /// <summary>可視的反対が増えると個人の表明閾値が下がる＝閾値カスケード。</summary>
        [Test]
        public void RevealedThreshold_カスケードで下がる()
        {
            // 見えている反対0なら素の閾値のまま
            Assert.AreEqual(0.6f, PreferenceFalsificationRules.RevealedThreshold(0.6f, 0f), Eps);
            // 反対0.4：0.6 - 0.4*0.5 = 0.4（明かしやすくなる）
            Assert.AreEqual(0.4f, PreferenceFalsificationRules.RevealedThreshold(0.6f, 0.4f), Eps);
        }

        /// <summary>抑圧の蓋を本音の不満が上回れば可視的反対は噴出して増える。</summary>
        [Test]
        public void CascadeTick_噴出()
        {
            // visible0.2・private0.1(不満0.9)・抑圧0.3・dt1
            // pressure=0.9*(1+0.2*0.5)=0.99, net=0.99-0.3=0.69, shift=0.69*0.2=0.138 → 0.338
            float v = PreferenceFalsificationRules.CascadeTick(0.2f, 0.1f, 0.3f, 1f);
            Assert.AreEqual(0.338f, v, Eps);
            Assert.Greater(v, 0.2f);
        }

        /// <summary>抑圧の蓋が不満を上回れば可視的反対は沈静してやや戻る。</summary>
        [Test]
        public void CascadeTick_沈静()
        {
            // visible0.2・private0.8(不満0.2)・抑圧0.9・dt1
            // pressure=0.2*1.1=0.22, net=0.22-0.9=-0.68, shift=-0.136 → 0.064
            float v = PreferenceFalsificationRules.CascadeTick(0.2f, 0.8f, 0.9f, 1f);
            Assert.AreEqual(0.064f, v, Eps);
            Assert.Less(v, 0.2f);
        }

        /// <summary>可視的反対が臨界質量を超えると革命的カスケードと判定される（突然の噴出）。</summary>
        [Test]
        public void IsPreferenceCascade_臨界()
        {
            Assert.IsFalse(PreferenceFalsificationRules.IsPreferenceCascade(0.4f, 0.5f));
            Assert.IsTrue(PreferenceFalsificationRules.IsPreferenceCascade(0.5f, 0.5f));
            Assert.IsTrue(PreferenceFalsificationRules.IsPreferenceCascade(0.7f, 0.5f));
        }

        /// <summary>抑圧が強くギャップが大きいほど体制は足元が見えない＝強権ほど突然倒れる。</summary>
        [Test]
        public void RegimeBlindness_強権ほど盲目()
        {
            // 抑圧0.9・ギャップ0.336：0.9*0.336*0.7 + 0.336*0.3 = 0.31248
            float b = PreferenceFalsificationRules.RegimeBlindness(0.9f, 0.336f);
            Assert.AreEqual(0.31248f, b, Eps);
            // 同じギャップでも抑圧が低いと盲目度は小さい（盲目度は抑圧に対し単調増加）
            float bLow = PreferenceFalsificationRules.RegimeBlindness(0.2f, 0.336f);
            Assert.Less(bLow, b);
            // ギャップ0なら盲目度0
            Assert.AreEqual(0f, PreferenceFalsificationRules.RegimeBlindness(0.9f, 0f), Eps);
        }
    }
}
