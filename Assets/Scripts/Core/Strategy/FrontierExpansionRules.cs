using UnityEngine;

namespace Ginei
{
    /// <summary>辺境拡大の調整係数。拡張ペース・補給線の伸長・防衛コスト・果実・過伸長・統治低下の重みを束ねる。</summary>
    public readonly struct FrontierExpansionParams
    {
        /// <summary>拡張ペースの基準ゲイン（軍事力×未開拓×経済余剰に掛ける）。</summary>
        public readonly float expansionGain;
        /// <summary>補給線伸長の基準ゲイン（版図/兵站能力比に掛ける）。</summary>
        public readonly float stretchGain;
        /// <summary>辺境防衛コストの基準ゲイン（国境線×脅威に掛ける）。</summary>
        public readonly float defenseGain;
        /// <summary>拡大の経済的果実の基準ゲイン（版図×開発度に掛ける）。</summary>
        public readonly float yieldGain;
        /// <summary>過伸長の基準ゲイン（補給負荷＋防衛負荷の経済力比に掛ける）。</summary>
        public readonly float overstretchGain;
        /// <summary>辺境統治力低下の基準ゲイン（版図/統治能力比に掛ける）。</summary>
        public readonly float decayGain;

        public FrontierExpansionParams(float expansionGain, float stretchGain, float defenseGain,
                                       float yieldGain, float overstretchGain, float decayGain)
        {
            this.expansionGain = Mathf.Clamp(expansionGain, 0f, 4f);
            this.stretchGain = Mathf.Clamp(stretchGain, 0f, 4f);
            this.defenseGain = Mathf.Clamp(defenseGain, 0f, 4f);
            this.yieldGain = Mathf.Clamp(yieldGain, 0f, 4f);
            this.overstretchGain = Mathf.Clamp(overstretchGain, 0f, 4f);
            this.decayGain = Mathf.Clamp(decayGain, 0f, 4f);
        }

        /// <summary>既定＝拡張1.0・伸長1.0・防衛0.5・果実0.5・過伸長1.0・統治低下1.0。</summary>
        public static FrontierExpansionParams Default => new FrontierExpansionParams(1f, 1f, 0.5f, 0.5f, 1f, 1f);
    }

    /// <summary>
    /// 辺境拡大の純ロジック。勢力圏を辺境へ広げる動きを扱う＝拡張のペース・補給線の伸長・辺境の防衛コスト・
    /// 版図拡大の経済的果実・過伸長（オーバーストレッチ）の危険・辺境の統治力低下、そして正味の拡大価値。
    /// 「広げすぎた版図は補給と統治で自壊する＝勝ちすぎが負ける＝過伸長」を表す。
    /// 中央からの距離が生む文化的効果は <see cref="FrontierRules"/>、版図の連結度は <see cref="LogisticsRules"/> が担う
    /// ＝こちらは「どこまで広げるべきか」の損益勘定に特化（分担を分ける）。
    /// 全入力クランプ・乱数なし決定論・基準値非破壊（係数/評価を返すのみ）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FrontierExpansionRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---
        public const float MaxRate = 1f;             // 拡張ペースの上限(0..1)
        public const float MaxStretch = 1f;          // 補給線伸長の上限(0..1)
        public const float MaxDefenseCost = 1f;      // 防衛コストの上限(0..1)
        public const float MaxYield = 1f;            // 経済的果実の上限(0..1)
        public const float MaxOverstretch = 1f;      // 過伸長の上限(0..1)
        public const float MaxDecay = 1f;            // 統治力低下の上限(0..1)
        public const float MaxCapacity = 4f;         // 兵站/経済/統治能力の正規化上限（比の分母安定）
        public const float MinCapacity = 0.0001f;    // 能力の下限（ゼロ割回避）
        public const float SustainThreshold = 0f;    // 持続可能と見なす正味価値の既定閾値

        /// <summary>
        /// 拡張ペース(0..1)＝軍事力×未開拓辺境×経済余剰。三者すべてが揃って初めて広げられる＝
        /// 力がなければ取れず、未開拓がなければ広げる先がなく、余剰がなければ広げる金がない。
        /// </summary>
        /// <param name="militaryPower">軍事力(0..1)。辺境へ投射できる戦力の余裕。</param>
        /// <param name="openFrontier">未開拓辺境(0..1)。広げる余地の大きさ。</param>
        /// <param name="economicSurplus">経済余剰(0..1)。拡張を支える財政の余裕。</param>
        public static float ExpansionRate(float militaryPower, float openFrontier, float economicSurplus,
                                          FrontierExpansionParams p)
        {
            float mil = Mathf.Clamp01(militaryPower);
            float open = Mathf.Clamp01(openFrontier);
            float surplus = Mathf.Clamp01(economicSurplus);
            float rate = p.expansionGain * mil * open * surplus;
            return Mathf.Clamp(rate, 0f, MaxRate);
        }

        public static float ExpansionRate(float militaryPower, float openFrontier, float economicSurplus)
            => ExpansionRate(militaryPower, openFrontier, economicSurplus, FrontierExpansionParams.Default);

        /// <summary>
        /// 補給線の伸長(0..1)＝版図/兵站能力。版図が兵站能力に対して大きいほど補給線が伸びきる＝
        /// 兵站能力に等しい版図で半飽和（比1で0.5）、版図が能力を超えると急速に伸びる。
        /// </summary>
        /// <param name="territorySize">版図の大きさ(0以上)。</param>
        /// <param name="logisticsCapacity">兵站能力(0以上)。補給を届けられる射程・量。</param>
        public static float SupplyLineStretch(float territorySize, float logisticsCapacity, FrontierExpansionParams p)
        {
            float size = Mathf.Max(0f, territorySize);
            float cap = Mathf.Clamp(logisticsCapacity, MinCapacity, MaxCapacity);
            float ratio = p.stretchGain * size / cap;
            // 比 → 飽和(0..1)。ratio=cap相当(=1/stretchGain×cap)で0.5、伸びるほど1へ漸近
            float stretch = ratio / (ratio + 1f);
            return Mathf.Clamp(stretch, 0f, MaxStretch);
        }

        public static float SupplyLineStretch(float territorySize, float logisticsCapacity)
            => SupplyLineStretch(territorySize, logisticsCapacity, FrontierExpansionParams.Default);

        /// <summary>
        /// 辺境の防衛コスト(0..1)＝国境線×脅威。守るべき国境が長く脅威が高いほど守備に資源を食われる＝
        /// 広げた版図は守る縁(へり)も長くなる（広げるほど守りが薄くなる）。
        /// </summary>
        /// <param name="borderLength">国境線の長さ(0..1)。版図が広いほど長い。</param>
        /// <param name="threatLevel">辺境の脅威度(0..1)。外敵・盗賊の圧力。</param>
        public static float FrontierDefenseCost(float borderLength, float threatLevel, FrontierExpansionParams p)
        {
            float border = Mathf.Clamp01(borderLength);
            float threat = Mathf.Clamp01(threatLevel);
            float cost = p.defenseGain * border * threat;
            return Mathf.Clamp(cost, 0f, MaxDefenseCost);
        }

        public static float FrontierDefenseCost(float borderLength, float threatLevel)
            => FrontierDefenseCost(borderLength, threatLevel, FrontierExpansionParams.Default);

        /// <summary>
        /// 拡大の経済的果実(0..1)＝版図×開発度。広い版図でも未開発なら実りは薄く、開発が進むほど果実になる＝
        /// 「広さ×中身」で初めて富む（征服しただけでは富まない）。
        /// </summary>
        /// <param name="territorySize">版図の大きさ(0..1)。</param>
        /// <param name="developmentLevel">開発度(0..1)。版図がどれだけ生産的に育っているか。</param>
        public static float TerritorialYield(float territorySize, float developmentLevel, FrontierExpansionParams p)
        {
            float size = Mathf.Clamp01(territorySize);
            float dev = Mathf.Clamp01(developmentLevel);
            float yield = p.yieldGain * size * dev;
            return Mathf.Clamp(yield, 0f, MaxYield);
        }

        public static float TerritorialYield(float territorySize, float developmentLevel)
            => TerritorialYield(territorySize, developmentLevel, FrontierExpansionParams.Default);

        /// <summary>
        /// 過伸長(0..1)＝(補給伸長＋防衛コスト)/経済力。版図維持の負荷が経済力を上回るほど過伸長＝
        /// 「広げすぎた版図が国力を食い潰す」。比1（負荷＝経済力）で0.5、超えると1へ漸近。
        /// </summary>
        /// <param name="supplyLineStretch">補給線の伸長(0..1)。<see cref="SupplyLineStretch"/> の出力。</param>
        /// <param name="frontierDefenseCost">辺境の防衛コスト(0..1)。<see cref="FrontierDefenseCost"/> の出力。</param>
        /// <param name="economicCapacity">負荷を支える経済力(0以上)。</param>
        public static float Overstretch(float supplyLineStretch, float frontierDefenseCost,
                                        float economicCapacity, FrontierExpansionParams p)
        {
            float stretch = Mathf.Clamp01(supplyLineStretch);
            float defense = Mathf.Clamp01(frontierDefenseCost);
            float cap = Mathf.Clamp(economicCapacity, MinCapacity, MaxCapacity);
            float load = p.overstretchGain * (stretch + defense);
            float ratio = load / cap;
            float over = ratio / (ratio + 1f);
            return Mathf.Clamp(over, 0f, MaxOverstretch);
        }

        public static float Overstretch(float supplyLineStretch, float frontierDefenseCost, float economicCapacity)
            => Overstretch(supplyLineStretch, frontierDefenseCost, economicCapacity, FrontierExpansionParams.Default);

        /// <summary>
        /// 辺境の統治力低下(0..1)＝版図/統治能力。統治能力に対して版図が大きいほど辺境に手が回らず統治が緩む＝
        /// 「広すぎる国は辺境を統べきれない」。比1（版図＝統治能力）で0.5、超えると1へ漸近。
        /// </summary>
        /// <param name="territorySize">版図の大きさ(0以上)。</param>
        /// <param name="administrativeCapacity">統治能力(0以上)。官僚機構が及ぶ範囲。</param>
        public static float GovernanceDecay(float territorySize, float administrativeCapacity, FrontierExpansionParams p)
        {
            float size = Mathf.Max(0f, territorySize);
            float cap = Mathf.Clamp(administrativeCapacity, MinCapacity, MaxCapacity);
            float ratio = p.decayGain * size / cap;
            float decay = ratio / (ratio + 1f);
            return Mathf.Clamp(decay, 0f, MaxDecay);
        }

        public static float GovernanceDecay(float territorySize, float administrativeCapacity)
            => GovernanceDecay(territorySize, administrativeCapacity, FrontierExpansionParams.Default);

        /// <summary>
        /// 拡大の正味価値(-1..1)＝果実−過伸長−統治低下。広げて得た富から、過伸長と統治の緩みの代償を差し引く＝
        /// 正なら拡大は実り、負なら広げただけ国が傷む（勝ちすぎが負ける）。
        /// </summary>
        /// <param name="territorialYield">経済的果実(0..1)。<see cref="TerritorialYield"/> の出力。</param>
        /// <param name="overstretch">過伸長(0..1)。<see cref="Overstretch"/> の出力。</param>
        /// <param name="governanceDecay">統治力低下(0..1)。<see cref="GovernanceDecay"/> の出力。</param>
        public static float NetExpansionValue(float territorialYield, float overstretch, float governanceDecay,
                                              FrontierExpansionParams p)
        {
            float yield = Mathf.Clamp01(territorialYield);
            float over = Mathf.Clamp01(overstretch);
            float decay = Mathf.Clamp01(governanceDecay);
            // 代償（過伸長＋統治低下）は平均して果実と同じ尺度で比べる
            float cost = (over + decay) * 0.5f;
            float net = yield - cost;
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float NetExpansionValue(float territorialYield, float overstretch, float governanceDecay)
            => NetExpansionValue(territorialYield, overstretch, governanceDecay, FrontierExpansionParams.Default);

        /// <summary>
        /// 拡大が持続可能か＝正味の拡大価値が閾値を超えるか。閾値以上なら広げ続けてよい・下回れば過伸長で守勢へ。
        /// </summary>
        public static bool IsExpansionSustainable(float netExpansionValue, float threshold)
        {
            float net = Mathf.Clamp(netExpansionValue, -1f, 1f);
            return net >= Mathf.Clamp(threshold, -1f, 1f);
        }

        public static bool IsExpansionSustainable(float netExpansionValue)
            => IsExpansionSustainable(netExpansionValue, SustainThreshold);
    }
}
