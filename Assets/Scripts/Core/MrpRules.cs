using UnityEngine;

namespace Ginei
{
    /// <summary>MRP所要量計算の調整係数（#984）。</summary>
    public readonly struct MrpParams
    {
        /// <summary>安全在庫の標準係数（需要ばらつき×√リードタイム相当の緩衝の強さ・既定1.5）。大きいほど厚いバッファ。</summary>
        public readonly float safetyFactor;
        /// <summary>計画オーダーの最小ロット（これ未満は0＝端数は積まない・既定0＝端数も発注）。</summary>
        public readonly float defaultMinOrderQty;

        public MrpParams(float safetyFactor, float defaultMinOrderQty)
        {
            this.safetyFactor = Mathf.Max(0f, safetyFactor);
            this.defaultMinOrderQty = Mathf.Max(0f, defaultMinOrderQty);
        }

        /// <summary>既定＝安全係数1.5・最小ロット0（端数も発注）。</summary>
        public static MrpParams Default => new MrpParams(1.5f, 0f);
    }

    /// <summary>
    /// MRP所要量計算＝資材所要計画の純ロジック（#984・唯一の窓口）。
    /// 総所要量から手持在庫と入荷予定を差し引いて<b>正味所要</b>を出し（手持ちで足りる分は発注しない＝負は余剰）、
    /// 計画オーダーをロットサイズで丸め、必要日からリードタイムを<b>遡って</b>発注時期を決める。
    /// 核は「逆算＝必要なものを必要な時に必要なだけ・在庫で足りる分は発注しない」＝在庫を持ちすぎず欠品もしない。
    /// 入力の総所要量は <see cref="BomRules"/>（#983・部品表展開＝艦→部品→素材の積算）の <c>Explode</c> 結果を受け取る。
    /// 在庫の出所は <see cref="ResourceStockpile"/>（勢力備蓄）、実際の発注キュー操作は <c>ProductionOrderRules</c>（同Wave並行＝発注）が担う
    /// ＝こちらは「いくつ・いつ発注すべきか」の算出のみで、キューには触れない。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MrpRules
    {
        /// <summary>
        /// 正味所要＝総所要−手持在庫−入荷予定（MRPの核・在庫で足りる分は発注しない）。
        /// 総所要から既にある在庫と既発注の入荷予定を差し引いた「これから手当てすべき量」。
        /// 負（手持ち＋入荷で総所要を上回る＝余剰）は0にクランプ＝余っていても発注はしない。各入力は負を0クランプ。
        /// </summary>
        public static float NetRequirement(float grossRequirement, float onHandInventory, float scheduledReceipts)
        {
            float gross = Mathf.Max(0f, grossRequirement);
            float onHand = Mathf.Max(0f, onHandInventory);
            float scheduled = Mathf.Max(0f, scheduledReceipts);
            return Mathf.Max(0f, gross - onHand - scheduled);
        }

        /// <summary>
        /// 計画オーダー数量＝正味所要をロットサイズで丸めた発注量（まとめ買い）。
        /// 正味所要が0以下なら発注不要で0。lotSize&gt;0 ならその倍数へ切り上げ（端数でも1ロット起こす＝まとめ買い）、
        /// lotSize&lt;=0 なら正味所要そのまま（ロットフォーロット）。minOrderQty 以上へ引き上げ（最小発注量を満たす）。
        /// </summary>
        public static float PlannedOrderQuantity(float netRequirement, float lotSize, float minOrderQty)
        {
            float net = Mathf.Max(0f, netRequirement);
            if (net <= 0f) return 0f; // 在庫で足りる＝発注しない

            float qty;
            if (lotSize > 0f)
            {
                int lots = Mathf.CeilToInt(net / lotSize); // 端数も1ロットへ切り上げ
                qty = lots * lotSize;
            }
            else
            {
                qty = net; // ロットフォーロット＝必要なだけ
            }

            float minOq = Mathf.Max(0f, minOrderQty);
            return Mathf.Max(qty, minOq);
        }

        public static float PlannedOrderQuantity(float netRequirement, float lotSize)
            => PlannedOrderQuantity(netRequirement, lotSize, 0f);

        /// <summary>
        /// 発注時期＝必要日からリードタイムを遡った発注タイミング（間に合わせるには今出すか＝逆算の核）。
        /// 必要日（その素材が要る時刻）から調達リードタイムぶん手前が「今すぐ出さねば間に合う最遅の発注日」。
        /// 負（過去＝既に手遅れ＝今すぐ出しても間に合わない）は0にクランプ＝即時発注すべき。各入力は負を0クランプ。
        /// </summary>
        public static float OrderReleaseTiming(float needDate, float leadTime)
        {
            float need = Mathf.Max(0f, needDate);
            float lead = Mathf.Max(0f, leadTime);
            return Mathf.Max(0f, need - lead); // 0＝今すぐ出さねば間に合わない
        }

        /// <summary>
        /// 安全在庫＝需要のばらつき×リードタイムで欠品を防ぐ緩衝。
        /// リードタイムが長いほど・需要が読めない（variability 大）ほど厚いバッファが要る
        /// ＝<see cref="MrpParams.safetyFactor"/>×needバラつき×√リードタイム。
        /// ばらつき0（需要が確定）なら安全在庫0＝余分な在庫を持たない。variability は0..1にクランプ。
        /// </summary>
        public static float SafetyStockRequirement(float demandVariability, float leadTime, MrpParams p)
        {
            float v = Mathf.Clamp01(demandVariability);
            float lead = Mathf.Max(0f, leadTime);
            if (v <= 0f || lead <= 0f) return 0f;
            // リードタイムが長いほどばらつきの累積が増える＝√で効かせる（標準的な安全在庫式）。
            return p.safetyFactor * v * Mathf.Pow(lead, 0.5f);
        }

        public static float SafetyStockRequirement(float demandVariability, float leadTime)
            => SafetyStockRequirement(demandVariability, leadTime, MrpParams.Default);

        /// <summary>
        /// 発注点＝在庫がここまで減ったら発注すべき水準（リードタイム需要＋安全在庫）。
        /// 平均需要×リードタイム（補充が届くまでに消費する量）に安全在庫を足す
        /// ＝在庫がこれを割ったら次の入荷が届く前に欠品する＝発注の引き金。各入力は負を0クランプ。
        /// </summary>
        public static float ReorderPoint(float averageDemand, float leadTime, float safetyStock)
        {
            float demand = Mathf.Max(0f, averageDemand);
            float lead = Mathf.Max(0f, leadTime);
            float safety = Mathf.Max(0f, safetyStock);
            return demand * lead + safety;
        }

        /// <summary>
        /// 在庫の予測残高＝期末に在庫がいくら残るか／不足するか（手持＋入荷予定−総所要）。
        /// 正なら期末に余る量、負なら不足する量（＝そのぶん正味所要が立つ）。<see cref="NetRequirement"/> と表裏
        /// （正味所要＝Max(0, −この予測残高)）。手持・入荷予定は負を0クランプ、総所要は負を0クランプ。
        /// 残高は符号付きで返す（不足を負で表す）。
        /// </summary>
        public static float InventoryProjection(float onHand, float scheduledReceipts, float grossRequirement)
        {
            float onHandC = Mathf.Max(0f, onHand);
            float scheduled = Mathf.Max(0f, scheduledReceipts);
            float gross = Mathf.Max(0f, grossRequirement);
            return onHandC + scheduled - gross; // 負＝不足
        }
    }
}
