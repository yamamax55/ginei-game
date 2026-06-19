using UnityEngine;

namespace Ginei
{
    /// <summary>損傷艦トリアージ（修理優先配分）の調整係数。</summary>
    public readonly struct RepairPriorityParams
    {
        /// <summary>修理時間の基準（損傷100%・価値1.0の艦の所要時間）。</summary>
        public readonly float baseRepairTime;
        /// <summary>艦価値が修理時間に効く度合い（大型ほど長い・0で無効）。</summary>
        public readonly float valueTimeWeight;
        /// <summary>修理より新造が安いと判断する閾値（損傷×価値係数がこれ以上で廃棄）。</summary>
        public readonly float scrapDamageThreshold;
        /// <summary>応急修理（早期復帰）の戦力回復率（完全修理に対する割合・0..1）。</summary>
        public readonly float quickReturnRatio;

        public RepairPriorityParams(float baseRepairTime, float valueTimeWeight, float scrapDamageThreshold, float quickReturnRatio)
        {
            this.baseRepairTime = Mathf.Max(0f, baseRepairTime);
            this.valueTimeWeight = Mathf.Clamp01(valueTimeWeight);
            this.scrapDamageThreshold = Mathf.Clamp01(scrapDamageThreshold);
            this.quickReturnRatio = Mathf.Clamp01(quickReturnRatio);
        }

        /// <summary>既定＝基準10時間・価値の時間寄与0.5・廃棄閾値0.85・応急復帰0.6。</summary>
        public static RepairPriorityParams Default => new RepairPriorityParams(10f, 0.5f, 0.85f, 0.6f);
    }

    /// <summary>
    /// 損傷艦のトリアージ（修理優先）の純ロジック。会戦で多数の艦が損傷したとき、
    /// 限られた修理能力（ドック・工作艦）をどの艦に優先配分するかを決める＝軽傷を早く戻すか、
    /// 主力艦を時間をかけて直すか、修理不能（新造の方が安い）は廃棄するか。
    /// 修理そのもの（戦力回復の積分＝<see cref="RepairRules"/>）・定期整備（<see cref="MaintenanceCycleRules"/>）・
    /// 新造（<see cref="ShipyardRules"/>）とは別＝こちらは「どの損傷艦を先に直すか」の優先順位付けに特化する。
    /// 盤面非依存の plain 引数・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RepairPriorityRules
    {
        /// <summary>
        /// 修理に要る時間。damageSeverity(0..1 損傷度)に比例し、shipValue(艦の価値・大型ほど大)で
        /// valueTimeWeight ぶん延びる＝大型艦・重損傷ほど長い。
        /// </summary>
        public static float RepairTime(float damageSeverity, float shipValue, RepairPriorityParams p)
        {
            float dmg = Mathf.Clamp01(damageSeverity);
            float val = Mathf.Max(0f, shipValue);
            // 価値による時間倍率＝1 + weight×value（大型ほど長い）
            float valueFactor = 1f + p.valueTimeWeight * val;
            return p.baseRepairTime * dmg * valueFactor;
        }

        public static float RepairTime(float damageSeverity, float shipValue)
            => RepairTime(damageSeverity, shipValue, RepairPriorityParams.Default);

        /// <summary>
        /// 修理優先度スコア。価値が高く（取り戻す戦力が大）、早く戻せる（修理時間が短い）艦ほど高い。
        /// ＝shipValue / (修理時間 + 1)。0以上。重損傷の主力よりも軽傷の主力・軽傷の駆逐を優先する地金。
        /// </summary>
        public static float TriageScore(float damageSeverity, float shipValue, float repairTime)
        {
            float val = Mathf.Max(0f, shipValue);
            float t = Mathf.Max(0f, repairTime);
            // damageSeverity は repairTime に既に織り込まれている前提だが、未損傷(=修理不要)を弾く
            if (Mathf.Clamp01(damageSeverity) <= 0f) return 0f;
            return val / (t + 1f);
        }

        /// <summary>
        /// 修理より新造が安いか（廃棄判断）。損傷度×（価値の重さ）が閾値を超えると経済的全損
        /// ＝重損傷で高価な艦ほど「直すより造り直した方が安い」。
        /// </summary>
        public static bool BeyondEconomicRepair(float damageSeverity, float shipValue, RepairPriorityParams p)
        {
            float dmg = Mathf.Clamp01(damageSeverity);
            float val = Mathf.Max(0f, shipValue);
            // 価値が高いほど少し甘く（直す価値はある）が、重損傷では効かなくなる。
            // 修理コスト係数＝損傷度×（1 - 価値による据え置き）… 単純化して dmg を主因に置く。
            float repairCostFactor = dmg * (1f + 0.1f * val);
            return repairCostFactor >= p.scrapDamageThreshold;
        }

        public static bool BeyondEconomicRepair(float damageSeverity, float shipValue)
            => BeyondEconomicRepair(damageSeverity, shipValue, RepairPriorityParams.Default);

        /// <summary>
        /// 応急修理で早く戻すか、完全修理か。-1=完全修理（時間をかけて全回復）/ +1=応急修理（早期復帰）。
        /// urgency(0..1 戦線の逼迫度)が高いほど応急へ、損傷が浅いほど応急で十分＝+側へ寄る。
        /// </summary>
        public static float QuickReturnVsFullRepair(float damageSeverity, float urgency)
        {
            float dmg = Mathf.Clamp01(damageSeverity);
            float urg = Mathf.Clamp01(urgency);
            // 逼迫＋軽傷 → 応急(+1)、平穏＋重傷 → 完全(-1)
            float quick = urg * (1f - dmg);   // 応急が望ましい度 0..1
            float full = (1f - urg) * dmg;    // 完全が望ましい度 0..1
            return Mathf.Clamp(quick - full, -1f, 1f);
        }

        /// <summary>ドックの稼働状況＝修理中の艦数/ドック容量（0..上限なしだが実質0..1付近・1で満杯）。容量0なら満杯(1)。</summary>
        public static float DockUtilization(float shipsInRepair, float dockCapacity)
        {
            float ships = Mathf.Max(0f, shipsInRepair);
            float cap = Mathf.Max(0f, dockCapacity);
            if (cap <= 0f) return 1f;
            return ships / cap;
        }

        /// <summary>
        /// 艦隊の戦力回復速度（再編成）。修理スループット×dt ぶん戻すが、損傷艦隊規模を超えない。
        /// ＝この tick で戦線へ戻る戦力量。
        /// </summary>
        public static float FleetReconstitution(float repairThroughput, float damagedFleetSize, float dt)
        {
            float thru = Mathf.Max(0f, repairThroughput);
            float size = Mathf.Max(0f, damagedFleetSize);
            float t = Mathf.Max(0f, dt);
            return Mathf.Min(size, thru * t);
        }

        /// <summary>
        /// 廃棄艦からの部品取り（共食い整備）で得られる修理加速倍率。廃棄艦が多く修理対象が少ないほど
        /// 部品が潤沢＝速くなる。1.0(なし)〜上限。修理対象0なら部品の使い道なし(1.0)。
        /// </summary>
        public static float CannibalizationGain(float scrapShips, float repairableShips)
        {
            float scrap = Mathf.Max(0f, scrapShips);
            float repairable = Mathf.Max(0f, repairableShips);
            if (repairable <= 0f) return 1f;
            // 廃棄1隻あたり修理対象へ部品供給。比 scrap/repairable を最大+1.0(=2倍速)で頭打ち。
            float ratio = scrap / repairable;
            return 1f + Mathf.Clamp01(ratio);
        }

        /// <summary>修理する価値があるか＝トリアージスコアが閾値以上。</summary>
        public static bool IsShipWorthRepairing(float triageScore, float threshold)
        {
            return triageScore >= threshold;
        }
    }
}
