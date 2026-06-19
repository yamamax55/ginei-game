using UnityEngine;

namespace Ginei
{
    /// <summary>攻城砲撃の調整係数（要塞・拠点への組織的砲撃＝攻める側）。火力統制の下限・スクリーン重み・装甲損傷率・距離減衰・対砲戦致死性・弾薬消費率・突破口形成率・突破閾値。ctor で全値をクランプする。</summary>
    public readonly struct SiegeBatteryParams
    {
        /// <summary>火力統制（fireCoordination）が0でも残る砲撃集中の最低係数（0..1・統制が崩れても各個に撃てる）。1.0なら統制に依らず満額。</summary>
        public readonly float coordinationFloor;
        /// <summary>防御スクリーンに掛ける重み（≥0・大きいほどスクリーンが効く＝飽和突破しにくい）。</summary>
        public readonly float screenWeight;
        /// <summary>装甲への累積損傷率（≥0・集中×飽和×時間に掛ける係数）。</summary>
        public readonly float armorDamageRate;
        /// <summary>距離による精度減衰（≥0・距離が伸びるほど照準が利かなくなる）。</summary>
        public readonly float rangeFalloff;
        /// <summary>対砲戦の致死性係数（0..1・防御側の反撃が砲撃側を削る強さ）。</summary>
        public readonly float counterLethality;
        /// <summary>弾薬消費率（≥0・集中×時間に掛ける係数＝長期砲撃で弾を食う）。</summary>
        public readonly float ammoRate;
        /// <summary>突破口の形成率（≥0・累積損傷×精度に掛ける係数）。</summary>
        public readonly float breachRate;
        /// <summary>突破口の既定閾値（0..1・<see cref="SiegeBatteryRules.IsBreachAchieved(float)"/> 委譲版が使う）。</summary>
        public readonly float breachThreshold;

        public SiegeBatteryParams(float coordinationFloor, float screenWeight, float armorDamageRate,
                                  float rangeFalloff, float counterLethality, float ammoRate,
                                  float breachRate, float breachThreshold)
        {
            this.coordinationFloor = Mathf.Clamp01(coordinationFloor);
            this.screenWeight = Mathf.Max(0f, screenWeight);
            this.armorDamageRate = Mathf.Max(0f, armorDamageRate);
            this.rangeFalloff = Mathf.Max(0f, rangeFalloff);
            this.counterLethality = Mathf.Clamp01(counterLethality);
            this.ammoRate = Mathf.Max(0f, ammoRate);
            this.breachRate = Mathf.Max(0f, breachRate);
            this.breachThreshold = Mathf.Clamp01(breachThreshold);
        }

        /// <summary>既定＝統制下限0.4／スクリーン重み1.0／装甲損傷率0.01／距離減衰1.0／対砲戦致死性0.5／弾薬消費率1.0／突破形成率1.0／突破閾値0.5。</summary>
        public static SiegeBatteryParams Default
            => new SiegeBatteryParams(0.4f, 1f, 0.01f, 1f, 0.5f, 1f, 1f, 0.5f);
    }

    /// <summary>
    /// 攻城砲撃の純ロジック（要塞・拠点への組織的砲撃＝攻める側・唯一の窓口）。
    /// <b>砲門を集中（<see cref="BarrageConcentration"/>）し、防御スクリーンを飽和突破（<see cref="ScreenSaturation"/>）して、要塞装甲に累積損傷（<see cref="CumulativeArmorDamage"/>）を与える</b>。
    /// 砲撃は距離で精度が落ち（<see cref="BarrageAccuracy"/>）、防御側の対砲戦で削られ（<see cref="CounterBatteryLoss"/>）、長期砲撃は弾薬を食う（<see cref="AmmunitionExpenditure"/>）。
    /// 累積損傷×精度が突破口を開く（<see cref="BreachFormation"/>／<see cref="IsBreachAchieved(float,float)"/>）。
    /// <see cref="AmphibiousAssaultRules"/>（軌道→地上の強襲上陸）とは別＝こちらは<b>要塞・拠点への砲撃そのもの</b>。
    /// 盤面（StrategicFleet/Planet 等）に依存せず plain 引数で受ける。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SiegeBatteryRules
    {
        /// <summary>
        /// 砲撃の集中（≥0）＝砲門数 batteryCount を火力統制 fireCoordination で底上げ。
        /// 統制1.0で満額、0.0でも <see cref="SiegeBatteryParams.coordinationFloor"/> ぶんは各個に撃てる。砲門数は負を切り上げ。
        /// </summary>
        public static float BarrageConcentration(float batteryCount, float fireCoordination, SiegeBatteryParams p)
        {
            float batteries = Mathf.Max(0f, batteryCount);
            float coordination = Mathf.Clamp01(fireCoordination);
            float factor = Mathf.Lerp(p.coordinationFloor, 1f, coordination);
            return batteries * factor;
        }

        /// <summary>既定係数での砲撃集中（≥0）。</summary>
        public static float BarrageConcentration(float batteryCount, float fireCoordination)
            => BarrageConcentration(batteryCount, fireCoordination, SiegeBatteryParams.Default);

        /// <summary>
        /// スクリーンの飽和突破（0..1）＝集中砲火 barrageConcentration と防御スクリーン defensiveScreen の押し合い比。
        /// 集中が大きいほど1へ（飽和突破）、スクリーンが厚いほど0へ。スクリーン重みは <see cref="SiegeBatteryParams.screenWeight"/>。
        /// 双方0なら飽和なし（0）。
        /// </summary>
        public static float ScreenSaturation(float barrageConcentration, float defensiveScreen, SiegeBatteryParams p)
        {
            float conc = Mathf.Max(0f, barrageConcentration);
            float screen = Mathf.Max(0f, defensiveScreen) * p.screenWeight;
            float denom = conc + screen;
            if (denom <= 0f) return 0f; // 双方ゼロ＝飽和なし
            return Mathf.Clamp01(conc / denom);
        }

        /// <summary>既定係数でのスクリーン飽和（0..1）。</summary>
        public static float ScreenSaturation(float barrageConcentration, float defensiveScreen)
            => ScreenSaturation(barrageConcentration, defensiveScreen, SiegeBatteryParams.Default);

        /// <summary>
        /// 要塞装甲への累積損傷（≥0）＝集中 barrageConcentration ×飽和突破 screenSaturation ×砲撃時間 bombardmentTime ×損傷率。
        /// スクリーンを飽和させ砲撃を続けるほど装甲が削れる。損傷率は <see cref="SiegeBatteryParams.armorDamageRate"/>。時間は負を切り上げ。
        /// </summary>
        public static float CumulativeArmorDamage(float barrageConcentration, float screenSaturation, float bombardmentTime, SiegeBatteryParams p)
        {
            float conc = Mathf.Max(0f, barrageConcentration);
            float sat = Mathf.Clamp01(screenSaturation);
            float time = Mathf.Max(0f, bombardmentTime);
            return Mathf.Max(0f, conc * sat * time * p.armorDamageRate);
        }

        /// <summary>既定係数での累積装甲損傷（≥0）。</summary>
        public static float CumulativeArmorDamage(float barrageConcentration, float screenSaturation, float bombardmentTime)
            => CumulativeArmorDamage(barrageConcentration, screenSaturation, bombardmentTime, SiegeBatteryParams.Default);

        /// <summary>
        /// 砲撃の精度（0..1）＝照準データ targetingData を距離 range で割り引く（1/(1+距離×減衰)）。
        /// 照準が良いほど・距離が近いほど1へ。減衰は <see cref="SiegeBatteryParams.rangeFalloff"/>。両入力はクランプ／切り上げ。
        /// </summary>
        public static float BarrageAccuracy(float targetingData, float range, SiegeBatteryParams p)
        {
            float targeting = Mathf.Clamp01(targetingData);
            float dist = Mathf.Max(0f, range);
            return Mathf.Clamp01(targeting / (1f + dist * p.rangeFalloff));
        }

        /// <summary>既定係数での砲撃精度（0..1）。</summary>
        public static float BarrageAccuracy(float targetingData, float range)
            => BarrageAccuracy(targetingData, range, SiegeBatteryParams.Default);

        /// <summary>
        /// 防御側の対砲戦による砲撃側の損失（0..1）＝防御側の反撃 defenderCounterFire ×攻撃側の露出 attackerExposure ×致死性。
        /// 防御側が撃ち返し・砲撃側が身を晒すほど削られる。致死性は <see cref="SiegeBatteryParams.counterLethality"/>。両入力はクランプ。
        /// </summary>
        public static float CounterBatteryLoss(float defenderCounterFire, float attackerExposure, SiegeBatteryParams p)
        {
            float counterFire = Mathf.Clamp01(defenderCounterFire);
            float exposure = Mathf.Clamp01(attackerExposure);
            return Mathf.Clamp01(counterFire * exposure * p.counterLethality);
        }

        /// <summary>既定係数での対砲戦損失（0..1）。</summary>
        public static float CounterBatteryLoss(float defenderCounterFire, float attackerExposure)
            => CounterBatteryLoss(defenderCounterFire, attackerExposure, SiegeBatteryParams.Default);

        /// <summary>
        /// 弾薬の消費（≥0）＝砲撃集中 barrageConcentration ×砲撃時間 bombardmentTime ×消費率。
        /// 砲門を集中し撃ち続けるほど弾を食う（長期砲撃のコスト）。消費率は <see cref="SiegeBatteryParams.ammoRate"/>。両入力は負を切り上げ。
        /// </summary>
        public static float AmmunitionExpenditure(float barrageConcentration, float bombardmentTime, SiegeBatteryParams p)
        {
            float conc = Mathf.Max(0f, barrageConcentration);
            float time = Mathf.Max(0f, bombardmentTime);
            return Mathf.Max(0f, conc * time * p.ammoRate);
        }

        /// <summary>既定係数での弾薬消費（≥0）。</summary>
        public static float AmmunitionExpenditure(float barrageConcentration, float bombardmentTime)
            => AmmunitionExpenditure(barrageConcentration, bombardmentTime, SiegeBatteryParams.Default);

        /// <summary>
        /// 突破口の形成（0..1）＝累積装甲損傷 cumulativeArmorDamage ×砲撃精度 barrageAccuracy ×形成率。
        /// 装甲を削り命中を集めるほど突破口が開く。形成率は <see cref="SiegeBatteryParams.breachRate"/>。損傷は切り上げ・精度はクランプ。
        /// </summary>
        public static float BreachFormation(float cumulativeArmorDamage, float barrageAccuracy, SiegeBatteryParams p)
        {
            float damage = Mathf.Max(0f, cumulativeArmorDamage);
            float accuracy = Mathf.Clamp01(barrageAccuracy);
            return Mathf.Clamp01(damage * accuracy * p.breachRate);
        }

        /// <summary>既定係数での突破口形成（0..1）。</summary>
        public static float BreachFormation(float cumulativeArmorDamage, float barrageAccuracy)
            => BreachFormation(cumulativeArmorDamage, barrageAccuracy, SiegeBatteryParams.Default);

        /// <summary>
        /// 突破口が開いたか＝形成度 breachFormation が閾値 threshold 以上なら true（攻略の取っ掛かり成立）。
        /// threshold はクランプ。形成度が閾値未満なら装甲はまだ持ちこたえる。
        /// </summary>
        public static bool IsBreachAchieved(float breachFormation, float threshold)
        {
            float breach = Mathf.Clamp01(breachFormation);
            float thr = Mathf.Clamp01(threshold);
            return breach >= thr;
        }

        /// <summary>既定閾値（<see cref="SiegeBatteryParams.breachThreshold"/>）での突破口判定。</summary>
        public static bool IsBreachAchieved(float breachFormation)
            => IsBreachAchieved(breachFormation, SiegeBatteryParams.Default.breachThreshold);
    }
}
