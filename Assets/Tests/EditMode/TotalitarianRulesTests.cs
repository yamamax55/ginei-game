using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 全体主義の動態（BNAL-3 #1535・アーレント型）の純ロジックを担保。既定 Params の具体値で期待値固定。
    /// </summary>
    public class TotalitarianRulesTests
    {
        /// <summary>予測不能な弾圧が恐怖を深める：0.2 + 0.3×1×1×1 = 0.5。</summary>
        [Test]
        public void TerrorTick_予測不能な弾圧が恐怖を深める()
        {
            float t = TotalitarianRules.TerrorTick(0.2f, 1f, 1f, 1f);
            Assert.AreEqual(0.5f, t, 1e-4f);
        }

        /// <summary>予測可能（unpredictability=0）な暴力は恐怖を深めない＝据え置き。</summary>
        [Test]
        public void TerrorTick_予測可能な暴力は恐怖を深めない()
        {
            float t = TotalitarianRules.TerrorTick(0.2f, 1f, 0f, 1f);
            Assert.AreEqual(0.2f, t, 1e-4f);
        }

        /// <summary>恐怖ループ利得（核）：0.8×0.6×0.5 = 0.24。どちらか0なら回らない。</summary>
        [Test]
        public void TerrorLoopGain_恐怖と原子化の積で自己増幅し片方ゼロなら回らない()
        {
            Assert.AreEqual(0.24f, TotalitarianRules.TerrorLoopGain(0.8f, 0.6f), 1e-4f);
            Assert.AreEqual(0f, TotalitarianRules.TerrorLoopGain(0.8f, 0f), 1e-4f);
            Assert.AreEqual(0f, TotalitarianRules.TerrorLoopGain(0f, 0.6f), 1e-4f);
        }

        /// <summary>イデオロギー代替：掌握1.0なら矛盾0.5でも虚構が完全に上書き＝1.0。掌握0.5は0.5。</summary>
        [Test]
        public void IdeologySubstitution_掌握が強いほど現実の矛盾を呑み込む()
        {
            Assert.AreEqual(1f, TotalitarianRules.IdeologySubstitution(1f, 0.5f), 1e-4f);
            Assert.AreEqual(0.5f, TotalitarianRules.IdeologySubstitution(0.5f, 1f), 1e-4f);
            Assert.AreEqual(0.25f, TotalitarianRules.IdeologySubstitution(0.5f, 0f), 1e-4f);
        }

        /// <summary>原子化：恐怖と監視の最大効果で孤立が進む＝0.3 + 0.2×max(0.8,0.4)×1 = 0.46。</summary>
        [Test]
        public void AtomizationTick_恐怖と監視が人々を孤立させる()
        {
            float a = TotalitarianRules.AtomizationTick(0.3f, 0.8f, 0.4f, 1f);
            Assert.AreEqual(0.46f, a, 1e-4f);
        }

        /// <summary>全体的支配＝3要素の積。0.5×0.5×0.5 = 0.125。どれか0なら成立しない。</summary>
        [Test]
        public void TotalControl_3要素が揃って初めて成立する()
        {
            Assert.AreEqual(0.125f, TotalitarianRules.TotalControl(0.5f, 0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, TotalitarianRules.TotalControl(0f, 1f, 1f), 1e-4f);

            var tp = new TotalitarianPressure(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(0.125f, TotalitarianRules.TotalControl(tp), 1e-4f);
        }

        /// <summary>抵抗の不可能：原子化と恐怖が相乗で抵抗を奪う＝0.6+0.6−0.36 = 0.84。</summary>
        [Test]
        public void ResistanceImpossibility_原子化と恐怖が抵抗をほぼ不可能にする()
        {
            float r = TotalitarianRules.ResistanceImpossibility(0.6f, 0.6f);
            Assert.AreEqual(0.84f, r, 1e-4f);
            // 両者ゼロなら抵抗は妨げられない。
            Assert.AreEqual(0f, TotalitarianRules.ResistanceImpossibility(0f, 0f), 1e-4f);
        }

        /// <summary>イデオロギーの暴走：内的論理で自乗的に加速＝0.8 + 0.1×0.8² ×1 = 0.864。</summary>
        [Test]
        public void IdeologicalMomentum_内的論理で現実から乖離しても止まらない()
        {
            float g = TotalitarianRules.IdeologicalMomentum(0.8f, 1f);
            Assert.AreEqual(0.864f, g, 1e-4f);
        }

        /// <summary>全体主義固定判定：全体的支配が閾値以上で固まる。</summary>
        [Test]
        public void IsTotalitarianConsolidated_閾値で固まりを判定する()
        {
            Assert.IsTrue(TotalitarianRules.IsTotalitarianConsolidated(0.7f, 0.6f));
            Assert.IsFalse(TotalitarianRules.IsTotalitarianConsolidated(0.5f, 0.6f));
        }
    }
}
