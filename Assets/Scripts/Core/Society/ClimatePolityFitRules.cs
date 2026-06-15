using UnityEngine;

namespace Ginei
{
    /// <summary>風土と政体の相性の調整係数（#1443・モンテスキュー風土論）。</summary>
    public readonly struct ClimatePolityFitParams
    {
        /// <summary>過酷さが気質（活力）に効く強さ（過酷な環境ほど活力ある気質を育てる重み）。</summary>
        public readonly float harshnessWeight;
        /// <summary>豊かさが気質（穏やかさ＝活力を和らげる）に効く強さ。</summary>
        public readonly float abundanceWeight;
        /// <summary>適合が安定度へ寄与する最小係数（風土に全く合わない政体の安定度倍率の下限）。</summary>
        public readonly float minStability;
        /// <summary>適合が安定度へ寄与する最大係数（風土に完全に適合した政体の安定度倍率の上限）。</summary>
        public readonly float maxStability;
        /// <summary>ミスマッチが統治効率を削る最大幅（風土不適合のときの最大ペナルティ）。</summary>
        public readonly float maxMismatchPenalty;
        /// <summary>政体が時間で風土へ馴染む速さ（1秒・乖離最大あたり）。</summary>
        public readonly float adaptationRate;
        /// <summary>風土に適合した安定状態とみなす既定の適合閾値（これ以上で適合）。</summary>
        public readonly float fitThreshold;

        public ClimatePolityFitParams(float harshnessWeight, float abundanceWeight,
            float minStability, float maxStability, float maxMismatchPenalty,
            float adaptationRate, float fitThreshold)
        {
            this.harshnessWeight = Mathf.Clamp01(harshnessWeight);
            this.abundanceWeight = Mathf.Clamp01(abundanceWeight);
            this.minStability = Mathf.Clamp01(minStability);
            this.maxStability = Mathf.Clamp01(maxStability);
            this.maxMismatchPenalty = Mathf.Clamp01(maxMismatchPenalty);
            this.adaptationRate = Mathf.Max(0f, adaptationRate);
            this.fitThreshold = Mathf.Clamp01(fitThreshold);
        }

        /// <summary>既定＝過酷重み0.6/豊か重み0.4・安定係数0.6〜1.1・ミスマッチ罰0.5・適応0.1/秒・適合閾値0.5。</summary>
        public static ClimatePolityFitParams Default
            => new ClimatePolityFitParams(0.6f, 0.4f, 0.6f, 1.1f, 0.5f, 0.1f, 0.5f);
    }

    /// <summary>
    /// 風土と政体の相性＝モンテスキュー『法の精神』の風土論の純ロジック（MONT-3 #1443・参考）。
    /// <b>気候・地理・土壌が国民の気質を形作り、それに適した政体が異なる</b>＝寒冷で過酷な地は活力ある自由を、
    /// 温暖で豊かな地は穏やかさを生み、専制は広大で単調な地に向き、険しく分断された地形は自由・独立を育てる。
    /// <b>政体は風土に適合せねば安定しない</b>＝国民の気質と政体の原動力が噛み合えば安定し、ミスマッチは不安定化する。
    /// <see cref="TerrainRules"/>（戦術宙域の探知/機動）とは別＝こちらは惑星環境×政体の<b>安定度係数</b>を解く。
    /// <see cref="GovernanceRules"/>（per-system 内政の安定度の収束）へは適合係数・ミスマッチ罰を係数で供給し、
    /// <see cref="GovernmentPrincipleRules"/>（政体の原理＝徳/名誉/恐怖→服従コスト・生成済み）が政体の原動力を、
    /// <c>PolityScaleRules</c>（政体の規模・生成済み）が版図の大きさを担う＝本ルールは風土と原動力の<b>適合</b>だけを解く。
    /// 乱数なし・決定論・全入力クランプ・基準非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ClimatePolityFitRules
    {
        /// <summary>
        /// 環境が形作る国民の気質（0..1、高いほど活力ある＝低いほど穏やか）。
        /// <b>過酷な環境は活力を、豊かな環境は穏やかさを育てる</b>＝過酷さが気質を押し上げ、豊かさが押し下げる。
        /// harshness（環境の過酷さ）×重み − abundance（豊かさ）×重み を 0.5 基準に振って 0..1 へ収める。
        /// </summary>
        public static float ClimateTemperament(float harshness, float abundance, ClimatePolityFitParams p)
        {
            float h = Mathf.Clamp01(harshness);
            float a = Mathf.Clamp01(abundance);
            // 0.5 を中立として、過酷さで活力側へ、豊かさで穏やか側へ振る。
            float t = 0.5f + 0.5f * (h * p.harshnessWeight - a * p.abundanceWeight);
            return Mathf.Clamp01(t);
        }

        public static float ClimateTemperament(float harshness, float abundance)
            => ClimateTemperament(harshness, abundance, ClimatePolityFitParams.Default);

        /// <summary>
        /// 国民の気質と政体の原動力の適合度（0..1、1で完全適合）。
        /// <b>活力ある民には活力ある政体（自由）・穏やかな民には穏やかな政体</b>＝気質(temperament)と
        /// 政体の原動力(polityVigor)の近さで測る。乖離が大きいほど適合は下がる（適合＝1−|差|）。
        /// </summary>
        public static float PolityClimateFit(float temperament, float polityVigor)
        {
            float t = Mathf.Clamp01(temperament);
            float v = Mathf.Clamp01(polityVigor);
            return Mathf.Clamp01(1f - Mathf.Abs(t - v));
        }

        /// <summary>
        /// 風土と政体の適合が安定度へ寄与する係数（minStability..maxStability）。
        /// <b>適合すれば安定・ミスマッチは不安定</b>＝適合度0で下限、適合度1で上限を線形補間する。
        /// 既定では適合で 1.0 を超え（安定ボーナス）、不適合で 1.0 未満（不安定）になる。<see cref="GovernanceRules"/> の安定度に掛ける。
        /// </summary>
        public static float StabilityFromFit(float polityClimateFit, ClimatePolityFitParams p)
        {
            float fit = Mathf.Clamp01(polityClimateFit);
            return Mathf.Clamp01(Mathf.Lerp(p.minStability, p.maxStability, fit));
        }

        public static float StabilityFromFit(float polityClimateFit)
            => StabilityFromFit(polityClimateFit, ClimatePolityFitParams.Default);

        /// <summary>
        /// 専制への地形親和（0..1）。<b>広大で単調な地は専制に向く</b>＝モンテスキューいわく大平原は専制を生む。
        /// territorySize（版図の広大さ）×terrainUniformity（地形の単調さ）の積で測る＝両方そろってはじめて専制が根づく。
        /// </summary>
        public static float DespotismTerrainAffinity(float territorySize, float terrainUniformity)
        {
            float size = Mathf.Clamp01(territorySize);
            float uni = Mathf.Clamp01(terrainUniformity);
            return Mathf.Clamp01(size * uni);
        }

        /// <summary>
        /// 自由・独立への地形親和（0..1）。<b>険しく分断された地形は自由を育てる</b>＝山岳は自由の砦。
        /// terrainRuggedness（地形の険しさ）×fragmentation（分断度）の積で測る＝守りやすく束ねにくい地は自由を生む。
        /// </summary>
        public static float FreedomTerrainAffinity(float terrainRuggedness, float fragmentation)
        {
            float rug = Mathf.Clamp01(terrainRuggedness);
            float frag = Mathf.Clamp01(fragmentation);
            return Mathf.Clamp01(rug * frag);
        }

        /// <summary>
        /// 風土に合わない政体を強いたときの統治効率の低下（0..1、大きいほど効率が落ちる）。
        /// <b>風土に合わない政体を強いると統治効率が落ちる</b>＝適合度が低いほどペナルティが大きい。
        /// ペナルティ＝(1−適合度)×最大ペナルティ幅。
        /// </summary>
        public static float ClimateMismatchPenalty(float polityClimateFit, ClimatePolityFitParams p)
        {
            float fit = Mathf.Clamp01(polityClimateFit);
            return Mathf.Clamp01((1f - fit) * p.maxMismatchPenalty);
        }

        public static float ClimateMismatchPenalty(float polityClimateFit)
            => ClimateMismatchPenalty(polityClimateFit, ClimatePolityFitParams.Default);

        /// <summary>
        /// 政体が時間で風土に馴染む（馴染んだ後の政体の原動力 0..1 を返す）。
        /// <b>政体は時間をかけて風土へ適応する（または風土が政体を変える）</b>＝政体の原動力(polityVigor)を
        /// 国民の気質(temperament)へ adaptationRate×dt ぶん近づける（基準非破壊＝新しい値を返す）。
        /// </summary>
        public static float AdaptationOverTime(float temperament, float polityVigor, float dt, ClimatePolityFitParams p)
        {
            float target = Mathf.Clamp01(temperament);
            float v = Mathf.Clamp01(polityVigor);
            float step = p.adaptationRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.MoveTowards(v, target, step));
        }

        public static float AdaptationOverTime(float temperament, float polityVigor, float dt)
            => AdaptationOverTime(temperament, polityVigor, dt, ClimatePolityFitParams.Default);

        /// <summary>
        /// 政体が風土に適合した安定状態かの判定。適合度(polityClimateFit)が閾値(threshold)以上なら、
        /// <b>政体が風土に適合して安定している</b>とみなす。
        /// </summary>
        public static bool IsClimaticallySuited(float polityClimateFit, float threshold)
        {
            return Mathf.Clamp01(polityClimateFit) >= Mathf.Clamp01(threshold);
        }

        public static bool IsClimaticallySuited(float polityClimateFit)
            => IsClimaticallySuited(polityClimateFit, ClimatePolityFitParams.Default.fitThreshold);
    }
}
