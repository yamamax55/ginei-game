using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// <see cref="CultureTickRules"/> の EditMode テスト（test-first・手計算で厳密に検証）。
    /// Unity ランタイム・MonoBehaviour に依存しない純ロジック検証。
    /// </summary>
    public class CultureTickRulesTests
    {
        // 手計算用定数（CultureParams.Default の値と一致）
        const float AssimilationSpeed = 0.04f;      // CultureParams.Default.assimilationSpeed
        const float WarPenalty = 0.4f;              // CultureParams.Default.warAssimilationPenalty
        const float SepThreshold = 40f;             // CultureParams.Default.separatismStabilityThreshold
        const float NatBonus = 0.3f;                // CultureParams.Default.nationalismMaxBonus
        const float Eps = 1e-5f;

        // ──────────────────────────────────────────────
        // EnsureCulture
        // ──────────────────────────────────────────────

        [Test]
        public void EnsureCulture_Null省略_Provnull_何もしない()
        {
            // null Province でも例外を出さない
            Assert.DoesNotThrow(() => CultureTickRules.EnsureCulture(null, "帝国人"));
        }

        [Test]
        public void EnsureCulture_cultureがNull_初期化される()
        {
            var prov = new Province { culture = null };
            CultureTickRules.EnsureCulture(prov, "帝国人");

            Assert.IsNotNull(prov.culture);
            Assert.AreEqual("帝国人", prov.culture.cultureName);
        }

        [Test]
        public void EnsureCulture_冪等_既存cultureを上書きしない()
        {
            var existing = new Culture("既存文化", 200f, 0.5f, true);
            var prov = new Province { culture = existing };

            CultureTickRules.EnsureCulture(prov, "別の文化");

            // 呼び出し前後でオブジェクト同一・値変更なし
            Assert.AreSame(existing, prov.culture);
            Assert.AreEqual("既存文化", prov.culture.cultureName);
            Assert.AreEqual(0.5f, prov.culture.assimilation, Eps);
        }

        [Test]
        public void EnsureCulture_nativeCultureNull_空文字で初期化()
        {
            var prov = new Province { culture = null };
            CultureTickRules.EnsureCulture(prov, null);

            Assert.IsNotNull(prov.culture);
            Assert.AreEqual("", prov.culture.cultureName);
        }

        // ──────────────────────────────────────────────
        // TickYear
        // ──────────────────────────────────────────────

        [Test]
        public void TickYear_Provnull_例外なし()
        {
            Assert.DoesNotThrow(() => CultureTickRules.TickYear(null, true, false));
        }

        [Test]
        public void TickYear_dominantMatch_平時_同化が進む()
        {
            // 未同化の少数民族が多数派支配下に1年いると同化が進む。
            // effectiveSpeed = 0.04, dt = 1 → assimilation += 0.04
            var prov = new Province { culture = new Culture("少数派", 100f, 0f, true) };

            CultureTickRules.TickYear(prov, dominantCultureMatch: true, atWar: false);

            float expected = 0f + AssimilationSpeed * 1f; // = 0.04
            Assert.AreEqual(expected, prov.culture.assimilation, Eps);
        }

        [Test]
        public void TickYear_dominantMatch_戦時_同化が遅くなる()
        {
            // effectiveSpeed = 0.04 * Clamp01(1 - 0.4) = 0.04 * 0.6 = 0.024
            var prov = new Province { culture = new Culture("少数派", 100f, 0f, true) };

            CultureTickRules.TickYear(prov, dominantCultureMatch: true, atWar: true);

            float effectiveSpeed = AssimilationSpeed * (1f - WarPenalty); // 0.024
            float expected = 0f + effectiveSpeed * 1f;
            Assert.AreEqual(expected, prov.culture.assimilation, Eps);
        }

        [Test]
        public void TickYear_戦時は平時より同化が遅い()
        {
            var provPeace = new Province { culture = new Culture("少数派", 100f, 0f, true) };
            var provWar   = new Province { culture = new Culture("少数派", 100f, 0f, true) };

            CultureTickRules.TickYear(provPeace, true, atWar: false);
            CultureTickRules.TickYear(provWar,   true, atWar: true);

            Assert.Greater(provPeace.culture.assimilation, provWar.culture.assimilation);
        }

        [Test]
        public void TickYear_dominantMismatch_同化は進まない()
        {
            // dominantCultureMatch=false → CultureRules.Tick が即 return → assimilation 変化なし
            var prov = new Province { culture = new Culture("少数派", 100f, 0f, true) };

            CultureTickRules.TickYear(prov, dominantCultureMatch: false, atWar: false);

            Assert.AreEqual(0f, prov.culture.assimilation, Eps);
        }

        [Test]
        public void TickYear_stability等の基準値を変えない()
        {
            // TickYear は culture のみ変更し stability は触らない
            var prov = new Province
            {
                stability = 77f,
                integration = 0.42f,
                culture = new Culture("少数派", 100f, 0.1f, true)
            };

            CultureTickRules.TickYear(prov, dominantCultureMatch: true, atWar: false);

            Assert.AreEqual(77f, prov.stability, Eps);
            Assert.AreEqual(0.42f, prov.integration, Eps);
        }

        [Test]
        public void TickYear_cultureがNull_Ensure後に進める_例外なし()
        {
            // culture が null でも EnsureCulture が nativeIdeology で補完してから Tick する
            var prov = new Province { culture = null, nativeIdeology = "自由主義" };

            Assert.DoesNotThrow(() => CultureTickRules.TickYear(prov, dominantCultureMatch: true, atWar: false));
            Assert.IsNotNull(prov.culture);
        }

        [Test]
        public void TickYear_完全同化済み_それ以上増えない()
        {
            // assimilation = 1.0（上限）→ Clamp01 により 1.0 を超えない
            var prov = new Province { culture = new Culture("少数派", 100f, 1f, false) };

            CultureTickRules.TickYear(prov, dominantCultureMatch: true, atWar: false);

            Assert.LessOrEqual(prov.culture.assimilation, 1f);
        }

        // ──────────────────────────────────────────────
        // SeparatismRisk
        // ──────────────────────────────────────────────

        [Test]
        public void SeparatismRisk_Provnull_0を返す()
        {
            Assert.AreEqual(0f, CultureTickRules.SeparatismRisk(null), Eps);
        }

        [Test]
        public void SeparatismRisk_cultureNull_0を返す()
        {
            var prov = new Province { culture = null };
            Assert.AreEqual(0f, CultureTickRules.SeparatismRisk(prov), Eps);
        }

        [Test]
        public void SeparatismRisk_非少数民族_0を返す()
        {
            var prov = new Province
            {
                stability = 10f,
                culture = new Culture("多数派", 100f, 0f, isMinority: false)
            };
            Assert.AreEqual(0f, CultureTickRules.SeparatismRisk(prov), Eps);
        }

        [Test]
        public void SeparatismRisk_安定高め_0を返す()
        {
            // stability >= threshold(40) → 0
            var prov = new Province
            {
                stability = SepThreshold,
                culture = new Culture("少数派", 100f, 0f, isMinority: true)
            };
            Assert.AreEqual(0f, CultureTickRules.SeparatismRisk(prov), Eps);
        }

        [Test]
        public void SeparatismRisk_手計算で一致()
        {
            // stability=20, assimilation=0, isMinority=true
            // instability = (40-20)/40 = 0.5
            // disaffection = 1 - 0 = 1.0
            // result = 0.5
            var prov = new Province
            {
                stability = 20f,
                culture = new Culture("少数派", 100f, 0f, isMinority: true)
            };
            float expected = 0.5f;
            Assert.AreEqual(expected, CultureTickRules.SeparatismRisk(prov), Eps);
        }

        [Test]
        public void SeparatismRisk_安定が低いほど高い_単調性()
        {
            // stability を下げるほどリスクが上がる
            var provHigh = new Province
            {
                stability = 30f,
                culture = new Culture("少数派", 100f, 0f, true)
            };
            var provLow = new Province
            {
                stability = 10f,
                culture = new Culture("少数派", 100f, 0f, true)
            };

            Assert.Greater(
                CultureTickRules.SeparatismRisk(provLow),
                CultureTickRules.SeparatismRisk(provHigh));
        }

        [Test]
        public void SeparatismRisk_同化が進むほど低い_単調性()
        {
            // 同化度が高いほど disaffection が低くリスクが下がる
            var provLowAssim = new Province
            {
                stability = 20f,
                culture = new Culture("少数派", 100f, 0.0f, true)
            };
            var provHighAssim = new Province
            {
                stability = 20f,
                culture = new Culture("少数派", 100f, 0.5f, true)
            };

            Assert.Greater(
                CultureTickRules.SeparatismRisk(provLowAssim),
                CultureTickRules.SeparatismRisk(provHighAssim));
        }

        // ──────────────────────────────────────────────
        // NationalismFactor
        // ──────────────────────────────────────────────

        [Test]
        public void NationalismFactor_Provnull_0を返す()
        {
            // null (未配線) は 0 = 係数として無効を示す
            Assert.AreEqual(0f, CultureTickRules.NationalismFactor(null), Eps);
        }

        [Test]
        public void NationalismFactor_cultureNull_0を返す()
        {
            // culture 未配線 (null) は 0 = 未配線を明示
            var prov = new Province { culture = null };
            Assert.AreEqual(0f, CultureTickRules.NationalismFactor(prov), Eps);
        }

        [Test]
        public void NationalismFactor_非少数民族_1を返す()
        {
            var prov = new Province
            {
                culture = new Culture("多数派", 100f, 0f, isMinority: false)
            };
            Assert.AreEqual(1f, CultureTickRules.NationalismFactor(prov), Eps);
        }

        [Test]
        public void NationalismFactor_未同化少数民族_手計算で一致()
        {
            // assimilation=0 → fervor=1 → factor = 1 + 0.3 * 1 = 1.3
            var prov = new Province
            {
                culture = new Culture("少数派", 100f, 0f, isMinority: true)
            };
            float expected = 1f + NatBonus * 1f; // 1.3
            Assert.AreEqual(expected, CultureTickRules.NationalismFactor(prov), Eps);
        }

        [Test]
        public void NationalismFactor_完全同化_1を返す()
        {
            // assimilation=1 → fervor=0 → factor = 1.0
            var prov = new Province
            {
                culture = new Culture("少数派", 100f, 1f, isMinority: true)
            };
            Assert.AreEqual(1f, CultureTickRules.NationalismFactor(prov), Eps);
        }

        [Test]
        public void NationalismFactor_同化が低いほど高い_単調性()
        {
            // assimilation が低いほど factor が大きい
            var provLow = new Province
            {
                culture = new Culture("少数派", 100f, 0.0f, true)
            };
            var provHigh = new Province
            {
                culture = new Culture("少数派", 100f, 0.8f, true)
            };

            Assert.Greater(
                CultureTickRules.NationalismFactor(provLow),
                CultureTickRules.NationalismFactor(provHigh));
        }

        [Test]
        public void NationalismFactor_中間値_手計算で一致()
        {
            // assimilation=0.4 → fervor=0.6 → factor = 1 + 0.3 * 0.6 = 1.18
            var prov = new Province
            {
                culture = new Culture("少数派", 100f, 0.4f, isMinority: true)
            };
            float expected = 1f + NatBonus * (1f - 0.4f); // 1.18
            Assert.AreEqual(expected, CultureTickRules.NationalismFactor(prov), Eps);
        }
    }
}
