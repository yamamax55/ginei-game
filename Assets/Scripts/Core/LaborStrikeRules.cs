using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 労働争議（ストライキ）の調整係数。賃金停滞・労働条件から不満が湧き、
    /// 組織化と連帯がスト参加へ、参加が枢要産業を止め、経営は弾圧か譲歩かを迫られ、
    /// 長期化は労働者を困窮させ、最後に妥結し、軍需産業が止まれば戦力供給へ波及する。
    /// </summary>
    public readonly struct LaborStrikeParams
    {
        /// <summary>賃金停滞の不満への寄与（0..1・賃金が伸びないことの怒り）。</summary>
        public readonly float wageWeight;
        /// <summary>労働条件悪化の不満への寄与（0..1・過酷さ・危険の怒り）。</summary>
        public readonly float conditionWeight;
        /// <summary>団結ゲイン（不満×組織化の自由を団結へ増幅）。</summary>
        public readonly float unionGain;
        /// <summary>連帯ゲイン（団結×連帯圧力を参加へ増幅）。</summary>
        public readonly float solidarityGain;
        /// <summary>枢要産業の損害増幅（枢要なほど止まると痛い）。</summary>
        public readonly float criticalityGain;
        /// <summary>弾圧の対応への寄与（経営側の力での押さえ込み）。</summary>
        public readonly float repressionWeight;
        /// <summary>譲歩の対応への寄与（経営側の懐の深さ）。</summary>
        public readonly float concessionWeight;
        /// <summary>困窮の鋭さ（スト期間が効く速さ）。</summary>
        public readonly float hardshipSharpness;
        /// <summary>妥結における困窮の重み（飢えると折れやすい）。</summary>
        public readonly float hardshipWeight;
        /// <summary>軍需波及の増幅（軍需比率が高いほど戦力供給を直撃）。</summary>
        public readonly float warImpactGain;

        public LaborStrikeParams(float wageWeight, float conditionWeight,
                                 float unionGain, float solidarityGain,
                                 float criticalityGain, float repressionWeight,
                                 float concessionWeight, float hardshipSharpness,
                                 float hardshipWeight, float warImpactGain)
        {
            this.wageWeight = Mathf.Clamp01(wageWeight);
            this.conditionWeight = Mathf.Clamp01(conditionWeight);
            this.unionGain = Mathf.Clamp(unionGain, 0f, 2f);
            this.solidarityGain = Mathf.Clamp(solidarityGain, 0f, 2f);
            this.criticalityGain = Mathf.Clamp(criticalityGain, 0f, 2f);
            this.repressionWeight = Mathf.Clamp01(repressionWeight);
            this.concessionWeight = Mathf.Clamp01(concessionWeight);
            this.hardshipSharpness = Mathf.Clamp(hardshipSharpness, 0f, 4f);
            this.hardshipWeight = Mathf.Clamp01(hardshipWeight);
            this.warImpactGain = Mathf.Clamp(warImpactGain, 0f, 2f);
        }

        /// <summary>既定＝賃金0.6・条件0.4・団結1.2・連帯1.1・枢要1.3・弾圧0.6・譲歩0.5・困窮鋭さ1・困窮重み0.5・軍需1.2。</summary>
        public static LaborStrikeParams Default
            => new LaborStrikeParams(0.6f, 0.4f, 1.2f, 1.1f, 1.3f, 0.6f, 0.5f, 1f, 0.5f, 1.2f);
    }

    /// <summary>
    /// 労働争議の純ロジック＝不満→組織化→スト→生産打撃→対応→困窮→妥結→軍需波及。
    /// 既存 <see cref="StrikeRules"/>（交渉力・我慢比べの妥結賃金）とは別系統で、
    /// こちらは不満の源泉から軍需波及までの一連の連鎖を扱う。乱数なしの決定論。
    /// 実効値パターン（入力を壊さずローカルに合成）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class LaborStrikeRules
    {
        /// <summary>
        /// 労働者の不満（0..1）＝賃金停滞×条件悪化の加重平均。重みは正規化し、
        /// どちらかが0でも他方が立てば不満は残る。
        /// </summary>
        public static float WorkerGrievance(float wageStagnation, float workConditions, LaborStrikeParams p)
        {
            float wage = Mathf.Clamp01(wageStagnation);
            float cond = Mathf.Clamp01(workConditions);
            float wsum = p.wageWeight + p.conditionWeight;
            if (wsum <= 0f) return 0f;
            return Mathf.Clamp01((wage * p.wageWeight + cond * p.conditionWeight) / wsum);
        }

        public static float WorkerGrievance(float wageStagnation, float workConditions)
            => WorkerGrievance(wageStagnation, workConditions, LaborStrikeParams.Default);

        /// <summary>
        /// 団結（0..1）＝不満×組織化の自由×団結ゲイン。組織する自由が無ければ
        /// 不満は団結に結実しない（弾圧的な体制ほど union は育たない）。
        /// </summary>
        public static float Unionization(float workerGrievance, float organizingFreedom, LaborStrikeParams p)
        {
            float g = Mathf.Clamp01(workerGrievance);
            float free = Mathf.Clamp01(organizingFreedom);
            return Mathf.Clamp01(g * free * p.unionGain);
        }

        public static float Unionization(float workerGrievance, float organizingFreedom)
            => Unionization(workerGrievance, organizingFreedom, LaborStrikeParams.Default);

        /// <summary>
        /// スト参加率（0..1）＝団結×連帯圧力×連帯ゲイン。連帯（仲間が立つ圧力）が
        /// 個々の躊躇を超えさせる＝団結だけでは止まらず連帯で参加が広がる。
        /// </summary>
        public static float StrikeParticipation(float unionization, float solidarityPressure, LaborStrikeParams p)
        {
            float u = Mathf.Clamp01(unionization);
            float sol = Mathf.Clamp01(solidarityPressure);
            return Mathf.Clamp01(u * sol * p.solidarityGain);
        }

        public static float StrikeParticipation(float unionization, float solidarityPressure)
            => StrikeParticipation(unionization, solidarityPressure, LaborStrikeParams.Default);

        /// <summary>
        /// 生産への打撃（0..1）＝スト参加×（産業の枢要度を criticalityGain で増幅）。
        /// 枢要な産業が止まるほど生産損は大きく効く＝同じ参加率でも痛みが違う。
        /// </summary>
        public static float ProductionLoss(float strikeParticipation, float sectorCriticality, LaborStrikeParams p)
        {
            float part = Mathf.Clamp01(strikeParticipation);
            float crit = Mathf.Clamp01(sectorCriticality);
            return Mathf.Clamp01(part * Mathf.Clamp01(crit * p.criticalityGain));
        }

        public static float ProductionLoss(float strikeParticipation, float sectorCriticality)
            => ProductionLoss(strikeParticipation, sectorCriticality, LaborStrikeParams.Default);

        /// <summary>
        /// 経営側の対応（-1..1）＝譲歩×概念重み − 弾圧×弾圧重み。
        /// 正＝懐柔（譲歩寄り）／負＝強硬（弾圧寄り）。意志と余力の綱引き。
        /// </summary>
        public static float EmployerResponse(float repressionWillingness, float concessionCapacity, LaborStrikeParams p)
        {
            float rep = Mathf.Clamp01(repressionWillingness);
            float con = Mathf.Clamp01(concessionCapacity);
            float v = con * p.concessionWeight - rep * p.repressionWeight;
            return Mathf.Clamp(v, -1f, 1f);
        }

        public static float EmployerResponse(float repressionWillingness, float concessionCapacity)
            => EmployerResponse(repressionWillingness, concessionCapacity, LaborStrikeParams.Default);

        /// <summary>
        /// 労働者の困窮（0..1）＝スト期間/(1+スト資金)を鋭さで調整。長引くほど苦しく、
        /// スト資金（持久力）が分母で困窮を和らげる＝資金が時間を買う。
        /// </summary>
        public static float StrikerHardship(float strikeDuration, float strikeFund, LaborStrikeParams p)
        {
            float dur = Mathf.Max(0f, strikeDuration);
            float fund = Mathf.Max(0f, strikeFund);
            float raw = dur / (1f + fund);
            return Mathf.Clamp01(raw * p.hardshipSharpness);
        }

        public static float StrikerHardship(float strikeDuration, float strikeFund)
            => StrikerHardship(strikeDuration, strikeFund, LaborStrikeParams.Default);

        /// <summary>
        /// 妥結の度合い（-1..1）＝不満×経営対応 − 困窮×困窮重み。
        /// 経営が懐柔的（response 正）で不満が強いほど労働側に有利な妥結（正）、
        /// 困窮が深い／対応が強硬（response 負）なら労働側が折れる（負）。
        /// </summary>
        public static float Settlement(float workerGrievance, float employerResponse, float strikerHardship, LaborStrikeParams p)
        {
            float g = Mathf.Clamp01(workerGrievance);
            float resp = Mathf.Clamp(employerResponse, -1f, 1f);
            float hard = Mathf.Clamp01(strikerHardship);
            float v = g * resp - hard * p.hardshipWeight;
            return Mathf.Clamp(v, -1f, 1f);
        }

        public static float Settlement(float workerGrievance, float employerResponse, float strikerHardship)
            => Settlement(workerGrievance, employerResponse, strikerHardship, LaborStrikeParams.Default);

        /// <summary>
        /// 軍需生産への波及（0..1）＝生産打撃×軍需比率×軍需増幅。
        /// 軍需産業のストは戦力供給（FleetPool 補充）を直撃する。
        /// </summary>
        public static float WarProductionImpact(float productionLoss, float militarySectorShare, LaborStrikeParams p)
        {
            float loss = Mathf.Clamp01(productionLoss);
            float share = Mathf.Clamp01(militarySectorShare);
            return Mathf.Clamp01(loss * share * p.warImpactGain);
        }

        public static float WarProductionImpact(float productionLoss, float militarySectorShare)
            => WarProductionImpact(productionLoss, militarySectorShare, LaborStrikeParams.Default);
    }
}
