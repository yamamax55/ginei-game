using UnityEngine;

namespace Ginei
{
    /// <summary>占領地平定（鎮静化作戦）の調整係数。</summary>
    public readonly struct PacificationParams
    {
        /// <summary>掃討で投入兵力にかかる重み（兵力1のときの分子寄与・1以上で兵力優位）。</summary>
        public readonly float forceWeight;
        /// <summary>地形が反乱勢力を守る最大倍率（密林/山岳ほど掃討が難しい・1以上）。</summary>
        public readonly float terrainShelterMax;
        /// <summary>民心獲得で民生支援にかかる重み（0..1・援助1・自制1・不満0のときの上限）。</summary>
        public readonly float aidWeight;
        /// <summary>過剰武力が反発を生む重み（0..1・強硬策の副作用の強さ）。</summary>
        public readonly float backlashWeight;
        /// <summary>抵抗勢力弱体化の最大率（0..1・掃討×民心が満点のとき削る割合）。</summary>
        public readonly float attritionMax;
        /// <summary>反発×不満が新規参加を生む重み（0..1・鎮圧が敵を生む強さ）。</summary>
        public readonly float recruitWeight;
        /// <summary>平定度で掃討・民心が押し上げる重み（0..1）。</summary>
        public readonly float gainWeight;
        /// <summary>平定度で反発が押し下げる重み（0..1）。</summary>
        public readonly float penaltyWeight;
        /// <summary>平定完了の既定閾値（0..1・これ以上で平定済み）。</summary>
        public readonly float pacifiedThreshold;

        public PacificationParams(float forceWeight, float terrainShelterMax, float aidWeight,
            float backlashWeight, float attritionMax, float recruitWeight,
            float gainWeight, float penaltyWeight, float pacifiedThreshold)
        {
            this.forceWeight = Mathf.Max(0f, forceWeight);
            this.terrainShelterMax = Mathf.Max(1f, terrainShelterMax);
            this.aidWeight = Mathf.Clamp01(aidWeight);
            this.backlashWeight = Mathf.Clamp01(backlashWeight);
            this.attritionMax = Mathf.Clamp01(attritionMax);
            this.recruitWeight = Mathf.Clamp01(recruitWeight);
            this.gainWeight = Mathf.Clamp01(gainWeight);
            this.penaltyWeight = Mathf.Clamp01(penaltyWeight);
            this.pacifiedThreshold = Mathf.Clamp01(pacifiedThreshold);
        }

        /// <summary>
        /// 既定＝兵力重み1.0・地形上限2.0・援助重み0.8・反発重み0.6・弱体化上限0.7・
        /// 新規参加重み0.5・平定加点0.6・反発減点0.4・完了閾値0.75。
        /// </summary>
        public static PacificationParams Default =>
            new PacificationParams(1.0f, 2.0f, 0.8f, 0.6f, 0.7f, 0.5f, 0.6f, 0.4f, 0.75f);
    }

    /// <summary>
    /// 占領地平定（鎮静化作戦）の純ロジック。反抗的な占領地を鎮める側の作戦解決を扱う。
    /// 掃討作戦の進捗・住民の心をつかむ民生支援・強硬策と懐柔策のバランス・抵抗勢力の弱体化・平定の完了度。
    /// <para>
    /// 分担：<see cref="InsurgencyRules"/> は反乱が組織化され成長する＝湧く側を扱う。本クラスは鎮める側＝
    /// 掃討（兵力）と民心（懐柔）で抵抗を削りつつ、過剰武力の反発が新たな敵を生むトレードオフを解いて平定度を出す。
    /// 式の核：掃討＝投入兵力/(兵力+反乱×地形)／民心＝援助×自制/(自制+不満)／反発＝過剰武力×巻き添え／
    /// 弱体化＝掃討×民心（支持基盤の剥奪）／新規参加＝反発×不満／平定度＝民心と掃討で上げ反発で下げる。
    /// </para>
    /// 全入力クランプ・乱数なし決定論・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PacificationRules
    {
        /// <summary>
        /// 掃討作戦の進捗（0..1）＝投入兵力/(投入兵力+反乱勢力×地形)。
        /// 地形 terrain（密林/山岳）は反乱勢力を匿い分母を膨らませる＝同兵力でも掃討が進みにくくなる。
        /// 反乱勢力0なら進捗1.0（掃討すべき敵がいない）。
        /// </summary>
        public static float SweepProgress(float forceCommitted, float insurgentStrength, float terrain, PacificationParams p)
        {
            float force = Mathf.Max(0f, forceCommitted) * p.forceWeight;
            float shelter = Mathf.Lerp(1f, p.terrainShelterMax, Mathf.Clamp01(terrain));
            float foe = Mathf.Max(0f, insurgentStrength) * shelter;
            float denom = force + foe;
            if (denom <= 0f) return 1f;
            return Mathf.Clamp01(force / denom);
        }

        public static float SweepProgress(float forceCommitted, float insurgentStrength, float terrain)
            => SweepProgress(forceCommitted, insurgentStrength, terrain, PacificationParams.Default);

        /// <summary>
        /// 民心の獲得（0..1）＝民生支援 civilAid × 自制/(自制+不満)。
        /// 自制 restraint（節度ある占領）が高く現地の不満 localGrievance が低いほど援助が心に届く。
        /// 不満が高いと同じ援助でも民心が伸びない（懐柔の効きを不満が殺す）。
        /// </summary>
        public static float HeartsAndMinds(float civilAid, float restraint, float localGrievance, PacificationParams p)
        {
            float aid = Mathf.Clamp01(civilAid);
            float r = Mathf.Clamp01(restraint);
            float g = Mathf.Clamp01(localGrievance);
            float denom = r + g;
            float receptivity = denom <= 0f ? 1f : r / denom;
            return Mathf.Clamp01(aid * receptivity * p.aidWeight);
        }

        public static float HeartsAndMinds(float civilAid, float restraint, float localGrievance)
            => HeartsAndMinds(civilAid, restraint, localGrievance, PacificationParams.Default);

        /// <summary>
        /// 強硬策の反発（0..1）＝過剰武力 forceUsed × 巻き添え collateralDamage × 反発重み。
        /// 武力を振るうほど、巻き添えが多いほど住民が敵に回る＝鎮圧が反感を買う副作用。
        /// </summary>
        public static float HeavyHandedBacklash(float forceUsed, float collateralDamage, PacificationParams p)
        {
            float t = Mathf.Clamp01(forceUsed) * Mathf.Clamp01(collateralDamage);
            return Mathf.Clamp01(t * p.backlashWeight);
        }

        public static float HeavyHandedBacklash(float forceUsed, float collateralDamage)
            => HeavyHandedBacklash(forceUsed, collateralDamage, PacificationParams.Default);

        /// <summary>
        /// 抵抗勢力の弱体化（0..attritionMax）＝掃討進捗 × 民心獲得（兵糧攻め＝支持基盤の剥奪）。
        /// 掃討で削り、民心を奪って反乱の海を干上げる＝両輪が揃うほど抵抗を弱められる（どちらか0なら効かない）。
        /// </summary>
        public static float InsurgentAttrition(float sweepProgress, float heartsAndMinds, PacificationParams p)
        {
            float t = Mathf.Clamp01(sweepProgress) * Mathf.Clamp01(heartsAndMinds);
            return Mathf.Clamp01(t * p.attritionMax);
        }

        public static float InsurgentAttrition(float sweepProgress, float heartsAndMinds)
            => InsurgentAttrition(sweepProgress, heartsAndMinds, PacificationParams.Default);

        /// <summary>
        /// 新規の反乱参加（0..1）＝反発 backlash × 不満 grievance × 新規参加重み。
        /// 過剰な鎮圧の反発が、くすぶる不満に火を点けて新たな反乱参加者を生む＝鎮圧が敵を増やす。
        /// </summary>
        public static float RecruitmentToInsurgency(float backlash, float grievance, PacificationParams p)
        {
            float t = Mathf.Clamp01(backlash) * Mathf.Clamp01(grievance);
            return Mathf.Clamp01(t * p.recruitWeight);
        }

        public static float RecruitmentToInsurgency(float backlash, float grievance)
            => RecruitmentToInsurgency(backlash, grievance, PacificationParams.Default);

        /// <summary>
        /// 抵抗勢力の純増減（-1..1）＝弱体化 attrition − 新規参加 recruitment。
        /// 正＝差し引きで抵抗が減る（平定が進む）／負＝鎮圧が生む新規参加が弱体化を上回り抵抗が増える（逆効果）。
        /// </summary>
        public static float NetInsurgentStrength(float attrition, float recruitment, PacificationParams p)
        {
            float net = Mathf.Clamp01(attrition) - Mathf.Clamp01(recruitment);
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float NetInsurgentStrength(float attrition, float recruitment)
            => NetInsurgentStrength(attrition, recruitment, PacificationParams.Default);

        /// <summary>
        /// 総合平定度（0..1）＝(弱体化×掃討側 + 民心×民心側) の加点を、反発で減点する。
        /// 抵抗の弱体化 insurgentAttrition と民心 heartsAndMinds で押し上げ、強硬策の反発 backlash で押し下げる
        /// ＝懐柔と掃討で地を鎮め、過剰武力で振り出しに戻すバランスの帰結。
        /// </summary>
        public static float PacificationLevel(float insurgentAttrition, float heartsAndMinds, float backlash, PacificationParams p)
        {
            float gain = (Mathf.Clamp01(insurgentAttrition) + Mathf.Clamp01(heartsAndMinds)) * 0.5f * p.gainWeight;
            float penalty = Mathf.Clamp01(backlash) * p.penaltyWeight;
            return Mathf.Clamp01(gain - penalty);
        }

        public static float PacificationLevel(float insurgentAttrition, float heartsAndMinds, float backlash)
            => PacificationLevel(insurgentAttrition, heartsAndMinds, backlash, PacificationParams.Default);

        /// <summary>平定が完了したか＝平定度 pacificationLevel が threshold 以上。</summary>
        public static bool IsPacified(float pacificationLevel, float threshold)
            => Mathf.Clamp01(pacificationLevel) >= Mathf.Clamp01(threshold);

        public static bool IsPacified(float pacificationLevel)
            => IsPacified(pacificationLevel, PacificationParams.Default.pacifiedThreshold);
    }
}
