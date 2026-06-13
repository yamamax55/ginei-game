using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>多数者の専制（MajorityTyrannyRules・TOCQ-1 #1478）の純ロジックを担保する。</summary>
    public class MajorityTyrannyRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>多数者の権力＝シェア×（1−制度的歯止め）＝歯止めなき多数派は全能になる。</summary>
        [Test]
        public void MajorityPower_歯止めが権力を抑える()
        {
            // 歯止めゼロ＝シェアそのまま全能
            Assert.AreEqual(0.8f, MajorityTyrannyRules.MajorityPower(0.8f, 0f), Eps);
            // 歯止め0.5＝半減
            Assert.AreEqual(0.4f, MajorityTyrannyRules.MajorityPower(0.8f, 0.5f), Eps);
            // 完全な歯止め＝権力ゼロ
            Assert.AreEqual(0f, MajorityTyrannyRules.MajorityPower(1f, 1f), Eps);
        }

        /// <summary>社会的同調圧力＝多数者の権力×同質性＝多様な社会は圧力が弱い。</summary>
        [Test]
        public void SocialConformityPressure_同質な社会ほど空気が縛る()
        {
            // 権力0.8×同質性0.5＝0.4
            Assert.AreEqual(0.4f, MajorityTyrannyRules.SocialConformityPressure(0.8f, 0.5f), Eps);
            // 同質性ゼロ（多様）＝圧力ゼロ
            Assert.AreEqual(0f, MajorityTyrannyRules.SocialConformityPressure(0.8f, 0f), Eps);
        }

        /// <summary>少数意見の封殺＝圧力×（1−シェア）＝少数派ほど孤立して呑まれる。</summary>
        [Test]
        public void MinoritySuppression_少数派ほど封殺されやすい()
        {
            // 圧力0.6×(1−0.1)＝0.54
            Assert.AreEqual(0.54f, MajorityTyrannyRules.MinoritySuppression(0.6f, 0.1f), Eps);
            // 大きな少数派（0.5）は呑まれにくい＝0.3
            Assert.AreEqual(0.3f, MajorityTyrannyRules.MinoritySuppression(0.6f, 0.5f), Eps);
        }

        /// <summary>道徳的全能＝多数者の権力×moralEmpireGain（既定0.8）＝多数派が正義を独占する。</summary>
        [Test]
        public void MoralEmpire_多数派が道徳を独占する()
        {
            // 権力0.5×0.8＝0.4
            Assert.AreEqual(0.4f, MajorityTyrannyRules.MoralEmpire(0.5f), Eps);
            Assert.AreEqual(0.8f, MajorityTyrannyRules.MoralEmpire(1f), Eps);
        }

        /// <summary>魂の幽閉＝社会的圧力が精神的に少数派を萎縮させる（imprisonmentRate 既定0.1）。</summary>
        [Test]
        public void SoulImprisonment_精神が時間で縛られていく()
        {
            // 0 + 0.1×0.8×1.0 ＝ 0.08
            Assert.AreEqual(0.08f, MajorityTyrannyRules.SoulImprisonment(0f, 0.8f, 1f), Eps);
            // 圧力ゼロなら進まない
            Assert.AreEqual(0.2f, MajorityTyrannyRules.SoulImprisonment(0.2f, 0f, 1f), Eps);
            // 単調増加（圧力下で幽閉は深まる）
            Assert.Greater(MajorityTyrannyRules.SoulImprisonment(0.2f, 0.8f, 1f), 0.2f);
        }

        /// <summary>制度的保護＝権利章典と司法独立の合成（judicialWeight 既定0.5）＝権利だけでは守れない。</summary>
        [Test]
        public void InstitutionalProtection_司法独立が権利を実効化する()
        {
            // rights1.0×(1−0.5) + 1.0×judicial1.0×0.5 ＝ 1.0
            Assert.AreEqual(1f, MajorityTyrannyRules.InstitutionalProtection(1f, 1f), Eps);
            // 権利はあるが司法独立ゼロ＝0.5（文面だけでは半分しか守れない）
            Assert.AreEqual(0.5f, MajorityTyrannyRules.InstitutionalProtection(1f, 0f), Eps);
            // 権利ゼロなら保護ゼロ
            Assert.AreEqual(0f, MajorityTyrannyRules.InstitutionalProtection(0f, 1f), Eps);
        }

        /// <summary>異論の萎縮＝社会的圧力×排斥の恐怖＝村八分を恐れて口をつぐむ。</summary>
        [Test]
        public void DissentChilling_排斥の恐怖が異論を萎縮させる()
        {
            // 0.6×0.5＝0.3
            Assert.AreEqual(0.3f, MajorityTyrannyRules.DissentChilling(0.6f, 0.5f), Eps);
            // 恐怖ゼロなら萎縮しない
            Assert.AreEqual(0f, MajorityTyrannyRules.DissentChilling(0.6f, 0f), Eps);
        }

        /// <summary>多数者の専制の判定＝圧力が高く制度的保護が弱いとき成立。</summary>
        [Test]
        public void IsMajorityTyranny_圧力高かつ保護弱で専制()
        {
            // 圧力0.8≥0.6 かつ 保護0.2<0.4 ＝ 専制
            Assert.IsTrue(MajorityTyrannyRules.IsMajorityTyranny(0.8f, 0.2f, 0.6f));
            // 制度的保護が強い（0.7≥0.4）＝専制でない
            Assert.IsFalse(MajorityTyrannyRules.IsMajorityTyranny(0.8f, 0.7f, 0.6f));
            // 圧力が低い（0.4<0.6）＝専制でない
            Assert.IsFalse(MajorityTyrannyRules.IsMajorityTyranny(0.4f, 0.2f, 0.6f));
        }

        /// <summary>MinorityOpinion はコンストラクタで全フィールドをクランプする。</summary>
        [Test]
        public void MinorityOpinion_コンストラクタでクランプ()
        {
            var op = new MinorityOpinion(1.5f, -0.2f, 2f);
            Assert.AreEqual(1f, op.share, Eps);
            Assert.AreEqual(0f, op.expressionFreedom, Eps);
            Assert.AreEqual(1f, op.socialPressure, Eps);
        }
    }
}
