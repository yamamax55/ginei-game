using UnityEngine;

namespace Ginei
{
    /// <summary>規格化（共通規格の採用）の調整係数。</summary>
    public readonly struct StandardizationParams
    {
        /// <summary>採用度1のときの規格価値の最大スケール（メトカーフ的価値の天井）。</summary>
        public readonly float valueScale;
        /// <summary>採用度1のとき得られる輸送コスト低減率の最大値（0..1）。</summary>
        public readonly float maxCostReduction;
        /// <summary>採用が自己強化する基準速度（採用度0.5・誘因1・互換コスト0のとき per dt）。</summary>
        public readonly float baseAdoptionRate;
        /// <summary>互換コスト（既存非互換資産の乗り換え抵抗）が採用速度を絞る最大割合（0..1）。</summary>
        public readonly float switchingResistance;
        /// <summary>非互換に留まる孤立コストの最大値（採用度1で標準外に居続ける損）。</summary>
        public readonly float maxHoldoutPenalty;

        public StandardizationParams(float valueScale, float maxCostReduction, float baseAdoptionRate,
                                     float switchingResistance, float maxHoldoutPenalty)
        {
            this.valueScale = Mathf.Max(0f, valueScale);
            this.maxCostReduction = Mathf.Clamp01(maxCostReduction);
            this.baseAdoptionRate = Mathf.Max(0f, baseAdoptionRate);
            this.switchingResistance = Mathf.Clamp01(switchingResistance);
            this.maxHoldoutPenalty = Mathf.Max(0f, maxHoldoutPenalty);
        }

        /// <summary>既定＝価値スケール1.0・最大コスト低減0.4・採用基準速度0.5・互換抵抗0.6・最大孤立コスト0.5。</summary>
        public static StandardizationParams Default => new StandardizationParams(1f, 0.4f, 0.5f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 規格化の外部性の純ロジック（CNTR-3 #1614・レビンソン『コンテナ物語』参考）。共通規格（コンテナ規格）は
    /// 採用者が増えるほど価値を増す＝ネットワーク外部性。規格価値は採用度の二乗的に跳ね（メトカーフ的）、
    /// 採用が進むほど輸送コストが下がって採用がさらに採用を呼ぶ自己強化が働く。臨界採用度（ティッピングポイント）
    /// を越えると一気に標準化し、二規格が競えば先行する側へ傾いて勝者総取りになる＝先行者の囲い込み・後発の互換コスト。
    /// 輸送コストそのものの計算（<see cref="TransportCostRules"/>）・交易利得の分配（<see cref="TradeRules"/>）・
    /// 版図の物理連結（<see cref="LogisticsRules"/>）とは別系統＝ここは「規格採用のネットワーク外部性」そのものを扱う。
    /// 全入力クランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class StandardizationRules
    {
        /// <summary>
        /// 規格のネットワーク価値（0..valueScale）。採用度 adoptionRate(0..1) の二乗で増す＝採用者が増える
        /// ほど互いに繋がれる組み合わせが跳ね上がる（メトカーフ的・採用が進むほど価値が非線形に跳ねる）。
        /// </summary>
        public static float NetworkValue(float adoptionRate, StandardizationParams p)
        {
            float a = Mathf.Clamp01(adoptionRate);
            return p.valueScale * a * a;
        }

        public static float NetworkValue(float adoptionRate)
            => NetworkValue(adoptionRate, StandardizationParams.Default);

        /// <summary>
        /// 規格採用による輸送コスト低減率（0..maxCostReduction）。採用度に比例して安くなる＝みなが同じ
        /// 規格を使うほど積み替え・荷役の摩擦が消える。輸送コストへ掛けて使う（基準非破壊）。
        /// </summary>
        public static float TransportCostReduction(float adoptionRate, StandardizationParams p)
        {
            return p.maxCostReduction * Mathf.Clamp01(adoptionRate);
        }

        public static float TransportCostReduction(float adoptionRate)
            => TransportCostReduction(adoptionRate, StandardizationParams.Default);

        /// <summary>
        /// 未採用者がいま規格に乗る誘因（0..1）。currentAdoption が高いほど「みなが使うのに乗らない損」が
        /// 増えて誘因が上がる。すでに採用済み（ownAdopted）なら追加の参入誘因はない（0）。
        /// </summary>
        public static float AdoptionIncentive(float currentAdoption, bool ownAdopted)
        {
            if (ownAdopted) return 0f;
            return Mathf.Clamp01(currentAdoption);
        }

        /// <summary>
        /// 採用度の1tick後の値（0..1）。既採用が多いほど（adoptionRate）参入動機（incentive 0..1）が
        /// 効いて伸び、互換コスト switchingCost(0..1) が抵抗する＝採用が採用を呼ぶ自己強化（ロジスティック型）。
        /// adoptionRate×(1−adoptionRate) で立ち上がりが速く満杯付近で鈍る。
        /// </summary>
        public static float AdoptionTick(float adoptionRate, float incentive, float switchingCost, float dt, StandardizationParams p)
        {
            float a = Mathf.Clamp01(adoptionRate);
            float inc = Mathf.Clamp01(incentive);
            float resist = 1f - p.switchingResistance * Mathf.Clamp01(switchingCost); // 互換コストが採用速度を絞る（常に正）
            // 既採用が多いほど参入動機が効く＝ネットワーク外部性。a×(1−a) でロジスティック成長。
            float gain = p.baseAdoptionRate * (2f * a) * (1f - a) * inc * resist * Mathf.Max(0f, dt);
            return Mathf.Clamp01(a + gain);
        }

        public static float AdoptionTick(float adoptionRate, float incentive, float switchingCost, float dt)
            => AdoptionTick(adoptionRate, incentive, switchingCost, dt, StandardizationParams.Default);

        /// <summary>
        /// 非互換（標準外）に留まる孤立コスト（0..maxHoldoutPenalty）。採用度 adoptionRate の二乗で増す＝
        /// みなが標準に乗るほど取り残された者の損が跳ねる（標準に乗らない者は取り残される）。すでに採用済み
        /// （ownAdopted）なら孤立しないので0。
        /// </summary>
        public static float HoldoutPenalty(float adoptionRate, bool ownAdopted, StandardizationParams p)
        {
            if (ownAdopted) return 0f;
            float a = Mathf.Clamp01(adoptionRate);
            return p.maxHoldoutPenalty * a * a;
        }

        public static float HoldoutPenalty(float adoptionRate, bool ownAdopted)
            => HoldoutPenalty(adoptionRate, ownAdopted, StandardizationParams.Default);

        /// <summary>
        /// 臨界採用度（ティッピングポイント）を越えたか＝採用度が threshold(0..1) 以上。越えると外部性が
        /// 自己強化を上回り一気に標準化へ向かう（不可逆な勝者総取りへの入口）。
        /// </summary>
        public static bool TippingPoint(float adoptionRate, float threshold)
        {
            return Mathf.Clamp01(adoptionRate) >= Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// 二規格競争の傾き（0..1）＝規格Aが標準を取る勝率。先行する側（採用度の高い側）のネットワーク
        /// 価値が大きいほどそちらへ傾く＝勝者総取り。両者0なら拮抗で0.5、Aだけ採用があれば1へ寄る。
        /// </summary>
        public static float StandardWar(float adoptionA, float adoptionB, StandardizationParams p)
        {
            float va = NetworkValue(adoptionA, p);
            float vb = NetworkValue(adoptionB, p);
            float sum = va + vb;
            if (sum <= 0f) return 0.5f; // どちらも採用ゼロ＝拮抗
            return Mathf.Clamp01(va / sum);
        }

        public static float StandardWar(float adoptionA, float adoptionB)
            => StandardWar(adoptionA, adoptionB, StandardizationParams.Default);
    }
}
