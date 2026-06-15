using UnityEngine;

namespace Ginei
{
    /// <summary>教育投資の調整係数。</summary>
    public readonly struct EducationParams
    {
        /// <summary>投資が教育の質を押し上げる速度（per dt・投資水準1のとき）。</summary>
        public readonly float qualityGrowthRate;
        /// <summary>投資が絶えたとき教育の質が落ちる速度（per dt）。</summary>
        public readonly float qualityDecayRate;
        /// <summary>世代遅延＝教育の質が人材の質に現れるまでの時間（この間は旧世代の質が支配する）。</summary>
        public readonly float generationLag;
        /// <summary>人材の質が研究力に返す最大ボーナス。</summary>
        public readonly float researchBonusScale;
        /// <summary>人材の質が産出に返す最大ボーナス。</summary>
        public readonly float outputBonusScale;

        public EducationParams(float qualityGrowthRate, float qualityDecayRate, float generationLag,
                               float researchBonusScale, float outputBonusScale)
        {
            this.qualityGrowthRate = Mathf.Max(0f, qualityGrowthRate);
            this.qualityDecayRate = Mathf.Max(0f, qualityDecayRate);
            this.generationLag = Mathf.Max(0f, generationLag);
            this.researchBonusScale = Mathf.Max(0f, researchBonusScale);
            this.outputBonusScale = Mathf.Max(0f, outputBonusScale);
        }

        /// <summary>既定＝成長0.05・減衰0.02・世代遅延20・研究ボーナス0.5・産出ボーナス0.2。</summary>
        public static EducationParams Default => new EducationParams(0.05f, 0.02f, 20f, 0.5f, 0.2f);
    }

    /// <summary>
    /// 教育投資の純ロジック。投資は「学校の質」（schoolQuality）を上げるが、それが「人材の質」
    /// （talentQuality）に現れるのは世代遅延の後＝今日の投資は20年後の人材を作り、今日の削減は
    /// 20年後に祟る。投資が絶えれば質は静かに落ちる。人材の質は研究力・産出にボーナスを返す。
    /// 研究そのもの（<see cref="ResearchRules"/>）・人材の出自（<see cref="CareerPipelineRules"/>）
    /// とは別系統で、質の時間動態のみを扱う。倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EducationRules
    {
        /// <summary>
        /// 学校の質の1tick後の値（0..1）。投資水準 investment(0..1) が質の不足分へ向けて押し上げ
        /// （成長率×投資×dt）、投資ゼロなら減衰率×dt で落ちる。
        /// </summary>
        public static float SchoolQualityTick(float schoolQuality, float investment, float dt, EducationParams p)
        {
            float q = Mathf.Clamp01(schoolQuality);
            float inv = Mathf.Clamp01(investment);
            float d = Mathf.Max(0f, dt);
            if (inv > 0f)
                q += p.qualityGrowthRate * inv * (1f - q) * d; // 上限1へ漸近
            else
                q -= p.qualityDecayRate * d;
            return Mathf.Clamp01(q);
        }

        public static float SchoolQualityTick(float schoolQuality, float investment, float dt)
            => SchoolQualityTick(schoolQuality, investment, dt, EducationParams.Default);

        /// <summary>
        /// 人材の質の1tick後の値（0..1）。学校の質へ向けて世代遅延の速度（1/generationLag per dt）で
        /// ゆっくり収束する＝教育改革の効果は一世代かけて現れ、劣化も一世代かけて祟る。
        /// </summary>
        public static float TalentQualityTick(float talentQuality, float schoolQuality, float dt, EducationParams p)
        {
            float t = Mathf.Clamp01(talentQuality);
            float target = Mathf.Clamp01(schoolQuality);
            if (p.generationLag <= 0f) return target; // 遅延なし＝即時反映
            float rate = Mathf.Max(0f, dt) / p.generationLag;
            return Mathf.Clamp01(Mathf.Lerp(t, target, Mathf.Clamp01(rate)));
        }

        public static float TalentQualityTick(float talentQuality, float schoolQuality, float dt)
            => TalentQualityTick(talentQuality, schoolQuality, dt, EducationParams.Default);

        /// <summary>人材の質→研究力倍率（1..1+researchBonusScale）。研究出力に掛けて使う。</summary>
        public static float ResearchFactor(float talentQuality, EducationParams p)
        {
            return 1f + p.researchBonusScale * Mathf.Clamp01(talentQuality);
        }

        public static float ResearchFactor(float talentQuality) => ResearchFactor(talentQuality, EducationParams.Default);

        /// <summary>人材の質→産出倍率（1..1+outputBonusScale）。生産係数に掛けて使う。</summary>
        public static float OutputFactor(float talentQuality, EducationParams p)
        {
            return 1f + p.outputBonusScale * Mathf.Clamp01(talentQuality);
        }

        public static float OutputFactor(float talentQuality) => OutputFactor(talentQuality, EducationParams.Default);

        /// <summary>
        /// 教育負債か＝学校の質が人材の質を大きく下回る（gap 以上）＝将来の人材劣化が仕込まれている状態。
        /// 今は人材が回っていても、一世代後に枯れる警告。
        /// </summary>
        public static bool HasEducationDebt(float schoolQuality, float talentQuality, float gap = 0.2f)
        {
            return Mathf.Clamp01(talentQuality) - Mathf.Clamp01(schoolQuality) >= Mathf.Max(0f, gap);
        }
    }
}
