using UnityEngine;

namespace Ginei
{
    /// <summary>データリンク（艦隊間の戦術データ共有）の調整係数。</summary>
    public readonly struct DataLinkParams
    {
        /// <summary>共通戦術状況図に効くリンク艦数のスケール（この隻数で寄与が約飽和）。</summary>
        public readonly float linkSaturation;
        /// <summary>センサー統合で「データの質」が効く重み。</summary>
        public readonly float fusionQualityWeight;
        /// <summary>目標共有の帯域飽和定数（統合/(統合+帯域×この値)）。大きいほど帯域制約が重い。</summary>
        public readonly float bandwidthSaturation;
        /// <summary>協調交戦が「鮮度」へ依存する度合い（0=鮮度無視、1=鮮度なしで成立せず）。</summary>
        public readonly float freshnessWeight;
        /// <summary>各個自律が途絶の打撃を相殺する効き（1=自律満点で打撃ゼロ）。</summary>
        public readonly float autonomyOffset;

        public DataLinkParams(float linkSaturation, float fusionQualityWeight, float bandwidthSaturation, float freshnessWeight, float autonomyOffset)
        {
            this.linkSaturation = Mathf.Max(0.0001f, linkSaturation);
            this.fusionQualityWeight = Mathf.Clamp01(fusionQualityWeight);
            this.bandwidthSaturation = Mathf.Max(0.0001f, bandwidthSaturation);
            this.freshnessWeight = Mathf.Clamp01(freshnessWeight);
            this.autonomyOffset = Mathf.Clamp01(autonomyOffset);
        }

        /// <summary>既定＝飽和隻数8・質重み0.5・帯域飽和1・鮮度重み0.6・自律相殺1。</summary>
        public static DataLinkParams Default => new DataLinkParams(8f, 0.5f, 1f, 0.6f, 1f);
    }

    /// <summary>
    /// データリンク（艦隊内の戦術情報共有ネットワーク）の純ロジック。リンクで結ばれた艦が
    /// センサー情報を持ち寄って<b>共通戦術状況図</b>を描き、目標情報を共有して、自艦のセンサーに
    /// 映らない敵も僚艦のデータで撃つ（協調交戦能力＝CEC）。帯域は有限で、情報は時間で腐り、
    /// リンクが切れれば各個分散して個艦の自律性に戻る。各値は実効値パターンで合成し、ネットワーク化
    /// された戦闘力（個艦火力の単純和を超える優位）を出す＝会戦経路はこれを倍率として読む想定。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DataLinkRules
    {
        /// <summary>
        /// 共通戦術状況図（0..1）＝リンク艦数×センサー寄与で構築。リンク艦が増えるほど（飽和カーブ）、
        /// 各艦のセンサー寄与が高いほど図が濃くなる。1隻だけ・寄与ゼロなら図は薄い。
        /// </summary>
        public static float SharedPicture(int linkedUnits, float sensorContribution, DataLinkParams p)
        {
            int units = Mathf.Max(0, linkedUnits);
            // リンク艦数の飽和カーブ（0隻=0、飽和隻数で約0.5、多数で1へ漸近）
            float reach = units / (units + p.linkSaturation);
            return Mathf.Clamp01(reach * Mathf.Clamp01(sensorContribution));
        }

        public static float SharedPicture(int linkedUnits, float sensorContribution)
            => SharedPicture(linkedUnits, sensorContribution, DataLinkParams.Default);

        /// <summary>
        /// センサー情報の統合（0..1）＝状況図×データの質。質重みで「生データの質」が効く度合いを調整＝
        /// 図が濃くても質が低ければ統合は鈍る（誤探知混じり）。
        /// </summary>
        public static float SensorFusion(float sharedPicture, float dataQuality, DataLinkParams p)
        {
            float picture = Mathf.Clamp01(sharedPicture);
            float quality = Mathf.Lerp(1f - p.fusionQualityWeight, 1f, Mathf.Clamp01(dataQuality));
            return Mathf.Clamp01(picture * quality);
        }

        public static float SensorFusion(float sharedPicture, float dataQuality)
            => SensorFusion(sharedPicture, dataQuality, DataLinkParams.Default);

        /// <summary>
        /// 目標情報の共有（0..1）＝統合/(統合+帯域制約)。帯域 linkBandwidth(0..1) が広いほど制約が軽く
        /// 統合がそのまま共有へ通る。帯域が細いと統合が高くても共有が頭打ち（パイプが詰まる）。
        /// </summary>
        public static float TargetSharing(float sensorFusion, float linkBandwidth, DataLinkParams p)
        {
            float fusion = Mathf.Clamp01(sensorFusion);
            float bandwidth = Mathf.Clamp01(linkBandwidth);
            // 帯域不足＝制約が大きい（1-帯域）。帯域満点で制約ゼロ＝共有=統合。
            float constraint = (1f - bandwidth) * p.bandwidthSaturation;
            float denom = fusion + constraint;
            if (denom <= 0.0001f) return 0f;
            return Mathf.Clamp01(fusion / denom);
        }

        public static float TargetSharing(float sensorFusion, float linkBandwidth)
            => TargetSharing(sensorFusion, linkBandwidth, DataLinkParams.Default);

        /// <summary>
        /// 情報の鮮度（0..1）＝更新頻度×(1-遅延)。高頻度に更新され遅延が小さいほど図は「今」を映す。
        /// 更新が遅い・遅延が大きいと古い航跡で撃つことになる。
        /// </summary>
        public static float DataFreshness(float updateRate, float latency, DataLinkParams p)
        {
            float rate = Mathf.Clamp01(updateRate);
            float lag = Mathf.Clamp01(latency);
            return Mathf.Clamp01(rate * (1f - lag));
        }

        public static float DataFreshness(float updateRate, float latency)
            => DataFreshness(updateRate, latency, DataLinkParams.Default);

        /// <summary>
        /// 協調交戦能力（0..1）＝目標共有×鮮度。自艦が見えない敵を僚艦データで撃つ（CEC）。鮮度重み
        /// freshnessWeight で「古いデータでは撃てない」度合いを調整＝鮮度0でも重み0なら共有だけで成立。
        /// </summary>
        public static float CooperativeEngagement(float targetSharing, float dataFreshness, DataLinkParams p)
        {
            float sharing = Mathf.Clamp01(targetSharing);
            float fresh = Mathf.Lerp(1f - p.freshnessWeight, 1f, Mathf.Clamp01(dataFreshness));
            return Mathf.Clamp01(sharing * fresh);
        }

        public static float CooperativeEngagement(float targetSharing, float dataFreshness)
            => CooperativeEngagement(targetSharing, dataFreshness, DataLinkParams.Default);

        /// <summary>
        /// リンク途絶の打撃（0..1）＝リンク途絶×(1-各個自律)。リンクが切れても各個自律が高ければ
        /// 打撃は小さい（艦は自前のセンサーと判断で戦える）。autonomyOffset で相殺の効きを調整。
        /// </summary>
        public static float LinkDisruptionImpact(float linkLoss, float unitAutonomy, DataLinkParams p)
        {
            float loss = Mathf.Clamp01(linkLoss);
            float autonomy = Mathf.Clamp01(unitAutonomy) * p.autonomyOffset;
            return Mathf.Clamp01(loss * (1f - autonomy));
        }

        public static float LinkDisruptionImpact(float linkLoss, float unitAutonomy)
            => LinkDisruptionImpact(linkLoss, unitAutonomy, DataLinkParams.Default);

        /// <summary>
        /// ネットワーク化された戦闘力（0..1）＝協調交戦×状況図。共通図の上で協調して撃てるほど、
        /// 個艦火力の単純和を超える優位が出る（情報優位が火力へ転化）。
        /// </summary>
        public static float NetworkedLethality(float cooperativeEngagement, float sharedPicture, DataLinkParams p)
        {
            float cec = Mathf.Clamp01(cooperativeEngagement);
            float picture = Mathf.Clamp01(sharedPicture);
            return Mathf.Clamp01(cec * picture);
        }

        public static float NetworkedLethality(float cooperativeEngagement, float sharedPicture)
            => NetworkedLethality(cooperativeEngagement, sharedPicture, DataLinkParams.Default);

        /// <summary>データリンクで優位か＝ネットワーク化戦闘力が閾値以上（情報優位が戦力差を生む）。</summary>
        public static bool IsLinkAdvantaged(float networkedLethality, float threshold)
        {
            return Mathf.Clamp01(networkedLethality) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値0.5＝半分以上のネットワーク化戦闘力でリンク優位とみなす。</summary>
        public static bool IsLinkAdvantaged(float networkedLethality)
            => IsLinkAdvantaged(networkedLethality, 0.5f);
    }
}
