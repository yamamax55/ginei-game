using UnityEngine;

namespace Ginei
{
    /// <summary>連鎖の脆さの調整係数（#1112）。</summary>
    public readonly struct ChainFragilityParams
    {
        /// <summary>貯蔵不能率（上流に溜まる行き場のない生産のうち、貯蔵できず<b>廃棄</b>される割合＝グルットの損失）。</summary>
        public readonly float spoilRate;
        /// <summary>バッファ1段ぶんが吸収できる欠品深刻度（これより薄いほどカスケードが深く伝播する＝即連鎖）。</summary>
        public readonly float bufferAbsorption;
        /// <summary>復旧の基礎時間（カスケード1段＋再起動コストに掛ける係数）。</summary>
        public readonly float recoveryBase;

        public ChainFragilityParams(float spoilRate, float bufferAbsorption, float recoveryBase)
        {
            this.spoilRate = Mathf.Clamp01(spoilRate);
            this.bufferAbsorption = Mathf.Clamp01(bufferAbsorption);
            this.recoveryBase = Mathf.Max(0f, recoveryBase);
        }

        /// <summary>既定＝貯蔵不能率0.5／バッファ吸収0.25／復旧基礎時間2.0。</summary>
        public static ChainFragilityParams Default => new ChainFragilityParams(0.5f, 0.25f, 2f);
    }

    /// <summary>
    /// 連鎖の脆さの純ロジック（#1112・唯一の窓口）。生産網の1工程（最弱ノード）が止まると、
    /// <b>上流は在庫が溢れ（グルット）下流は原料が枯れる（欠品）</b>＝1点の遮断が上下流へ同時に伝播する。
    /// 連鎖は最弱ノードで切れる＝単一障害点（<see cref="SinglePointRisk"/>）ほど・連鎖が長いほど脆く、
    /// バッファが薄いほど深くカスケード（<see cref="CascadeDepth"/>）する。冗長と在庫が脆さを緩める（<see cref="Resilience"/>）。
    /// <see cref="SupplyRules"/>（補給線＝回廊到達/ZOC遮断）とは別＝こちらは<b>生産網内の伝播</b>。
    /// 連産の結合は <see cref="CoupledProductionRules"/>（#1110・固定比同時産出）、各段のバッファ運用は
    /// IntermediateBufferRules（同Wave並行・中間在庫の積み増し/取り崩し）が扱う＝本ルールは遮断の上下流カスケードに特化。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ChainFragilityRules
    {
        /// <summary>
        /// 遮断ノードの上流に溜まる在庫（行き場を失った生産＝グルット）。
        /// 遮断で流せなくなったぶん（upstreamProduction−blockedThroughput の下回り＝下流が受け取れない超過）が dt 時間ぶん滞留し、
        /// 貯蔵不能率 <see cref="ChainFragilityParams.spoilRate"/> ぶんは廃棄され在庫として残らない（保管しきれず腐る）。
        /// 上流生産が下流の通過能力以下なら溢れない（0）。
        /// </summary>
        public static float UpstreamGlut(float blockedThroughput, float upstreamProduction, float dt, ChainFragilityParams p)
        {
            if (dt <= 0f) return 0f;
            float prod = Mathf.Max(0f, upstreamProduction);
            float through = Mathf.Max(0f, blockedThroughput);
            float overflow = Mathf.Max(0f, prod - through);          // 下流が受け取れない超過＝行き場のない生産
            float stored = overflow * (1f - p.spoilRate);            // 貯蔵不能ぶんは廃棄＝在庫に残らない
            return stored * dt;
        }

        public static float UpstreamGlut(float blockedThroughput, float upstreamProduction, float dt)
            => UpstreamGlut(blockedThroughput, upstreamProduction, dt, ChainFragilityParams.Default);

        /// <summary>
        /// 下流の欠品（原料が来ない＝操業停止が連鎖）。
        /// 下流需要 downstreamDemand に対し、遮断で届く量 blockedThroughput が満たせないぶん（不足量）が dt 時間ぶん積み上がる。
        /// 通過量が需要を満たせば欠品なし（0）。蓄積した欠品が下流の操業を止め、さらにその下流へ連鎖していく起点。
        /// </summary>
        public static float DownstreamShortage(float blockedThroughput, float downstreamDemand, float dt)
        {
            if (dt <= 0f) return 0f;
            float demand = Mathf.Max(0f, downstreamDemand);
            float through = Mathf.Max(0f, blockedThroughput);
            float shortfall = Mathf.Max(0f, demand - through);       // 届かないぶん＝欠品
            return shortfall * dt;
        }

        /// <summary>
        /// カスケードが伝播する段数（欠品の連鎖がいくつ下流の工程まで波及するか）。
        /// 欠品の深刻度 shortageSeverity（0..1）を、各段のバッファ <paramref name="bufferStock"/>（0..1）が
        /// <see cref="ChainFragilityParams.bufferAbsorption"/> ぶん吸収しながら減衰させる＝
        /// <b>バッファが薄いほど深く伝播</b>（バッファ無し＝吸収0＝深刻度そのままの段数だけ即カスケード）。
        /// 戻り値は伝播段数（切り上げ＝1段でも届けばその工程は止まる）。深刻度0なら0段。
        /// </summary>
        public static int CascadeDepth(float shortageSeverity, float bufferStock, ChainFragilityParams p)
        {
            float severity = Mathf.Clamp01(shortageSeverity);
            if (severity <= 0f) return 0;
            float buffer = Mathf.Clamp01(bufferStock);
            // 各段で吸収される深刻度＝buffer×吸収係数。薄いバッファほど1段あたりの減衰が小さく深く届く。
            float absorbPerStage = buffer * p.bufferAbsorption;      // 0..bufferAbsorption
            float reach = severity / (absorbPerStage + 0.05f);       // 吸収が薄いほど大きい（0除け）
            return Mathf.Max(1, Mathf.CeilToInt(reach));             // 欠品があれば最低1段は止まる
        }

        public static int CascadeDepth(float shortageSeverity, float bufferStock)
            => CascadeDepth(shortageSeverity, bufferStock, ChainFragilityParams.Default);

        /// <summary>
        /// 単一障害点リスク（0..1）。代替の無いノード（nodeCriticality 0..1＝代替の無さ）ほど、
        /// 連鎖が長い（chainLength＝直列工程数）ほど脆い＝<b>連鎖は最弱ノードで切れる</b>。
        /// 直列が長いほど「どこか1点が止まる」確率が積み上がる＝長さで単調増加（飽和あり）。代替が無いほど致命的。
        /// </summary>
        public static float SinglePointRisk(float nodeCriticality, int chainLength)
        {
            float crit = Mathf.Clamp01(nodeCriticality);
            int len = Mathf.Max(1, chainLength);
            // 連鎖の長さによる脆弱化＝1段でも長くなるほど1に飽和（1段=0.5・無限長=1）。
            float lengthFactor = 1f - 1f / (1f + len);               // len1→0.5, len3→0.75 …
            return Mathf.Clamp01(crit * lengthFactor);
        }

        /// <summary>
        /// 復旧時間（深いカスケードほど立て直しに時間がかかる）。
        /// 伝播段数 cascadeDepth ＋再起動コスト restartCost（0..1＝1段あたりの再起動の重さ）を
        /// 基礎時間 <see cref="ChainFragilityParams.recoveryBase"/> に掛ける＝止まった工程を順に立ち上げ直す時間。
        /// 段数0なら復旧なし（0）。
        /// </summary>
        public static float RecoveryTime(int cascadeDepth, float restartCost, ChainFragilityParams p)
        {
            int depth = Mathf.Max(0, cascadeDepth);
            if (depth == 0) return 0f;
            float restart = Mathf.Clamp01(restartCost);
            return p.recoveryBase * depth * (1f + restart);          // 段数×(再起動の重さで割増)
        }

        public static float RecoveryTime(int cascadeDepth, float restartCost)
            => RecoveryTime(cascadeDepth, restartCost, ChainFragilityParams.Default);

        /// <summary>
        /// 連鎖の頑健性（0..1）。冗長性 redundancy（0..1＝代替経路の厚み）と在庫 bufferStock（0..1）が
        /// 脆さを緩める＝両方が高いほど1点の遮断に耐える。どちらも0なら頑健性0（最弱ノードで即破断）。
        /// 冗長は破断そのものを避け、在庫は破断後の連鎖を遅らせる＝重み付き合成（冗長0.6/在庫0.4）。
        /// </summary>
        public static float Resilience(float redundancy, float bufferStock)
        {
            float redun = Mathf.Clamp01(redundancy);
            float buffer = Mathf.Clamp01(bufferStock);
            return Mathf.Clamp01(redun * 0.6f + buffer * 0.4f);
        }
    }
}
