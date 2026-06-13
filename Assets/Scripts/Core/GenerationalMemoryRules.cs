using UnityEngine;

namespace Ginei
{
    /// <summary>戦争記憶の世代風化の調整係数。</summary>
    public readonly struct GenerationalMemoryParams
    {
        /// <summary>集合記憶が半減する年数（記憶の半減期）。</summary>
        public readonly float halfLifeYears;
        /// <summary>記憶最大時に開戦閾値へ上乗せされる割合（抑止の内面化の強さ）。</summary>
        public readonly float thresholdScale;
        /// <summary>美化の進行速度（記憶ゼロのとき1年あたりに進む美化量）。</summary>
        public readonly float romanticizationRate;
        /// <summary>教育による記憶保存の上限（体験の代わりにはならない＝1未満）。</summary>
        public readonly float educationCeiling;
        /// <summary>戦争を知らない世代が全員のとき生じる好戦度ギャップの最大値。</summary>
        public readonly float hawkishGapScale;

        public GenerationalMemoryParams(
            float halfLifeYears, float thresholdScale, float romanticizationRate,
            float educationCeiling, float hawkishGapScale)
        {
            this.halfLifeYears = Mathf.Max(1f, halfLifeYears);
            this.thresholdScale = Mathf.Max(0f, thresholdScale);
            this.romanticizationRate = Mathf.Max(0f, romanticizationRate);
            this.educationCeiling = Mathf.Clamp01(educationCeiling);
            this.hawkishGapScale = Mathf.Max(0f, hawkishGapScale);
        }

        /// <summary>既定＝半減期30年・閾値上乗せ0.5・美化速度0.02/年・教育上限0.4・ギャップ最大0.5。</summary>
        public static GenerationalMemoryParams Default
            => new GenerationalMemoryParams(30f, 0.5f, 0.02f, 0.4f, 0.5f);
    }

    /// <summary>
    /// 戦争記憶の世代風化の純ロジック（「平和の最大の敵は平和の長さ」）。
    /// 戦争を知る世代が退場すると集合記憶が半減期カーブで薄れ、実体験の空白を美化された物語が埋め、
    /// 開戦をためらわせていた閾値が静かに下がる＝好戦論の再生。
    /// <see cref="WarGoalRules"/> の WarWeariness（進行中の戦争が生む短期の厭戦）とは別系統＝
    /// こちらは平時に世代スケールで進む忘却を扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GenerationalMemoryRules
    {
        /// <summary>
        /// 集合記憶の強度（0..1）＝半減期カーブ。終戦直後は戦争の苛烈さ（warSeverity 0..1）がそのまま初期値
        /// （酷い戦争ほど深く刻まれる）、以後 halfLifeYears ごとに半減する。
        /// </summary>
        public static float MemoryIntensity(float yearsSinceWar, float warSeverity, GenerationalMemoryParams p)
        {
            float years = Mathf.Max(0f, yearsSinceWar);
            float severity = Mathf.Clamp01(warSeverity);
            return severity * Mathf.Pow(0.5f, years / p.halfLifeYears);
        }

        public static float MemoryIntensity(float yearsSinceWar, float warSeverity)
            => MemoryIntensity(yearsSinceWar, warSeverity, GenerationalMemoryParams.Default);

        /// <summary>
        /// 戦争を知る世代の人口比（0..1）＝平均余命 lifespan 年をかけて線形に退場する。
        /// 終戦直後は全員が証人、lifespan 年後には誰も戦争を知らない。
        /// </summary>
        public static float WitnessShare(float yearsSinceWar, float lifespan)
        {
            float years = Mathf.Max(0f, yearsSinceWar);
            float span = Mathf.Max(1f, lifespan);
            return Mathf.Clamp01(1f - years / span);
        }

        /// <summary>
        /// 開戦閾値の修正倍率（1.0..1+thresholdScale）＝記憶が強いほど開戦をためらう（抑止の内面化）。
        /// 記憶が風化しきると 1.0 へ戻る＝平和が長いほど開戦の敷居が下がる。
        /// </summary>
        public static float WarThresholdModifier(float memoryIntensity, GenerationalMemoryParams p)
        {
            return 1f + Mathf.Clamp01(memoryIntensity) * p.thresholdScale;
        }

        public static float WarThresholdModifier(float memoryIntensity)
            => WarThresholdModifier(memoryIntensity, GenerationalMemoryParams.Default);

        /// <summary>
        /// 戦争の美化（0..1）を dt 年ぶん進める＝実体験の記憶が薄れた分だけ物語が美化する（反比例）。
        /// 記憶が満ちている間は進まず、記憶ゼロなら romanticizationRate×dt で進む。
        /// </summary>
        public static float RomanticizationTick(float romanticization, float memoryIntensity, float dt, GenerationalMemoryParams p)
        {
            float current = Mathf.Clamp01(romanticization);
            float memory = Mathf.Clamp01(memoryIntensity);
            float years = Mathf.Max(0f, dt);
            return Mathf.Clamp01(current + (1f - memory) * p.romanticizationRate * years);
        }

        public static float RomanticizationTick(float romanticization, float memoryIntensity, float dt)
            => RomanticizationTick(romanticization, memoryIntensity, dt, GenerationalMemoryParams.Default);

        /// <summary>
        /// 記憶の制度的保存＝教育努力（0..1）は記憶の下限 educationCeiling×effort を支えるが、
        /// 体験の代わりにはならない（上限 educationCeiling）。生きた記憶が上限を超えている間は何も足さない。
        /// </summary>
        public static float EducationPreservation(float memoryIntensity, float educationEffort, GenerationalMemoryParams p)
        {
            float memory = Mathf.Clamp01(memoryIntensity);
            float effort = Mathf.Clamp01(educationEffort);
            return Mathf.Max(memory, p.educationCeiling * effort);
        }

        public static float EducationPreservation(float memoryIntensity, float educationEffort)
            => EducationPreservation(memoryIntensity, educationEffort, GenerationalMemoryParams.Default);

        /// <summary>
        /// 世代間の好戦度ギャップ（0..hawkishGapScale）＝戦争を知らない世代の比率に比例
        /// （知らない世代ほど勇ましい）。全員が証人なら 0。
        /// </summary>
        public static float HawkishGenerationGap(float witnessShare, GenerationalMemoryParams p)
        {
            return (1f - Mathf.Clamp01(witnessShare)) * p.hawkishGapScale;
        }

        public static float HawkishGenerationGap(float witnessShare)
            => HawkishGenerationGap(witnessShare, GenerationalMemoryParams.Default);

        /// <summary>
        /// 実効開戦閾値＝基準閾値×記憶修正。「平和の最大の敵は平和の長さ」を式にしたもの＝
        /// 終戦から年月が経つほど単調に下がり、基準値へ漸近する（基準値は非破壊＝実効値パターン）。
        /// </summary>
        public static float EffectiveWarThreshold(float baseThreshold, float yearsSinceWar, float warSeverity, GenerationalMemoryParams p)
        {
            float baseValue = Mathf.Max(0f, baseThreshold);
            return baseValue * WarThresholdModifier(MemoryIntensity(yearsSinceWar, warSeverity, p), p);
        }

        public static float EffectiveWarThreshold(float baseThreshold, float yearsSinceWar, float warSeverity)
            => EffectiveWarThreshold(baseThreshold, yearsSinceWar, warSeverity, GenerationalMemoryParams.Default);
    }
}
