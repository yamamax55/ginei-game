using UnityEngine;

namespace Ginei
{
    /// <summary>小惑星帯戦（障害物宙域）の調整係数。すべて ctor で Clamp。</summary>
    public readonly struct AsteroidFieldParams
    {
        /// <summary>航行危険のスケール（密度×サイズ/技量に掛ける）。</summary>
        public readonly float hazardScale;
        /// <summary>機動制限のスケール（密度×サイズに掛ける）。</summary>
        public readonly float mobilityScale;
        /// <summary>射線遮蔽のスケール（密度×距離に掛ける）。</summary>
        public readonly float losScale;
        /// <summary>遮蔽が飽和する基準距離（これで距離を正規化）。</summary>
        public readonly float losReferenceRange;
        /// <summary>衝突リスクのスケール（航行危険×速度に掛ける）。</summary>
        public readonly float collisionScale;
        /// <summary>遮蔽の価値の上限（被弾軽減の最大＝0..1）。</summary>
        public readonly float coverScale;
        /// <summary>小型艦優位のスケール（(1−サイズ)×密度に掛ける）。</summary>
        public readonly float smallShipScale;
        /// <summary>待ち伏せ拠点の有利のスケール（遮蔽×密度に掛ける）。</summary>
        public readonly float ambushScale;
        /// <summary>操艦技量が航行危険を御す度合い（0..1、技量1でこの割合ぶん危険を減じる）。</summary>
        public readonly float pilotMitigation;

        public AsteroidFieldParams(float hazardScale, float mobilityScale, float losScale,
            float losReferenceRange, float collisionScale, float coverScale,
            float smallShipScale, float ambushScale, float pilotMitigation)
        {
            this.hazardScale = Mathf.Max(0f, hazardScale);
            this.mobilityScale = Mathf.Max(0f, mobilityScale);
            this.losScale = Mathf.Max(0f, losScale);
            this.losReferenceRange = Mathf.Max(0.0001f, losReferenceRange);
            this.collisionScale = Mathf.Max(0f, collisionScale);
            this.coverScale = Mathf.Clamp01(coverScale);
            this.smallShipScale = Mathf.Max(0f, smallShipScale);
            this.ambushScale = Mathf.Max(0f, ambushScale);
            this.pilotMitigation = Mathf.Clamp01(pilotMitigation);
        }

        /// <summary>既定＝危険1.0/機動1.0/遮蔽1.0(基準距離1.0)/衝突1.0/遮蔽価値0.8/小型優位1.0/待伏1.0/操艦軽減0.5。</summary>
        public static AsteroidFieldParams Default =>
            new AsteroidFieldParams(1f, 1f, 1f, 1f, 1f, 0.8f, 1f, 1f, 0.5f);
    }

    /// <summary>
    /// 小惑星帯戦（岩塊の密集宙域での会戦）の純ロジック。密度の高い宙域では航行が危険になり（操艦技量で御す）、
    /// 大型艦ほど機動が制限され（小型艦が相対的に有利）、岩塊が射線を遮るため遮蔽が生まれ（被弾を減らす）、
    /// その遮蔽は待ち伏せ拠点としての有利を生む。飛ばし過ぎると衝突する。
    /// 全入力 Clamp・決定論（乱数なし）・実効値パターン（基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// fieldDensity/shipSize/pilotSkill/range/speed 等は 0..1 正規化を想定（range は基準距離で正規化）。
    /// </summary>
    public static class AsteroidFieldRules
    {
        private const float SkillFloor = 0.1f; // 操艦技量の下限（0除算回避＝完全な素人でも多少は避ける）

        /// <summary>
        /// 航行の危険（0..1）＝密度×艦サイズ /（操艦技量）。密集宙域・大型艦ほど危険、操艦が巧いほど下がる。
        /// </summary>
        public static float NavigationHazard(float fieldDensity, float shipSize, float pilotSkill, AsteroidFieldParams p)
        {
            float d = Mathf.Clamp01(fieldDensity);
            float s = Mathf.Clamp01(shipSize);
            float skill = Mathf.Max(SkillFloor, Mathf.Clamp01(pilotSkill));
            return Mathf.Clamp01(p.hazardScale * d * s / skill);
        }

        public static float NavigationHazard(float fieldDensity, float shipSize, float pilotSkill)
            => NavigationHazard(fieldDensity, shipSize, pilotSkill, AsteroidFieldParams.Default);

        /// <summary>
        /// 機動制限（0..1、1=身動きできない）＝密度×艦サイズ。大型艦ほど岩塊の間を抜けられず動けない。
        /// </summary>
        public static float MobilityRestriction(float fieldDensity, float shipSize, AsteroidFieldParams p)
        {
            float d = Mathf.Clamp01(fieldDensity);
            float s = Mathf.Clamp01(shipSize);
            return Mathf.Clamp01(p.mobilityScale * d * s);
        }

        public static float MobilityRestriction(float fieldDensity, float shipSize)
            => MobilityRestriction(fieldDensity, shipSize, AsteroidFieldParams.Default);

        /// <summary>
        /// 射線遮蔽（0..1、1=完全遮蔽）＝密度×距離（基準距離で正規化）。遠い相手ほど間に岩塊が増えて見えない。
        /// </summary>
        public static float LineOfSightBlockage(float fieldDensity, float range, AsteroidFieldParams p)
        {
            float d = Mathf.Clamp01(fieldDensity);
            float r = Mathf.Max(0f, range) / p.losReferenceRange;
            return Mathf.Clamp01(p.losScale * d * r);
        }

        public static float LineOfSightBlockage(float fieldDensity, float range)
            => LineOfSightBlockage(fieldDensity, range, AsteroidFieldParams.Default);

        /// <summary>
        /// 衝突リスク（0..1）＝航行危険×速度。危険な宙域を高速で突っ切るほど岩塊に激突する。
        /// </summary>
        public static float CollisionRisk(float navigationHazard, float speed, AsteroidFieldParams p)
        {
            float h = Mathf.Clamp01(navigationHazard);
            float v = Mathf.Clamp01(speed);
            return Mathf.Clamp01(p.collisionScale * h * v);
        }

        public static float CollisionRisk(float navigationHazard, float speed)
            => CollisionRisk(navigationHazard, speed, AsteroidFieldParams.Default);

        /// <summary>
        /// 遮蔽の価値（0..coverScale）＝射線遮蔽に比例した被弾軽減。岩塊に隠れるほど撃たれにくい。
        /// </summary>
        public static float CoverValue(float lineOfSightBlockage, AsteroidFieldParams p)
        {
            float b = Mathf.Clamp01(lineOfSightBlockage);
            return Mathf.Clamp01(p.coverScale * b);
        }

        public static float CoverValue(float lineOfSightBlockage)
            => CoverValue(lineOfSightBlockage, AsteroidFieldParams.Default);

        /// <summary>
        /// 小型艦の相対優位（0..1）＝(1−艦サイズ)×密度。密集宙域では小回りの利く小型艦が大型艦より優位。
        /// </summary>
        public static float SmallShipAdvantage(float shipSize, float fieldDensity, AsteroidFieldParams p)
        {
            float s = Mathf.Clamp01(shipSize);
            float d = Mathf.Clamp01(fieldDensity);
            return Mathf.Clamp01(p.smallShipScale * (1f - s) * d);
        }

        public static float SmallShipAdvantage(float shipSize, float fieldDensity)
            => SmallShipAdvantage(shipSize, fieldDensity, AsteroidFieldParams.Default);

        /// <summary>
        /// 待ち伏せ拠点の有利（0..1）＝遮蔽×密度。隠れられて密度も高い場所が伏撃の好適地。
        /// </summary>
        public static float AmbushPosition(float coverValue, float fieldDensity, AsteroidFieldParams p)
        {
            float c = Mathf.Clamp01(coverValue);
            float d = Mathf.Clamp01(fieldDensity);
            return Mathf.Clamp01(p.ambushScale * c * d);
        }

        public static float AmbushPosition(float coverValue, float fieldDensity)
            => AmbushPosition(coverValue, fieldDensity, AsteroidFieldParams.Default);

        /// <summary>既定の通過可否閾値。実効危険がこれ以下なら通れる。</summary>
        public const float DefaultPassThreshold = 0.6f;

        /// <summary>
        /// 通過可能か（bool）＝操艦技量で軽減した実効危険が閾値以下なら true。
        /// 実効危険＝航行危険×(1 − 0.5×操艦技量)＝巧者は危険な岩塊宙域も御して抜ける。
        /// </summary>
        public static bool IsPassable(float navigationHazard, float pilotSkill, float threshold)
        {
            float h = Mathf.Clamp01(navigationHazard);
            float skill = Mathf.Clamp01(pilotSkill);
            float effective = h * (1f - 0.5f * skill);
            return effective <= Mathf.Max(0f, threshold);
        }

        public static bool IsPassable(float navigationHazard, float pilotSkill)
            => IsPassable(navigationHazard, pilotSkill, DefaultPassThreshold);
    }
}
