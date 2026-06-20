using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 教義→実効係数の写像（#172-175・橋渡し層）の純ロジックを既定 Params で固定。
    /// 特性→倍率の単調性・境界、核（ReligionRules）への委譲（倍率は核の結果に乗る）・clamp・決定論を担保。
    /// </summary>
    public class ReligionDoctrineRulesTests
    {
        const float Tol = 1e-4f;
        static readonly ReligionDoctrineParams DP = ReligionDoctrineParams.Default;
        static readonly ReligionParams RP = ReligionParams.Default;

        static ReligionDef Def(float prose, float milit, float cohesion, float tol)
            => new ReligionDef("t", prose, milit, cohesion, tol, "", ReligionFamily.その他);

        [Test]
        public void ConversionFactor_布教熱に単調増加()
        {
            float lo = ReligionDoctrineRules.ConversionFactor(Def(0f, 0f, 0f, 0f), DP);
            float hi = ReligionDoctrineRules.ConversionFactor(Def(1f, 0f, 0f, 0f), DP);
            Assert.AreEqual(DP.conversionMin, lo, Tol);
            Assert.AreEqual(DP.conversionMax, hi, Tol);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void HolyWarFactor_戦闘性に単調増加()
        {
            float lo = ReligionDoctrineRules.HolyWarFactor(Def(0f, 0f, 0f, 0f), DP);
            float hi = ReligionDoctrineRules.HolyWarFactor(Def(0f, 1f, 0f, 0f), DP);
            Assert.AreEqual(DP.holyWarMin, lo, Tol);
            Assert.AreEqual(DP.holyWarMax, hi, Tol);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void CohesionFactor_結束に単調増加()
        {
            float lo = ReligionDoctrineRules.CohesionFactor(Def(0f, 0f, 0f, 0f), DP);
            float hi = ReligionDoctrineRules.CohesionFactor(Def(0f, 0f, 1f, 0f), DP);
            Assert.AreEqual(DP.cohesionMin, lo, Tol);
            Assert.AreEqual(DP.cohesionMax, hi, Tol);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void DoctrinalConversion_布教熱の高い宗教ほど圧力が高い()
        {
            ReligionDef zealot = Def(1f, 0f, 0f, 0f);
            ReligionDef ethnic = Def(0f, 0f, 0f, 0f);
            float pZ = ReligionDoctrineRules.DoctrinalConversionPressure(0.2f, 0.9f, false, zealot, RP, DP);
            float pE = ReligionDoctrineRules.DoctrinalConversionPressure(0.2f, 0.9f, false, ethnic, RP, DP);
            Assert.Greater(pZ, pE);
        }

        [Test]
        public void DoctrinalConversion_核と整合し0to1にClamp()
        {
            // 核の圧力に布教熱(=conversionMax)を掛けると 1 を超えうる→clamp で 1 に収まる。
            float p = ReligionDoctrineRules.DoctrinalConversionPressure(0f, 1f, true, Def(1f, 0f, 0f, 0f), RP, DP);
            Assert.LessOrEqual(p, 1f + Tol);
            Assert.GreaterOrEqual(p, 0f);
        }

        [Test]
        public void DoctrinalConversion_住民の方が信仰強ければ核に従い圧力ゼロ()
        {
            // 核 ConversionPressure は diff<=0 で 0 を返す＝倍率を掛けても 0（核へ委譲）。
            float p = ReligionDoctrineRules.DoctrinalConversionPressure(0.9f, 0.2f, false, Def(1f, 0f, 0f, 0f), RP, DP);
            Assert.AreEqual(0f, p, Tol);
        }

        [Test]
        public void DoctrinalHolyWar_双方好戦的なほど高い()
        {
            ReligionDef war = Def(0f, 1f, 0f, 0f);
            ReligionDef peace = Def(0f, 0f, 0f, 0f);
            float bothWar = ReligionDoctrineRules.DoctrinalHolyWarPressure(0.8f, 0.8f, false, war, war, RP, DP);
            float onePeace = ReligionDoctrineRules.DoctrinalHolyWarPressure(0.8f, 0.8f, false, war, peace, RP, DP);
            Assert.Greater(bothWar, onePeace);
        }

        [Test]
        public void DoctrinalHolyWar_聖地係争で上がり0to1にClamp()
        {
            ReligionDef war = Def(0f, 1f, 0f, 0f);
            float quiet = ReligionDoctrineRules.DoctrinalHolyWarPressure(0.7f, 0.7f, false, war, war, RP, DP);
            float contested = ReligionDoctrineRules.DoctrinalHolyWarPressure(0.7f, 0.7f, true, war, war, RP, DP);
            Assert.Greater(contested, quiet);
            Assert.LessOrEqual(contested, 1f + Tol);
        }

        [Test]
        public void DoctrinalSocial_結束の強い宗教ほど効果が高い()
        {
            var r = new Religion("t", 0.8f);
            float strong = ReligionDoctrineRules.DoctrinalSocialEffect(r, Def(0f, 0f, 1f, 0f), RP, DP);
            float weak = ReligionDoctrineRules.DoctrinalSocialEffect(r, Def(0f, 0f, 0f, 0f), RP, DP);
            Assert.Greater(strong, weak);
        }

        [Test]
        public void DoctrinalSocial_Null宗教でも安全に正の値()
        {
            float v = ReligionDoctrineRules.DoctrinalSocialEffect(null, Def(0f, 0f, 0.5f, 0f), RP, DP);
            Assert.Greater(v, 0f);
        }

        [Test]
        public void 決定論_同入力は同出力()
        {
            ReligionDef d = ReligionCatalog.For("イスラム教");
            float a = ReligionDoctrineRules.DoctrinalConversionPressure(0.3f, 0.8f, true, d, RP, DP);
            float b = ReligionDoctrineRules.DoctrinalConversionPressure(0.3f, 0.8f, true, d, RP, DP);
            Assert.AreEqual(a, b, Tol);
        }
    }
}
