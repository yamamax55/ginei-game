using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 精鋭部隊（少数精鋭・質的優位）の調整係数。マジックナンバー禁止＝ここに集約。
    /// </summary>
    public readonly struct EliteUnitParams
    {
        /// <summary>精鋭度1.0（最精鋭）のときの戦闘力倍率の最大上乗せ幅（基準1.0＋これ）。</summary>
        public readonly float combatBonusScale;
        /// <summary>戦闘力倍率の下限（精鋭度0でもこの値を下回らない＝それでも精鋭は基準以上）。</summary>
        public readonly float minCombatMultiplier;
        /// <summary>奇襲（surprise=1）が衝撃を増幅する最大倍率（surprise=0で1.0、=1でこの値）。</summary>
        public readonly float surpriseShockScale;
        /// <summary>特殊作戦の基礎成功度（難度0・精鋭度0でこの値＝最低限の練度）。</summary>
        public readonly float specialOpsBase;
        /// <summary>精鋭を失う代償の最大係数（全損 lossFraction=1 でこの倍率の損失感）。</summary>
        public readonly float irreplaceabilityScale;
        /// <summary>代替不能性の非線形さ（lossFraction を強調する指数＝少量損失でも重く効く）。</summary>
        public readonly float irreplaceabilityExponent;
        /// <summary>精鋭が周囲士気へ与える支え幅の最大（presence=1 で周囲士気をこの割合だけ底上げ）。</summary>
        public readonly float moraleAnchorScale;
        /// <summary>精鋭頼みリスクの最大値（commitment=1・通常質0でこの危険度）。</summary>
        public readonly float overrelianceScale;

        public EliteUnitParams(
            float combatBonusScale,
            float minCombatMultiplier,
            float surpriseShockScale,
            float specialOpsBase,
            float irreplaceabilityScale,
            float irreplaceabilityExponent,
            float moraleAnchorScale,
            float overrelianceScale)
        {
            this.combatBonusScale = Mathf.Clamp(combatBonusScale, 0f, 3f);
            this.minCombatMultiplier = Mathf.Clamp(minCombatMultiplier, 1f, 2f);
            this.surpriseShockScale = Mathf.Clamp(surpriseShockScale, 1f, 3f);
            this.specialOpsBase = Mathf.Clamp01(specialOpsBase);
            this.irreplaceabilityScale = Mathf.Clamp(irreplaceabilityScale, 1f, 5f);
            this.irreplaceabilityExponent = Mathf.Clamp(irreplaceabilityExponent, 1f, 3f);
            this.moraleAnchorScale = Mathf.Clamp01(moraleAnchorScale);
            this.overrelianceScale = Mathf.Clamp01(overrelianceScale);
        }

        /// <summary>既定の調整係数。</summary>
        public static EliteUnitParams Default => new EliteUnitParams(
            combatBonusScale: 1.0f,
            minCombatMultiplier: 1.1f,
            surpriseShockScale: 2.0f,
            specialOpsBase: 0.2f,
            irreplaceabilityScale: 3.0f,
            irreplaceabilityExponent: 2.0f,
            moraleAnchorScale: 0.4f,
            overrelianceScale: 1.0f);
    }

    /// <summary>
    /// 精鋭部隊＝質的優位の少数精鋭（ローゼンリッター／イゼルローン要塞奪取型）の純ロジック
    /// （test-first・盤面非依存・唯一の窓口）。数は少ないが突出した戦闘力・士気・特殊技能で
    /// 局面を覆す＝決定的な一点に投入して突破口を開く・要塞を内部から奪う。だが消耗すると代替が
    /// 利かない（育成に時間）＝精鋭を失う代償は重い。
    /// <para>分担（混同しない）：
    /// <c>VeterancyRules</c>（練度の段階・既存）は<b>連続的な経験値の蓄積</b>を扱い、本窓口は
    /// <b>少数精鋭の質的飛躍と代替不能性</b>を扱う＝別レイヤー。
    /// <c>ForceQualityRules</c>（軍全体の質）は<b>軍全体の底上げ</b>を扱い、本窓口は
    /// <b>特定の精鋭部隊の局所的な質的優位</b>を扱う＝別レイヤー。</para>
    /// 実効値パターン（基準兵力・基準ダメージは変えず倍率で効かせる）。plain 引数のみ（盤面非依存）。
    /// </summary>
    public static class EliteUnitRules
    {
        /// <summary>
        /// 精鋭度（0..1）から戦闘力倍率（minCombatMultiplier..1+combatBonusScale）。
        /// 精鋭度に線形比例し、下限はあくまで基準以上（実効値・基準非破壊）。
        /// </summary>
        public static float CombatMultiplier(float eliteTier, EliteUnitParams p)
        {
            float t = Mathf.Clamp01(eliteTier);
            float mult = 1f + t * p.combatBonusScale;
            return Mathf.Max(mult, p.minCombatMultiplier);
        }

        /// <summary>既定 Params 版。</summary>
        public static float CombatMultiplier(float eliteTier)
            => CombatMultiplier(eliteTier, EliteUnitParams.Default);

        /// <summary>
        /// 精鋭の一撃が与える衝撃。戦闘力倍率に奇襲（surprise 0..1）を乗じる
        /// （surprise=0で等倍、=1で surpriseShockScale 倍＝不意打ちは衝撃が増す）。
        /// </summary>
        public static float ShockValue(float combatMultiplier, float surprise, EliteUnitParams p)
        {
            float mult = Mathf.Max(0f, combatMultiplier);
            float s = Mathf.Clamp01(surprise);
            float amp = Mathf.Lerp(1f, p.surpriseShockScale, s);
            return mult * amp;
        }

        /// <summary>既定 Params 版。</summary>
        public static float ShockValue(float combatMultiplier, float surprise)
            => ShockValue(combatMultiplier, surprise, EliteUnitParams.Default);

        /// <summary>
        /// 精鋭が突破口を開く力。投入兵力×戦闘力倍率で実効打撃力を出し、敵防御線の厚み
        /// （enemyLine&gt;0）で割って「線を貫く度合い」を返す（線が薄いほど突破しやすい）。
        /// enemyLine&lt;=0 は防御線なし＝実効打撃力そのまま。
        /// </summary>
        public static float BreakthroughPower(float eliteStrength, float combatMultiplier, float enemyLine)
        {
            float str = Mathf.Max(0f, eliteStrength);
            float mult = Mathf.Max(0f, combatMultiplier);
            float effective = str * mult;
            if (enemyLine <= 0f) return effective;
            return effective / enemyLine;
        }

        /// <summary>
        /// 特殊作戦（要塞内部奪取等）の成功度（0..1）。基礎成功度に精鋭度を上乗せし、
        /// 任務難度（missionDifficulty 0..1）で割り引く（難しいほど成功度が下がる）。
        /// </summary>
        public static float SpecialOpsSuccess(float eliteTier, float missionDifficulty, EliteUnitParams p)
        {
            float t = Mathf.Clamp01(eliteTier);
            float diff = Mathf.Clamp01(missionDifficulty);
            // 精鋭度が高いほど基礎+(1-基礎)へ近づく素の成功率、難度で線形に削る。
            float baseSuccess = p.specialOpsBase + (1f - p.specialOpsBase) * t;
            return Mathf.Clamp01(baseSuccess * (1f - diff));
        }

        /// <summary>既定 Params 版。</summary>
        public static float SpecialOpsSuccess(float eliteTier, float missionDifficulty)
            => SpecialOpsSuccess(eliteTier, missionDifficulty, EliteUnitParams.Default);

        /// <summary>
        /// 精鋭を失う代償（0..irreplaceabilityScale）。喪失割合（0..1）を指数で強調して
        /// irreplaceabilityScale 倍する＝代替が利かないので少量の損失でも重く効く（非線形）。
        /// </summary>
        public static float IrreplaceabilityCost(float eliteLossFraction, EliteUnitParams p)
        {
            float loss = Mathf.Clamp01(eliteLossFraction);
            float weighted = Mathf.Pow(loss, p.irreplaceabilityExponent);
            return weighted * p.irreplaceabilityScale;
        }

        /// <summary>既定 Params 版。</summary>
        public static float IrreplaceabilityCost(float eliteLossFraction)
            => IrreplaceabilityCost(eliteLossFraction, EliteUnitParams.Default);

        /// <summary>
        /// 精鋭が周囲の士気を支える（戦意の核）。精鋭の在席度（presence 0..1）に応じて
        /// 周囲士気（0..1）を moraleAnchorScale 幅だけ未充足分から底上げして返す（0..1）。
        /// </summary>
        public static float MoraleAnchor(float elitePresence, float surroundingMorale, EliteUnitParams p)
        {
            float presence = Mathf.Clamp01(elitePresence);
            float morale = Mathf.Clamp01(surroundingMorale);
            float lift = presence * p.moraleAnchorScale * (1f - morale);
            return Mathf.Clamp01(morale + lift);
        }

        /// <summary>既定 Params 版。</summary>
        public static float MoraleAnchor(float elitePresence, float surroundingMorale)
            => MoraleAnchor(elitePresence, surroundingMorale, EliteUnitParams.Default);

        /// <summary>
        /// 精鋭頼みで通常部隊が育たないリスク（0..overrelianceScale）。精鋭への依存度
        /// （commitment 0..1）が高く、通常部隊の質（regularQuality 0..1）が低いほど高い。
        /// </summary>
        public static float OverrelianceRisk(float eliteCommitment, float regularQuality, EliteUnitParams p)
        {
            float commit = Mathf.Clamp01(eliteCommitment);
            float quality = Mathf.Clamp01(regularQuality);
            return Mathf.Clamp01(commit * (1f - quality)) * p.overrelianceScale;
        }

        /// <summary>既定 Params 版。</summary>
        public static float OverrelianceRisk(float eliteCommitment, float regularQuality)
            => OverrelianceRisk(eliteCommitment, regularQuality, EliteUnitParams.Default);

        /// <summary>精鋭が局面を決したか（突破力が閾値以上）。</summary>
        public static bool IsEliteDecisive(float breakthroughPower, float threshold)
            => breakthroughPower >= threshold;
    }
}
