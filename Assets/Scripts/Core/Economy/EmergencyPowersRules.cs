using UnityEngine;

namespace Ginei
{
    /// <summary>国家緊急権の調整係数。</summary>
    public readonly struct EmergencyPowersParams
    {
        /// <summary>発動中の危機処理速度倍率（≥1・非常大権の本来の効用）。</summary>
        public readonly float crisisSpeedMultiplier;
        /// <summary>権力集中の基礎進行速度（per dt・長引くほど加速）。</summary>
        public readonly float concentrationRate;
        /// <summary>常態化が始まる継続時間（これを超えて危機なき延長は全権委任法の罠）。</summary>
        public readonly float normalizationOnset;
        /// <summary>議会・司法の萎縮速度（per dt・権力集中に比例＝使われない筋肉は痩せる）。</summary>
        public readonly float atrophyRate;
        /// <summary>慢性化とみなす継続時間（平時復帰の難しさが飽和する長さ）。</summary>
        public readonly float chronicTime;

        public EmergencyPowersParams(float crisisSpeedMultiplier, float concentrationRate,
            float normalizationOnset, float atrophyRate, float chronicTime)
        {
            this.crisisSpeedMultiplier = Mathf.Max(1f, crisisSpeedMultiplier);
            this.concentrationRate = Mathf.Max(0f, concentrationRate);
            this.normalizationOnset = Mathf.Max(0.0001f, normalizationOnset);
            this.atrophyRate = Mathf.Max(0f, atrophyRate);
            this.chronicTime = Mathf.Max(0.0001f, chronicTime);
        }

        /// <summary>既定＝処理倍率2・集中0.02・常態化開始20・萎縮0.05・慢性化60。</summary>
        public static EmergencyPowersParams Default => new EmergencyPowersParams(2f, 0.02f, 20f, 0.05f, 60f);
    }

    /// <summary>
    /// 国家緊急権の純ロジック（憲法停止の力学＝全権委任法の罠）。非常大権は危機を速く処理するが、
    /// 続けるほど権力が一極に集中し、議会・司法が形骸化し、解除されない非常事態が常態になる
    /// ＝「非常事態の最大の危機は、それが終わらないこと」。<see cref="MartialLawRules"/>
    /// （治安戒厳＝騒乱鎮圧の時限措置）とは別系統＝こちらは統治機構そのものの停止と萎縮を扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EmergencyPowersRules
    {
        /// <summary>復帰難度における萎縮の重み（残りは継続時間の重み）。</summary>
        public const float RestorationAtrophyWeight = 0.6f;

        /// <summary>危機処理の速度倍率。発動中は倍率（既定2）、平時は1＝非常大権の本来の効用。</summary>
        public static float CrisisResponseSpeed(bool powersActive, EmergencyPowersParams p)
        {
            return powersActive ? p.crisisSpeedMultiplier : 1f;
        }

        public static float CrisisResponseSpeed(bool powersActive)
            => CrisisResponseSpeed(powersActive, EmergencyPowersParams.Default);

        /// <summary>
        /// 権力集中の1tick後の値（0..1）。基礎速度×(1＋継続時間/常態化開始)で進む
        /// ＝長引くほど他機関の形骸化が加速する（集中は集中を呼ぶ）。
        /// </summary>
        public static float PowerConcentrationTick(float concentration, float duration, float dt, EmergencyPowersParams p)
        {
            float c = Mathf.Clamp01(concentration);
            float d = Mathf.Max(0f, duration);
            float accel = 1f + d / p.normalizationOnset;
            return Mathf.Clamp01(c + p.concentrationRate * accel * Mathf.Max(0f, dt));
        }

        public static float PowerConcentrationTick(float concentration, float duration, float dt)
            => PowerConcentrationTick(concentration, duration, dt, EmergencyPowersParams.Default);

        /// <summary>
        /// 常態化リスク（0..1）＝危機が去ったのに解除されない度合い。常態化開始を超えた継続が
        /// 危機の現実度（crisisStillReal 0..1）の薄れた分だけリスクになる＝本物の危機が続く限りは
        /// 延長も正当（リスク0）、危機が去った延長こそ全権委任法の罠。
        /// </summary>
        public static float NormalizationRisk(float duration, float crisisStillReal, EmergencyPowersParams p)
        {
            float d = Mathf.Max(0f, duration);
            float overstay = Mathf.Clamp01((d - p.normalizationOnset) / p.normalizationOnset);
            return overstay * (1f - Mathf.Clamp01(crisisStillReal));
        }

        public static float NormalizationRisk(float duration, float crisisStillReal)
            => NormalizationRisk(duration, crisisStillReal, EmergencyPowersParams.Default);

        /// <summary>
        /// 議会・司法の萎縮の1tick後の値（0..1）。萎縮速度×権力集中度で進む
        /// ＝迂回される機関ほど速く痩せる（使われない筋肉は痩せる）。集中0なら進まない。
        /// </summary>
        public static float InstitutionalAtrophyTick(float atrophy, float concentration, float dt, EmergencyPowersParams p)
        {
            float a = Mathf.Clamp01(atrophy);
            float c = Mathf.Clamp01(concentration);
            return Mathf.Clamp01(a + p.atrophyRate * c * Mathf.Max(0f, dt));
        }

        public static float InstitutionalAtrophyTick(float atrophy, float concentration, float dt)
            => InstitutionalAtrophyTick(atrophy, concentration, dt, EmergencyPowersParams.Default);

        /// <summary>
        /// 平時復帰の難しさ（0..1）＝萎縮（重み0.6）＋慢性化した継続時間（重み0.4）。
        /// 萎縮した制度は戻し方を忘れる＝早く解くほど安く、慢性化してからでは高くつく。
        /// </summary>
        public static float RestorationDifficulty(float atrophy, float duration, EmergencyPowersParams p)
        {
            float a = Mathf.Clamp01(atrophy);
            float chronic = Mathf.Clamp01(Mathf.Max(0f, duration) / p.chronicTime);
            return Mathf.Clamp01(RestorationAtrophyWeight * a + (1f - RestorationAtrophyWeight) * chronic);
        }

        public static float RestorationDifficulty(float atrophy, float duration)
            => RestorationDifficulty(atrophy, duration, EmergencyPowersParams.Default);

        /// <summary>
        /// 時限条項の価値（0..1）＝自動失効の有無×執行の信憑性。条項が無ければ0、
        /// あっても執行されないなら0＝紙の上の時限は意味がない（全権委任法にも期限はあった）。
        /// </summary>
        public static float SunsetClauseValue(bool hasSunset, float enforcementCredibility)
        {
            return hasSunset ? Mathf.Clamp01(enforcementCredibility) : 0f;
        }
    }
}
