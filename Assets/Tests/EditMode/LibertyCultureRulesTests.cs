using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 自由文化（個性と生活の実験）の純ロジックの担保（MILL-5 #1487・ミル『自由論』第3章）。
    /// 個性の発露・生活の実験・革新の配当・適応力ボーナス・画一化の停滞・奇人への寛容・モノカルチャーの
    /// リスク・活力ある文化判定を既定 Params の具体値で固定する。
    /// </summary>
    public class LibertyCultureRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>個性の発露＝自由×(1−同調圧力)。同調圧力が満点なら自由があっても0。</summary>
        [Test]
        public void Individuality_自由と非同調の積()
        {
            // 自由0.8×(1−0.25)=0.6
            Assert.AreEqual(0.6f, LibertyCultureRules.Individuality(0.8f, 0.25f), Eps);
            // 同調圧力満点＝個性は出ない
            Assert.AreEqual(0f, LibertyCultureRules.Individuality(1f, 1f), Eps);
        }

        /// <summary>生活の実験＝個性×多様性。両方揃って初めて実験が生まれる。</summary>
        [Test]
        public void ExperimentsOfLiving_個性と多様性の積()
        {
            // 0.6×0.5=0.3
            Assert.AreEqual(0.3f, LibertyCultureRules.ExperimentsOfLiving(0.6f, 0.5f), Eps);
            // 多様性ゼロ＝実験は生まれない
            Assert.AreEqual(0f, LibertyCultureRules.ExperimentsOfLiving(0.9f, 0f), Eps);
        }

        /// <summary>革新の配当＝実験度で下駄0.7〜最大1.5を補間（社会の実験室＝研究への係数）。</summary>
        [Test]
        public void InnovationDividend_下駄から最大へ補間()
        {
            // 実験0＝下駄0.7
            Assert.AreEqual(0.7f, LibertyCultureRules.InnovationDividend(0f), Eps);
            // 実験満点＝最大1.5
            Assert.AreEqual(1.5f, LibertyCultureRules.InnovationDividend(1f), Eps);
            // 実験0.5＝Lerp(0.7,1.5,0.5)=1.1
            Assert.AreEqual(1.1f, LibertyCultureRules.InnovationDividend(0.5f), Eps);
        }

        /// <summary>適応力ボーナス＝多様性×最大0.4（選択肢の幅）。</summary>
        [Test]
        public void AdaptabilityBonus_多様性に比例()
        {
            // 0.4×0.5=0.2
            Assert.AreEqual(0.2f, LibertyCultureRules.AdaptabilityBonus(0.5f), Eps);
            // 多様性ゼロ＝ボーナスなし
            Assert.AreEqual(0f, LibertyCultureRules.AdaptabilityBonus(0f), Eps);
        }

        /// <summary>画一化の停滞＝停滞率0.05×同調圧力×dt。同調圧力ゼロなら停滞しない。</summary>
        [Test]
        public void ConformityStagnation_同調圧力で進む()
        {
            // 0.05×0.8×2=0.08
            Assert.AreEqual(0.08f, LibertyCultureRules.ConformityStagnation(0.8f, 2f), Eps);
            // 同調圧力ゼロ＝停滞なし
            Assert.AreEqual(0f, LibertyCultureRules.ConformityStagnation(0f, 5f), Eps);
        }

        /// <summary>奇人への寛容＝寛容度×最大0.3（天才と革新の余地）。寛容ゼロは0。</summary>
        [Test]
        public void EccentricityValue_寛容に比例()
        {
            // 0.3×1=0.3
            Assert.AreEqual(0.3f, LibertyCultureRules.EccentricityValue(1f), Eps);
            // 寛容ゼロ＝革新の芽なし
            Assert.AreEqual(0f, LibertyCultureRules.EccentricityValue(0f), Eps);
        }

        /// <summary>モノカルチャーのリスク＝多様性が閾値未満で不足ぶんを正規化。閾値以上は0。</summary>
        [Test]
        public void MonocultureRisk_多様性不足で立ち上がる()
        {
            // 多様性0.2＜閾値0.5＝(0.5−0.2)/0.5=0.6
            Assert.AreEqual(0.6f, LibertyCultureRules.MonocultureRisk(0.2f, 0.5f), Eps);
            // 多様性0＝完全な画一化＝最大1
            Assert.AreEqual(1f, LibertyCultureRules.MonocultureRisk(0f, 0.5f), Eps);
            // 多様性が閾値以上＝リスクなし
            Assert.AreEqual(0f, LibertyCultureRules.MonocultureRisk(0.8f, 0.5f), Eps);
        }

        /// <summary>活力ある文化＝個性と多様性がともに閾値超。どちらか一方でも未満なら停滞へ。</summary>
        [Test]
        public void IsVibrantCulture_個性と多様性の両立で活力()
        {
            // 両方が閾値0.5超＝活力ある文化
            Assert.IsTrue(LibertyCultureRules.IsVibrantCulture(0.7f, 0.6f, 0.5f));
            // 多様性が閾値以下＝活力なし
            Assert.IsFalse(LibertyCultureRules.IsVibrantCulture(0.9f, 0.4f, 0.5f));
            // 個性が閾値以下＝活力なし
            Assert.IsFalse(LibertyCultureRules.IsVibrantCulture(0.3f, 0.8f, 0.5f));
        }
    }
}
