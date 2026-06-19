using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 機甲戦（地上機甲部隊の機動打撃）の調整パラメータ（実効値パターン・基準値非破壊）。
    /// ctor で全フィールドをクランプし不正値を弾く。
    /// </summary>
    public readonly struct ArmoredWarfareParams
    {
        /// <summary>突破力＝√(装甲量×火力)に掛ける倍率(0..1へ正規化)。</summary>
        public readonly float breakthroughScale;
        /// <summary>対戦車防御＝敵対戦車兵器×防御縦深に掛ける倍率。</summary>
        public readonly float antiTankScale;
        /// <summary>諸兵科連合ボーナスの歩兵支援の重み(0..1)。</summary>
        public readonly float infantryWeight;
        /// <summary>諸兵科連合ボーナスの航空支援の重み(0..1)。</summary>
        public readonly float airWeight;
        /// <summary>諸兵科連合ボーナスの上限(1以上＝最大の積み増し)。</summary>
        public readonly float combinedArmsMax;
        /// <summary>補給制約で進撃距離が効く度合い（突出するほど止まる）。</summary>
        public readonly float distanceWeight;

        public ArmoredWarfareParams(
            float breakthroughScale,
            float antiTankScale,
            float infantryWeight,
            float airWeight,
            float combinedArmsMax,
            float distanceWeight)
        {
            this.breakthroughScale = Mathf.Clamp(breakthroughScale, 0.1f, 4f);
            this.antiTankScale = Mathf.Clamp(antiTankScale, 0.1f, 4f);
            this.infantryWeight = Mathf.Clamp01(infantryWeight);
            this.airWeight = Mathf.Clamp01(airWeight);
            this.combinedArmsMax = Mathf.Clamp(combinedArmsMax, 1f, 3f);
            this.distanceWeight = Mathf.Clamp(distanceWeight, 0f, 4f);
        }

        public static ArmoredWarfareParams Default =>
            new ArmoredWarfareParams(1f, 1f, 0.3f, 0.2f, 1.5f, 1f);
    }

    /// <summary>
    /// 機甲戦＝地上機甲部隊（戦車・装甲歩兵）の機動打撃の純ロジック（static）。
    /// 装甲×火力の突破力で敵防御線を破り、機動で側背に回り込み、対戦車防御と衝突する。
    /// 燃料・補給が突出を縛り（突出すると止まる）、歩戦協同が突破を助け、地形が機動を制約する。
    ///
    /// 分担：
    /// - <c>BreakthroughRules</c>（もしあれば）／<c>EncirclementRules</c>（包囲度・降伏）とは別＝
    ///   こちらは「地上機甲の突破・包囲・対戦車・補給」を一連の価値に束ねる地上機動戦モデル。
    /// - <c>GroundInvasionRules</c>（惑星地上侵攻の抽象解決）とは別＝機甲部隊の機動打撃に特化。
    /// 盤面非依存・plain 引数・乱数なし。各メソッドは Params 明示版＋Default 委譲版。引数は全てクランプ。
    /// </summary>
    public static class ArmoredWarfareRules
    {
        // ---- 突破力 ----
        /// <summary>
        /// 突破力(0..1)。装甲量×火力の幾何平均（どちらか欠けると突破できない）に scale を掛けてクランプ。
        /// = clamp01( √(armorMass × firepower) × breakthroughScale )
        /// </summary>
        public static float BreakthroughPower(float armorMass, float firepower, ArmoredWarfareParams p)
        {
            armorMass = Mathf.Clamp01(armorMass);
            firepower = Mathf.Clamp01(firepower);
            return Mathf.Clamp01(Mathf.Sqrt(armorMass * firepower) * p.breakthroughScale);
        }

        public static float BreakthroughPower(float armorMass, float firepower) =>
            BreakthroughPower(armorMass, firepower, ArmoredWarfareParams.Default);

        // ---- 機動性 ----
        /// <summary>機動性(0..1)。機動力×地形通過性（悪地形で機動が殺される）。</summary>
        public static float Maneuverability(float mobility, float terrainPassability, ArmoredWarfareParams p)
        {
            mobility = Mathf.Clamp01(mobility);
            terrainPassability = Mathf.Clamp01(terrainPassability);
            return Mathf.Clamp01(mobility * terrainPassability);
        }

        public static float Maneuverability(float mobility, float terrainPassability) =>
            Maneuverability(mobility, terrainPassability, ArmoredWarfareParams.Default);

        // ---- 機動包囲の能力 ----
        /// <summary>機動包囲の能力(0..1)。機動性×行動半径（速く・遠くまで回り込めるほど包める）。</summary>
        public static float EnvelopmentCapacity(float maneuverability, float operationalRange, ArmoredWarfareParams p)
        {
            maneuverability = Mathf.Clamp01(maneuverability);
            operationalRange = Mathf.Clamp01(operationalRange);
            return Mathf.Clamp01(maneuverability * operationalRange);
        }

        public static float EnvelopmentCapacity(float maneuverability, float operationalRange) =>
            EnvelopmentCapacity(maneuverability, operationalRange, ArmoredWarfareParams.Default);

        // ---- 対戦車防御 ----
        /// <summary>対戦車防御(0..1)。敵対戦車兵器×防御縦深に scale を掛けてクランプ（深い縦深ほど止める）。</summary>
        public static float AntiTankResistance(float enemyAtWeapons, float defensiveDepth, ArmoredWarfareParams p)
        {
            enemyAtWeapons = Mathf.Clamp01(enemyAtWeapons);
            defensiveDepth = Mathf.Clamp01(defensiveDepth);
            return Mathf.Clamp01(enemyAtWeapons * defensiveDepth * p.antiTankScale);
        }

        public static float AntiTankResistance(float enemyAtWeapons, float defensiveDepth) =>
            AntiTankResistance(enemyAtWeapons, defensiveDepth, ArmoredWarfareParams.Default);

        // ---- 突破の成否 ----
        /// <summary>
        /// 突破の成否(0..1)。突破力／(突破力＋対戦車防御)＝拮抗で0.5、突破が勝れば1へ。
        /// 双方0なら膠着＝0.5（0除算ガード）。
        /// </summary>
        public static float BreakthroughSuccess(float breakthroughPower, float antiTankResistance, ArmoredWarfareParams p)
        {
            breakthroughPower = Mathf.Clamp01(breakthroughPower);
            antiTankResistance = Mathf.Clamp01(antiTankResistance);
            float denom = breakthroughPower + antiTankResistance;
            if (denom <= 0f) return 0.5f;
            return Mathf.Clamp01(breakthroughPower / denom);
        }

        public static float BreakthroughSuccess(float breakthroughPower, float antiTankResistance) =>
            BreakthroughSuccess(breakthroughPower, antiTankResistance, ArmoredWarfareParams.Default);

        // ---- 諸兵科連合ボーナス ----
        /// <summary>
        /// 諸兵科連合ボーナス倍率(1..combinedArmsMax)。歩兵支援×重み＋航空支援×重み の積み増し。
        /// 歩戦協同・近接航空支援が機甲の突破を助ける。= clamp( 1 + inf×infW + air×airW, 1, max )
        /// </summary>
        public static float CombinedArmsBonus(float infantrySupport, float airSupport, ArmoredWarfareParams p)
        {
            infantrySupport = Mathf.Clamp01(infantrySupport);
            airSupport = Mathf.Clamp01(airSupport);
            float bonus = 1f + infantrySupport * p.infantryWeight + airSupport * p.airWeight;
            return Mathf.Clamp(bonus, 1f, p.combinedArmsMax);
        }

        public static float CombinedArmsBonus(float infantrySupport, float airSupport) =>
            CombinedArmsBonus(infantrySupport, airSupport, ArmoredWarfareParams.Default);

        // ---- 補給の制約 ----
        /// <summary>
        /// 補給の制約(0..1)。燃料／(1＋進撃距離×distanceWeight)＝突出するほど止まる。
        /// 燃料が満ち距離が短いほど1へ、遠く突出すれば燃料切れで減衰。
        /// </summary>
        public static float LogisticsConstraint(float fuelSupply, float advanceDistance, ArmoredWarfareParams p)
        {
            fuelSupply = Mathf.Clamp01(fuelSupply);
            advanceDistance = Mathf.Max(0f, advanceDistance);
            float denom = 1f + advanceDistance * p.distanceWeight;
            return Mathf.Clamp01(fuelSupply / denom);
        }

        public static float LogisticsConstraint(float fuelSupply, float advanceDistance) =>
            LogisticsConstraint(fuelSupply, advanceDistance, ArmoredWarfareParams.Default);

        // ---- 機甲攻勢の価値 ----
        /// <summary>
        /// 機甲攻勢の総合価値(0..1)。突破の成否×機動包囲の能力×補給の制約。
        /// どれか1つでも欠ければ攻勢は成立しない（積＝最小律）。
        /// </summary>
        public static float ArmoredOffensiveValue(
            float breakthroughSuccess, float envelopmentCapacity, float logisticsConstraint, ArmoredWarfareParams p)
        {
            breakthroughSuccess = Mathf.Clamp01(breakthroughSuccess);
            envelopmentCapacity = Mathf.Clamp01(envelopmentCapacity);
            logisticsConstraint = Mathf.Clamp01(logisticsConstraint);
            return Mathf.Clamp01(breakthroughSuccess * envelopmentCapacity * logisticsConstraint);
        }

        public static float ArmoredOffensiveValue(
            float breakthroughSuccess, float envelopmentCapacity, float logisticsConstraint) =>
            ArmoredOffensiveValue(breakthroughSuccess, envelopmentCapacity, logisticsConstraint,
                ArmoredWarfareParams.Default);
    }
}
