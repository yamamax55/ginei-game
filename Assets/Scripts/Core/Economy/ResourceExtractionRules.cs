using UnityEngine;

namespace Ginei
{
    /// <summary>資源採掘（鉱物資源の採取と枯渇）の調整係数。</summary>
    public readonly struct ResourceExtractionParams
    {
        /// <summary>採掘ペースの基準スケール（品位×設備×労働力に掛ける全体倍率）。</summary>
        public readonly float rateScale;
        /// <summary>労働力の逓減指数（人手を増やすほど効果は逓減＝1未満で収穫逓減）。</summary>
        public readonly float laborExponent;
        /// <summary>枯渇進行のペース感度（採掘ペースが速いほど早く掘り尽くす）。</summary>
        public readonly float depletionPace;
        /// <summary>採掘コストの基準値（浅く高品位な鉱床でも掛かる最低コスト）。</summary>
        public readonly float costBase;
        /// <summary>低品位ぶんのコスト増（品位が低いほど精錬・選鉱で割高）。</summary>
        public readonly float lowGradeCost;
        /// <summary>枯渇ぶんのコスト増（深く掘るほど割高＝枯渇が進むと急騰）。</summary>
        public readonly float depletionCost;
        /// <summary>環境負荷の基準スケール（採掘ペースに掛ける）。</summary>
        public readonly float impactScale;
        /// <summary>環境技術が負荷を緩和する最大割合（1で完全相殺はせず下限を残す）。</summary>
        public readonly float mitigationCap;
        /// <summary>希少資源プレミアムの基準倍率（希少性×戦略価値に掛ける）。</summary>
        public readonly float premiumScale;

        public ResourceExtractionParams(float rateScale, float laborExponent, float depletionPace,
            float costBase, float lowGradeCost, float depletionCost,
            float impactScale, float mitigationCap, float premiumScale)
        {
            this.rateScale = Mathf.Max(0f, rateScale);
            this.laborExponent = Mathf.Clamp01(laborExponent);
            this.depletionPace = Mathf.Max(0f, depletionPace);
            this.costBase = Mathf.Max(0f, costBase);
            this.lowGradeCost = Mathf.Max(0f, lowGradeCost);
            this.depletionCost = Mathf.Max(0f, depletionCost);
            this.impactScale = Mathf.Max(0f, impactScale);
            this.mitigationCap = Mathf.Clamp01(mitigationCap);
            this.premiumScale = Mathf.Max(0f, premiumScale);
        }

        /// <summary>
        /// 既定＝ペース倍率1.0・労働逓減0.5（人手は収穫逓減）・枯渇ペース1.0・
        /// コスト基準0.2＋低品位増0.5＋枯渇増0.6（深く掘るほど急騰）・
        /// 環境負荷スケール1.0＋環境技術の緩和上限0.9（完全相殺はせず1割残る）・プレミアム倍率2.0。
        /// </summary>
        public static ResourceExtractionParams Default =>
            new ResourceExtractionParams(1f, 0.5f, 1f, 0.2f, 0.5f, 0.6f, 1f, 0.9f, 2f);
    }

    /// <summary>
    /// 資源採掘（鉱物資源の採取と枯渇）の純ロジック。惑星・小惑星の鉱床から鉱物を採取し、
    /// 採掘ペースを上げるほど早く枯渇する＝採取と枯渇のトレードオフが核。鉱床の品位・採掘設備・労働力で
    /// 採掘ペースが決まり、品位の低下と枯渇の進行でコストが上がり（深く掘るほど高い）、採掘ペースに比例して
    /// 環境負荷が生じる（環境技術で緩和）。希少性×戦略価値で希少資源にプレミアムが乗り、産出×単価−コストで純益を出す。
    /// <para>分担：<see cref="AsteroidMiningRules"/> は宇宙採掘（小惑星・ガス＝探鉱の当たり外れ・打ち上げ費）。
    /// 本クラスは鉱床一般の採取と枯渇のダイナミクス（品位低下・枯渇進行・環境負荷・希少プレミアム）に特化。
    /// 産出は希少資源#178/資源#92 へ。</para>
    /// 純ロジック（非 MonoBehaviour・乱数なし決定論・実効値パターン・test-first）。
    /// </summary>
    public static class ResourceExtractionRules
    {
        /// <summary>
        /// 採掘ペース（≥0）＝鉱床品位(0..1)×採掘設備(0..1)×労働力^laborExponent×rateScale。
        /// 高品位・高性能設備・十分な労働力で速いが、労働力は収穫逓減（指数で逓減）。
        /// </summary>
        public static float ExtractionRate(float depositGrade, float miningEquipment, float laborForce, ResourceExtractionParams p)
        {
            float grade = Mathf.Clamp01(depositGrade);
            float equip = Mathf.Clamp01(miningEquipment);
            float labor = Mathf.Max(0f, laborForce);
            float laborFactor = Mathf.Pow(labor, p.laborExponent);
            return Mathf.Max(0f, grade * equip * laborFactor * p.rateScale);
        }

        public static float ExtractionRate(float depositGrade, float miningEquipment, float laborForce)
            => ExtractionRate(depositGrade, miningEquipment, laborForce, ResourceExtractionParams.Default);

        /// <summary>
        /// 枯渇の進行(0..1)＝(累積採掘＋この期の採掘ペース×depletionPace)÷鉱床規模。
        /// 採掘ペースが速いほど早く掘り尽くす（累積に当期ぶんを上乗せ）。鉱床規模0以下は完全枯渇=1。
        /// </summary>
        public static float DepletionProgress(float extractionRate, float depositSize, float cumulativeExtracted, ResourceExtractionParams p)
        {
            float size = Mathf.Max(0f, depositSize);
            if (size <= 0f) return 1f;
            float rate = Mathf.Max(0f, extractionRate);
            float cumulative = Mathf.Max(0f, cumulativeExtracted);
            float extracted = cumulative + rate * p.depletionPace;
            return Mathf.Clamp01(extracted / size);
        }

        public static float DepletionProgress(float extractionRate, float depositSize, float cumulativeExtracted)
            => DepletionProgress(extractionRate, depositSize, cumulativeExtracted, ResourceExtractionParams.Default);

        /// <summary>
        /// 採掘コスト(≥0)＝costBase＋低品位ぶん((1−品位)×lowGradeCost)＋枯渇ぶん(枯渇進行×depletionCost)。
        /// 品位が低いほど精錬で割高、枯渇が進むほど深く掘って割高＝終盤に急騰する。
        /// </summary>
        public static float ExtractionCost(float depositGrade, float depletionProgress, ResourceExtractionParams p)
        {
            float grade = Mathf.Clamp01(depositGrade);
            float depletion = Mathf.Clamp01(depletionProgress);
            float lowGrade = (1f - grade) * p.lowGradeCost;
            float deep = depletion * p.depletionCost;
            return Mathf.Max(0f, p.costBase + lowGrade + deep);
        }

        public static float ExtractionCost(float depositGrade, float depletionProgress)
            => ExtractionCost(depositGrade, depletionProgress, ResourceExtractionParams.Default);

        /// <summary>残存埋蔵量(≥0)＝鉱床規模−累積採掘。掘り尽くせば0で下げ止まる。</summary>
        public static float RemainingReserves(float depositSize, float cumulativeExtracted, ResourceExtractionParams p)
        {
            float size = Mathf.Max(0f, depositSize);
            float cumulative = Mathf.Max(0f, cumulativeExtracted);
            return Mathf.Max(0f, size - cumulative);
        }

        public static float RemainingReserves(float depositSize, float cumulativeExtracted)
            => RemainingReserves(depositSize, cumulativeExtracted, ResourceExtractionParams.Default);

        /// <summary>
        /// 環境負荷(0..1)＝採掘ペース×impactScale×(1−環境技術緩和)。環境技術(mitigationTech 0..1)は
        /// mitigationCap を上限に負荷を緩和するが完全には消せない（下限が残る）。掘れば掘るほど環境が傷む。
        /// </summary>
        public static float EnvironmentalImpact(float extractionRate, float mitigationTech, ResourceExtractionParams p)
        {
            float rate = Mathf.Max(0f, extractionRate);
            float mitigation = Mathf.Clamp01(mitigationTech) * p.mitigationCap;
            float gross = rate * p.impactScale;
            return Mathf.Clamp01(gross * (1f - mitigation));
        }

        public static float EnvironmentalImpact(float extractionRate, float mitigationTech)
            => EnvironmentalImpact(extractionRate, mitigationTech, ResourceExtractionParams.Default);

        /// <summary>
        /// 希少資源プレミアム(≥0)＝希少性(0..1)×戦略価値(0..1)×premiumScale。
        /// 希少かつ戦略的に重要な資源ほど単価が跳ね上がる（レアメタル・希少元素）。
        /// </summary>
        public static float ResourcePremium(float scarcity, float strategicValue, ResourceExtractionParams p)
        {
            float s = Mathf.Clamp01(scarcity);
            float v = Mathf.Clamp01(strategicValue);
            return Mathf.Max(0f, s * v * p.premiumScale);
        }

        public static float ResourcePremium(float scarcity, float strategicValue)
            => ResourcePremium(scarcity, strategicValue, ResourceExtractionParams.Default);

        /// <summary>
        /// 採掘の純益(−1..1)＝産出(採掘ペース)×単価(1＋プレミアム)−採掘コスト を採掘ペース基準で正規化。
        /// 高プレミアム資源なら低品位でも採算が合い、枯渇でコストが嵩むと赤字に沈む。採掘ペース0なら産出なし＝
        /// 純益0（コストは固定費として別途扱い＝ここでは産出由来の損益のみ）。
        /// </summary>
        public static float NetExtractionProfit(float extractionRate, float resourcePremium, float extractionCost, ResourceExtractionParams p)
        {
            float rate = Mathf.Max(0f, extractionRate);
            if (rate <= 0f) return 0f;
            float premium = Mathf.Max(0f, resourcePremium);
            float cost = Mathf.Max(0f, extractionCost);
            float revenue = rate * (1f + premium);
            float profit = revenue - cost;
            // 産出規模で正規化＝採掘量に対する採算性（−1..1）。
            return Mathf.Clamp(profit / rate, -1f, 1f);
        }

        public static float NetExtractionProfit(float extractionRate, float resourcePremium, float extractionCost)
            => NetExtractionProfit(extractionRate, resourcePremium, extractionCost, ResourceExtractionParams.Default);

        /// <summary>鉱床が枯渇したか＝残存埋蔵量が閾値以下（採掘続行不能）。</summary>
        public static bool IsDepositExhausted(float remainingReserves, float threshold)
            => Mathf.Max(0f, remainingReserves) <= Mathf.Max(0f, threshold);

        /// <summary>既定閾値（残存1.0以下で枯渇とみなす）委譲版。</summary>
        public static bool IsDepositExhausted(float remainingReserves)
            => IsDepositExhausted(remainingReserves, 1f);
    }
}
