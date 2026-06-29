using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 制宙戦略（宇宙の制海権思想＝マハン的シーパワーの宇宙版）の調整値。
    /// 航路支配の効き・制宙権確立の効き（指数）・維持費がコストに効く倍率・戦略価値のコスト重み。
    /// </summary>
    public readonly struct MaritimeStrategyParams
    {
        /// <summary>主力艦隊×航路プレゼンス→航路支配の効き（0..1のクランプ後に乗算）。</summary>
        public readonly float laneControlScale;
        /// <summary>主力戦力差→制宙権確立の効き（指数・大きいほど僅差で一方に振れる）。</summary>
        public readonly float commandExponent;
        /// <summary>主力艦隊×維持費→制宙のコストの効き（0..1のクランプ後に乗算）。</summary>
        public readonly float costScale;
        /// <summary>制宙戦略の価値計算でコスト（負の項）に掛ける重み。</summary>
        public readonly float costWeight;

        public MaritimeStrategyParams(float laneControlScale, float commandExponent, float costScale, float costWeight)
        {
            this.laneControlScale = Mathf.Clamp01(laneControlScale);
            this.commandExponent = Mathf.Max(0.01f, commandExponent);
            this.costScale = Mathf.Clamp01(costScale);
            this.costWeight = Mathf.Clamp01(costWeight);
        }

        /// <summary>既定：航路支配の効き1.0・制宙権指数1.0（線形）・コストの効き0.5・コスト重み0.5。</summary>
        public static MaritimeStrategyParams Default
            => new MaritimeStrategyParams(DefaultLaneControlScale, DefaultCommandExponent, DefaultCostScale, DefaultCostWeight);

        public const float DefaultLaneControlScale = 1.0f;
        public const float DefaultCommandExponent = 1.0f;
        public const float DefaultCostScale = 0.5f;
        public const float DefaultCostWeight = 0.5f;
    }

    /// <summary>
    /// 制宙戦略（Command of the Space＝マハン的制海権思想の宇宙版）の純ロジック。
    /// 宇宙航路の支配による経済的利益・主力艦隊による制宙権の確立・通商路の確保・敵艦隊の封じ込め・
    /// 制宙のコスト・海洋国家型（艦隊重視）vs大陸国家型（領域重視）の戦略選択を扱う「勢力規模の戦略思想」。
    ///
    /// <b>分担</b>：
    /// - <see cref="SpaceSuperiorityRules"/>（宙域レベルの制宙権の局所獲得）とは別＝こちらは<b>勢力規模の制宙戦略思想</b>（経済・封じ込め・国家戦略の選択）。
    /// 盤面非依存の plain 引数のみ（艦隊・シーンに触れない）。test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class MaritimeStrategyRules
    {
        /// <summary>既定パラメータで航路支配を返す。</summary>
        public static float SpaceLaneControl(float battleFleetStrength, float lanePresence)
            => SpaceLaneControl(battleFleetStrength, lanePresence, MaritimeStrategyParams.Default);

        /// <summary>
        /// 主力艦隊（battleFleetStrength）×航路プレゼンス（lanePresence）で宇宙航路の支配度(0..1)を返す。
        /// どちらかが0なら支配なし（艦隊だけでも航路に居なければ支配できない）。
        /// </summary>
        public static float SpaceLaneControl(float battleFleetStrength, float lanePresence, MaritimeStrategyParams p)
        {
            float fleet = Mathf.Clamp01(battleFleetStrength);
            float presence = Mathf.Clamp01(lanePresence);
            return Mathf.Clamp01(fleet * presence * p.laneControlScale);
        }

        /// <summary>既定パラメータで経済的利益を返す。</summary>
        public static float EconomicBenefit(float spaceLaneControl, float tradeVolume)
            => EconomicBenefit(spaceLaneControl, tradeVolume, MaritimeStrategyParams.Default);

        /// <summary>
        /// 航路支配（spaceLaneControl）×交易量（tradeVolume）で制宙の経済的利益(0..1)を返す。
        /// 航路を握るほど・交易量が多いほど利益が大きい（マハンのシーパワー＝航路支配が富を生む）。
        /// </summary>
        public static float EconomicBenefit(float spaceLaneControl, float tradeVolume, MaritimeStrategyParams p)
        {
            float control = Mathf.Clamp01(spaceLaneControl);
            float trade = Mathf.Clamp01(tradeVolume);
            return Mathf.Clamp01(control * trade);
        }

        /// <summary>既定パラメータで制宙権を返す。</summary>
        public static float CommandOfTheSpace(float battleFleetStrength, float enemyFleetStrength)
            => CommandOfTheSpace(battleFleetStrength, enemyFleetStrength, MaritimeStrategyParams.Default);

        /// <summary>
        /// 主力/(主力+敵主力) で制宙権(0..1)を返す。拮抗で0.5・敵不在で1・自不在で0。
        /// 指数 commandExponent で僅差の効きを調整（大きいほど一方に振れる）。双方0なら0.5（真空＝拮抗扱い）。
        /// </summary>
        public static float CommandOfTheSpace(float battleFleetStrength, float enemyFleetStrength, MaritimeStrategyParams p)
        {
            float own = Mathf.Max(0f, battleFleetStrength);
            float enemy = Mathf.Max(0f, enemyFleetStrength);
            float total = own + enemy;
            if (total <= 0f) return 0.5f; // 双方不在＝拮抗
            float share = own / total;     // 0..1
            return Mathf.Clamp01(Mathf.Pow(share, p.commandExponent));
        }

        /// <summary>既定パラメータで敵封じ込めを返す。</summary>
        public static float EnemyContainment(float commandOfTheSpace, float blockadePosture)
            => EnemyContainment(commandOfTheSpace, blockadePosture, MaritimeStrategyParams.Default);

        /// <summary>
        /// 制宙権（commandOfTheSpace）×封鎖態勢（blockadePosture）で敵艦隊の封じ込め度(0..1)を返す。
        /// 制宙権を握り封鎖に出るほど敵を港に押し込める（マハンの艦隊封鎖）。
        /// </summary>
        public static float EnemyContainment(float commandOfTheSpace, float blockadePosture, MaritimeStrategyParams p)
        {
            float command = Mathf.Clamp01(commandOfTheSpace);
            float blockade = Mathf.Clamp01(blockadePosture);
            return Mathf.Clamp01(command * blockade);
        }

        /// <summary>既定パラメータで制宙コストを返す。</summary>
        public static float SeaPowerCost(float battleFleetStrength, float fleetMaintenance)
            => SeaPowerCost(battleFleetStrength, fleetMaintenance, MaritimeStrategyParams.Default);

        /// <summary>
        /// 主力艦隊（battleFleetStrength）×維持費（fleetMaintenance）で制宙のコスト(0..1)を返す。
        /// 大艦隊ほど・維持費が高いほど制宙の負担が重い（シーパワーは高くつく）。
        /// </summary>
        public static float SeaPowerCost(float battleFleetStrength, float fleetMaintenance, MaritimeStrategyParams p)
        {
            float fleet = Mathf.Clamp01(battleFleetStrength);
            float maintenance = Mathf.Clamp01(fleetMaintenance);
            return Mathf.Clamp01(fleet * maintenance * p.costScale);
        }

        /// <summary>既定パラメータで海洋国家型の傾斜を返す。</summary>
        public static float NavalVsContinental(float fleetInvestment, float territorialInvestment)
            => NavalVsContinental(fleetInvestment, territorialInvestment, MaritimeStrategyParams.Default);

        /// <summary>
        /// 艦隊投資/(艦隊+領域投資) で戦略の傾斜(0..1)を返す。0=大陸国家型（領域重視）/1=海洋国家型（艦隊重視）。
        /// 双方0なら0.5（中立＝どちらにも傾かない）。
        /// </summary>
        public static float NavalVsContinental(float fleetInvestment, float territorialInvestment, MaritimeStrategyParams p)
        {
            float fleet = Mathf.Max(0f, fleetInvestment);
            float territory = Mathf.Max(0f, territorialInvestment);
            float total = fleet + territory;
            if (total <= 0f) return 0.5f; // 双方無投資＝中立
            return Mathf.Clamp01(fleet / total);
        }

        /// <summary>既定パラメータで戦略的到達範囲を返す。</summary>
        public static float StrategicReach(float commandOfTheSpace, float spaceLaneControl)
            => StrategicReach(commandOfTheSpace, spaceLaneControl, MaritimeStrategyParams.Default);

        /// <summary>
        /// 制宙権（commandOfTheSpace）×航路支配（spaceLaneControl）で戦略的到達範囲(0..1)を返す。
        /// 制宙権を握り航路を支配するほど遠くへ力を投射できる（マハンの戦略的機動）。
        /// </summary>
        public static float StrategicReach(float commandOfTheSpace, float spaceLaneControl, MaritimeStrategyParams p)
        {
            float command = Mathf.Clamp01(commandOfTheSpace);
            float lane = Mathf.Clamp01(spaceLaneControl);
            return Mathf.Clamp01(command * lane);
        }

        /// <summary>既定パラメータで制宙戦略の価値を返す。</summary>
        public static float MaritimeStrategyValue(float economicBenefit, float enemyContainment, float seaPowerCost)
            => MaritimeStrategyValue(economicBenefit, enemyContainment, seaPowerCost, MaritimeStrategyParams.Default);

        /// <summary>
        /// 経済利益×封じ込め − コスト×重み で制宙戦略の総合価値(-1..1)を返す。
        /// 経済的利益と敵封じ込めの相乗がコストを上回れば正（制宙戦略は割に合う）、下回れば負。
        /// </summary>
        public static float MaritimeStrategyValue(float economicBenefit, float enemyContainment, float seaPowerCost, MaritimeStrategyParams p)
        {
            float benefit = Mathf.Clamp01(economicBenefit);
            float containment = Mathf.Clamp01(enemyContainment);
            float cost = Mathf.Clamp01(seaPowerCost);
            float gain = benefit * containment;        // 0..1（利益と封じ込めの相乗）
            float value = gain - cost * p.costWeight;   // -costWeight..1
            return Mathf.Clamp(value, -1f, 1f);
        }
    }
}
