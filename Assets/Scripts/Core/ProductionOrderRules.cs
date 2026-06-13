using UnityEngine;

namespace Ginei
{
    /// <summary>発注種別（#985）。素材は外から買う購買オーダー、部品/製品は自分で作る生産オーダー＝作るか買うか。</summary>
    public enum OrderType
    {
        購買オーダーPO, // PO＝外部サプライヤーへの発注（素材・調達）
        生産オーダーPrO, // PrO＝自社設備での製造指示（部品・製品）
    }

    /// <summary>生産設備の種別（#985）。割付先となる設備の系統。鉱業＝素材、工業＝部品/製品、船渠＝艦の最終組立。</summary>
    public enum FacilityType
    {
        鉱業, // 採掘・素材産出
        工業, // 部品/製品の製造
        船渠, // 艦の建造（最終組立）
    }

    /// <summary>
    /// 1件の生産/購買オーダー（#985・純データ）。MRPの計画オーダーを「品目・数量・どの設備・いつまで」へ具体化した発注票。
    /// 割付先設備（<see cref="facility"/>）・期日（<see cref="dueDate"/>）を持ち、能力スケジューリングで設備容量を食う。
    /// </summary>
    [System.Serializable]
    public class ProductionOrder
    {
        /// <summary>発注種別（購買PO／生産PrO）。</summary>
        public OrderType type;
        /// <summary>品目の識別子（BOM/MRPの品目）。</summary>
        public int itemId;
        /// <summary>発注数量（負にはならない）。</summary>
        public int quantity;
        /// <summary>割付先の設備種別（鉱業/工業/船渠）。</summary>
        public FacilityType facility;
        /// <summary>期日（戦略秒。これまでに完成させたい）。</summary>
        public float dueDate;
        /// <summary>重要度（0..1。前線直結・代替不能なほど高い）。</summary>
        public float criticality;

        public ProductionOrder() { }

        public ProductionOrder(OrderType type, int itemId, int quantity, FacilityType facility,
                               float dueDate, float criticality = 0f)
        {
            this.type = type;
            this.itemId = itemId;
            this.quantity = Mathf.Max(0, quantity);
            this.facility = facility;
            this.dueDate = dueDate;
            this.criticality = Mathf.Clamp01(criticality);
        }
    }

    /// <summary>
    /// 発注・生産オーダー＝PO/PrO の割付ロジック（#985・純ロジック test-first・唯一の窓口）。
    /// MRP（<c>MrpRules</c>・#984計画＝入力）の計画オーダーを、実際の生産設備（鉱業/工業/船渠）へ割り当てる
    /// ＝<b>発注の実行段階</b>：作るか買うか（<see cref="DecideOrderType"/>/<see cref="MakeOrBuyDecision"/>）・どの設備で
    /// （<see cref="AssignFacility"/>＝負荷分散）・いつ優先するか（<see cref="OrderPriority"/>）・どうまとめるか（<see cref="ConsolidateOrders"/>）・
    /// 能力をどれだけ食うか（<see cref="CapacityReservation"/>）を式に出す。
    /// 役割分担：<c>MrpRules</c>（#984＝所要計算・計画オーダー生成＝こちらの入力）／
    /// <see cref="ShipyardRules"/>（艦の就役＝こちらの後段＝発注が完成してから）／
    /// <c>CapacitySchedulingRules</c>（能力・同Wave並行＝詳細な負荷山積み・有限能力スケジュール）。
    /// 調整値は <see cref="ProductionOrderParams"/> に集約（既定 <see cref="ProductionOrderParams.Default"/>）。
    /// </summary>
    public static class ProductionOrderRules
    {
        /// <summary>
        /// 発注種別を決める（#985）。素材（原材料）は購買PO＝外から買う、部品/製品は生産PrO＝自分で作る。
        /// MRPの計画オーダーは「作るか買うか」が品目属性で分かれる＝この最初の分岐。
        /// </summary>
        public static OrderType DecideOrderType(bool itemIsRawMaterial)
        {
            return itemIsRawMaterial ? OrderType.購買オーダーPO : OrderType.生産オーダーPrO;
        }

        /// <summary>
        /// 必要設備の同種のうち最も空いている設備の添字を返す（#985・負荷分散）。
        /// <paramref name="facilityLoads"/> は候補設備の現在負荷（0..1・大きいほど混んでいる）。
        /// 最小負荷（同値は先着）を選ぶ＝オーダーを空いた設備へ流して詰まりを避ける。空/nullは-1（割付先なし）。
        /// </summary>
        public static int AssignFacility(float[] facilityLoads)
        {
            if (facilityLoads == null || facilityLoads.Length == 0) return -1;
            int best = -1;
            float bestLoad = float.PositiveInfinity;
            for (int i = 0; i < facilityLoads.Length; i++)
            {
                float load = Mathf.Max(0f, facilityLoads[i]);
                if (best < 0 || load < bestLoad)
                {
                    best = i;
                    bestLoad = load;
                }
            }
            return best;
        }

        /// <summary>
        /// 内製（PrO）か外注（PO）かを決める（#985・作るか買うか）。
        /// 自社能力に余裕があり（<paramref name="capacityAvailable"/>）かつ内製コストが（外注コスト×内製許容比）以下なら内製、
        /// そうでなければ外注（能力が無い／自社の方が割高なら買う）。比は <see cref="ProductionOrderParams.inHouseCostTolerance"/>。
        /// </summary>
        public static OrderType MakeOrBuyDecision(float inHouseCost, float purchaseCost, bool capacityAvailable,
                                                  ProductionOrderParams p)
        {
            if (!capacityAvailable) return OrderType.購買オーダーPO; // 能力がなければ買うしかない
            float inHouse = Mathf.Max(0f, inHouseCost);
            float purchase = Mathf.Max(0f, purchaseCost);
            // 内製がやや割高でも許容比までは内製（自社雇用維持・供給保全）。閾値以下なら作る。
            if (inHouse <= purchase * Mathf.Max(0f, p.inHouseCostTolerance))
                return OrderType.生産オーダーPrO;
            return OrderType.購買オーダーPO;
        }

        /// <summary>既定Params版の作るか買うか。</summary>
        public static OrderType MakeOrBuyDecision(float inHouseCost, float purchaseCost, bool capacityAvailable)
            => MakeOrBuyDecision(inHouseCost, purchaseCost, capacityAvailable, ProductionOrderParams.Default);

        /// <summary>
        /// オーダーの優先度（#985・大きいほど先に着手）。納期が近く重要なものを優先する。
        /// 緊急度＝1−余裕時間/緊急基準（期日まで近いほど1へ・期日超過は1）を重要度と重み付け合成：
        /// 緊急度×urgencyWeight＋criticality×criticalityWeight（合計1へ正規化）。0..1。
        /// </summary>
        public static float OrderPriority(float dueDate, float currentTime, float criticality, ProductionOrderParams p)
        {
            float slack = dueDate - currentTime; // 残り時間（負＝納期遅れ）
            float urgency;
            if (slack <= 0f) urgency = 1f; // 遅延は最優先
            else if (p.urgencyHorizon <= 0f) urgency = 1f;
            else urgency = Mathf.Clamp01(1f - slack / p.urgencyHorizon);

            float crit = Mathf.Clamp01(criticality);
            float weightSum = p.urgencyWeight + p.criticalityWeight;
            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01((urgency * p.urgencyWeight + crit * p.criticalityWeight) / weightSum);
        }

        /// <summary>既定Params版の優先度。</summary>
        public static float OrderPriority(float dueDate, float currentTime, float criticality)
            => OrderPriority(dueDate, currentTime, criticality, ProductionOrderParams.Default);

        /// <summary>
        /// 近い時期の発注をまとめた1ロットの合計数量を返す（#985・ロット集約）。
        /// <paramref name="quantities"/> の先頭から <paramref name="consolidationWindow"/> 件ぶんを束ねる
        /// ＝段取り回数を減らしロット効率を上げる。窓が0以下/全件なら1ロットに全合算。空/nullは0。
        /// </summary>
        public static int ConsolidateOrders(int[] quantities, int consolidationWindow)
        {
            if (quantities == null || quantities.Length == 0) return 0;
            int window = consolidationWindow <= 0 ? quantities.Length : consolidationWindow;
            window = Mathf.Min(window, quantities.Length);
            int total = 0;
            for (int i = 0; i < window; i++)
                total += Mathf.Max(0, quantities[i]);
            return total;
        }

        /// <summary>
        /// 設備能力の引き当て＝オーダーが食う能力の割合（#985・0..1）。
        /// 発注数量を設備能力で割った占有率（能力を超える発注は1＝飽和）。空き容量管理の基本量。
        /// 能力0以下は数量があれば1（容量なしを満たせない）／数量0は0。詳細な山積みは <c>CapacitySchedulingRules</c>。
        /// </summary>
        public static float CapacityReservation(int orderQty, float facilityCapacity)
        {
            int qty = Mathf.Max(0, orderQty);
            if (qty == 0) return 0f;
            if (facilityCapacity <= 0f) return 1f; // 容量ゼロを需要が叩く＝飽和
            return Mathf.Clamp01(qty / facilityCapacity);
        }
    }

    /// <summary>発注割付の調整値（#985・作るか買うかの許容比・優先度の重み・緊急基準）。</summary>
    public readonly struct ProductionOrderParams
    {
        /// <summary>内製を許容する対外注コスト比（1.0で同コストまで内製・1超で割高でも内製許容）。</summary>
        public readonly float inHouseCostTolerance;
        /// <summary>優先度における納期緊急度の重み。</summary>
        public readonly float urgencyWeight;
        /// <summary>優先度における重要度（criticality）の重み。</summary>
        public readonly float criticalityWeight;
        /// <summary>緊急度の基準時間（残りこの秒数で緊急度0＝これより先の期日は急がない）。</summary>
        public readonly float urgencyHorizon;

        public ProductionOrderParams(float inHouseCostTolerance, float urgencyWeight,
                                     float criticalityWeight, float urgencyHorizon)
        {
            this.inHouseCostTolerance = Mathf.Max(0f, inHouseCostTolerance);
            this.urgencyWeight = Mathf.Max(0f, urgencyWeight);
            this.criticalityWeight = Mathf.Max(0f, criticalityWeight);
            this.urgencyHorizon = Mathf.Max(0f, urgencyHorizon);
        }

        /// <summary>
        /// 既定＝内製許容比1.0（外注と同コストまでは内製＝自社能力があれば作る）／緊急度重み0.6・重要度重み0.4
        /// （納期主導だが重要度も効く）／緊急基準100秒（残り100秒で緊急度0・期日が近いほど1へ）。
        /// </summary>
        public static ProductionOrderParams Default =>
            new ProductionOrderParams(1.0f, 0.6f, 0.4f, 100f);
    }
}
