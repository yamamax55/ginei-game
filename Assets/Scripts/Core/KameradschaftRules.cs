using UnityEngine;

namespace Ginei
{
    /// <summary>戦友紐帯（一次集団の戦友愛）の調整係数。</summary>
    public readonly struct KameradschaftParams
    {
        /// <summary>戦友愛→戦闘力ボーナスの最大幅（隣の戦友のために踏ん張る・cohesion=1 で 1+この値）。</summary>
        public readonly float combatBonusScale;
        /// <summary>凝集の平時成長率（per dt・共に過ごすだけでもゆっくり育つ）。</summary>
        public readonly float buildBase;
        /// <summary>共闘（修羅場）による成長加速倍率（sharedCombat=1 で成長率が (1+この値) 倍）。</summary>
        public readonly float sharedCombatMultiplier;
        /// <summary>戦友喪失→悲嘆の最大深さ（凝集が強いほど痛い）。</summary>
        public readonly float griefScale;
        /// <summary>集団崩壊→戦闘力ペナルティの最大幅（戦友がいなくなると戦えない）。</summary>
        public readonly float dissolutionPenaltyScale;
        /// <summary>新兵がよそ者として弾かれる強さ（0..1・既存の凝集が強いほど輪に入れない）。</summary>
        public readonly float newcomerExclusion;

        public KameradschaftParams(float combatBonusScale, float buildBase, float sharedCombatMultiplier,
                                   float griefScale, float dissolutionPenaltyScale, float newcomerExclusion)
        {
            this.combatBonusScale = Mathf.Max(0f, combatBonusScale);
            this.buildBase = Mathf.Max(0f, buildBase);
            this.sharedCombatMultiplier = Mathf.Max(0f, sharedCombatMultiplier);
            this.griefScale = Mathf.Max(0f, griefScale);
            this.dissolutionPenaltyScale = Mathf.Max(0f, dissolutionPenaltyScale);
            this.newcomerExclusion = Mathf.Clamp01(newcomerExclusion);
        }

        /// <summary>既定＝戦闘ボーナス+30%/成長0.02/共闘×4/悲嘆0.7/崩壊ペナルティ0.6/新兵排他0.5。</summary>
        public static KameradschaftParams Default => new KameradschaftParams(0.3f, 0.02f, 4f, 0.7f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 戦友紐帯の純ロジック（RMK-2 #1405・レマルク『西部戦線異状なし』型＝一次集団理論）。戦争を耐えさせるのは
    /// 大義でも愛国でもなく、塹壕を共にする戦友（Kameradschaft）＝兵は祖国や思想のためでなく隣の戦友を
    /// 見捨てられないから戦う。共有された苦難×共に過ごした時間×相互依存で小隊レベルの凝集（戦友愛）が育ち、
    /// その凝集が戦闘力を支えるが、戦友の死は凝集が強いほど深い喪失を生み、損耗で小集団が崩壊すると戦闘力も崩れる。
    /// 「戦争を耐えさせるのは大義でなく戦友愛で、小集団の凝集が戦闘力を支えるが戦友の死は深い喪失を生み
    /// 集団が崩壊すると戦闘力も崩れる」を式に出す。
    /// <see cref="FriendshipRules"/>（キルヒアイス/双璧型＝役職に依らない個人の盟友の紐帯）とは別系統＝こちらは
    /// 小隊レベル・一次集団の戦友愛（顔の見える小集団の凝集）を扱う。士気の <see cref="FleetMorale"/>（Game層・
    /// 部隊単位の士気値）・疲弊の CombatFatigueRules（同EPIC RMK・累積疲労）・練度の <see cref="VeterancyRules"/>
    /// （会戦経験の熟練度）とも別＝戦友愛は「隣の戦友のために戦う」凝集そのもの。倍率は基準値に掛けて使う
    /// （実効値パターン・基準非破壊）。乱数なし決定論・全入力クランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class KameradschaftRules
    {
        /// <summary>
        /// 一次集団の凝集（戦友愛・0..1）。共有された苦難 sharedHardship×共に過ごした時間 timeTogether×
        /// 相互依存 mutualReliance の積＝三つが揃って初めて深い戦友愛になる（一つでも欠ければ凝集は弱い）。
        /// </summary>
        public static float PrimaryGroupCohesion(float sharedHardship, float timeTogether, float mutualReliance)
        {
            float h = Mathf.Clamp01(sharedHardship);
            float t = Mathf.Clamp01(timeTogether);
            float r = Mathf.Clamp01(mutualReliance);
            return Mathf.Clamp01(h * t * r);
        }

        /// <summary>
        /// 戦友愛→戦闘力ボーナス（1..1+combatBonusScale）。凝集の二乗でスケール＝薄い間柄はほぼ無益、
        /// 強い戦友愛で初めて「隣の戦友のために踏ん張る」力が出る。基準ダメージ・防御に掛けて使う（基準非破壊）。
        /// </summary>
        public static float CohesionCombatBonus(float primaryGroupCohesion, KameradschaftParams p)
        {
            float c = Mathf.Clamp01(primaryGroupCohesion);
            return 1f + c * c * p.combatBonusScale;
        }

        public static float CohesionCombatBonus(float primaryGroupCohesion)
            => CohesionCombatBonus(primaryGroupCohesion, KameradschaftParams.Default);

        /// <summary>
        /// 大義より戦友のための戦意（0..1）。一次集団（戦友愛 cohesion）＞二次集団（イデオロギー ideologyCommitment）
        /// ＝戦友愛が主、思想は従。凝集が高ければイデオロギーが薄くても戦える＝両者の重みづけ和で、戦友愛に
        /// 大きな重みを置く（隣の戦友を見捨てられない動機が戦争を耐えさせる）。
        /// </summary>
        public static float FightForComradesNotCause(float cohesion, float ideologyCommitment)
        {
            float c = Mathf.Clamp01(cohesion);
            float ideo = Mathf.Clamp01(ideologyCommitment);
            // 戦友愛0.7・大義0.3の重み＝一次集団が二次集団に勝る。
            return Mathf.Clamp01(0.7f * c + 0.3f * ideo);
        }

        /// <summary>
        /// 凝集の時間発展（0..1）。共に戦い苦難を分かつほど育つ：平時（sharedCombat=0）は buildBase でゆっくり、
        /// 共闘 sharedCombat(0..1) が濃いほど (1 + sharedCombatMultiplier×sharedCombat) 倍で速く深まる（修羅場が絆を深める）。
        /// </summary>
        public static float CohesionBuildTick(float cohesion, float sharedCombat, float dt, KameradschaftParams p)
        {
            float c = Mathf.Clamp01(cohesion);
            float sc = Mathf.Clamp01(sharedCombat);
            float rate = p.buildBase * (1f + p.sharedCombatMultiplier * sc);
            return Mathf.MoveTowards(c, 1f, rate * Mathf.Max(0f, dt));
        }

        public static float CohesionBuildTick(float cohesion, float sharedCombat, float dt)
            => CohesionBuildTick(cohesion, sharedCombat, dt, KameradschaftParams.Default);

        /// <summary>
        /// 戦友の死の悲嘆（0..griefScale）。失った戦友の割合 comradesLost(0..1) に比例し、凝集の二乗でスケール
        /// ＝強い戦友愛で結ばれた集団ほど不釣り合いに深い喪失・悲嘆になる（士気・戦闘力ペナルティの初期値に使う）。
        /// </summary>
        public static float ComradeLossGrief(float cohesion, float comradesLost, KameradschaftParams p)
        {
            float c = Mathf.Clamp01(cohesion);
            float lost = Mathf.Clamp01(comradesLost);
            return c * c * lost * p.griefScale;
        }

        public static float ComradeLossGrief(float cohesion, float comradesLost)
            => ComradeLossGrief(cohesion, comradesLost, KameradschaftParams.Default);

        /// <summary>
        /// 集団崩壊の戦闘力ペナルティ（0..dissolutionPenaltyScale）。損耗率 attritionRate(0..1) で小集団が崩れると
        /// 戦闘力も崩れる。凝集が強い部隊ほど崩壊の打撃が大きい（戦友がいなくなると戦えない）＝凝集×損耗の積で
        /// スケールし、基準戦闘力から引く減算ペナルティとして使う（基準非破壊）。
        /// </summary>
        public static float GroupDissolutionPenalty(float cohesion, float attritionRate, KameradschaftParams p)
        {
            float c = Mathf.Clamp01(cohesion);
            float a = Mathf.Clamp01(attritionRate);
            return c * a * p.dissolutionPenaltyScale;
        }

        public static float GroupDissolutionPenalty(float cohesion, float attritionRate)
            => GroupDissolutionPenalty(cohesion, attritionRate, KameradschaftParams.Default);

        /// <summary>
        /// 新兵補充後の凝集（希釈・0..1）。新兵 newcomers(0..1) は既存の戦友愛に溶け込みにくく凝集を薄める
        /// ＝補充比率に応じて凝集が下がり、newcomerExclusion が高いほど（よそ者は輪に入れない）希釈が強い。
        /// 残った戦友の凝集と、薄められた新顔（凝集の (1-exclusion) 倍しか引き継がない）の加重平均。
        /// </summary>
        public static float ReplacementIntegration(float cohesion, float newcomers, KameradschaftParams p)
        {
            float c = Mathf.Clamp01(cohesion);
            float n = Mathf.Clamp01(newcomers);
            float newcomerCohesion = c * (1f - p.newcomerExclusion);
            return Mathf.Clamp01(c * (1f - n) + newcomerCohesion * n);
        }

        public static float ReplacementIntegration(float cohesion, float newcomers)
            => ReplacementIntegration(cohesion, newcomers, KameradschaftParams.Default);

        /// <summary>強い戦友愛で結ばれた部隊か（凝集が閾値以上）。</summary>
        public static bool IsBondedUnit(float primaryGroupCohesion, float threshold)
            => Mathf.Clamp01(primaryGroupCohesion) >= Mathf.Clamp01(threshold);
    }
}
