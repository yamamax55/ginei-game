using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 徴兵波＝総動員（CONSCRIPT-WAVE・純ロジック）。
    /// 戦争で人的資源が枯れると徴兵を強め、より広い年齢層・より多くの人口を兵に取る。
    /// だが徴兵を強めると労働力が抜け（LaborDrain）、社会・国民感情に負荷がかかり（SocialStrain）、
    /// 条件の悪い者まで取るので兵の質が下がる（ConscriptQuality）。累積でいずれ底をさらう（BarrelScraping）。
    /// 分担：徴募の基本（充足・徴募源）は <see cref="ConscriptionRules"/>、人口動態は <see cref="DemographicsRules"/> が担い、
    /// 本クラスは「総動員の波と社会負荷」専従＝盤面非依存の plain 引数・実効値パターン（基準値非破壊）。乱数なし。test-first。
    /// </summary>
    public readonly struct ConscriptionWaveParams
    {
        /// <summary>徴兵で兵に取れる適齢人口の最大割合（draftIntensity=1 のとき）。</summary>
        public readonly float MaxDraftFraction;
        /// <summary>労働力流出の感応度（徴兵強度→労働喪失割合）。</summary>
        public readonly float LaborDrainSensitivity;
        /// <summary>質低下のカーブ指数（大きいほど序盤は質を保ち終盤に急落）。</summary>
        public readonly float QualityFalloffExponent;
        /// <summary>これ以上は質を下げない下限（最低限の練度）。</summary>
        public readonly float MinQuality;
        /// <summary>社会負荷の基準スケール（戦争支持で割り引く前の負荷）。</summary>
        public readonly float StrainScale;
        /// <summary>底さらいのカーブ指数（累積徴兵/総人口に対する枯渇の立ち上がり）。</summary>
        public readonly float ScrapeExponent;

        public ConscriptionWaveParams(
            float maxDraftFraction,
            float laborDrainSensitivity,
            float qualityFalloffExponent,
            float minQuality,
            float strainScale,
            float scrapeExponent)
        {
            MaxDraftFraction = Mathf.Clamp01(maxDraftFraction);
            LaborDrainSensitivity = Mathf.Clamp01(laborDrainSensitivity);
            QualityFalloffExponent = Mathf.Clamp(qualityFalloffExponent, 0.1f, 8f);
            MinQuality = Mathf.Clamp01(minQuality);
            StrainScale = Mathf.Clamp(strainScale, 0f, 4f);
            ScrapeExponent = Mathf.Clamp(scrapeExponent, 0.1f, 8f);
        }

        /// <summary>既定パラメータ。</summary>
        public static ConscriptionWaveParams Default => new ConscriptionWaveParams(
            maxDraftFraction: 0.5f,
            laborDrainSensitivity: 0.6f,
            qualityFalloffExponent: 2f,
            minQuality: 0.3f,
            strainScale: 0.8f,
            scrapeExponent: 1.5f);
    }

    /// <summary>徴兵波＝総動員で人口から兵を集める。社会への負荷を計算する純関数群。</summary>
    public static class ConscriptionWaveRules
    {
        // ---- DraftPool：徴兵で集められる兵 ----

        /// <summary>徴兵で集められる兵＝適齢人口×最大割合×徴兵強度。強度を上げるほど多く取れる。</summary>
        public static float DraftPool(float eligiblePopulation, float draftIntensity, ConscriptionWaveParams p)
        {
            float pop = Mathf.Max(0f, eligiblePopulation);
            float intensity = Mathf.Clamp01(draftIntensity);
            return pop * p.MaxDraftFraction * intensity;
        }

        public static float DraftPool(float eligiblePopulation, float draftIntensity)
            => DraftPool(eligiblePopulation, draftIntensity, ConscriptionWaveParams.Default);

        // ---- LaborDrain：徴兵による労働力の流出 ----

        /// <summary>徴兵による労働力の流出＝就労人口×強度×感応度。徴兵を強めるほど工場・農地から人が抜ける。</summary>
        public static float LaborDrain(float draftIntensity, float workingPopulation, ConscriptionWaveParams p)
        {
            float intensity = Mathf.Clamp01(draftIntensity);
            float labor = Mathf.Max(0f, workingPopulation);
            return labor * intensity * p.LaborDrainSensitivity;
        }

        public static float LaborDrain(float draftIntensity, float workingPopulation)
            => LaborDrain(draftIntensity, workingPopulation, ConscriptionWaveParams.Default);

        // ---- ConscriptQuality：徴兵を広げるほど質が下がる ----

        /// <summary>徴兵兵の質（0..1）＝1から強度のべき乗ぶん落とし、下限でクランプ。
        /// 軽い徴兵なら適格者だけ取れて質が高いが、広げると条件の悪い者まで取って質が落ちる。</summary>
        public static float ConscriptQuality(float draftIntensity, ConscriptionWaveParams p)
        {
            float intensity = Mathf.Clamp01(draftIntensity);
            float drop = Mathf.Pow(intensity, p.QualityFalloffExponent);
            float quality = 1f - (1f - p.MinQuality) * drop;
            return Mathf.Clamp(quality, p.MinQuality, 1f);
        }

        public static float ConscriptQuality(float draftIntensity)
            => ConscriptQuality(draftIntensity, ConscriptionWaveParams.Default);

        // ---- SocialStrain：徴兵強化の社会的負荷 ----

        /// <summary>社会的負荷（0..1）＝強度×基準スケール×(2−戦争支持)。戦争支持が低いほど同じ徴兵が重くのしかかる。</summary>
        public static float SocialStrain(float draftIntensity, float populationWarSupport, ConscriptionWaveParams p)
        {
            float intensity = Mathf.Clamp01(draftIntensity);
            float support = Mathf.Clamp01(populationWarSupport);
            float raw = intensity * p.StrainScale * (2f - support);
            return Mathf.Clamp01(raw);
        }

        public static float SocialStrain(float draftIntensity, float populationWarSupport)
            => SocialStrain(draftIntensity, populationWarSupport, ConscriptionWaveParams.Default);

        // ---- BarrelScraping：人的資源の枯渇（底をさらう） ----

        /// <summary>底さらい度（0..1）＝(累積徴兵/総人口)のべき乗。累積で取り尽くすほど1に近づく。</summary>
        public static float BarrelScraping(float cumulativeDraft, float totalPopulation, ConscriptionWaveParams p)
        {
            float total = Mathf.Max(0f, totalPopulation);
            if (total <= 0f) return 1f;
            float ratio = Mathf.Clamp01(Mathf.Max(0f, cumulativeDraft) / total);
            return Mathf.Clamp01(Mathf.Pow(ratio, 1f / p.ScrapeExponent));
        }

        public static float BarrelScraping(float cumulativeDraft, float totalPopulation)
            => BarrelScraping(cumulativeDraft, totalPopulation, ConscriptionWaveParams.Default);

        // ---- DraftEvasion：徴兵忌避 ----

        /// <summary>徴兵忌避率（0..1）＝強度×(1−強制力)。強制が弱いほど、徴兵が強いほど逃れる者が増える。</summary>
        public static float DraftEvasion(float draftIntensity, float enforcement)
        {
            float intensity = Mathf.Clamp01(draftIntensity);
            float enf = Mathf.Clamp01(enforcement);
            return Mathf.Clamp01(intensity * (1f - enf));
        }

        // ---- ManpowerSustainability：損耗を補える徴兵の持続性 ----

        /// <summary>持続性（0..1）＝徴兵プールが損耗を補える割合。損耗以上に集められれば1、足りなければ比例で下がる。</summary>
        public static float ManpowerSustainability(float draftPool, float casualtyRate)
        {
            float pool = Mathf.Max(0f, draftPool);
            float casualties = Mathf.Max(0f, casualtyRate);
            if (casualties <= 0f) return 1f;
            return Mathf.Clamp01(pool / casualties);
        }

        // ---- IsManpowerExhausted：人的資源が枯渇したか ----

        /// <summary>底さらい度が閾値以上なら人的資源は枯渇したとみなす。</summary>
        public static bool IsManpowerExhausted(float barrelScraping, float threshold)
            => Mathf.Clamp01(barrelScraping) >= Mathf.Clamp01(threshold);
    }
}
