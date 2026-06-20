using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政体システム（#政体）の純ロジック検証＝<see cref="GovernanceFormCatalog"/>（7政体の差別化特性カタログ）／
    /// <see cref="GovernanceFormEffectRules"/>（特性→既存窓口の実効係数）／<see cref="GovernanceFormTransitionRules"/>
    /// （政治形態の移り変わり＝<see cref="AnacyclosisRules"/>/<see cref="PoliticalUpheavalRules"/> へ委譲する上位層）。
    /// カタログの引き/フォールバック/null安全、特性の単調性と範囲、もっともらしい遷移、決定論を担保する。
    /// </summary>
    public class GovernanceFormSystemTests
    {
        private const float Eps = 1e-4f;

        // ---- カタログ：引き / 範囲 / フォールバック / null安全 ----

        [Test]
        public void Profile_全特性が0to1範囲に収まる()
        {
            foreach (var a in GovernanceFormCatalog.All)
            {
                var p = GovernanceFormCatalog.Profile(a);
                AssertUnit(p.legitimacySource);
                AssertUnit(p.decisionSpeed);
                AssertUnit(p.corruptionResistance);
                AssertUnit(p.successionStability);
                AssertUnit(p.economicCentralization);
                AssertUnit(p.repressionCapacity);
                AssertUnit(p.popularConsentDependence);
            }
        }

        [Test]
        public void Profile_形態ごとに差別化されている()
        {
            // 民主主義は同意依存が最高・独裁政治は決定速度と抑圧が最高・共産主義は経済集権が最高。
            var dem = GovernanceFormCatalog.Profile(GovernanceArchetype.民主主義);
            var dic = GovernanceFormCatalog.Profile(GovernanceArchetype.独裁政治);
            var com = GovernanceFormCatalog.Profile(GovernanceArchetype.共産主義);

            Assert.Greater(dem.popularConsentDependence, dic.popularConsentDependence);
            Assert.Greater(dic.decisionSpeed, dem.decisionSpeed);
            Assert.Greater(dic.repressionCapacity, dem.repressionCapacity);
            Assert.Greater(com.economicCentralization, dem.economicCentralization);
        }

        [Test]
        public void Profile_既存GovernmentForm経由でも引ける()
        {
            // 共和制(enum) 経由＝共和制アーキタイプの特性と一致。
            var viaForm = GovernanceFormCatalog.Profile(GovernmentForm.共和制);
            var viaArch = GovernanceFormCatalog.Profile(GovernanceArchetype.共和制);
            Assert.AreEqual(viaArch.popularConsentDependence, viaForm.popularConsentDependence, Eps);
        }

        [Test]
        public void TryParse_既知名は成功し未知名はフォールバックする()
        {
            Assert.IsTrue(GovernanceFormCatalog.TryParse("寡頭政治", out var a));
            Assert.AreEqual(GovernanceArchetype.寡頭政治, a);

            Assert.IsFalse(GovernanceFormCatalog.TryParse("存在しない政体", out _));
            Assert.AreEqual(GovernanceArchetype.君主制,
                GovernanceFormCatalog.ParseOrDefault("存在しない政体", GovernanceArchetype.君主制));
        }

        [Test]
        public void TryParse_nullと空白に頑健()
        {
            Assert.IsFalse(GovernanceFormCatalog.TryParse(null, out _));
            Assert.IsFalse(GovernanceFormCatalog.TryParse("", out _));
            Assert.IsTrue(GovernanceFormCatalog.TryParse("  民主主義  ", out var a));
            Assert.AreEqual(GovernanceArchetype.民主主義, a);
        }

        [Test]
        public void EnumマッピングはToとFromで往復整合する()
        {
            // enum に存在する形態は往復で保たれる。
            foreach (var f in new[] { GovernmentForm.君主制, GovernmentForm.立憲君主制,
                                      GovernmentForm.共和制, GovernmentForm.共産主義, GovernmentForm.指導者独裁 })
            {
                var arch = GovernanceFormCatalog.FromGovernmentForm(f);
                Assert.AreEqual(f, GovernanceFormCatalog.ToGovernmentForm(arch),
                    $"{f} の往復が壊れている");
            }
        }

        [Test]
        public void 民主独裁の分類が一貫する()
        {
            Assert.IsTrue(GovernanceFormCatalog.IsDemocratic(GovernanceArchetype.民主主義));
            Assert.IsTrue(GovernanceFormCatalog.IsDemocratic(GovernanceArchetype.共和制));
            Assert.IsTrue(GovernanceFormCatalog.IsDemocratic(GovernanceArchetype.立憲君主制));
            Assert.IsTrue(GovernanceFormCatalog.IsAutocratic(GovernanceArchetype.独裁政治));
            Assert.IsTrue(GovernanceFormCatalog.IsAutocratic(GovernanceArchetype.寡頭政治));
            Assert.IsTrue(GovernanceFormCatalog.IsAutocratic(GovernanceArchetype.共産主義));
        }

        // ---- 効果ルール：単調性・範囲・既存窓口への委譲 ----

        [Test]
        public void StabilityFactor_質が高い形態ほど大きい()
        {
            // 立憲君主制(高い制度の質) > 独裁政治(低い質)。
            float constMon = GovernanceFormEffectRules.StabilityFactor(GovernanceArchetype.立憲君主制);
            float dic = GovernanceFormEffectRules.StabilityFactor(GovernanceArchetype.独裁政治);
            Assert.Greater(constMon, dic);
            // 中立倍率1.0 を跨ぐ範囲（0.6..1.4）。
            Assert.GreaterOrEqual(constMon, 0.6f);
            Assert.LessOrEqual(constMon, 1.4f);
        }

        [Test]
        public void ConsentFragility_同意依存が高く正統性が低い形態ほど脆い()
        {
            // 民主主義(同意依存最高) は独裁政治(同意非依存) より合意崩壊に弱い側へ。
            float dem = GovernanceFormEffectRules.ConsentFragility(GovernanceArchetype.民主主義);
            float dic = GovernanceFormEffectRules.ConsentFragility(GovernanceArchetype.独裁政治);
            Assert.Greater(dem, dic);
            AssertUnit(dem);
            AssertUnit(dic);
        }

        [Test]
        public void DecisionAgility_独裁は速く民主は遅い()
        {
            float dic = GovernanceFormEffectRules.DecisionAgilityFactor(GovernanceArchetype.独裁政治);
            float dem = GovernanceFormEffectRules.DecisionAgilityFactor(GovernanceArchetype.民主主義);
            Assert.Greater(dic, 1f); // 即断
            Assert.Less(dem, 1f);    // 合議で遅い
        }

        [Test]
        public void ControlTypeOf_既存軍政窓口へ委譲する()
        {
            // 共産主義＝党軍・立憲君主制/共和制＝文民統制（既存 GovernmentFormRules と一致）。
            Assert.AreEqual(CivilianControlType.党軍,
                GovernanceFormEffectRules.ControlTypeOf(GovernanceArchetype.共産主義));
            Assert.AreEqual(CivilianControlType.文民統制,
                GovernanceFormEffectRules.ControlTypeOf(GovernanceArchetype.共和制));
        }

        [Test]
        public void EconomicCentralization_共産が最高()
        {
            float com = GovernanceFormEffectRules.EconomicCentralization(GovernanceArchetype.共産主義);
            foreach (var a in GovernanceFormCatalog.All)
                Assert.LessOrEqual(GovernanceFormEffectRules.EconomicCentralization(a), com + Eps);
        }

        [Test]
        public void CorruptionSusceptibility_腐敗耐性の裏返しで範囲内()
        {
            foreach (var a in GovernanceFormCatalog.All)
            {
                float s = GovernanceFormEffectRules.CorruptionSusceptibility(a);
                float expected = 1f - GovernanceFormCatalog.Profile(a).corruptionResistance;
                Assert.AreEqual(expected, s, Eps);
                AssertUnit(s);
            }
        }

        [Test]
        public void MilitaryControlStrength_制度の質が高いほど高く範囲内()
        {
            float lib = GovernanceFormEffectRules.MilitaryControlStrength(GovernanceArchetype.立憲君主制);
            float dic = GovernanceFormEffectRules.MilitaryControlStrength(GovernanceArchetype.独裁政治);
            Assert.Greater(lib, dic);
            AssertUnit(lib);
            AssertUnit(dic);
        }

        // ---- 遷移オーケストレータ：循環委譲・危機遷移・決定論 ----

        [Test]
        public void ToFromRegimeForm_アナキュクローシス分類へ橋渡しする()
        {
            // 民主主義/共和制＝民主政・独裁政治＝僭主政・寡頭政治＝寡頭政。
            Assert.AreEqual(RegimeForm.民主政,
                GovernanceFormTransitionRules.ToRegimeForm(GovernanceArchetype.民主主義));
            Assert.AreEqual(RegimeForm.僭主政,
                GovernanceFormTransitionRules.ToRegimeForm(GovernanceArchetype.独裁政治));
            Assert.AreEqual(RegimeForm.寡頭政,
                GovernanceFormTransitionRules.ToRegimeForm(GovernanceArchetype.寡頭政治));
        }

        [Test]
        public void Recommend_圧力が低ければ現状維持()
        {
            var calm = new GovernanceFormPressures(0.05f, 0.05f, 0f, 0f, 0.95f);
            var t = GovernanceFormTransitionRules.Recommend(GovernanceArchetype.共和制, calm);
            Assert.AreEqual(GovernanceArchetype.共和制, t.to);
            Assert.Less(t.propensity, GovernanceFormTransitionRules.DefaultTransitionThreshold);
        }

        [Test]
        public void Recommend_君主制の高不安定は別形態への圧を生む()
        {
            // 君主制＋後継危機＋不安定＝遷移が起きる（循環の核は AnacyclosisRules へ委譲）。
            var crisis = new GovernanceFormPressures(0.9f, 0.7f, 0.1f, 0.9f, 0.4f);
            var t = GovernanceFormTransitionRules.Recommend(GovernanceArchetype.君主制, crisis);
            Assert.AreNotEqual(GovernanceArchetype.君主制, t.to);
            Assert.GreaterOrEqual(t.propensity, GovernanceFormTransitionRules.DefaultTransitionThreshold);
        }

        [Test]
        public void Recommend_収奪と同意崩壊は共産主義革命へ飛ぶ()
        {
            // 高格差＋同意ほぼ消失＋非戦時＝収奪への革命＝共産主義（PoliticalUpheaval 系の危機遷移）。
            var revolt = new GovernanceFormPressures(0.8f, 0.9f, 0.1f, 0.3f, 0.15f);
            var t = GovernanceFormTransitionRules.Recommend(GovernanceArchetype.寡頭政治, revolt);
            Assert.AreEqual(GovernanceArchetype.共産主義, t.to);
            Assert.IsFalse(t.cyclical); // 危機遷移＝非循環
        }

        [Test]
        public void Recommend_戦時の権力集中は独裁政治へ飛ぶ()
        {
            var wartime = new GovernanceFormPressures(0.7f, 0.6f, 0.85f, 0.3f, 0.2f);
            var t = GovernanceFormTransitionRules.Recommend(GovernanceArchetype.共和制, wartime);
            Assert.AreEqual(GovernanceArchetype.独裁政治, t.to);
            Assert.IsFalse(t.cyclical);
        }

        [Test]
        public void TransitionPropensity_範囲内かつ同意喪失で上がる()
        {
            var low = new GovernanceFormPressures(0.1f, 0.1f, 0f, 0f, 0.9f);
            var high = new GovernanceFormPressures(0.1f, 0.1f, 0f, 0f, 0.1f);
            float pLow = GovernanceFormTransitionRules.TransitionPropensity(GovernanceArchetype.民主主義, low);
            float pHigh = GovernanceFormTransitionRules.TransitionPropensity(GovernanceArchetype.民主主義, high);
            Assert.Greater(pHigh, pLow); // 同意依存の高い民主主義は同意喪失で傾向が上がる
            AssertUnit(pLow);
            AssertUnit(pHigh);
        }

        [Test]
        public void Recommend_決定論である()
        {
            var pr = new GovernanceFormPressures(0.6f, 0.7f, 0.2f, 0.5f, 0.3f);
            var a = GovernanceFormTransitionRules.Recommend(GovernanceArchetype.独裁政治, pr);
            var b = GovernanceFormTransitionRules.Recommend(GovernanceArchetype.独裁政治, pr);
            Assert.AreEqual(a.to, b.to);
            Assert.AreEqual(a.propensity, b.propensity, Eps);
            Assert.AreEqual(a.cyclical, b.cyclical);
        }

        [Test]
        public void WouldTransition_閾値で切り替わる()
        {
            var crisis = new GovernanceFormPressures(0.9f, 0.8f, 0.1f, 0.9f, 0.2f);
            Assert.IsTrue(GovernanceFormTransitionRules.WouldTransition(GovernanceArchetype.君主制, crisis));
            // 閾値を1.0に上げれば（事実上）遷移しない。
            Assert.IsFalse(GovernanceFormTransitionRules.WouldTransition(GovernanceArchetype.君主制, crisis, 1.01f));
        }

        [Test]
        public void Pressures入力はクランプされる()
        {
            var pr = new GovernanceFormPressures(2f, -1f, 5f, -0.5f, 9f);
            AssertUnit(pr.instability);
            AssertUnit(pr.inequality);
            AssertUnit(pr.war);
            AssertUnit(pr.successionCrisis);
            AssertUnit(pr.popularConsent);
            Assert.AreEqual(1f, pr.instability, Eps);
            Assert.AreEqual(0f, pr.inequality, Eps);
        }

        // ---- ヘルパ ----

        private static void AssertUnit(float v)
        {
            Assert.GreaterOrEqual(v, 0f - Eps);
            Assert.LessOrEqual(v, 1f + Eps);
        }
    }
}
