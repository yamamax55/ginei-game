using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊陳腐化（技術世代遅れ）の調整係数（#1385）。</summary>
    public readonly struct ObsolescenceParams
    {
        /// <summary>技術世代差1あたりのペナルティの強さ（敵が新しいほど自艦が弱る速度）。</summary>
        public readonly float gapPenaltyWeight;
        /// <summary>陳腐化ペナルティの下限（どれだけ時代遅れでも性能は健在なので最低これだけは出る）。</summary>
        public readonly float minPenalty;
        /// <summary>破壊的飛躍が非連続に効く強さ（弩級艦のような画期的世代が旧世代を一挙に陳腐化させる加速）。</summary>
        public readonly float leapDisruption;
        /// <summary>更新圧力の強さ（陳腐化×艦隊価値が更新需要に変わる係数）。</summary>
        public readonly float upgradeWeight;
        /// <summary>陳腐化艦隊と判定するペナルティの既定閾値（これ以下なら戦力にならない）。</summary>
        public readonly float obsoleteThreshold;

        public ObsolescenceParams(float gapPenaltyWeight, float minPenalty, float leapDisruption,
                                  float upgradeWeight, float obsoleteThreshold)
        {
            this.gapPenaltyWeight = Mathf.Max(0f, gapPenaltyWeight);
            this.minPenalty = Mathf.Clamp01(minPenalty);
            this.leapDisruption = Mathf.Max(0f, leapDisruption);
            this.upgradeWeight = Mathf.Max(0f, upgradeWeight);
            this.obsoleteThreshold = Mathf.Clamp01(obsoleteThreshold);
        }

        /// <summary>既定＝世代差ペナルティ0.8・ペナルティ下限0.2・飛躍加速1.5・更新圧力0.6・陳腐化閾値0.5。</summary>
        public static ObsolescenceParams Default =>
            new ObsolescenceParams(0.8f, 0.2f, 1.5f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 艦隊陳腐化の純ロジック（#1385・技術世代遅れによる相対的な戦闘力ペナルティ）。艦が物理的に古びる
    /// 経年劣化（<see cref="ShipAgingRules"/>＝艦齢・直しても戻らない経年）とは別に、敵がより新しい技術世代の
    /// 艦を持つと自艦は相対的に陳腐化して戦闘力が落ちる＝弩級戦艦が前弩級を一挙に陳腐化させたように、
    /// 技術世代差が大きいほど旧式艦は戦力にならない（性能は健在でも「時代遅れ」になる）。破壊的な技術飛躍は
    /// 艦隊全体を一斉に陳腐化させ（保有艦が一夜で旧式に）、陳腐化した艦隊は更新（<see cref="RefitPurchaseRules"/>＝
    /// 改装/新造）の圧力を生むが、投じた費用が無駄になる埋没投資のジレンマも抱える。技術そのものの前提グラフは
    /// <see cref="TechTreeRules"/>（前提依存）、世代差が縮む技術伝播は <see cref="InnovationDiffusionRules"/>（伝播）が
    /// 担い、ここは「世代差→相対的陳腐化（世代差ペナルティ）」だけを扱う。倍率は基準値に掛けて使う
    /// （実効値パターン・基準非破壊）。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ObsolescenceRules
    {
        /// <summary>
        /// 技術世代差（-1..1）＝自艦の技術世代−敵艦の技術世代。敵が新しいほど負（自艦が陳腐化）、
        /// 自艦が新しいほど正（自艦が優位）。0＝同世代。
        /// </summary>
        public static float GenerationGap(float ownTechGen, float enemyTechGen)
        {
            float own = Mathf.Clamp01(ownTechGen);
            float enemy = Mathf.Clamp01(enemyTechGen);
            return Mathf.Clamp(own - enemy, -1f, 1f);
        }

        /// <summary>
        /// 技術世代遅れが戦闘力に与えるペナルティ倍率（minPenalty..1）。世代差が負（敵が新しい）ほど
        /// 旧式艦は弱く、その遅れぶん×ペナルティ強さで目減りする＝性能は健在でも時代遅れになる。
        /// 同世代以上（gap>=0）は陳腐化なし＝1.0。基準戦力に掛けて使う。
        /// </summary>
        public static float ObsolescencePenalty(float generationGap, ObsolescenceParams p)
        {
            float gap = Mathf.Clamp(generationGap, -1f, 1f);
            // 自艦が遅れている分（負の世代差）だけ陳腐化する。優位（正）は陳腐化を生まない。
            float behind = Mathf.Max(0f, -gap);
            float penalty = 1f - behind * p.gapPenaltyWeight;
            return Mathf.Clamp(penalty, p.minPenalty, 1f);
        }

        public static float ObsolescencePenalty(float generationGap)
            => ObsolescencePenalty(generationGap, ObsolescenceParams.Default);

        /// <summary>
        /// 相対戦闘力（0..1）＝生の戦力が陳腐化ペナルティで目減りした実効戦力。性能（rawStrength）は健在でも
        /// 技術世代遅れで時代遅れになり、戦場では rawStrength×obsolescencePenalty しか出せない。
        /// </summary>
        public static float RelativeCombatPower(float rawStrength, float obsolescencePenalty)
        {
            float raw = Mathf.Clamp01(rawStrength);
            float pen = Mathf.Clamp01(obsolescencePenalty);
            return Mathf.Clamp01(raw * pen);
        }

        /// <summary>
        /// 破壊的飛躍の世代差押し上げ（0..1）＝弩級艦のような画期的な新世代が旧世代に与える非連続な世代差。
        /// 飛躍の大きさに飛躍加速を掛けて非線形に効かせる＝小さな改良ではなく一挙に陳腐化させる飛び。
        /// 戻り値は <see cref="ObsolescencePenalty"/> へ負の世代差として渡せる「実効世代差」。
        /// </summary>
        public static float DisruptiveLeap(float newGenerationMagnitude, ObsolescenceParams p)
        {
            float mag = Mathf.Clamp01(newGenerationMagnitude);
            // 飛躍は非連続＝大きさを加速で増幅し（leapDisruption>1で1点を超えて跳ねる）、世代差は1で頭打ち。
            return Mathf.Clamp01(mag * p.leapDisruption);
        }

        public static float DisruptiveLeap(float newGenerationMagnitude)
            => DisruptiveLeap(newGenerationMagnitude, ObsolescenceParams.Default);

        /// <summary>
        /// 艦隊を更新する圧力（0..1）＝陳腐化が深いほど・艦隊価値が大きいほど、旧式艦を一新する必要が高まる
        /// （<see cref="RefitPurchaseRules"/>＝改装/新造 へ）。ペナルティが軽い（1に近い）なら圧力は小さい。
        /// 陳腐化ぶん（1−ペナルティ）×艦隊価値×更新圧力係数。
        /// </summary>
        public static float UpgradePressure(float obsolescencePenalty, float fleetValue, ObsolescenceParams p)
        {
            float pen = Mathf.Clamp01(obsolescencePenalty);
            float value = Mathf.Clamp01(fleetValue);
            float obsolete = 1f - pen;
            return Mathf.Clamp01(obsolete * value * p.upgradeWeight);
        }

        public static float UpgradePressure(float obsolescencePenalty, float fleetValue)
            => UpgradePressure(obsolescencePenalty, fleetValue, ObsolescenceParams.Default);

        /// <summary>
        /// 一斉陳腐化のペナルティ倍率（minPenalty..1）＝技術飛躍が艦隊全体を一夜で旧式にする。
        /// 飛躍の大きさ（disruptiveLeap）が艦隊全体の実効世代差となり、世代がばらつくほど
        /// （fleetGenerationSpread 大）一部の新しめの艦が緩衝になって全滅は緩む。
        /// 飛躍を実効的な「遅れ」に変換し（広がりで割り引いた飛躍）<see cref="ObsolescencePenalty"/> へ通す。
        /// </summary>
        public static float MassObsolescence(float disruptiveLeap, float fleetGenerationSpread, ObsolescenceParams p)
        {
            float leap = Mathf.Clamp01(disruptiveLeap);
            float spread = Mathf.Clamp01(fleetGenerationSpread);
            // 世代がばらつくほど飛躍の打撃が薄まる（新しめの艦が一部生き残る）。
            float effectiveLeap = leap * (1f - 0.5f * spread);
            // 一斉陳腐化＝艦隊全体が effectiveLeap ぶん遅れた状態のペナルティ。
            return ObsolescencePenalty(-effectiveLeap, p);
        }

        public static float MassObsolescence(float disruptiveLeap, float fleetGenerationSpread)
            => MassObsolescence(disruptiveLeap, fleetGenerationSpread, ObsolescenceParams.Default);

        /// <summary>
        /// 埋没投資のジレンマ（0..1）＝陳腐化した艦隊に投じた費用が無駄になる度合い。更新は高くつくが旧式艦は弱い
        /// ＝「金をかけた艦隊が時代遅れになり、それでも更新せねば戦えない」板挟みの大きさ。
        /// 艦隊価値が大きいほど（投じた費用が大きいほど）・陳腐化が深いほどジレンマは重い。
        /// </summary>
        public static float SunkInvestmentDilemma(float fleetValue, float obsolescencePenalty)
        {
            float value = Mathf.Clamp01(fleetValue);
            float pen = Mathf.Clamp01(obsolescencePenalty);
            float obsolete = 1f - pen;
            return Mathf.Clamp01(value * obsolete);
        }

        /// <summary>
        /// 陳腐化艦隊か＝技術世代遅れで戦力にならない（ペナルティが閾値以下）。性能が健在でも、
        /// 敵の新世代に対して相対的に時代遅れなら戦力として数えられない。
        /// </summary>
        public static bool IsObsoleteFleet(float obsolescencePenalty, float threshold)
        {
            float pen = Mathf.Clamp01(obsolescencePenalty);
            float thr = Mathf.Clamp01(threshold);
            return pen <= thr;
        }

        public static bool IsObsoleteFleet(float obsolescencePenalty)
            => IsObsoleteFleet(obsolescencePenalty, ObsolescenceParams.Default.obsoleteThreshold);
    }
}
