using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 制海権てこ作用の調整係数（SKUN-4 #1434・坂の上の雲/マハン）。
    /// 制宙権（command of the space）が陸上（惑星）作戦へ与えるボーナスの強さを決める。
    /// </summary>
    public readonly struct SeaControlLeverageParams
    {
        /// <summary>制宙権による攻城ボーナスの最大上乗せ（実効値＝1+これ。上陸・砲撃の自由）。</summary>
        public readonly float siegeBonusMax;
        /// <summary>制宙権による補給保証の強さ（制宙権があれば補給線が通る度合い）。</summary>
        public readonly float supplyAssuranceWeight;
        /// <summary>制宙権による敵補給遮断の最大効果（敵が補給依存なら兵糧攻めが効く）。</summary>
        public readonly float interdictionMax;
        /// <summary>制宙権による上陸の実現性の床（制宙権ゼロでも残る最低限の実現性）。</summary>
        public readonly float amphibiousFloor;
        /// <summary>制宙権確立とみなす既定閾値（IsSeaControlEstablished のフォールバック）。</summary>
        public readonly float establishedThreshold;

        public SeaControlLeverageParams(float siegeBonusMax, float supplyAssuranceWeight,
            float interdictionMax, float amphibiousFloor, float establishedThreshold)
        {
            this.siegeBonusMax = siegeBonusMax;
            this.supplyAssuranceWeight = supplyAssuranceWeight;
            this.interdictionMax = interdictionMax;
            this.amphibiousFloor = amphibiousFloor;
            this.establishedThreshold = establishedThreshold;
        }

        /// <summary>既定係数（攻城上乗せ0.5・補給保証0.8・敵補給遮断0.9・上陸床0.1・確立閾値0.7）。</summary>
        public static SeaControlLeverageParams Default => new SeaControlLeverageParams(0.5f, 0.8f, 0.9f, 0.1f, 0.7f);
    }

    /// <summary>
    /// 制海権×陸上作戦協調の純ロジック（SKUN-4 #1434・坂の上の雲/マハン）。
    /// マハン的海軍戦略＝<b>制海権（command of the sea＝宇宙では制宙権）を握ると陸上（惑星）作戦に決定的優位をもたらす</b>：
    /// 海上交通の自由が補給を保証し、上陸・攻城を可能にし、敵の補給を断つ。<b>制宙権なき陸上作戦は補給に窮する</b>。
    /// 制宙権の保有が隣接惑星の攻城・補給にボーナスを与える（制宙権→攻城/補給ボーナス）。
    /// <see cref="LogisticsRules"/>（版図の一体化＝国力割引）／<see cref="PlanetSiegeRules"/>（惑星攻城の実体）／
    /// <see cref="SupplyRules"/>（補給線の到達＝回廊グラフ）／<see cref="BlockadeRules"/>（回廊封鎖の面）とは別＝
    /// こちらは<b>制宙権が陸上作戦を有利にするてこ作用の係数</b>のみを算出する（基準値非破壊・実効値パターン）。
    /// 乱数なし決定論・全入力クランプ。純ロジック（test-first）。
    /// </summary>
    public static class SeaControlLeverageRules
    {
        /// <summary>
        /// 制宙権の確実さ 0..1＝艦隊の優越 × (1−係争度)。
        /// 敵艦隊が残る（係争度が高い）ほど制宙権は不確実になる＝完全な制宙権には敵艦隊の排除が要る。
        /// </summary>
        public static float CommandOfSpace(float fleetDominance, float contested)
        {
            float dom = Mathf.Clamp01(fleetDominance);
            float con = Mathf.Clamp01(contested);
            return dom * (1f - con);
        }

        /// <summary>
        /// 制宙権が惑星攻城を有利にする実効倍率（上陸・砲撃の自由）。
        /// 制宙権ゼロで1.0（ボーナスなし）、最大で 1+siegeBonusMax＝攻城が加速する。実効値≥1.0（基準非破壊）。
        /// </summary>
        public static float SiegeBonus(float commandOfSpace, SeaControlLeverageParams prm)
        {
            float cmd = Mathf.Clamp01(commandOfSpace);
            return 1f + Mathf.Max(0f, prm.siegeBonusMax) * cmd;
        }

        /// <summary>既定係数版。</summary>
        public static float SiegeBonus(float commandOfSpace) => SiegeBonus(commandOfSpace, SeaControlLeverageParams.Default);

        /// <summary>
        /// 制宙権が補給線を保証する度合い 0..1（海上交通の自由）。
        /// supplyLineExposure＝補給線の露出度（通商破壊に晒される度合い）。制宙権があれば露出していても補給が通り、
        /// なければ露出ぶんだけ補給が断たれる＝<b>制宙権が補給を保証する</b>。露出ゼロなら制宙権に依らず満額。
        /// </summary>
        public static float SupplyAssurance(float commandOfSpace, float supplyLineExposure, SeaControlLeverageParams prm)
        {
            float cmd = Mathf.Clamp01(commandOfSpace);
            float exposure = Mathf.Clamp01(supplyLineExposure);
            float weight = Mathf.Clamp01(prm.supplyAssuranceWeight);
            // 露出している補給線のうち、制宙権が守れる割合だけ保証される。
            float lost = exposure * (1f - cmd) * weight;
            return Mathf.Clamp01(1f - lost);
        }

        /// <summary>既定係数版。</summary>
        public static float SupplyAssurance(float commandOfSpace, float supplyLineExposure)
            => SupplyAssurance(commandOfSpace, supplyLineExposure, SeaControlLeverageParams.Default);

        /// <summary>
        /// 制宙権で敵の補給を断つ効果 0..1（兵糧攻め）。
        /// enemySupplyDependence＝敵が外部補給にどれだけ依存するか。依存が大きいほど遮断が効く＝
        /// <b>制宙権で敵の補給線を断つ</b>。敵が自給自足（依存0）なら制宙権を握っても遮断効果は薄い。
        /// </summary>
        public static float EnemySupplyInterdiction(float commandOfSpace, float enemySupplyDependence, SeaControlLeverageParams prm)
        {
            float cmd = Mathf.Clamp01(commandOfSpace);
            float dep = Mathf.Clamp01(enemySupplyDependence);
            return Mathf.Clamp01(Mathf.Max(0f, prm.interdictionMax) * cmd * dep);
        }

        /// <summary>既定係数版。</summary>
        public static float EnemySupplyInterdiction(float commandOfSpace, float enemySupplyDependence)
            => EnemySupplyInterdiction(commandOfSpace, enemySupplyDependence, SeaControlLeverageParams.Default);

        /// <summary>
        /// 制宙権が上陸作戦を可能にする実現性 0..1。
        /// landingResistance＝着上陸への抵抗（軌道防衛・地上火力）。制宙権があれば抵抗を制圧して上陸できるが、
        /// <b>制宙権なき上陸は不可能に近い</b>＝制宙権ゼロでは amphibiousFloor まで落ちる。
        /// </summary>
        public static float AmphibiousFeasibility(float commandOfSpace, float landingResistance, SeaControlLeverageParams prm)
        {
            float cmd = Mathf.Clamp01(commandOfSpace);
            float resist = Mathf.Clamp01(landingResistance);
            float floor = Mathf.Clamp01(prm.amphibiousFloor);
            // 制宙権が抵抗を抑え込む：抵抗の (1−制宙権) ぶんだけ実現性が削られ、下限は floor。
            float feasible = 1f - resist * (1f - cmd);
            return Mathf.Clamp(feasible, floor, 1f);
        }

        /// <summary>既定係数版。</summary>
        public static float AmphibiousFeasibility(float commandOfSpace, float landingResistance)
            => AmphibiousFeasibility(commandOfSpace, landingResistance, SeaControlLeverageParams.Default);

        /// <summary>
        /// 制宙権と陸上戦力の協調効果 0..1（海陸一体＝相乗）。
        /// 海と陸が揃ってはじめて決定的優位になる＝積で相乗（どちらか欠けると効果が出ない）。
        /// </summary>
        public static float CombinedArmsSynergy(float seaControl, float landForce)
        {
            float sea = Mathf.Clamp01(seaControl);
            float land = Mathf.Clamp01(landForce);
            return sea * land;
        }

        /// <summary>
        /// 制宙権が争われていると陸上作戦が不安定になるペナルティ倍率 0..1。
        /// contested＝制宙権の係争度。係争が大きいほど陸上作戦が不安定になる＝1−係争度。
        /// </summary>
        public static float ContestedSpacePenalty(float contested)
            => 1f - Mathf.Clamp01(contested);

        /// <summary>制宙権を確立した判定（commandOfSpace が閾値以上）。</summary>
        public static bool IsSeaControlEstablished(float commandOfSpace, float threshold)
            => Mathf.Clamp01(commandOfSpace) >= Mathf.Clamp01(threshold);

        /// <summary>既定閾値版（SeaControlLeverageParams.Default.establishedThreshold）。</summary>
        public static bool IsSeaControlEstablished(float commandOfSpace)
            => IsSeaControlEstablished(commandOfSpace, SeaControlLeverageParams.Default.establishedThreshold);
    }
}
