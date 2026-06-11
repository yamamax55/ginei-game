using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>複数性と公的領域（PluralityRules・BNAL-2 #1532）の純ロジックを既定Paramsで担保。</summary>
    public class PluralityRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>公的領域の活力＝視点の多様性×自由な集会（両方が要る・積）。</summary>
        [Test]
        public void PublicRealmVitality_視点の多様性と自由な集会の積()
        {
            // 0.8 × 0.5 = 0.4
            Assert.AreEqual(0.4f, PluralityRules.PublicRealmVitality(0.8f, 0.5f), Eps);
            // 集会が無ければ活力ゼロ＝複数性があっても政治空間は立たない
            Assert.AreEqual(0f, PluralityRules.PublicRealmVitality(0.9f, 0f), Eps);
        }

        /// <summary>共に行動する力＝多様性が高く原子化が低いほど大きい。孤立は行動を奪う。</summary>
        [Test]
        public void ActionCapacity_原子化が行動する力を奪う()
        {
            // 0.8 × (1−0.25) = 0.6
            Assert.AreEqual(0.6f, PluralityRules.ActionCapacity(0.8f, 0.25f), Eps);
            // 完全な原子化では多様性があっても行動できない
            Assert.AreEqual(0f, PluralityRules.ActionCapacity(0.9f, 1f), Eps);
        }

        /// <summary>原子化tick＝孤立と恐怖が原子化を進める（全体主義の手口）。</summary>
        [Test]
        public void AtomizationTick_孤立と恐怖が原子化を進める()
        {
            // 0.2 + 0.06×((0.8+0.6)/2)×2 = 0.2 + 0.06×0.7×2 = 0.284
            float r = PluralityRules.AtomizationTick(0.2f, 0.8f, 0.6f, 2f);
            Assert.AreEqual(0.284f, r, Eps);
            // 孤立も恐怖も無ければ原子化は進まない
            Assert.AreEqual(0.2f, PluralityRules.AtomizationTick(0.2f, 0f, 0f, 2f), Eps);
        }

        /// <summary>複数性の侵食＝画一化圧力が視点の多様性を削る。</summary>
        [Test]
        public void PluralityErosion_画一化圧力が多様性を削る()
        {
            // 0.7 − 0.05×0.8×2 = 0.7 − 0.08 = 0.62
            float r = PluralityRules.PluralityErosion(0.7f, 0.8f, 2f);
            Assert.AreEqual(0.62f, r, Eps);
            // 圧力が無ければ多様性は保たれる
            Assert.AreEqual(0.7f, PluralityRules.PluralityErosion(0.7f, 0f, 5f), Eps);
        }

        /// <summary>全体主義判定＝原子化が進み複数性が失われた状態。</summary>
        [Test]
        public void IsTotalitarian_原子化と複数性喪失の両立で成立()
        {
            // threshold 0.6：原子化0.7(≥0.6) かつ 多様性0.3(<0.4) → true
            Assert.IsTrue(PluralityRules.IsTotalitarian(0.7f, 0.3f, 0.6f));
            // 多様性が残っていれば全体主義ではない
            Assert.IsFalse(PluralityRules.IsTotalitarian(0.7f, 0.5f, 0.6f));
            // 原子化が浅ければ全体主義ではない
            Assert.IsFalse(PluralityRules.IsTotalitarian(0.4f, 0.2f, 0.6f));
        }

        /// <summary>自発的結社＝共に行動する力×共通の関心（市民社会・原子化の逆）。</summary>
        [Test]
        public void SpontaneousAssociation_行動する力と共通の関心の積()
        {
            // 0.6 × 0.5 = 0.3
            Assert.AreEqual(0.3f, PluralityRules.SpontaneousAssociation(0.6f, 0.5f), Eps);
            // 関心が無ければ結社は生まれない
            Assert.AreEqual(0f, PluralityRules.SpontaneousAssociation(0.9f, 0f), Eps);
        }

        /// <summary>共にある権力＝共に行動する力から（暴力でない）権力が生まれる（アーレントの権力観）。</summary>
        [Test]
        public void PowerFromTogetherness_行動する力が権力を生む()
        {
            // 0.8 × 0.9 = 0.72
            Assert.AreEqual(0.72f, PluralityRules.PowerFromTogetherness(0.8f), Eps);
            // 孤立した原子（行動する力ゼロ）からは権力は生まれない
            Assert.AreEqual(0f, PluralityRules.PowerFromTogetherness(0f), Eps);
        }

        /// <summary>孤独の脆弱性＝孤独と無意味感が全体主義イデオロギーへの脆弱性を生む。</summary>
        [Test]
        public void LonelinessVulnerability_孤独と無意味感が脆弱性を生む()
        {
            // ((0.8+0.6)/2)×0.8 = 0.7×0.8 = 0.56
            Assert.AreEqual(0.56f, PluralityRules.LonelinessVulnerability(0.8f, 0.6f), Eps);
            // 孤独も無意味感も無ければ脆弱性は無い
            Assert.AreEqual(0f, PluralityRules.LonelinessVulnerability(0f, 0f), Eps);
        }

        /// <summary>入力クランプ＝範囲外の入力でも0..1に収まる。</summary>
        [Test]
        public void 入力クランプ_範囲外でも安全()
        {
            Assert.AreEqual(1f, PluralityRules.PublicRealmVitality(5f, 5f), Eps);
            Assert.AreEqual(1f, PluralityRules.AtomizationTick(2f, 2f, 2f, 10f), Eps);
            Assert.AreEqual(0f, PluralityRules.PluralityErosion(-1f, 1f, 1f), Eps);
            Assert.AreEqual(0f, PluralityRules.ActionCapacity(-1f, -1f), Eps);
        }
    }
}
