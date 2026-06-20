using UnityEngine;

namespace Ginei
{
    /// <summary>宇宙要塞防衛（イゼルローン型）の調整係数。装甲・主砲・駐留艦隊・攻囲耐久の数値モデル。</summary>
    public readonly struct FortressDefenseParams
    {
        /// <summary>装甲厚が耐久へ効く係数（厚い装甲＝難攻不落の核）。</summary>
        public readonly float armorScale;
        /// <summary>防御スクリーン（液体金属/エネルギー障壁）の倍率寄与（0..1で正規化して掛ける）。</summary>
        public readonly float screenScale;
        /// <summary>主砲（トゥールハンマー型）の射程が制圧範囲へ効く係数。</summary>
        public readonly float gunRangeScale;
        /// <summary>主砲門数が制圧範囲へ効く係数（門数は線形＝飽和は range 側の正規化で頭打ち）。</summary>
        public readonly float gunCountScale;
        /// <summary>駐留艦隊との相乗強度（艦隊が要塞砲の傘の下で戦う＝単独の和を超える）。</summary>
        public readonly float synergyScale;
        /// <summary>備蓄が攻囲持久へ効く係数（兵糧攻めへの耐性）。</summary>
        public readonly float supplyScale;
        /// <summary>要塞の死角の効きやすさ（構造の不完全さ＝幾何形状で穴が空く度合い）。</summary>
        public readonly float blindSpotScale;
        /// <summary>攻撃側損耗の強さ（制圧範囲×敵露出＝要塞砲が攻囲艦隊を削る）。</summary>
        public readonly float attritionScale;

        public FortressDefenseParams(float armorScale, float screenScale, float gunRangeScale,
                                     float gunCountScale, float synergyScale, float supplyScale,
                                     float blindSpotScale, float attritionScale)
        {
            this.armorScale = Mathf.Max(0f, armorScale);
            this.screenScale = Mathf.Max(0f, screenScale);
            this.gunRangeScale = Mathf.Max(0f, gunRangeScale);
            this.gunCountScale = Mathf.Max(0f, gunCountScale);
            this.synergyScale = Mathf.Max(0f, synergyScale);
            this.supplyScale = Mathf.Max(0f, supplyScale);
            this.blindSpotScale = Mathf.Max(0f, blindSpotScale);
            this.attritionScale = Mathf.Max(0f, attritionScale);
        }

        /// <summary>
        /// 既定＝装甲1.0・スクリーン0.5・砲射程1.0・砲門数0.5・相乗0.3・備蓄0.4・死角0.5・損耗0.6。
        /// 装甲が耐久の核、スクリーンは上乗せ。主砲は射程主体・門数で補う。
        /// </summary>
        public static FortressDefenseParams Default
            => new FortressDefenseParams(1f, 0.5f, 1f, 0.5f, 0.3f, 0.4f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 宇宙要塞防衛の純ロジック（イゼルローン要塞型・難攻不落の総合防御力）。
    /// 要塞の装甲と防御スクリーンで耐久（<see cref="FortressArmor"/>）、主砲の射程と門数で制圧範囲
    /// （<see cref="MainGunCoverage"/>）、駐留艦隊が要塞砲の傘の下で戦う相乗（<see cref="GarrisonFleetSynergy"/>）、
    /// 備蓄で攻囲に持久（<see cref="SiegeResistance"/>）、幾何形状の不完全さが死角を生み
    /// （<see cref="BlindSpot"/>）、制圧範囲が攻囲艦隊を削り（<see cref="AttackerAttrition"/>）、
    /// それらを束ねた攻略の困難さ（<see cref="AssaultDifficulty"/>）と持ちこたえ判定（<see cref="IsFortressHolding"/>）。
    /// 惑星3層の層別迎撃は <see cref="PlanetaryDefenseRules"/>、占領そのものの解決は <see cref="PlanetSiegeRules"/> が担う（別系統）。
    /// 乱数なし・決定論（判定は呼び出し側 roll）。純ロジック（非 MonoBehaviour・test-first）。実効値パターン（基準値非破壊）。
    /// </summary>
    public static class FortressDefenseRules
    {
        /// <summary>
        /// 要塞の耐久（≧0）＝装甲厚×(1＋防御スクリーン寄与)。装甲が核、スクリーンは正規化して上乗せ。
        /// defensiveScreen は 0..1 へクランプして screenScale を掛けた倍率を装甲に乗じる（厚い装甲＋障壁で難攻不落）。
        /// </summary>
        public static float FortressArmor(float hullThickness, float defensiveScreen, FortressDefenseParams p)
        {
            float hull = Mathf.Max(0f, hullThickness) * p.armorScale;
            float screen = Mathf.Clamp01(defensiveScreen);
            return hull * (1f + p.screenScale * screen);
        }

        public static float FortressArmor(float hullThickness, float defensiveScreen)
            => FortressArmor(hullThickness, defensiveScreen, FortressDefenseParams.Default);

        /// <summary>
        /// 主砲の制圧範囲（0..1＝周囲をどれだけ覆うか）＝射程×門数。射程と門数で生の制圧力を出し、
        /// 1点で飽和する正規化 x/(1+x) で 0..1 に収める（射程・門数を増やすほど1へ近づくが越えない）。
        /// 射程・門数いずれかが0なら制圧範囲0（撃てない）。
        /// </summary>
        public static float MainGunCoverage(float gunRange, float gunCount, FortressDefenseParams p)
        {
            float range = Mathf.Max(0f, gunRange) * p.gunRangeScale;
            float count = Mathf.Max(0f, gunCount) * p.gunCountScale;
            float raw = range * count;
            if (raw <= 0f) return 0f;
            return Mathf.Clamp01(raw / (1f + raw));
        }

        public static float MainGunCoverage(float gunRange, float gunCount)
            => MainGunCoverage(gunRange, gunCount, FortressDefenseParams.Default);

        /// <summary>
        /// 要塞＋駐留艦隊の相乗総合戦力（≧0）。両者の単純な和に、双方が揃ったときの相互支援ぶん
        /// （synergyScale×両者の幾何平均）を上乗せ＝艦隊が要塞砲の傘の下で戦う（単独の和を超える）。
        /// どちらかが0なら相乗は無く和そのもの（裸の要塞／傘の無い艦隊）。
        /// </summary>
        public static float GarrisonFleetSynergy(float fortressArmor, float garrisonFleetStrength, FortressDefenseParams p)
        {
            float armor = Mathf.Max(0f, fortressArmor);
            float fleet = Mathf.Max(0f, garrisonFleetStrength);
            float sum = armor + fleet;
            if (armor <= 0f || fleet <= 0f) return sum; // 片方欠けると相乗なし
            float geoMean = Mathf.Sqrt(armor * fleet);
            return sum + p.synergyScale * geoMean;
        }

        public static float GarrisonFleetSynergy(float fortressArmor, float garrisonFleetStrength)
            => GarrisonFleetSynergy(fortressArmor, garrisonFleetStrength, FortressDefenseParams.Default);

        /// <summary>
        /// 攻囲への持久（≧0）＝装甲×(1＋備蓄寄与)。固い殻に加え兵糧/弾薬の備蓄が長期攻囲に耐える。
        /// suppliesStockpiled は 0..1 へクランプして supplyScale を掛けた倍率を装甲に乗じる
        /// （備蓄0でも装甲ぶんは耐えるが、満タンなら持久が伸びる＝兵糧攻めへの耐性）。
        /// </summary>
        public static float SiegeResistance(float fortressArmor, float suppliesStockpiled, FortressDefenseParams p)
        {
            float armor = Mathf.Max(0f, fortressArmor);
            float supply = Mathf.Clamp01(suppliesStockpiled);
            return armor * (1f + p.supplyScale * supply);
        }

        public static float SiegeResistance(float fortressArmor, float suppliesStockpiled)
            => SiegeResistance(fortressArmor, suppliesStockpiled, FortressDefenseParams.Default);

        /// <summary>
        /// 要塞の死角（0..1＝攻撃側が突ける穴の大きさ）＝(1−制圧範囲)×構造の不完全さ。
        /// 制圧範囲が広いほど死角は減り（1で穴なし）、幾何形状が歪なほど死角が増える。
        /// gunCoverage は 0..1・fortressGeometry は 0..1（1＝完全な歪み／0＝理想形状）へクランプし blindSpotScale を掛けて 0..1 に収める。
        /// </summary>
        public static float BlindSpot(float gunCoverage, float fortressGeometry, FortressDefenseParams p)
        {
            float coverage = Mathf.Clamp01(gunCoverage);
            float geometry = Mathf.Clamp01(fortressGeometry);
            float gap = (1f - coverage) * geometry * p.blindSpotScale;
            return Mathf.Clamp01(gap);
        }

        public static float BlindSpot(float gunCoverage, float fortressGeometry)
            => BlindSpot(gunCoverage, fortressGeometry, FortressDefenseParams.Default);

        /// <summary>
        /// 攻撃側の損耗（≧0）＝制圧範囲×敵の露出。要塞砲の傘が広く（制圧範囲大）、攻囲艦隊が身を晒すほど
        /// （露出大）攻撃側が削られる。露出を抑えれば（遮蔽・分散）損耗は減る。
        /// mainGunCoverage は 0..1・attackerExposure は 0..1 へクランプして attritionScale を掛ける。
        /// </summary>
        public static float AttackerAttrition(float mainGunCoverage, float attackerExposure, FortressDefenseParams p)
        {
            float coverage = Mathf.Clamp01(mainGunCoverage);
            float exposure = Mathf.Clamp01(attackerExposure);
            return coverage * exposure * p.attritionScale;
        }

        public static float AttackerAttrition(float mainGunCoverage, float attackerExposure)
            => AttackerAttrition(mainGunCoverage, attackerExposure, FortressDefenseParams.Default);

        /// <summary>
        /// 攻略の困難さ（≧0）＝装甲×(1＋攻撃側損耗)×(1＋駐留艦隊相乗の正規化)。
        /// 固い装甲に、攻囲艦隊を削る損耗と要塞砲の傘で戦う艦隊の相乗が重なって難攻不落になる。
        /// attackerAttrition は 0..1 へクランプ、garrisonFleetSynergy は飽和正規化 s/(1+s) で 0..1 にして掛ける
        /// （相乗が大きいほど 1 へ近づき困難さを最大2倍まで押し上げる＝艦隊の傘が効くほど落ちない）。
        /// </summary>
        public static float AssaultDifficulty(float fortressArmor, float attackerAttrition, float garrisonFleetSynergy, FortressDefenseParams p)
        {
            float armor = Mathf.Max(0f, fortressArmor);
            float attrition = Mathf.Clamp01(attackerAttrition);
            float synergy = Mathf.Max(0f, garrisonFleetSynergy);
            float synergyNorm = (synergy <= 0f) ? 0f : Mathf.Clamp01(synergy / (1f + synergy));
            return armor * (1f + attrition) * (1f + synergyNorm);
        }

        public static float AssaultDifficulty(float fortressArmor, float attackerAttrition, float garrisonFleetSynergy)
            => AssaultDifficulty(fortressArmor, attackerAttrition, garrisonFleetSynergy, FortressDefenseParams.Default);

        /// <summary>要塞防衛が持ちこたえる既定の攻囲耐久しきい値（攻略困難さ＋攻囲持久がこれ以上で陥落せず）。</summary>
        public const float DefaultHoldThreshold = 100f;

        /// <summary>
        /// 要塞が持ちこたえるか（true＝攻囲に耐え陥落しない）。攻囲持久(siegeResistance)と攻略困難さ
        /// (assaultDifficulty)の和が threshold 以上なら保持。負入力はクランプ（裸の要塞は持ちこたえない）。
        /// </summary>
        public static bool IsFortressHolding(float siegeResistance, float assaultDifficulty, float threshold)
        {
            float resist = Mathf.Max(0f, siegeResistance);
            float difficulty = Mathf.Max(0f, assaultDifficulty);
            return (resist + difficulty) >= threshold;
        }

        public static bool IsFortressHolding(float siegeResistance, float assaultDifficulty)
            => IsFortressHolding(siegeResistance, assaultDifficulty, DefaultHoldThreshold);
    }
}
