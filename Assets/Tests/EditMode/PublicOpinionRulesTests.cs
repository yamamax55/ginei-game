using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 世論ダイナミクスと多数派専制（MILL-2 #1477・ミル『自由論』）の純ロジック検証。
    /// 既定 PublicOpinionParams（熟議重み0.4・沈黙化0.6・集団思考0.7・配当0.3・画一化率0.2）で期待値を固定。
    /// </summary>
    public class PublicOpinionRulesTests
    {
        const float Tol = 1e-4f;

        /// <summary>情報品質＝多様度×熟議の配合（0.8,0.5→0.64）。多様度ゼロは品質ゼロ。</summary>
        [Test]
        public void InformationQuality_多様度と熟議の配合()
        {
            Assert.AreEqual(0.64f, PublicOpinionRules.InformationQuality(0.8f, 0.5f), Tol);
            // 多様度ゼロなら熟議があっても材料が無く品質は出ない
            Assert.AreEqual(0f, PublicOpinionRules.InformationQuality(0f, 1f), Tol);
            // 多様度・熟議とも満点なら最高品質
            Assert.AreEqual(1f, PublicOpinionRules.InformationQuality(1f, 1f), Tol);
        }

        /// <summary>多数派の社会的専制＝シェア×同調圧力（0.8,0.75→0.6）。どちらか弱ければ薄い。</summary>
        [Test]
        public void MajorityPressure_シェアと同調圧力の積()
        {
            Assert.AreEqual(0.6f, PublicOpinionRules.MajorityPressure(0.8f, 0.75f), Tol);
            // 同調圧力ゼロ＝法でなく空気が無ければ専制は成立しない
            Assert.AreEqual(0f, PublicOpinionRules.MajorityPressure(1f, 0f), Tol);
        }

        /// <summary>少数派の沈黙＝圧力×(1−勇気)×沈黙化（0.6,0.25→0.27）。勇気が圧力に抗う。</summary>
        [Test]
        public void MinoritySilencing_圧力が異論を黙らせ勇気が抗う()
        {
            Assert.AreEqual(0.27f, PublicOpinionRules.MinoritySilencing(0.6f, 0.25f), Tol);
            // 勇気満点なら沈黙しない
            Assert.AreEqual(0f, PublicOpinionRules.MinoritySilencing(0.9f, 1f), Tol);
        }

        /// <summary>集団思考＝低多様×高同調の非線形（0.2,0.9→0.86112）。高多様は陥りにくい。</summary>
        [Test]
        public void Groupthink_画一化と同調圧力で判断を誤る()
        {
            Assert.AreEqual(0.86112f, PublicOpinionRules.Groupthink(0.2f, 0.9f), Tol);
            // 多様度が高ければ同調圧力が強くても異論が残り集団思考にならない
            Assert.Less(PublicOpinionRules.Groupthink(0.9f, 0.9f),
                        PublicOpinionRules.Groupthink(0.2f, 0.9f));
        }

        /// <summary>意見の収束＝同調圧力が多様度を画一化（0.8,0.5,1→0.7）。圧力ゼロは不変。</summary>
        [Test]
        public void OpinionConvergenceTick_同調圧力が多様度を食い潰す()
        {
            Assert.AreEqual(0.7f, PublicOpinionRules.OpinionConvergenceTick(0.8f, 0.5f, 1f), Tol);
            // 圧力ゼロなら多様度は変わらない
            Assert.AreEqual(0.8f, PublicOpinionRules.OpinionConvergenceTick(0.8f, 0f, 1f), Tol);
        }

        /// <summary>多様性の配当＝多様度ほど判断・適応力が上がる倍率（0.8→1.24・0→1.0）。</summary>
        [Test]
        public void DiversityDividend_多様性が判断力を底上げ()
        {
            Assert.AreEqual(1.24f, PublicOpinionRules.DiversityDividend(0.8f), Tol);
            Assert.AreEqual(1f, PublicOpinionRules.DiversityDividend(0f), Tol);
        }

        /// <summary>沈黙の螺旋＝少数派認識×孤立恐怖の積（0.6,0.5→0.3）。どちらか欠けば回らない。</summary>
        [Test]
        public void SpiralOfSilence_孤立を恐れ少数派が沈黙する()
        {
            Assert.AreEqual(0.3f, PublicOpinionRules.SpiralOfSilence(0.6f, 0.5f), Tol);
            // 孤立への恐れが無ければ螺旋は回らない
            Assert.AreEqual(0f, PublicOpinionRules.SpiralOfSilence(1f, 0f), Tol);
        }

        /// <summary>画一化判定＝多様度が閾値未満で多数派専制成立（0.2&lt;0.3=true／0.5≥0.3=false）。</summary>
        [Test]
        public void IsConformistMonoculture_画一化で多数派専制()
        {
            Assert.IsTrue(PublicOpinionRules.IsConformistMonoculture(0.2f, 0.3f));
            Assert.IsFalse(PublicOpinionRules.IsConformistMonoculture(0.5f, 0.3f));
        }
    }
}
