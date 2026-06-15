using UnityEngine;

namespace Ginei
{
    /// <summary>普遍史の因果波及（ポリュビオス『歴史』）の調整係数。</summary>
    public readonly struct UniversalHistoryParams
    {
        /// <summary>距離による波及の減衰率（per unit distance・大きいほど遠方へ届かない）。</summary>
        public readonly float distanceDecayRate;
        /// <summary>連関ゼロの世界でも残る最低波及率（バラバラでも隣接へは漏れる）。0..1。</summary>
        public readonly float minReach;
        /// <summary>連関した世界が遠方へ波及を延ばす倍率（1.0で減衰そのまま・大きいほど遠くへ）。</summary>
        public readonly float integrationReach;
        /// <summary>因果カスケード1ホップあたりの基礎減衰率（風が吹けば桶屋＝伝わるたび薄まる）。0..1。</summary>
        public readonly float hopDecay;
        /// <summary>世界の連関度が時間で高まる収束速度（per dt・孤立した歴史が普遍史へ向かう）。</summary>
        public readonly float convergenceRate;
        /// <summary>普遍史（一体化した世界）と判定する連関度の既定閾値。0..1。</summary>
        public readonly float interconnectThreshold;

        public UniversalHistoryParams(float distanceDecayRate, float minReach, float integrationReach, float hopDecay, float convergenceRate, float interconnectThreshold)
        {
            this.distanceDecayRate = Mathf.Max(0f, distanceDecayRate);
            this.minReach = Mathf.Clamp01(minReach);
            this.integrationReach = Mathf.Max(0f, integrationReach);
            this.hopDecay = Mathf.Clamp01(hopDecay);
            this.convergenceRate = Mathf.Max(0f, convergenceRate);
            this.interconnectThreshold = Mathf.Clamp01(interconnectThreshold);
        }

        /// <summary>既定＝距離減衰1.5・最低波及0.05・連関到達倍率1.5・ホップ減衰0.3・収束速度0.1・一体化閾値0.6。</summary>
        public static UniversalHistoryParams Default => new UniversalHistoryParams(1.5f, 0.05f, 1.5f, 0.3f, 0.1f, 0.6f);
    }

    /// <summary>
    /// 普遍史の因果波及の純ロジック（POLY-4 #1451・ポリュビオス『歴史』参考）。ポリュビオスは「かつて
    /// 各地の出来事はバラバラだったが、ローマの台頭以降、世界の出来事は相互に連関し一つの有機的な全体に
    /// なった＝ある地域の事件が距離減衰しながら他地域へ因果的に波及する」と説いた。歴史は孤立した事件の
    /// 羅列でなく連関するシステムである＝これを式に出す。世界の連関度が高いほど事件は遠くまで波及し、
    /// 複数の局所事件が連関して系全体の大事件へまとまる。
    /// <para>
    /// 分担：<see cref="DisclosureRules"/> は前提条件の開示連鎖（物語の解放）、<see cref="NotificationCenter"/>
    /// は通知の単一窓口（プレイヤーへの提示）、<see cref="CampaignRules"/> は盤面そのものの状態波及。
    /// この UniversalHistoryRules は「事件の因果が距離減衰しながら他星系へ伝播する連関」の抽象モデルを
    /// 受け持つ（盤面・UI・通知を持たない純ロジック）。乱数なし・決定論。非 MonoBehaviour・test-first。
    /// </para>
    /// </summary>
    public static class UniversalHistoryRules
    {
        /// <summary>
        /// 距離による波及の減衰（0..1）＝指数減衰 e^(-decayRate×distance)。距離0で1、遠いほど薄まる。
        /// </summary>
        public static float DistanceDecay(float distance, float decayRate)
        {
            float d = Mathf.Clamp01(distance);
            float r = Mathf.Max(0f, decayRate);
            return Mathf.Clamp01(Mathf.Pow(2.71828f, -r * d));
        }

        public static float DistanceDecay(float distance)
            => DistanceDecay(distance, UniversalHistoryParams.Default.distanceDecayRate);

        /// <summary>
        /// 世界の連関度 0..1＝交易・政治・通信の結びつきの加重平均。バラバラ（低）か一つの全体（高）か。
        /// 交易0.4・政治0.35・通信0.25で重み付け（経済的結合を最も重く）。
        /// </summary>
        public static float Interconnectedness(float tradeLinks, float politicalTies, float communicationReach)
        {
            float t = Mathf.Clamp01(tradeLinks);
            float p = Mathf.Clamp01(politicalTies);
            float c = Mathf.Clamp01(communicationReach);
            return Mathf.Clamp01(t * 0.4f + p * 0.35f + c * 0.25f);
        }

        /// <summary>
        /// 他星系への波及の強さ（0..1）＝事件の大きさ×（距離減衰を連関度で緩めた到達率）。
        /// 連関した世界（integration 高）ほど距離減衰が integrationReach で延び、遠くまで波及する＝
        /// ローマ台頭以降の「世界の出来事は相互に連関する」。連関ゼロでも minReach ぶんは隣接へ漏れる。
        /// </summary>
        public static float EventPropagation(float eventMagnitude, float distance, float worldIntegration, UniversalHistoryParams p)
        {
            float mag = Mathf.Clamp01(eventMagnitude);
            float integ = Mathf.Clamp01(worldIntegration);
            // 連関度が高いほど実効減衰率を下げる＝遠くまで届く（integrationReach 倍で減衰を割り引く）。
            float effectiveDecay = p.distanceDecayRate / (1f + p.integrationReach * integ);
            float reach = DistanceDecay(distance, effectiveDecay);
            // 連関ゼロでも残る最低到達。
            reach = Mathf.Clamp01(Mathf.Lerp(p.minReach, 1f, reach));
            return Mathf.Clamp01(mag * reach);
        }

        public static float EventPropagation(float eventMagnitude, float distance, float worldIntegration)
            => EventPropagation(eventMagnitude, distance, worldIntegration, UniversalHistoryParams.Default);

        /// <summary>
        /// 事件がどこまで届くか＝波及の到達範囲 0..1＝事件の大きさ×連関度（大事件×一体化した世界で広範囲）。
        /// EventPropagation が「特定距離での強さ」なのに対し、こちらは「届く射程」を表す集約値。
        /// </summary>
        public static float RippleReach(float eventMagnitude, float integration)
        {
            return Mathf.Clamp01(Mathf.Clamp01(eventMagnitude) * Mathf.Clamp01(integration));
        }

        /// <summary>
        /// 因果カスケード（0..1）＝事件が連鎖を通じて伝播し、各ホップで減衰する（風が吹けば桶屋＝因果の鎖）。
        /// 鎖が長い（chainLength 高）・1ホップの減衰（decayPerHop 高）が大きいほど、末端に届く力は薄まる。
        /// 残存率＝(1−実効ホップ減衰)^(鎖長×基準ホップ数) を sourceMagnitude に掛ける。
        /// </summary>
        public static float CausalCascade(float sourceMagnitude, float chainLength, float decayPerHop, UniversalHistoryParams p)
        {
            float src = Mathf.Clamp01(sourceMagnitude);
            float len = Mathf.Clamp01(chainLength);
            float hop = Mathf.Clamp01(p.hopDecay + (1f - p.hopDecay) * Mathf.Clamp01(decayPerHop));
            // 鎖長0..1を実効ホップ数0..MaxHops へ写す（長い鎖ほど多段で薄まる）。
            const float MaxHops = 5f;
            float hops = len * MaxHops;
            float survival = Mathf.Pow(1f - hop, hops);
            return Mathf.Clamp01(src * survival);
        }

        public static float CausalCascade(float sourceMagnitude, float chainLength, float decayPerHop)
            => CausalCascade(sourceMagnitude, chainLength, decayPerHop, UniversalHistoryParams.Default);

        /// <summary>
        /// 系全体の大事件（0..1）＝複数の局所事件が連関して一つの大事件になる。連関度が高いほど局所事件は
        /// 単なる和でなく相互に増幅し合う（ポリュビオスの「有機的な全体」）。連関ゼロなら最大の局所事件
        /// （バラバラ＝独立事件）、連関1なら飽和した総和へ近づく。手書きループ・null/空安全。
        /// </summary>
        public static float SystemicEvent(float[] localEvents, float integration)
        {
            if (localEvents == null || localEvents.Length == 0) return 0f;
            float integ = Mathf.Clamp01(integration);
            float max = 0f;
            float sum = 0f;
            for (int i = 0; i < localEvents.Length; i++)
            {
                float e = Mathf.Clamp01(localEvents[i]);
                if (e > max) max = e;
                sum += e;
            }
            // 連関ゼロ＝最大の局所事件（独立）。連関高＝飽和した総和（1-Πで全事件が寄与）。
            float product = 1f;
            for (int i = 0; i < localEvents.Length; i++)
            {
                product *= (1f - Mathf.Clamp01(localEvents[i]));
            }
            float combined = 1f - product; // 連関時の系全体規模（飽和和）。
            return Mathf.Clamp01(Mathf.Lerp(max, combined, integ));
        }

        /// <summary>
        /// 歴史の収束（0..1）＝世界の連関度が時間で高まる（孤立した歴史が一つの普遍史へ収束＝ローマ型統一）。
        /// 1.0へ向かって convergenceRate×dt で漸近する（一度結びつくと一体化が進む一方向のドリフト）。
        /// </summary>
        public static float HistoricalConvergence(float integration, float dt, UniversalHistoryParams p)
        {
            float integ = Mathf.Clamp01(integration);
            float step = p.convergenceRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(integ + (1f - integ) * Mathf.Clamp01(step));
        }

        public static float HistoricalConvergence(float integration, float dt)
            => HistoricalConvergence(integration, dt, UniversalHistoryParams.Default);

        /// <summary>
        /// 普遍史の段階判定＝世界が一体化し事件が連関する段階か。連関度が閾値以上で true
        /// （ポリュビオスの「ローマ台頭以降、世界の出来事は一つの全体になった」段階）。
        /// </summary>
        public static bool IsInterconnectedWorld(float integration, float threshold)
        {
            return Mathf.Clamp01(integration) >= Mathf.Clamp01(threshold);
        }

        public static bool IsInterconnectedWorld(float integration)
            => IsInterconnectedWorld(integration, UniversalHistoryParams.Default.interconnectThreshold);
    }
}
