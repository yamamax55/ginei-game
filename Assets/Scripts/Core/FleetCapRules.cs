using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊配備上限（指揮容量）の調整係数。</summary>
    public readonly struct FleetCapParams
    {
        /// <summary>統率力1あたりの指揮容量の伸び（統率0でも最低限の容量を持つ＝base＋leadership×scale）。</summary>
        public readonly float capacityPerLeadership;
        /// <summary>統率0でも得られる基礎指揮容量（最も無能な提督でも小隊は率いる）。</summary>
        public readonly float baseCapacity;
        /// <summary>階級tier1段あたりの容量倍率の伸び（大将ほど大艦隊を率いる＝1＋tier×これ）。</summary>
        public readonly float rankCapacityScale;
        /// <summary>1艦あたりの必要指揮容量の下限（capPerShip がこれ未満でも0除算しない）。</summary>
        public readonly float minCapPerShip;
        /// <summary>階級による艦数上限の基準（tier1あたり rankCapPerTier 隻まで＝中将は艦隊・元帥は軍集団）。</summary>
        public readonly float rankCapPerTier;
        /// <summary>超過1隻あたりの統制低下率（容量を超えて率いると統制が落ちる）。</summary>
        public readonly float overCapacityPenaltyPerShip;

        public FleetCapParams(float capacityPerLeadership, float baseCapacity, float rankCapacityScale,
            float minCapPerShip, float rankCapPerTier, float overCapacityPenaltyPerShip)
        {
            this.capacityPerLeadership = Mathf.Max(0f, capacityPerLeadership);
            this.baseCapacity = Mathf.Max(0f, baseCapacity);
            this.rankCapacityScale = Mathf.Max(0f, rankCapacityScale);
            this.minCapPerShip = Mathf.Max(0.0001f, minCapPerShip);
            this.rankCapPerTier = Mathf.Max(0f, rankCapPerTier);
            this.overCapacityPenaltyPerShip = Mathf.Max(0f, overCapacityPenaltyPerShip);
        }

        /// <summary>既定＝統率係数2.0・基礎容量20・階級倍率0.25/tier・必要容量下限1・階級上限10隻/tier・超過ペナルティ0.1/隻。</summary>
        public static FleetCapParams Default => new FleetCapParams(2f, 20f, 0.25f, 1f, 10f, 0.1f);
    }

    /// <summary>
    /// 配備可能艦数＝指揮容量÷必要容量・階級と二重（FCS.Cap・#1067）の純ロジック。提督が指揮できる艦数には上限があり、
    /// 「指揮容量（統率×階級）÷1艦あたり必要容量」だけ配備でき、さらに階級による艦数上限と二重にかかる＝厳しい方が効く。
    /// 分担：`AdmiralData`(統率)＝容量の源／`RankSystem`(階級tier)＝容量倍率＋艦数上限の階級側／本クラス＝容量制約の計算窓口。
    /// `OrderOfBattle`(編制ツリー＝梯団の所属)とは別＝こちらは「率いきれるか」の指揮容量の制約。全入力クランプ・乱数なし決定論・基準値非破壊。test-first。
    /// </summary>
    public static class FleetCapRules
    {
        /// <summary>
        /// 指揮容量＝（基礎容量＋統率×統率係数）×階級倍率（1＋tier×階級倍率）。統率力×階級＝大将ほど大艦隊を率いる。
        /// </summary>
        public static float CommandCapacity(float leadership, int rankTier, FleetCapParams prm)
        {
            float lead = Mathf.Clamp(leadership, 0f, 100f);
            int tier = Mathf.Max(0, rankTier);
            float baseCap = prm.baseCapacity + lead * prm.capacityPerLeadership;
            float rankMul = 1f + tier * prm.rankCapacityScale;
            return baseCap * rankMul;
        }

        /// <summary>指揮容量（既定Params版）。</summary>
        public static float CommandCapacity(float leadership, int rankTier)
            => CommandCapacity(leadership, rankTier, FleetCapParams.Default);

        /// <summary>
        /// 配備可能艦数＝指揮容量÷1艦あたり必要容量（capPerShip＝大きい艦ほど多く容量を食う＝率いられる隻数が減る）。
        /// </summary>
        public static int DeployableShips(float commandCapacity, float capPerShip, FleetCapParams prm)
        {
            float cap = Mathf.Max(0f, commandCapacity);
            float per = Mathf.Max(prm.minCapPerShip, capPerShip);
            return Mathf.FloorToInt(cap / per);
        }

        /// <summary>配備可能艦数（既定Params版）。</summary>
        public static int DeployableShips(float commandCapacity, float capPerShip)
            => DeployableShips(commandCapacity, capPerShip, FleetCapParams.Default);

        /// <summary>
        /// 階級による艦数上限＝tier×rankCapPerTier（中将は艦隊規模・元帥は軍集団規模＝二重の制約の階級側）。
        /// </summary>
        public static int RankCapLimit(int rankTier, FleetCapParams prm)
            => Mathf.Max(0, rankTier) * Mathf.FloorToInt(prm.rankCapPerTier);

        /// <summary>階級による艦数上限（既定Params版）。</summary>
        public static int RankCapLimit(int rankTier)
            => RankCapLimit(rankTier, FleetCapParams.Default);

        /// <summary>
        /// 実効配備数＝容量制約（DeployableShips）と階級制約（RankCapLimit）のmin＝二重にかかる厳しい方が艦数を決める。
        /// 「指揮には容量の限界がある＝能力と階級の二重の上限が配備艦数を決める」を式に出す中核API。
        /// </summary>
        public static int EffectiveDeployable(float commandCapacity, float capPerShip, int rankTier, FleetCapParams prm)
        {
            int byCapacity = DeployableShips(commandCapacity, capPerShip, prm);
            int byRank = RankCapLimit(rankTier, prm);
            return Mathf.Min(byCapacity, byRank);
        }

        /// <summary>実効配備数（既定Params版）。</summary>
        public static int EffectiveDeployable(float commandCapacity, float capPerShip, int rankTier)
            => EffectiveDeployable(commandCapacity, capPerShip, rankTier, FleetCapParams.Default);

        /// <summary>配備上限内か（配備艦数が配備可能数以下＝指揮しきれている）。超過は統制崩壊。</summary>
        public static bool IsWithinCap(int assignedShips, int deployableShips)
            => Mathf.Max(0, assignedShips) <= Mathf.Max(0, deployableShips);

        /// <summary>
        /// 超過ペナルティ＝容量を超えて率いた1隻ごとに統制が落ちる（0..1の能力倍率＝1.0で無傷）。
        /// 上限内なら1.0、超過分×overCapacityPenaltyPerShip ぶん低下し下限0＝指揮の限界。
        /// </summary>
        public static float OverCapacityPenalty(int assignedShips, int deployableShips, FleetCapParams prm)
        {
            int assigned = Mathf.Max(0, assignedShips);
            int deployable = Mathf.Max(0, deployableShips);
            if (assigned <= deployable) return 1f;
            int over = assigned - deployable;
            return Mathf.Clamp01(1f - over * prm.overCapacityPenaltyPerShip);
        }

        /// <summary>超過ペナルティ（既定Params版）。</summary>
        public static float OverCapacityPenalty(int assignedShips, int deployableShips)
            => OverCapacityPenalty(assignedShips, deployableShips, FleetCapParams.Default);
    }
}
