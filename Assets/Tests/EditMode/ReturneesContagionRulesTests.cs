using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>帰還兵の厭戦伝播＝厭戦が伝染する純ロジック（RMK-6 #1418）の担保。</summary>
    public class ReturneesContagionRulesTests
    {
        const float Tol = 1e-4f;

        /// <summary>帰還兵の幻滅＝トラウマ×プロパガンダとの落差で深まる。</summary>
        [Test]
        public void ReturneeDisillusionment_DeepensWithTraumaAndGap()
        {
            // 0.8×0.5×(1+0.7)=0.68
            Assert.AreEqual(0.68f, ReturneesContagionRules.ReturneeDisillusionment(0.8f, 0.5f), Tol);
            // 落差ゼロ＝銃後も現実を知る＝幻滅なし
            Assert.AreEqual(0f, ReturneesContagionRules.ReturneeDisillusionment(0.8f, 0f), Tol);
            // トラウマゼロ＝持ち込む幻滅なし
            Assert.AreEqual(0f, ReturneesContagionRules.ReturneeDisillusionment(0f, 0.5f), Tol);
        }

        /// <summary>伝播速度＝幻滅した帰還兵が多いほど速い。</summary>
        [Test]
        public void ContagionRate_FasterWithMoreDisillusionedReturnees()
        {
            // 0.5×0.68×1.2=0.408
            Assert.AreEqual(0.408f, ReturneesContagionRules.ContagionRate(0.5f, 0.68f), Tol);
            // 帰還兵が居なければ伝播しない
            Assert.AreEqual(0f, ReturneesContagionRules.ContagionRate(0f, 0.68f), Tol);
            // より多くの帰還兵ほど速い
            Assert.Greater(
                ReturneesContagionRules.ContagionRate(0.8f, 0.68f),
                ReturneesContagionRules.ContagionRate(0.3f, 0.68f));
        }

        /// <summary>希望の侵食＝帰還兵の厭戦が後方の希望を時間で削る。</summary>
        [Test]
        public void HopeErosionTick_ErodesHopeOverTime()
        {
            // 0.6 − 0.6×0.5×0.5×1.0 = 0.45
            Assert.AreEqual(0.45f, ReturneesContagionRules.HopeErosionTick(0.6f, 0.5f, 1.0f), Tol);
            // 伝播なし＝希望は減らない
            Assert.AreEqual(0.6f, ReturneesContagionRules.HopeErosionTick(0.6f, 0f, 1.0f), Tol);
            // 下限0を割らない
            Assert.GreaterOrEqual(ReturneesContagionRules.HopeErosionTick(0.1f, 1f, 100f), 0f);
        }

        /// <summary>戦争支持の崩壊＝現実が幻想を破る。</summary>
        [Test]
        public void WarSupportDecay_CrumblesWithTestimony()
        {
            // 0.8 − 0.8×0.5×0.5×1.0 = 0.6
            Assert.AreEqual(0.6f, ReturneesContagionRules.WarSupportDecay(0.8f, 0.5f, 1.0f), Tol);
            // 伝播なし＝支持は崩れない
            Assert.AreEqual(0.8f, ReturneesContagionRules.WarSupportDecay(0.8f, 0f, 1.0f), Tol);
        }

        /// <summary>封殺の圧力＝当局が証言を封じようとする。</summary>
        [Test]
        public void SilencingPressure_RisesWithNarrativeAndTestimony()
        {
            // 0.9×0.7=0.63
            Assert.AreEqual(0.63f, ReturneesContagionRules.SilencingPressure(0.9f, 0.7f), Tol);
            // 証言が無ければ封じる動機は薄い
            Assert.AreEqual(0f, ReturneesContagionRules.SilencingPressure(0.9f, 0f), Tol);
        }

        /// <summary>地下の不満＝封じても厭戦は地下で広がる（完全には消えない）。</summary>
        [Test]
        public void UndergroundDiscontent_SpreadsDespiteSilencing()
        {
            // channel = 0.63×0.6 + (1−0.63) = 0.748 → 0.4×0.748 = 0.2992
            Assert.AreEqual(0.2992f, ReturneesContagionRules.UndergroundDiscontent(0.63f, 0.4f), Tol);
            // 封殺ゼロ＝表で広がる＝伝播そのもの
            Assert.AreEqual(0.4f, ReturneesContagionRules.UndergroundDiscontent(0f, 0.4f), Tol);
            // 完全封殺でも undergroundGain ぶん地下で残る（0.4×0.6=0.24）
            Assert.AreEqual(0.24f, ReturneesContagionRules.UndergroundDiscontent(1f, 0.4f), Tol);
        }

        /// <summary>帰還兵の証言の信用＝実体験ゆえプロパガンダより説得力がある。</summary>
        [Test]
        public void VeteranCredibility_BoostedByFirsthandAuthority()
        {
            // 0.6×(1+0.5×0.8)=0.84
            Assert.AreEqual(0.84f, ReturneesContagionRules.VeteranCredibility(0.6f, 0.5f), Tol);
            // 権威ゼロでも証言そのものは届く
            Assert.AreEqual(0.5f, ReturneesContagionRules.VeteranCredibility(0.5f, 0f), Tol);
            // 一次情報の重みで信用が底上げされる
            Assert.Greater(
                ReturneesContagionRules.VeteranCredibility(0.6f, 0.8f),
                ReturneesContagionRules.VeteranCredibility(0.6f, 0.1f));
        }

        /// <summary>厭戦の伝播判定＝閾値超えで戦意が崩れつつある。</summary>
        [Test]
        public void IsWarWearinessSpreading_TrueAboveThreshold()
        {
            // 既定閾値0.4
            Assert.IsTrue(ReturneesContagionRules.IsWarWearinessSpreading(0.408f));
            Assert.IsFalse(ReturneesContagionRules.IsWarWearinessSpreading(0.3f));
        }
    }
}
