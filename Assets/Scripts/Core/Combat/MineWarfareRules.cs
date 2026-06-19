using UnityEngine;

namespace Ginei
{
    /// <summary>機雷戦（宙雷源で航路を封鎖し回廊を扼す）の調整係数。</summary>
    public readonly struct MineWarfareParams
    {
        /// <summary>密度を1.0とみなす基準の機雷数（単位幅あたり）。</summary>
        public readonly float densityReferenceMines;
        /// <summary>密度1の機雷原を通過する敵が受ける基準損害（速度係数を掛ける前）。</summary>
        public readonly float maxTransitDamage;
        /// <summary>通過速度がこの値で損害倍率1.0（速いほど踏みやすく＝損害増）。</summary>
        public readonly float referenceTransitSpeed;
        /// <summary>掃海能力1で密度1.0を掃海し切るのに要する基準時間。</summary>
        public readonly float sweepTimePerDensity;
        /// <summary>機雷1個あたりの敷設コスト。</summary>
        public readonly float costPerMine;

        public MineWarfareParams(float densityReferenceMines, float maxTransitDamage,
            float referenceTransitSpeed, float sweepTimePerDensity, float costPerMine)
        {
            this.densityReferenceMines = Mathf.Max(1f, densityReferenceMines);
            this.maxTransitDamage = Mathf.Max(0f, maxTransitDamage);
            this.referenceTransitSpeed = Mathf.Max(0.0001f, referenceTransitSpeed);
            this.sweepTimePerDensity = Mathf.Max(0f, sweepTimePerDensity);
            this.costPerMine = Mathf.Max(0f, costPerMine);
        }

        /// <summary>既定＝基準1000機で密度1.0・最大損害200・基準速度10・掃海時間100・1機0.5コスト。</summary>
        public static MineWarfareParams Default => new MineWarfareParams(1000f, 200f, 10f, 100f, 0.5f);
    }

    /// <summary>
    /// 機雷戦の純ロジック（宙雷源で回廊・隘路を物理的に封鎖する）。敷設した機雷数と機雷原の幅から
    /// 密度（0..1）を出し、通過する敵への損害・航路封鎖度・掃海所要時間・安全航路の啓開・味方の通行
    /// リスク・敷設コストを決める。<see cref="MinefieldRules"/>（与えられた密度から通過損害率/減速/掃海
    /// tick を出す既存窓口）に対し、本ルールは「機雷の敷設量→密度」と「敷設コスト・物理封鎖・掃海時間・
    /// 味方の通行」に特化＝機雷戦の敷設/通過損害/掃海を担う。<see cref="BlockadeRules"/>（艦隊が面を抑える封鎖）
    /// とは別＝機雷による物理封鎖で兵力を拘束しない代わりに敵味方を選ばない。盤面非依存・plain引数・
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MineWarfareRules
    {
        /// <summary>機雷原の密度（0..1）＝敷設機雷数÷(基準機雷数×航路幅)。幅が広いほど薄まる。</summary>
        public static float MinefieldDensity(float minesLaid, float areaWidth, MineWarfareParams p)
        {
            float mines = Mathf.Max(0f, minesLaid);
            float width = Mathf.Max(0.0001f, areaWidth);
            float density = mines / (p.densityReferenceMines * width);
            return Mathf.Clamp01(density);
        }

        public static float MinefieldDensity(float minesLaid, float areaWidth)
            => MinefieldDensity(minesLaid, areaWidth, MineWarfareParams.Default);

        /// <summary>通過する敵が受ける損害＝密度×最大損害×速度倍率（速いほど踏みやすい＝損害増）。</summary>
        public static float TransitDamage(float minefieldDensity, float transitSpeed, MineWarfareParams p)
        {
            float density = Mathf.Clamp01(minefieldDensity);
            float speedFactor = Mathf.Max(0f, transitSpeed) / p.referenceTransitSpeed;
            return density * p.maxTransitDamage * speedFactor;
        }

        public static float TransitDamage(float minefieldDensity, float transitSpeed)
            => TransitDamage(minefieldDensity, transitSpeed, MineWarfareParams.Default);

        /// <summary>航路封鎖度（0..1）＝密度に等しい（密いほど敵の通行を妨げる）。Params非依存。</summary>
        public static float PassageDenial(float minefieldDensity)
        {
            return Mathf.Clamp01(minefieldDensity);
        }

        /// <summary>掃海に要る時間＝密度×基準掃海時間÷掃海能力。能力0は無限大。</summary>
        public static float MinesweepingTime(float minefieldDensity, float sweeperCapacity, MineWarfareParams p)
        {
            float density = Mathf.Clamp01(minefieldDensity);
            if (density <= 0f) return 0f;
            float capacity = Mathf.Max(0f, sweeperCapacity);
            if (capacity <= 0f) return float.PositiveInfinity;
            return density * p.sweepTimePerDensity / capacity;
        }

        public static float MinesweepingTime(float minefieldDensity, float sweeperCapacity)
            => MinesweepingTime(minefieldDensity, sweeperCapacity, MineWarfareParams.Default);

        /// <summary>
        /// 掃海の1tick後の密度（0..1）＝安全航路の啓開。掃海能力×dt を基準掃海時間で割った量だけ密度が減る。
        /// </summary>
        public static float ChannelClearing(float sweeperCapacity, float minefieldDensity, float dt, MineWarfareParams p)
        {
            float density = Mathf.Clamp01(minefieldDensity);
            float d = Mathf.Max(0f, dt);
            if (p.sweepTimePerDensity <= 0f) return 0f;
            float swept = Mathf.Max(0f, sweeperCapacity) * d / p.sweepTimePerDensity;
            return Mathf.Clamp01(density - swept);
        }

        public static float ChannelClearing(float sweeperCapacity, float minefieldDensity, float dt)
            => ChannelClearing(sweeperCapacity, minefieldDensity, dt, MineWarfareParams.Default);

        /// <summary>味方の通行リスク（0..1）＝密度×(1−航路標識)。標識が完全なら自軍はリスク0。Params非依存。</summary>
        public static float FriendlyTransitRisk(float minefieldDensity, float friendlyRouteMarking)
        {
            float density = Mathf.Clamp01(minefieldDensity);
            float marking = Mathf.Clamp01(friendlyRouteMarking);
            return density * (1f - marking);
        }

        /// <summary>敷設コスト＝機雷数×単価。</summary>
        public static float LayingCost(float minesLaid, MineWarfareParams p)
        {
            return Mathf.Max(0f, minesLaid) * p.costPerMine;
        }

        public static float LayingCost(float minesLaid)
            => LayingCost(minesLaid, MineWarfareParams.Default);

        /// <summary>航路が封鎖されているか＝封鎖度が閾値以上。</summary>
        public static bool IsChannelBlocked(float passageDenial, float threshold)
        {
            return Mathf.Clamp01(passageDenial) >= Mathf.Clamp01(threshold);
        }
    }
}
