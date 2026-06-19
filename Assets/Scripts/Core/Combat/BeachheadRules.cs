using UnityEngine;

namespace Ginei
{
    /// <summary>橋頭堡（上陸拠点）確保・拡張の調整係数。すべて 0..1 の比率で扱う（量は強度比へ正規化）。</summary>
    public readonly struct BeachheadParams
    {
        /// <summary>敵防御火力の効き（第一波の被害倍率）。大きいほど水際で削られる。</summary>
        public readonly float defensiveFireScale;
        /// <summary>確保面積（足場）の効き。橋頭堡確立への寄与倍率。</summary>
        public readonly float footholdScale;
        /// <summary>敵反撃（水際撃滅）圧力の効き。大きいほど反撃が橋頭堡を脅かす。</summary>
        public readonly float counterScale;
        /// <summary>増援流入（揚陸輸送）の効き。</summary>
        public readonly float sealiftScale;
        /// <summary>補給集積（物資処理）の効き。</summary>
        public readonly float logisticsScale;
        /// <summary>突破口への発展準備の既定閾値（拡張がこの値以上で突破口が開く）。</summary>
        public readonly float breakoutThreshold;

        public BeachheadParams(float defensiveFireScale, float footholdScale, float counterScale,
                               float sealiftScale, float logisticsScale, float breakoutThreshold)
        {
            this.defensiveFireScale = Mathf.Clamp01(defensiveFireScale);
            this.footholdScale = Mathf.Clamp01(footholdScale);
            this.counterScale = Mathf.Clamp01(counterScale);
            this.sealiftScale = Mathf.Clamp01(sealiftScale);
            this.logisticsScale = Mathf.Clamp01(logisticsScale);
            this.breakoutThreshold = Mathf.Clamp01(breakoutThreshold);
        }

        /// <summary>既定係数。各効きは等倍、突破口の閾値は 0.5（拡張が半ばを超えれば突破）。作者調整可。</summary>
        public static BeachheadParams Default =>
            new BeachheadParams(1f, 1f, 1f, 1f, 1f, 0.5f);
    }

    /// <summary>
    /// 橋頭堡（ビーチヘッド＝上陸拠点）の確保と拡張の純ロジック（test-first）。
    /// 上陸作戦の段階を二者の綱引きでモデル化する：
    /// ・<b>第一波の生存</b>＝上陸戦力×(1−敵防御火力)。水際で削られた残りが浜へ取り付く。
    /// ・<b>橋頭堡の確立</b>＝第一波生存×確保面積（足場）。
    /// ・<b>水際反撃の圧力</b>＝敵予備×反撃速度（敵の即応で押し戻される）。
    /// ・<b>橋頭堡の防衛</b>＝確立/(確立+反撃)。拮抗で 0.5、確立が勝てば踏みとどまる（0..1）。
    /// ・<b>増援の流入</b>／<b>補給の集積</b>＝防衛×輸送力／物資処理（守れている間だけ後続が入る）。
    /// ・<b>橋頭堡の拡張</b>＝増援と補給の相乗（幾何平均＝どちらか欠けると伸びない）。
    /// ・<b>突破口への発展</b>＝拡張が閾値を超え、なお反撃圧力に屈しなければ突破口が開く（bool）。
    /// すべて 0..1。決定論・乱数なし。純ロジック（非 MonoBehaviour）。実効値パターン（基準非破壊）。
    /// </summary>
    public static class BeachheadRules
    {
        /// <summary>
        /// 第一波の生存（0..1）＝上陸戦力×(1−敵防御火力)。敵火力が強いほど浜に届く戦力が減る。
        /// </summary>
        public static float FirstWaveSurvival(float landingForce, float enemyDefensiveFire, BeachheadParams p)
        {
            float force = Mathf.Clamp01(landingForce);
            float fire = Mathf.Clamp01(Mathf.Clamp01(enemyDefensiveFire) * p.defensiveFireScale);
            return Mathf.Clamp01(force * (1f - fire));
        }

        public static float FirstWaveSurvival(float landingForce, float enemyDefensiveFire)
            => FirstWaveSurvival(landingForce, enemyDefensiveFire, BeachheadParams.Default);

        /// <summary>
        /// 橋頭堡の確立（0..1）＝第一波生存×確保面積（足場）。足場が無ければ確立しない。
        /// </summary>
        public static float BeachheadEstablishment(float firstWaveSurvival, float footholdSize, BeachheadParams p)
        {
            float foothold = Mathf.Clamp01(Mathf.Clamp01(footholdSize) * p.footholdScale);
            return Mathf.Clamp01(Mathf.Clamp01(firstWaveSurvival) * foothold);
        }

        public static float BeachheadEstablishment(float firstWaveSurvival, float footholdSize)
            => BeachheadEstablishment(firstWaveSurvival, footholdSize, BeachheadParams.Default);

        /// <summary>
        /// 水際反撃の圧力（0..1）＝敵予備×反撃速度。敵が即応で予備を投じるほど橋頭堡を脅かす。
        /// </summary>
        public static float CounterattackPressure(float enemyReserves, float responseSpeed, BeachheadParams p)
        {
            float reserves = Mathf.Clamp01(enemyReserves);
            float speed = Mathf.Clamp01(responseSpeed);
            return Mathf.Clamp01(reserves * speed * p.counterScale);
        }

        public static float CounterattackPressure(float enemyReserves, float responseSpeed)
            => CounterattackPressure(enemyReserves, responseSpeed, BeachheadParams.Default);

        /// <summary>
        /// 橋頭堡の防衛（0..1）＝確立/(確立+反撃)。拮抗で 0.5、確立優位で踏みとどまる。両者0なら0。
        /// </summary>
        public static float BeachheadDefense(float beachheadEstablishment, float counterattackPressure, BeachheadParams p)
        {
            float est = Mathf.Clamp01(beachheadEstablishment);
            float pressure = Mathf.Clamp01(counterattackPressure);
            float denom = est + pressure;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(est / denom);
        }

        public static float BeachheadDefense(float beachheadEstablishment, float counterattackPressure)
            => BeachheadDefense(beachheadEstablishment, counterattackPressure, BeachheadParams.Default);

        /// <summary>
        /// 増援の流入（0..1）＝防衛×揚陸輸送力。守れている橋頭堡へだけ後続が流れ込む。
        /// </summary>
        public static float ReinforcementInflow(float beachheadDefense, float sealiftCapacity, BeachheadParams p)
        {
            float lift = Mathf.Clamp01(Mathf.Clamp01(sealiftCapacity) * p.sealiftScale);
            return Mathf.Clamp01(Mathf.Clamp01(beachheadDefense) * lift);
        }

        public static float ReinforcementInflow(float beachheadDefense, float sealiftCapacity)
            => ReinforcementInflow(beachheadDefense, sealiftCapacity, BeachheadParams.Default);

        /// <summary>
        /// 補給の集積（0..1）＝防衛×物資処理能力。守れている間だけ物資を浜へ揚げられる。
        /// </summary>
        public static float SupplyBuildup(float beachheadDefense, float logisticsThroughput, BeachheadParams p)
        {
            float thru = Mathf.Clamp01(Mathf.Clamp01(logisticsThroughput) * p.logisticsScale);
            return Mathf.Clamp01(Mathf.Clamp01(beachheadDefense) * thru);
        }

        public static float SupplyBuildup(float beachheadDefense, float logisticsThroughput)
            => SupplyBuildup(beachheadDefense, logisticsThroughput, BeachheadParams.Default);

        /// <summary>
        /// 橋頭堡の拡張（0..1）＝増援×補給の相乗（幾何平均）。人も物資も揃ってはじめて橋頭堡が広がる。
        /// </summary>
        public static float BeachheadExpansion(float reinforcementInflow, float supplyBuildup, BeachheadParams p)
        {
            float r = Mathf.Clamp01(reinforcementInflow);
            float s = Mathf.Clamp01(supplyBuildup);
            return Mathf.Clamp01(Mathf.Sqrt(r * s));
        }

        public static float BeachheadExpansion(float reinforcementInflow, float supplyBuildup)
            => BeachheadExpansion(reinforcementInflow, supplyBuildup, BeachheadParams.Default);

        /// <summary>
        /// 突破口への発展準備（bool）＝拡張から反撃圧力の半分を差し引いた実効拡張が閾値以上か。
        /// 橋頭堡が広がっても、なお水際反撃に押されていれば突破口は開かない（実効値パターン）。
        /// </summary>
        public static bool BreakoutReadiness(float beachheadExpansion, float counterattackPressure, float threshold)
        {
            float expansion = Mathf.Clamp01(beachheadExpansion);
            float pressure = Mathf.Clamp01(counterattackPressure);
            float effective = Mathf.Clamp01(expansion - 0.5f * pressure);
            return effective >= Mathf.Clamp01(threshold);
        }

        public static bool BreakoutReadiness(float beachheadExpansion, float counterattackPressure)
            => BreakoutReadiness(beachheadExpansion, counterattackPressure, BeachheadParams.Default.breakoutThreshold);
    }
}
