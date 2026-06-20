using UnityEngine;

namespace Ginei
{
    /// <summary>軍紀・査問の調整係数。</summary>
    public readonly struct DisciplineParams
    {
        /// <summary>厳格さが軍紀を引き締める強さ。</summary>
        public readonly float harshnessOrderGain;
        /// <summary>厳格さが士気を削る強さ（締めすぎは兵心が離れる）。</summary>
        public readonly float harshnessMoralePenalty;
        /// <summary>抗命が起こりうる軍紀の閾値（これ未満で抗命リスクが立つ）。</summary>
        public readonly float insubordinationThreshold;
        /// <summary>人望ある士官の査問・処断が全軍士気を削る最大幅。</summary>
        public readonly float inquiryMoraleScale;

        public DisciplineParams(float harshnessOrderGain, float harshnessMoralePenalty,
                                float insubordinationThreshold, float inquiryMoraleScale)
        {
            this.harshnessOrderGain = Mathf.Max(0f, harshnessOrderGain);
            this.harshnessMoralePenalty = Mathf.Max(0f, harshnessMoralePenalty);
            this.insubordinationThreshold = Mathf.Clamp01(insubordinationThreshold);
            this.inquiryMoraleScale = Mathf.Max(0f, inquiryMoraleScale);
        }

        /// <summary>既定＝引き締め0.5・士気減0.3・抗命閾値0.4・査問士気幅0.3。</summary>
        public static DisciplineParams Default => new DisciplineParams(0.5f, 0.3f, 0.4f, 0.3f);
    }

    /// <summary>
    /// 軍紀・査問の純ロジック。軍紀（discipline 0..1）は厳格さ（harshness）で引き締まるが、
    /// 締めすぎは士気を削り兵心が離れる＝厳格さは諸刃の剣。軍紀が崩れ不満が積もると抗命が起こりうる。
    /// 人望ある士官への査問・処断（ヤン査問会型）は軍紀の体裁と引き換えに全軍の士気を大きく削る。
    /// 士気管理本体（`FleetMorale`＝Game層）とは別系統で、係数の算出のみを担う（実効値パターン・基準非破壊）。
    /// 乱数は外から与える roll で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DisciplineRules
    {
        /// <summary>
        /// 厳格さを反映した軍紀（0..1）＝現軍紀＋厳格さ×引き締め係数。締めれば確かに軍紀は上がる。
        /// </summary>
        public static float OrderAfterEnforcement(float discipline, float harshness, DisciplineParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(discipline) + Mathf.Clamp01(harshness) * p.harshnessOrderGain);
        }

        public static float OrderAfterEnforcement(float discipline, float harshness)
            => OrderAfterEnforcement(discipline, harshness, DisciplineParams.Default);

        /// <summary>厳格さの士気コスト（0..harshnessMoralePenalty）＝締めた分だけ兵心が冷える。</summary>
        public static float MoraleCostOfHarshness(float harshness, DisciplineParams p)
        {
            return Mathf.Clamp01(harshness) * p.harshnessMoralePenalty;
        }

        public static float MoraleCostOfHarshness(float harshness)
            => MoraleCostOfHarshness(harshness, DisciplineParams.Default);

        /// <summary>
        /// 抗命確率（0..1）。軍紀が閾値未満のとき、不足分×（0.5＋不満 grievance/2）で立つ。閾値以上は0。
        /// 軍紀が崩れているだけでは半分、不満が重なって最大になる。
        /// </summary>
        public static float InsubordinationRisk(float discipline, float grievance, DisciplineParams p)
        {
            float d = Mathf.Clamp01(discipline);
            if (d >= p.insubordinationThreshold) return 0f;
            float shortfall = (p.insubordinationThreshold - d) / Mathf.Max(1e-4f, p.insubordinationThreshold);
            return Mathf.Clamp01(shortfall * (0.5f + 0.5f * Mathf.Clamp01(grievance)));
        }

        public static float InsubordinationRisk(float discipline, float grievance)
            => InsubordinationRisk(discipline, grievance, DisciplineParams.Default);

        /// <summary>抗命判定。roll∈[0,1) がリスク未満なら抗命発生＝true（決定論）。</summary>
        public static bool InsubordinationOccurs(float discipline, float grievance, float roll, DisciplineParams p)
        {
            return roll < InsubordinationRisk(discipline, grievance, p);
        }

        public static bool InsubordinationOccurs(float discipline, float grievance, float roll)
            => InsubordinationOccurs(discipline, grievance, roll, DisciplineParams.Default);

        /// <summary>
        /// 査問・処断の全軍士気ペナルティ（0..inquiryMoraleScale）＝対象士官の人望（renown 0..1）に比例。
        /// 無名の士官なら波風は立たないが、人望ある士官を吊るせば全軍が冷める（ヤン査問会型）。
        /// </summary>
        public static float InquiryMoralePenalty(float officerRenown, DisciplineParams p)
        {
            return Mathf.Clamp01(officerRenown) * p.inquiryMoraleScale;
        }

        public static float InquiryMoralePenalty(float officerRenown)
            => InquiryMoralePenalty(officerRenown, DisciplineParams.Default);

        /// <summary>
        /// 軍紀の戦闘秩序倍率（0.5..1）。軍紀が高いほど命令が徹る＝1.0、崩れるほど統制が利かず半減まで落ちる。
        /// 基準値に掛けて使う（基準非破壊）。
        /// </summary>
        public static float CommandEfficiency(float discipline)
        {
            return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(discipline));
        }
    }
}
