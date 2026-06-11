using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>テュケー（運命・偶然）への制度的耐性の純ロジックのテスト（POLY-3 #1448・ポリュビオス）。</summary>
    public class TycheRulesTests
    {
        const float Tol = 0.0001f;

        /// <summary>制度の質が高いほど運命耐性が高い（堅固な制度はテュケーを吸収）。</summary>
        [Test]
        public void FortuneResilience_質が高いほど耐性が高い()
        {
            // 既定：基礎0.1＋質×0.8
            Assert.AreEqual(0.1f, TycheRules.FortuneResilience(0f), Tol);
            Assert.AreEqual(0.5f, TycheRules.FortuneResilience(0.5f), Tol);
            Assert.AreEqual(0.9f, TycheRules.FortuneResilience(1f), Tol);
        }

        /// <summary>制度耐性が災厄（負イベント）を和らげ、好運（正イベント）はそのまま活かす。</summary>
        [Test]
        public void EventEffectModulation_災厄は和らげ好運はそのまま()
        {
            float r = TycheRules.FortuneResilience(1f); // 0.9
            // 災厄：-1×(1-0.7×0.9)=-1×0.37=-0.37
            Assert.AreEqual(-0.37f, TycheRules.EventEffectModulation(-1f, r), Tol);
            // 好運はそのまま
            Assert.AreEqual(0.5f, TycheRules.EventEffectModulation(0.5f, r), Tol);
            // 脆弱（耐性0.1）は災厄をほとんど受け切る：-1×(1-0.7×0.1)=-0.93
            Assert.AreEqual(-0.93f, TycheRules.EventEffectModulation(-1f, 0.1f), Tol);
        }

        /// <summary>不運の衝撃を制度が吸収する量（脆い国は吸収できず崩れる）。</summary>
        [Test]
        public void MisfortuneAbsorption_質が高いほど多く吸収()
        {
            // 質1：shock1×0.7×0.9=0.63
            Assert.AreEqual(0.63f, TycheRules.MisfortuneAbsorption(1f, 1f), Tol);
            // 質0：shock1×0.7×0.1=0.07（ほとんど吸収できない）
            Assert.AreEqual(0.07f, TycheRules.MisfortuneAbsorption(1f, 0f), Tol);
        }

        /// <summary>脆弱な制度は一度の不運で崩壊するが、堅固な制度は崩れない。</summary>
        [Test]
        public void FragileStateCollapse_脆弱は崩れ堅固は耐える()
        {
            // 脆弱（質0・耐性0.1<閾値0.3）：吸収0.07・残存0.93・(0.93-0.1)/0.9≈0.9222
            Assert.AreEqual(0.92222f, TycheRules.FragileStateCollapse(0f, 1f), 0.001f);
            // 堅固（質1・耐性0.9≥閾値0.3）：崩壊しない
            Assert.AreEqual(0f, TycheRules.FragileStateCollapse(1f, 1f), Tol);
        }

        /// <summary>堅固な制度は逆境を糧にして強くなり、脆弱な制度は逆境で弱る。</summary>
        [Test]
        public void AdversityIntoStrength_堅固は強化脆弱は弱化()
        {
            // 質1・逆境1：(1-0.5)/0.5=1 → 0.3×1×1=+0.3（鍛えられる）
            Assert.AreEqual(0.3f, TycheRules.AdversityIntoStrength(1f, 1f), Tol);
            // 質0・逆境1：(0-0.5)/0.5=-1 → -0.3（蝕まれる）
            Assert.AreEqual(-0.3f, TycheRules.AdversityIntoStrength(0f, 1f), Tol);
            // 分岐点（質0.5）：効果ゼロ
            Assert.AreEqual(0f, TycheRules.AdversityIntoStrength(0.5f, 1f), Tol);
        }

        /// <summary>時代の激動度が運命の振れ幅を決める。</summary>
        [Test]
        public void TycheVolatility_激動ほど振れが大きい()
        {
            // 穏やか：基準0.15
            Assert.AreEqual(0.15f, TycheRules.TycheVolatility(0f), Tol);
            // 激動：0.15＋0.7=0.85
            Assert.AreEqual(0.85f, TycheRules.TycheVolatility(1f), Tol);
        }

        /// <summary>制度の質が高いほど打撃から速く立ち直る。</summary>
        [Test]
        public void RecoverySpeed_質が高いほど速く回復()
        {
            // 質0.8・dt1：0.5×0.8×1=0.4
            Assert.AreEqual(0.4f, TycheRules.RecoverySpeed(0.8f, 1f), Tol);
            // 質低い方が遅い
            Assert.Less(TycheRules.RecoverySpeed(0.2f, 1f), TycheRules.RecoverySpeed(0.8f, 1f));
        }

        /// <summary>運命に強い堅固な国家の判定（耐性が閾値以上）。</summary>
        [Test]
        public void IsResilientToFortune_堅固な国家の判定()
        {
            // 既定閾値0.6
            Assert.IsTrue(TycheRules.IsResilientToFortune(TycheRules.FortuneResilience(1f)));   // 0.9≥0.6
            Assert.IsFalse(TycheRules.IsResilientToFortune(TycheRules.FortuneResilience(0f)));  // 0.1<0.6
        }
    }
}
