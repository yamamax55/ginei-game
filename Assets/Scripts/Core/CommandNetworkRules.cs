using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指揮通信網（C3I＝指揮・統制・通信・情報）の調整係数。接続性・冗長性・脆弱性・妨害・復旧の重み。
    /// </summary>
    public readonly struct CommandNetworkParams
    {
        /// <summary>接続性の基礎倍率（ノード数^指数 × リンク密度 に掛ける）。</summary>
        public readonly float connectivityScale;
        /// <summary>ノード数に対する接続性の逓減指数（0.5＝平方根的に飽和。増やすほど寄与が鈍る）。</summary>
        public readonly float nodeExponent;
        /// <summary>冗長性の逓減基数（代替経路1本あたりの残存途絶比＝小さいほど速く冗長に到達）。</summary>
        public readonly float redundancyBase;
        /// <summary>中枢脆弱性の集中度指数（1＝集中度がそのまま脆弱性。大きいほど集中の罰が強い）。</summary>
        public readonly float centralizationExponent;
        /// <summary>敵妨害の実効強度倍率（妨害入力に掛けて接続性と綱引きさせる）。</summary>
        public readonly float jammingScale;
        /// <summary>復旧能力の基礎倍率（冗長性×技術予備 に掛ける）。</summary>
        public readonly float recoveryScale;

        public CommandNetworkParams(float connectivityScale, float nodeExponent, float redundancyBase,
            float centralizationExponent, float jammingScale, float recoveryScale)
        {
            this.connectivityScale = Mathf.Max(0f, connectivityScale);
            this.nodeExponent = Mathf.Clamp(nodeExponent, 0f, 1f);
            this.redundancyBase = Mathf.Clamp(redundancyBase, 0f, 1f);
            this.centralizationExponent = Mathf.Max(0f, centralizationExponent);
            this.jammingScale = Mathf.Max(0f, jammingScale);
            this.recoveryScale = Mathf.Max(0f, recoveryScale);
        }

        /// <summary>既定＝接続倍率0.25・ノード指数0.5・冗長基数0.5・集中指数1・妨害倍率1・復旧倍率1。</summary>
        public static CommandNetworkParams Default =>
            new CommandNetworkParams(0.25f, 0.5f, 0.5f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 指揮通信網（C3I＝指揮・統制・通信・情報）の堅牢性の純ロジック。ノード（旗艦・指揮所）をリンクで結んだ網が、
    /// 中枢を撃たれてもどれだけ機能を保つか（分散指揮 vs 中央集権）、代替経路でどれだけ冗長化できるか、
    /// 敵の電子妨害（ジャミング）下でどれだけ指揮が通るか、途絶した網をどれだけ復旧できるかを表す。
    /// 集中した網は平時は効率的だが中枢喪失に脆く、分散した網は冗長で耐性が高い（実史の C2 のトレードオフ）。
    /// 盤面非依存の plain 引数のみ（座標・コンポーネント非依存）。乱数を持たず決定論。
    /// 実効値パターン（与えた基準値は非破壊）。入力は全て clamp。純ロジック（非 MonoBehaviour・test-first）。
    /// 分担：<see cref="SatelliteCommsRules"/>（衛星中継の通信そのもの）や <see cref="SignalIntelligenceRules"/>
    /// （信号情報の解析）とは別。こちらは「指揮網トポロジの堅牢性（接続/冗長/中枢脆弱/妨害/復旧/実効到達）」に特化。
    /// </summary>
    public static class CommandNetworkRules
    {
        /// <summary>既定の指揮網機能判定閾値（実効指揮到達がこれ以上で機能とみなす）。</summary>
        public const float DefaultOperationalThreshold = 0.3f;

        /// <summary>
        /// ネットワーク接続性 0..1＝clamp01(connectivityScale × ノード数^nodeExponent × リンク密度)。
        /// ノードが多くリンクが密なほどよく繋がるが、ノード数は指数<1 で飽和（増やすほど寄与が鈍る）。
        /// </summary>
        public static float NetworkConnectivity(int nodeCount, float linkDensity, CommandNetworkParams p)
        {
            int count = Mathf.Max(0, nodeCount);
            float density = Mathf.Clamp01(linkDensity);
            return Mathf.Clamp01(p.connectivityScale * Mathf.Pow(count, p.nodeExponent) * density);
        }

        public static float NetworkConnectivity(int nodeCount, float linkDensity)
            => NetworkConnectivity(nodeCount, linkDensity, CommandNetworkParams.Default);

        /// <summary>
        /// 通信の冗長性 0..1＝clamp01(リンク密度 × (1 - redundancyBase^代替経路数))。
        /// 代替経路が多いほど一本が切れても迂回でき（指数的に途絶確率が下がる）、密なリンクがそれを支える。
        /// 代替経路0で 0、増やすほど 1 に漸近。
        /// </summary>
        public static float Redundancy(float linkDensity, int alternativePaths, CommandNetworkParams p)
        {
            float density = Mathf.Clamp01(linkDensity);
            int paths = Mathf.Max(0, alternativePaths);
            float pathRobust = 1f - Mathf.Pow(p.redundancyBase, paths);
            return Mathf.Clamp01(density * pathRobust);
        }

        public static float Redundancy(float linkDensity, int alternativePaths)
            => Redundancy(linkDensity, alternativePaths, CommandNetworkParams.Default);

        /// <summary>
        /// 中枢喪失への脆弱性 0..1＝clamp01(集中度^centralizationExponent)。
        /// 指揮が中央に集中しているほど、その中枢を撃たれたとき網全体が崩れる（中央集権の罰）。
        /// 分散していれば（集中度低）脆弱性は小さい。
        /// </summary>
        public static float CentralNodeVulnerability(float centralization, CommandNetworkParams p)
        {
            float c = Mathf.Clamp01(centralization);
            return Mathf.Clamp01(Mathf.Pow(c, p.centralizationExponent));
        }

        public static float CentralNodeVulnerability(float centralization)
            => CentralNodeVulnerability(centralization, CommandNetworkParams.Default);

        /// <summary>
        /// 中枢喪失への耐性 0..1＝clamp01(冗長性 × (1 - 中枢脆弱性))。
        /// 冗長な迂回路があり、かつ集中していない（脆弱性が低い）ほど、中枢を失っても機能を保つ。
        /// </summary>
        public static float ResilienceToLoss(float redundancy, float centralNodeVulnerability, CommandNetworkParams p)
        {
            float r = Mathf.Clamp01(redundancy);
            float vuln = Mathf.Clamp01(centralNodeVulnerability);
            return Mathf.Clamp01(r * (1f - vuln));
        }

        public static float ResilienceToLoss(float redundancy, float centralNodeVulnerability)
            => ResilienceToLoss(redundancy, centralNodeVulnerability, CommandNetworkParams.Default);

        /// <summary>
        /// 妨害による劣化 0..1＝clamp01(妨害×jammingScale / (妨害×jammingScale + 接続性))。
        /// 敵の電子妨害と網の接続性の綱引き。妨害が接続性を上回るほど指揮通信が潰れる（飽和形）。
        /// 接続性が高いほど同じ妨害でも劣化が小さい。両方0なら劣化0。
        /// </summary>
        public static float JammingDegradation(float enemyJamming, float networkConnectivity, CommandNetworkParams p)
        {
            float jam = Mathf.Clamp01(enemyJamming) * p.jammingScale;
            float conn = Mathf.Clamp01(networkConnectivity);
            float denom = jam + conn;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(jam / denom);
        }

        public static float JammingDegradation(float enemyJamming, float networkConnectivity)
            => JammingDegradation(enemyJamming, networkConnectivity, CommandNetworkParams.Default);

        /// <summary>
        /// 復旧能力 0..1＝clamp01(recoveryScale × 冗長性 × 技術予備)。
        /// 迂回路（冗長性）と修復のための技術予備（予備機材・通信士）が揃うほど、途絶した網を立て直せる。
        /// </summary>
        public static float RecoveryCapacity(float redundancy, float technicalReserves, CommandNetworkParams p)
        {
            float r = Mathf.Clamp01(redundancy);
            float reserves = Mathf.Clamp01(technicalReserves);
            return Mathf.Clamp01(p.recoveryScale * r * reserves);
        }

        public static float RecoveryCapacity(float redundancy, float technicalReserves)
            => RecoveryCapacity(redundancy, technicalReserves, CommandNetworkParams.Default);

        /// <summary>
        /// 実効指揮到達 0..1＝clamp01(接続性 × (1 - 妨害劣化) × 中枢喪失耐性)。
        /// よく繋がり（接続性）、妨害に潰されず（1-劣化）、中枢を撃たれても保つ（耐性）ほど、
        /// 末端まで指揮が届く。どれか一つが0なら全体も0（積＝最弱リンクが効く）。
        /// </summary>
        public static float EffectiveCommandReach(float networkConnectivity, float jammingDegradation,
            float resilienceToLoss, CommandNetworkParams p)
        {
            float conn = Mathf.Clamp01(networkConnectivity);
            float deg = Mathf.Clamp01(jammingDegradation);
            float res = Mathf.Clamp01(resilienceToLoss);
            return Mathf.Clamp01(conn * (1f - deg) * res);
        }

        public static float EffectiveCommandReach(float networkConnectivity, float jammingDegradation,
            float resilienceToLoss)
            => EffectiveCommandReach(networkConnectivity, jammingDegradation, resilienceToLoss,
                CommandNetworkParams.Default);

        /// <summary>
        /// 指揮網が機能しているか＝実効指揮到達が閾値以上なら true。
        /// 閾値を下回ると指揮が各個分断され、艦隊は統制を失う（自律行動に陥る）。
        /// </summary>
        public static bool IsNetworkOperational(float effectiveCommandReach, float threshold)
        {
            float reach = Mathf.Clamp01(effectiveCommandReach);
            float th = Mathf.Clamp01(threshold);
            return reach >= th;
        }

        public static bool IsNetworkOperational(float effectiveCommandReach)
            => IsNetworkOperational(effectiveCommandReach, DefaultOperationalThreshold);
    }
}
