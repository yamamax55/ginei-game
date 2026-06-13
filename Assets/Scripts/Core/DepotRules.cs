using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 補給デポ（前進補給基地）の純データ（CRV-1 #1363・兵站）。本国から遠い前線でも、途中に中継デポを
    /// 築けば補給の基点が前方へ移り、そこから先の作戦到達限界が延伸する＝ナポレオンの倉庫システム。
    /// </summary>
    public struct Depot
    {
        /// <summary>デポの備蓄（0..1・前線需要の変動を吸収する在庫）。</summary>
        public float stockpile;
        /// <summary>デポの前進位置（0..1・本国=0／前線=1。前方ほど到達限界を遠くへ伸ばすが敵に近い）。</summary>
        public float forwardPosition;
        /// <summary>デポの処理能力（0..1・補給を通す吞み込み。高いほど作戦到達限界を延ばす）。</summary>
        public float throughput;

        public Depot(float stockpile, float forwardPosition, float throughput)
        {
            this.stockpile = Mathf.Clamp01(stockpile);
            this.forwardPosition = Mathf.Clamp01(forwardPosition);
            this.throughput = Mathf.Clamp01(throughput);
        }
    }

    /// <summary>前進補給基地の調整係数。</summary>
    public readonly struct DepotParams
    {
        /// <summary>デポによる到達限界の延伸の最大幅（0..1・前進×処理能力が満点のとき延ばせる上限）。</summary>
        public readonly float maxReachExtension;
        /// <summary>備蓄の緩衝力（0..1・在庫1あたり前線需要の振れをどれだけ吸収できるか）。</summary>
        public readonly float bufferStrength;
        /// <summary>前進したデポの基礎脆弱性（0..1・最前線=1に置いたデポの被狙い度のスケール）。</summary>
        public readonly float forwardExposure;
        /// <summary>デポ設置の基礎コスト（0..1・本国近傍に1基築く下地のコスト）。</summary>
        public readonly float baseEstablishCost;
        /// <summary>敵地（前進×敵対）によるデポ設置コストの割増重み（0..1）。</summary>
        public readonly float hostilityCostWeight;
        /// <summary>デポが攻勢終末点の到来を遅らせる最大割合（0..1・補給延伸が深追いを許す上限）。</summary>
        public readonly float culminationDelayWeight;
        /// <summary>補給リレー（複数デポ中継）が補給線を伸ばす最大割合（0..1）。</summary>
        public readonly float relayGain;
        /// <summary>前進補給基地が確立したとみなす既定の閾値（0..1・延伸×備蓄）。</summary>
        public readonly float establishThreshold;

        public DepotParams(float maxReachExtension, float bufferStrength, float forwardExposure,
            float baseEstablishCost, float hostilityCostWeight, float culminationDelayWeight,
            float relayGain, float establishThreshold)
        {
            this.maxReachExtension = Mathf.Clamp01(maxReachExtension);
            this.bufferStrength = Mathf.Clamp01(bufferStrength);
            this.forwardExposure = Mathf.Clamp01(forwardExposure);
            this.baseEstablishCost = Mathf.Clamp01(baseEstablishCost);
            this.hostilityCostWeight = Mathf.Clamp01(hostilityCostWeight);
            this.culminationDelayWeight = Mathf.Clamp01(culminationDelayWeight);
            this.relayGain = Mathf.Clamp01(relayGain);
            this.establishThreshold = Mathf.Clamp01(establishThreshold);
        }

        /// <summary>既定＝延伸上限0.6・緩衝0.5・前進脆弱0.8・設置基礎0.2・敵地割増0.5・終末点遅延0.5・リレー0.6・確立閾値0.4。</summary>
        public static DepotParams Default =>
            new DepotParams(0.6f, 0.5f, 0.8f, 0.2f, 0.5f, 0.5f, 0.6f, 0.4f);
    }

    /// <summary>
    /// 前進補給基地（補給デポ）の純ロジック（CRV-1 #1363・兵station）。補給デポを設けると<b>補給の基点が
    /// 前方へ移り</b>、そこから先の作戦到達限界（補給が届く範囲）が延伸する＝本国から遠い前線でも、
    /// 途中に中継デポを築けば補給線が段階的に伸ばせる＝デポが攻勢の射程を決める（ナポレオンの倉庫システム）。
    /// <see cref="SupplyRules"/>（本国の補給源から所有回廊で前線へ到達する面の補給線・ZOC遮断）とは別＝
    /// こちらは前進デポを補給基点として到達限界を延伸する。
    /// <see cref="SeaControlLeverageRules"/>（制宙権による補給保証・敵補給遮断）とも別＝こちらは前方の倉庫拠点。
    /// <see cref="CulminatingPointRules"/>（攻勢終末点＝距離による戦力減衰の限界）へはデポが終末点到来を遅らせる
    /// 形で効く（補給が続けばもっと進める）。<see cref="CorridorCapacityRules"/>（回廊容量・同EPIC CRV）とも別＝
    /// こちらはデポ拠点そのもの。<see cref="Depot"/> が中核データ。乱数なし・決定論。
    /// 倍率・延伸量は基準値に加算/乗算して使う（実効値パターン・基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DepotRules
    {
        /// <summary>
        /// デポによる作戦到達限界の延伸量（0..maxReachExtension）。デポが前方にあり（forwardPosition↑）処理能力が
        /// 高い（throughput↑）ほど、補給の基点が前進して到達限界を延ばす。どちらかが0なら延伸0（前進だけ・
        /// 倉庫だけでは伸びない＝前進した倉庫が補給基点になってはじめて射程が伸びる）。
        /// </summary>
        public static float ReachExtension(float depotForwardPosition, float depotThroughput, DepotParams p)
        {
            float fwd = Mathf.Clamp01(depotForwardPosition);
            float thru = Mathf.Clamp01(depotThroughput);
            return p.maxReachExtension * fwd * thru;
        }

        public static float ReachExtension(float depotForwardPosition, float depotThroughput)
            => ReachExtension(depotForwardPosition, depotThroughput, DepotParams.Default);

        /// <summary>
        /// 実効的な補給到達範囲（0..1）＝本国からの基礎補給範囲＋デポによる延伸（残り余地ぶんを延伸で詰める）。
        /// baseSupplyRange は本国の補給網そのもの（<see cref="SupplyRules"/> 的な面の到達の正規化値）、
        /// そこへ前進デポの延伸を足して実効範囲を出す＝デポが届く先まで補給が伸びる。上限は1。
        /// </summary>
        public static float EffectiveSupplyRange(float baseSupplyRange, float reachExtension)
        {
            float baseRange = Mathf.Clamp01(baseSupplyRange);
            float ext = Mathf.Clamp01(reachExtension);
            // 残り余地（1−baseRange）を延伸で埋める＝既に届く先は伸ばさず、未到達域を延ばす
            return Mathf.Clamp01(baseRange + (1f - baseRange) * ext);
        }

        /// <summary>
        /// デポ備蓄の緩衝による前線需要の更新（前線が実際に被る需要の振れを返す）。デポの在庫が需要の変動を
        /// 吸収する＝備蓄が高いほど前線が感じる実効需要が平準化される（在庫が補給を安定させる）。
        /// 備蓄0なら緩衝なし＝需要がそのまま前線を直撃。dt で吸収を時間方向に積む（瞬間的には全吸収しない）。
        /// </summary>
        public static float StockpileBuffer(float depotStockpile, float frontlineDemand, float dt, DepotParams p)
        {
            float stock = Mathf.Clamp01(depotStockpile);
            float demand = Mathf.Clamp01(frontlineDemand);
            if (dt <= 0f) return demand;
            // 在庫が吸収する割合＝緩衝力×備蓄、dt で時間方向に効かせる（一気には吸い切らない）
            float absorb = p.bufferStrength * stock * Mathf.Clamp01(dt);
            return Mathf.Clamp01(demand * (1f - absorb));
        }

        public static float StockpileBuffer(float depotStockpile, float frontlineDemand, float dt)
            => StockpileBuffer(depotStockpile, frontlineDemand, dt, DepotParams.Default);

        /// <summary>
        /// 前進したデポの脆弱性（0..1）。前方に置いた（forwardPosition↑）デポは敵に近く狙われやすく、
        /// 防御（defenseLevel↑）で守るほど下がる＝前進と防御のトレードオフ。<see cref="CorridorSabotageRules"/>／
        /// <see cref="RaidRules"/> の標的選定に渡す。前進0なら脆弱性0（本国の倉庫は安全）。
        /// </summary>
        public static float DepotVulnerability(float forwardPosition, float defenseLevel, DepotParams p)
        {
            float fwd = Mathf.Clamp01(forwardPosition);
            float def = Mathf.Clamp01(defenseLevel);
            float exposed = p.forwardExposure * fwd; // 前進ほど被狙い
            return Mathf.Clamp01(exposed * (1f - def)); // 防御で軽減
        }

        public static float DepotVulnerability(float forwardPosition, float defenseLevel)
            => DepotVulnerability(forwardPosition, defenseLevel, DepotParams.Default);

        /// <summary>
        /// 補給リレー（複数デポ中継）による補給線の延伸（0..relayGain）。デポを多く（depotCount↑）、かつ間隔よく
        /// （spacing↑＝適度な配置）中継すると補給線が段階的に伸びる＝倉庫から倉庫へリレーする倉庫システム。
        /// どちらかが0なら延伸0（1基だけ・偏った配置ではリレーが成立しない）。
        /// </summary>
        public static float SupplyRelayChain(float depotCount, float spacing, DepotParams p)
        {
            float count = Mathf.Clamp01(depotCount);
            float space = Mathf.Clamp01(spacing);
            return p.relayGain * count * space;
        }

        public static float SupplyRelayChain(float depotCount, float spacing)
            => SupplyRelayChain(depotCount, spacing, DepotParams.Default);

        /// <summary>
        /// 前進したデポを築くコスト（baseEstablishCost..1）。前方（forwardPosition↑）かつ敵地（hostility↑）に
        /// 近いほど高い＝敵の眼前に倉庫を据えるのは費用がかさむ。本国近傍（前進0・敵対0）なら基礎コストのみ。
        /// </summary>
        public static float DepotEstablishmentCost(float forwardPosition, float hostility, DepotParams p)
        {
            float fwd = Mathf.Clamp01(forwardPosition);
            float host = Mathf.Clamp01(hostility);
            float extra = p.hostilityCostWeight * fwd * host; // 前進×敵地で割増
            return Mathf.Clamp01(p.baseEstablishCost + extra);
        }

        public static float DepotEstablishmentCost(float forwardPosition, float hostility)
            => DepotEstablishmentCost(forwardPosition, hostility, DepotParams.Default);

        /// <summary>
        /// デポが攻勢終末点（<see cref="CulminatingPointRules"/>）の到来を遅らせる割合（0..culminationDelayWeight）。
        /// 到達限界の延伸（reachExtension↑）が大きく、進撃の深さ（advanceDepth↑）が深いほど効く＝補給が続けば
        /// もっと進める＝終末点が遠のく。進撃が浅い（手前）うちは恩恵が小さい（まだ補給に困っていない）。
        /// </summary>
        public static float AdvanceCulminationDelay(float reachExtension, float advanceDepth, DepotParams p)
        {
            float ext = Mathf.Clamp01(reachExtension);
            float depth = Mathf.Clamp01(advanceDepth);
            return p.culminationDelayWeight * ext * depth;
        }

        public static float AdvanceCulminationDelay(float reachExtension, float advanceDepth)
            => AdvanceCulminationDelay(reachExtension, advanceDepth, DepotParams.Default);

        /// <summary>
        /// 前進補給基地が確立し攻勢を支えられるか＝到達限界の延伸（reachExtension）とデポ備蓄（depotStockpile）の
        /// 積が閾値以上なら true。射程を伸ばしても在庫が無ければ前線は枯れる＝延伸と備蓄が両立してはじめて確立。
        /// </summary>
        public static bool IsForwardSupplyEstablished(float reachExtension, float depotStockpile, float threshold)
        {
            float ext = Mathf.Clamp01(reachExtension);
            float stock = Mathf.Clamp01(depotStockpile);
            float thr = Mathf.Clamp01(threshold);
            return ext * stock >= thr;
        }

        /// <summary>前進補給基地の確立判定（既定閾値 <see cref="DepotParams.establishThreshold"/>）。</summary>
        public static bool IsForwardSupplyEstablished(float reachExtension, float depotStockpile)
            => IsForwardSupplyEstablished(reachExtension, depotStockpile, DepotParams.Default.establishThreshold);
    }
}
