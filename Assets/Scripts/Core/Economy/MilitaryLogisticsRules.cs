using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 補給線・通商破壊の駆動（MILSUP-5・#2049・#94/#95 連携・純ロジック）。
    /// 軍需要が補給フローを引き、補給線#94（補給源→所有回廊→前線）で運ぶ。通商破壊#95 で補給船団を断つと前線が干上がる（兵糧攻め）。
    /// 既存 <see cref="SupplyRules"/>/<see cref="CommerceRaidingRules"/> を駆動（並行システムを作らない）。物流は集約（船団マイクロ無し）。test-first。
    /// </summary>
    public static class MilitaryLogisticsRules
    {
        /// <summary>補給線で運べる量＝min(需要, 在庫, 補給線容量)（容量＝補給源からの距離#94 で律速）。</summary>
        public static float DeliveredSupply(float demand, float available, float lineThroughput)
            => Mathf.Max(0f, Mathf.Min(Mathf.Min(Mathf.Max(0f, demand), Mathf.Max(0f, available)), Mathf.Max(0f, lineThroughput)));

        /// <summary>通商破壊#95 後の到達補給＝配送量×(1−襲撃損失率)（護衛で守る・断たれると0）。</summary>
        public static float RaidedSupply(float delivered, float raidLossFraction)
            => Mathf.Max(0f, delivered) * (1f - Mathf.Clamp01(raidLossFraction));

        /// <summary>補給切れか＝補給源から所有回廊で到達できない（#94 連携・前線で孤立）。</summary>
        public static bool IsCutOff(bool reachableViaOwnedCorridors, bool zocBlocked)
            => !reachableViaOwnedCorridors || zocBlocked;

        /// <summary>限られた補給の前線優先配分＝前線需要を優先して配り、残りを後方へ（不足時の配分）。前線へ回る量を返す。</summary>
        public static float PrioritizeFront(float totalSupply, float frontDemand)
            => Mathf.Min(Mathf.Max(0f, totalSupply), Mathf.Max(0f, frontDemand));
    }
}
