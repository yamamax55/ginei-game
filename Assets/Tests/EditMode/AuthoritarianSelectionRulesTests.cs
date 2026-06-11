using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// AuthoritarianSelectionRules の EditMode テスト（HAYK-3 #1547・ハイエク「最悪の者が頂点に立つ」）。
    /// 従順な大衆の動員・否定的結束・非情の優位・逆淘汰の選別圧・指導層の質低下・良心が足枷・最悪が上判定を担保。
    /// </summary>
    public class AuthoritarianSelectionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>①従順で無批判な大衆ほど扇動者に動員されやすい（批判精神が高いと効かない）。</summary>
        [Test]
        public void DocileMassAppeal_従順無批判ほど高く批判精神で消える()
        {
            // 従順1×(1−批判精神0)×0.5 = 0.5
            Assert.AreEqual(0.5f, AuthoritarianSelectionRules.DocileMassAppeal(1f, 0f), Eps);
            // 批判精神1なら動員されない
            Assert.AreEqual(0f, AuthoritarianSelectionRules.DocileMassAppeal(1f, 1f), Eps);
            // 従順0なら動員されない
            Assert.AreEqual(0f, AuthoritarianSelectionRules.DocileMassAppeal(0f, 0f), Eps);
        }

        /// <summary>②共通の敵への憎悪で否定的に結束する（敵も恐怖も要る）。</summary>
        [Test]
        public void NegativeSolidarity_共通の敵と恐怖で結束する()
        {
            // 敵1×恐怖1×0.5 = 0.5
            Assert.AreEqual(0.5f, AuthoritarianSelectionRules.NegativeSolidarity(1f, 1f), Eps);
            // 敵がいなければ結束しない
            Assert.AreEqual(0f, AuthoritarianSelectionRules.NegativeSolidarity(0f, 1f), Eps);
            // 恐怖がなければ結束しない
            Assert.AreEqual(0f, AuthoritarianSelectionRules.NegativeSolidarity(1f, 0f), Eps);
        }

        /// <summary>③良心が薄く賭け金が高いほど非情な者が有利（良心1は優位なし）。</summary>
        [Test]
        public void RuthlessnessAdvantage_良心が薄いほど非情が出世する()
        {
            // (1−良心0)×賭け金1×0.6 = 0.6
            Assert.AreEqual(0.6f, AuthoritarianSelectionRules.RuthlessnessAdvantage(0f, 1f), Eps);
            // 良心1なら優位なし
            Assert.AreEqual(0f, AuthoritarianSelectionRules.RuthlessnessAdvantage(1f, 1f), Eps);
            // 賭け金が低ければ優位は小さい
            Assert.Less(AuthoritarianSelectionRules.RuthlessnessAdvantage(0f, 0.2f),
                AuthoritarianSelectionRules.RuthlessnessAdvantage(0f, 1f));
        }

        /// <summary>逆淘汰の選別圧＝三メカニズムの平均（最悪が上へ）。</summary>
        [Test]
        public void SelectionBias_三メカニズムの平均()
        {
            // (0.5+0.5+0.6)/3 = 0.5333...
            Assert.AreEqual(1.6f / 3f, AuthoritarianSelectionRules.SelectionBias(0.5f, 0.5f, 0.6f), Eps);
            // 全て0なら選別圧なし
            Assert.AreEqual(0f, AuthoritarianSelectionRules.SelectionBias(0f, 0f, 0f), Eps);
            // 全て1なら最大1
            Assert.AreEqual(1f, AuthoritarianSelectionRules.SelectionBias(1f, 1f, 1f), Eps);
        }

        /// <summary>選別圧が強いほど指導層の質は時間で下がる（良心ある者が脱落）。</summary>
        [Test]
        public void AdverseSelectionTick_選別圧で指導層の質が下がる()
        {
            // 質1.0・選別圧1.0・dt1 → 1 − 1×0.05×1 = 0.95
            Assert.AreEqual(0.95f, AuthoritarianSelectionRules.AdverseSelectionTick(1f, 1f, 1f), Eps);
            // 選別圧0なら劣化しない
            Assert.AreEqual(1f, AuthoritarianSelectionRules.AdverseSelectionTick(1f, 0f, 1f), Eps);
            // 強い選別圧の方が質を多く削る
            Assert.Less(AuthoritarianSelectionRules.AdverseSelectionTick(1f, 1f, 1f),
                AuthoritarianSelectionRules.AdverseSelectionTick(1f, 0.2f, 1f));
            // 下限0でクランプ
            Assert.AreEqual(0f, AuthoritarianSelectionRules.AdverseSelectionTick(0f, 1f, 100f), Eps);
        }

        /// <summary>全体主義度が高い体制ほど良心が出世の足枷になる（自由体制では足枷なし）。</summary>
        [Test]
        public void MoralityAsLiability_全体主義度で良心が足枷になる()
        {
            // 良心1×全体主義1 = 1
            Assert.AreEqual(1f, AuthoritarianSelectionRules.MoralityAsLiability(1f, 1f), Eps);
            // 自由体制（全体主義度0）では足枷にならない
            Assert.AreEqual(0f, AuthoritarianSelectionRules.MoralityAsLiability(1f, 0f), Eps);
            // 良心がなければ足枷もない
            Assert.AreEqual(0f, AuthoritarianSelectionRules.MoralityAsLiability(0f, 1f), Eps);
        }

        /// <summary>弾圧下で従順が連鎖して批判が消える（後退はしない）。</summary>
        [Test]
        public void ConformityCascade_弾圧で従順が連鎖する()
        {
            // 従順0.5・弾圧1 → 0.5 + 0.5×1 = 1
            Assert.AreEqual(1f, AuthoritarianSelectionRules.ConformityCascade(0.5f, 1f), Eps);
            // 弾圧0なら変わらない
            Assert.AreEqual(0.5f, AuthoritarianSelectionRules.ConformityCascade(0.5f, 0f), Eps);
            // 弾圧があれば必ず元以上（後退しない）
            Assert.GreaterOrEqual(AuthoritarianSelectionRules.ConformityCascade(0.3f, 0.5f), 0.3f);
        }

        /// <summary>非情さが閾値を超えたら最悪が頂点に立った判定。</summary>
        [Test]
        public void IsWorstOnTop_閾値超で最悪が上判定()
        {
            Assert.IsTrue(AuthoritarianSelectionRules.IsWorstOnTop(0.9f, 0.7f));
            Assert.IsFalse(AuthoritarianSelectionRules.IsWorstOnTop(0.5f, 0.7f));
            // 閾値ちょうどは成立
            Assert.IsTrue(AuthoritarianSelectionRules.IsWorstOnTop(0.7f, 0.7f));
        }
    }
}
