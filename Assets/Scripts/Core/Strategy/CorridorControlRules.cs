using UnityEngine;

namespace Ginei
{
    /// <summary>航路回廊の戦略的支配の調整係数（イゼルローン回廊・フェザーン回廊型＝星間航路の隘路を握る）。</summary>
    public readonly struct CorridorControlParams
    {
        /// <summary>連結性→戦略価値のスケール（回廊が結ぶ星域の広さがそのまま価値に効く係数）。</summary>
        public readonly float valueScale;
        /// <summary>要塞火力→封鎖強度のスケール（人工要塞が封鎖に寄与する係数）。</summary>
        public readonly float fortressScale;
        /// <summary>哨戒密度→封鎖強度のスケール（巡回艦隊が封鎖に寄与する係数）。</summary>
        public readonly float patrolScale;
        /// <summary>補給線確保の上限スケール（回廊を握って得られる補給線安定の最大寄与）。</summary>
        public readonly float supplyScale;
        /// <summary>通行料収入のスケール（交通量×支配度から得る関税・口銭の係数）。</summary>
        public readonly float tollScale;
        /// <summary>迂回圧力のスケール（封鎖が厳しいほど迂回路へ流れる圧力の係数）。</summary>
        public readonly float bypassScale;

        public CorridorControlParams(float valueScale, float fortressScale, float patrolScale,
                                     float supplyScale, float tollScale, float bypassScale)
        {
            this.valueScale = Mathf.Max(0f, valueScale);
            this.fortressScale = Mathf.Max(0f, fortressScale);
            this.patrolScale = Mathf.Max(0f, patrolScale);
            this.supplyScale = Mathf.Max(0f, supplyScale);
            this.tollScale = Mathf.Max(0f, tollScale);
            this.bypassScale = Mathf.Max(0f, bypassScale);
        }

        /// <summary>既定＝価値1.0・要塞1.0・哨戒1.0・補給1.0・通行料1.0・迂回圧力1.0。</summary>
        public static CorridorControlParams Default => new CorridorControlParams(1f, 1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 航路回廊の戦略的支配の純ロジック（イゼルローン回廊・フェザーン回廊型）。星間航路の隘路（回廊）を
    /// 握ると補給線を確保し、通行料を取り、敵の進攻を封じられる。回廊は迂回路が無いほど戦略的に貴重で、
    /// 要塞火力と哨戒密度で封鎖の強度が決まり、突破の難度は封鎖と攻撃側兵力の比で決まる。封鎖が厳しすぎる
    /// と敵は迂回路へ流れる（迂回圧力）。<see cref="ChokeholdBattleRules"/>（隘路の会戦戦術＝狭所での正面制限）
    /// とは別＝こちらは**回廊そのものの戦略的支配**（補給線・通行料・封鎖・迂回）。
    /// 盤面非依存の plain 引数・入力クランプ・実効値パターン・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CorridorControlRules
    {
        /// <summary>回廊確保とみなす突破難度の既定閾値（これ以上なら封じられている）。</summary>
        public const float DefaultSecureThreshold = 0.6f;

        /// <summary>
        /// 回廊の戦略価値（≥0）＝連結性 connectivity × 交通量 trafficVolume ÷ (1+迂回路 alternativeRoutes)。
        /// 結ぶ星域が広く交通が多いほど高く、迂回路が無いほど貴重（代替が利かない隘路ほど価値が跳ねる）。
        /// </summary>
        public static float CorridorValue(float connectivity, float trafficVolume, float alternativeRoutes,
                                          CorridorControlParams p)
        {
            float conn = Mathf.Max(0f, connectivity);
            float traffic = Mathf.Max(0f, trafficVolume);
            float alt = Mathf.Max(0f, alternativeRoutes);
            return p.valueScale * conn * traffic / (1f + alt);
        }

        public static float CorridorValue(float connectivity, float trafficVolume, float alternativeRoutes)
            => CorridorValue(connectivity, trafficVolume, alternativeRoutes, CorridorControlParams.Default);

        /// <summary>
        /// 封鎖の強度（≥0）＝要塞火力 fortressPower × 要塞係数 ＋ 哨戒密度 patrolDensity × 哨戒係数。
        /// 要塞と巡回艦隊の両輪で回廊を封じる（要塞だけでも哨戒だけでも封鎖は成り立つ）。
        /// </summary>
        public static float BlockadeStrength(float fortressPower, float patrolDensity, CorridorControlParams p)
        {
            float fort = Mathf.Max(0f, fortressPower);
            float patrol = Mathf.Max(0f, patrolDensity);
            return p.fortressScale * fort + p.patrolScale * patrol;
        }

        public static float BlockadeStrength(float fortressPower, float patrolDensity)
            => BlockadeStrength(fortressPower, patrolDensity, CorridorControlParams.Default);

        /// <summary>
        /// 突破の難度（0..1）＝封鎖 blockadeStrength ÷ (封鎖 ＋ 攻撃側 attackerStrength)。
        /// 封鎖が攻撃側を圧倒するほど 1 に近づき（突破困難）、攻撃側が勝るほど 0 に近づく。両者0なら0。
        /// </summary>
        public static float BreakthroughDifficulty(float blockadeStrength, float attackerStrength,
                                                   CorridorControlParams p)
        {
            float block = Mathf.Max(0f, blockadeStrength);
            float att = Mathf.Max(0f, attackerStrength);
            float denom = block + att;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(block / denom);
        }

        public static float BreakthroughDifficulty(float blockadeStrength, float attackerStrength)
            => BreakthroughDifficulty(blockadeStrength, attackerStrength, CorridorControlParams.Default);

        /// <summary>
        /// 補給線の確保（≥0）＝回廊価値 corridorValue × 封鎖 blockadeStrength × 補給係数。
        /// 価値の高い回廊を強く封じているほど、その回廊を通る自軍補給線が安泰になる（敵の通商破壊を防ぐ）。
        /// </summary>
        public static float SupplyLineSecurity(float corridorValue, float blockadeStrength, CorridorControlParams p)
        {
            float val = Mathf.Max(0f, corridorValue);
            float block = Mathf.Max(0f, blockadeStrength);
            return p.supplyScale * val * block;
        }

        public static float SupplyLineSecurity(float corridorValue, float blockadeStrength)
            => SupplyLineSecurity(corridorValue, blockadeStrength, CorridorControlParams.Default);

        /// <summary>
        /// 隘路を握る戦略的てこ（≥0）＝回廊価値 corridorValue × 封鎖 blockadeStrength。
        /// 貴重な回廊を強く封じているほど、外交・進攻で相手に効かせられる圧力（てこ）が大きい。
        /// </summary>
        public static float ChokePointLeverage(float corridorValue, float blockadeStrength, CorridorControlParams p)
        {
            float val = Mathf.Max(0f, corridorValue);
            float block = Mathf.Max(0f, blockadeStrength);
            return val * block;
        }

        public static float ChokePointLeverage(float corridorValue, float blockadeStrength)
            => ChokePointLeverage(corridorValue, blockadeStrength, CorridorControlParams.Default);

        /// <summary>
        /// 通行料収入（≥0）＝交通量 trafficVolume × 支配度 ÷ (1+封鎖) × 通行料係数。
        /// 支配度＝封鎖/(1+封鎖)＝回廊をどれだけ握れているか（0..1）。交通が多く支配が強いほど関税・口銭が増える。
        /// </summary>
        public static float TollRevenue(float trafficVolume, float blockadeStrength, CorridorControlParams p)
        {
            float traffic = Mathf.Max(0f, trafficVolume);
            float block = Mathf.Max(0f, blockadeStrength);
            float control = block / (1f + block);
            return p.tollScale * traffic * control;
        }

        public static float TollRevenue(float trafficVolume, float blockadeStrength)
            => TollRevenue(trafficVolume, blockadeStrength, CorridorControlParams.Default);

        /// <summary>
        /// 迂回への圧力（≥0）＝迂回路 alternativeRoutes × 封鎖 blockadeStrength × 迂回係数。
        /// 迂回路が在り封鎖が厳しいほど、敵は回廊を諦め迂回路へ流れる圧力が高まる（封じすぎは素通りされる）。
        /// 迂回路が無ければ圧力0＝敵は回廊を通るしかない。
        /// </summary>
        public static float BypassPressure(float alternativeRoutes, float blockadeStrength, CorridorControlParams p)
        {
            float alt = Mathf.Max(0f, alternativeRoutes);
            float block = Mathf.Max(0f, blockadeStrength);
            return p.bypassScale * alt * block;
        }

        public static float BypassPressure(float alternativeRoutes, float blockadeStrength)
            => BypassPressure(alternativeRoutes, blockadeStrength, CorridorControlParams.Default);

        /// <summary>
        /// 回廊を確保できているか＝封鎖 blockadeStrength が正で、突破難度 breakthroughDifficulty が
        /// 閾値 threshold 以上なら確保（敵が抜けない）。封鎖が無ければ確保不可。
        /// </summary>
        public static bool IsCorridorSecure(float blockadeStrength, float breakthroughDifficulty, float threshold)
        {
            float block = Mathf.Max(0f, blockadeStrength);
            if (block <= 0f) return false;
            float diff = Mathf.Clamp01(breakthroughDifficulty);
            return diff >= Mathf.Clamp01(threshold);
        }

        public static bool IsCorridorSecure(float blockadeStrength, float breakthroughDifficulty)
            => IsCorridorSecure(blockadeStrength, breakthroughDifficulty, DefaultSecureThreshold);
    }
}
