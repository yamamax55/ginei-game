using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略予備（戦域全体のための予備兵力＝遊兵）の調整値。即応度の動員の効き・中央配置の価値の効き・
    /// 予備を使い切ったときの過伸展リスクの効き・投入の既定しきい値。
    /// </summary>
    public readonly struct StrategicReserveParams
    {
        /// <summary>動員度→即応度の効き（0..1のクランプ後に乗算。0で動員無視・1で完全反映）。</summary>
        public readonly float mobilizationWeight;
        /// <summary>中央配置の価値の効き（多戦線で活きる度合いの倍率・大きいほど中央配置が効く）。</summary>
        public readonly float centralityWeight;
        /// <summary>予備を使い切ったときの過伸展リスクの効き（投入率に対する非線形指数・大きいほど終盤で急増）。</summary>
        public readonly float overstretchExponent;
        /// <summary>戦略予備を投入すべきと判断する切迫度×即応度の既定しきい値（0..1）。</summary>
        public readonly float deployThreshold;

        public StrategicReserveParams(float mobilizationWeight, float centralityWeight, float overstretchExponent, float deployThreshold)
        {
            this.mobilizationWeight = Mathf.Clamp01(mobilizationWeight);
            this.centralityWeight = Mathf.Max(0f, centralityWeight);
            this.overstretchExponent = Mathf.Max(0.01f, overstretchExponent);
            this.deployThreshold = Mathf.Clamp01(deployThreshold);
        }

        /// <summary>既定：動員の効き0.5・中央配置の効き1.0・過伸展指数2.0（二乗で終盤急増）・投入しきい値0.5。</summary>
        public static StrategicReserveParams Default
            => new StrategicReserveParams(DefaultMobilizationWeight, DefaultCentralityWeight, DefaultOverstretchExponent, DefaultDeployThreshold);

        public const float DefaultMobilizationWeight = 0.5f;
        public const float DefaultCentralityWeight = 1.0f;
        public const float DefaultOverstretchExponent = 2.0f;
        public const float DefaultDeployThreshold = 0.5f;
    }

    /// <summary>
    /// 戦略予備（Strategic Reserve＝戦域全体のための予備兵力・どこへでも投入できる遊兵）の純ロジック。
    /// 前線に貼り付けず温存し、危機の戦線へ駆けつけ（消防）、好機の戦線へ投入する（拡張）。
    /// 中央に置くほど各戦線へ均等に届く（内線の利）。使い切ると次の危機に対応できない（過伸展）。
    ///
    /// <b>分担</b>：
    /// - <see cref="ReserveDeploymentRules"/>（戦術の予備投入・既存なら）とは別＝こちらは<b>戦域レベルの戦略予備</b>（作戦/戦略規模）。
    /// - <see cref="InteriorLineRules"/>（内線・既存なら）と連携するが別＝中央配置の価値は内線の利を借りるが、本ルールは予備兵力の運用に徹する。
    /// 盤面非依存の plain 引数のみ（艦隊・シーンに触れない）。test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class StrategicReserveRules
    {
        /// <summary>既定パラメータで戦略予備の即応度を返す。</summary>
        public static float ReserveReadiness(float reserveStrength, float mobilization)
            => ReserveReadiness(reserveStrength, mobilization, StrategicReserveParams.Default);

        /// <summary>
        /// 戦略予備の即応度(0..1)。温存された兵力規模(0..1正規化想定)を動員度で底上げする。
        /// 動員済み(mobilization=1)なら兵力規模そのまま、未動員(0)なら mobilizationWeight ぶん割り引く。
        /// </summary>
        public static float ReserveReadiness(float reserveStrength, float mobilization, StrategicReserveParams p)
        {
            float strength = Mathf.Clamp01(reserveStrength);
            float mob = Mathf.Clamp01(mobilization);
            // 動員度で底上げ：mob=0 で (1-weight) 倍、mob=1 で 1.0 倍。
            float mobFactor = (1f - p.mobilizationWeight) + p.mobilizationWeight * mob;
            return Mathf.Clamp01(strength * mobFactor);
        }

        /// <summary>
        /// 危機戦線へ駆けつける応答時間(0..1・小さいほど速い)。
        /// 中央配置(centralityOfPosition 0..1)ほど各戦線へ近い＝速く、目標戦線が遠いほど遅い。
        /// 内線の利＝中央に置けば外周のどの戦線へも均等に届く。
        /// </summary>
        public static float ResponseTime(float centralityOfPosition, float targetFrontDistance)
        {
            float centrality = Mathf.Clamp01(centralityOfPosition);
            float distance = Mathf.Clamp01(targetFrontDistance);
            // 中央配置は実効距離を割り引く（内線）。中央1.0なら実効距離は半減。
            float effectiveDistance = distance * (1f - 0.5f * centrality);
            return Mathf.Clamp01(effectiveDistance);
        }

        /// <summary>既定パラメータで危機戦線を救う効果を返す。</summary>
        public static float CrisisResponse(float reserveReadiness, float responseTime, float frontCriticality)
            => CrisisResponse(reserveReadiness, responseTime, frontCriticality, StrategicReserveParams.Default);

        /// <summary>
        /// 危機の戦線を救う効果(0..1)。即応度が高く・応答が速く(responseTime 小)・戦線が切迫しているほど価値が高い。
        /// 間に合わない（応答が遅い）と救援価値は薄れる＝(1-responseTime) を掛ける。
        /// </summary>
        public static float CrisisResponse(float reserveReadiness, float responseTime, float frontCriticality, StrategicReserveParams p)
        {
            float readiness = Mathf.Clamp01(reserveReadiness);
            float timeliness = 1f - Mathf.Clamp01(responseTime); // 速いほど1へ
            float criticality = Mathf.Clamp01(frontCriticality);
            return Mathf.Clamp01(readiness * timeliness * criticality);
        }

        /// <summary>
        /// 好機の戦線を拡張する効果(0..1)。温存兵力(reserveStrength)を突破口(breakthroughOpening)へ注ぐと戦果が広がる。
        /// 突破口が無ければ予備を投じても拡張効果は出ない（積）。
        /// </summary>
        public static float OpportunityExploitation(float reserveStrength, float breakthroughOpening)
        {
            float strength = Mathf.Clamp01(reserveStrength);
            float opening = Mathf.Clamp01(breakthroughOpening);
            return Mathf.Clamp01(strength * opening);
        }

        /// <summary>
        /// 複数戦線への戦略予備の配分。各戦線の切迫度(frontCriticalities)に比例して totalReserve を割り振る。
        /// 最も切迫した戦線へ多く回る。切迫度が全て0／配列が null・空なら全0配列（行き先なし）。
        /// 配列は null/空安全・負値は0クランプ。総量保存。
        /// </summary>
        public static float[] ReserveAllocation(float[] frontCriticalities, float totalReserve)
        {
            if (frontCriticalities == null || frontCriticalities.Length == 0)
                return new float[0];

            int n = frontCriticalities.Length;
            float[] result = new float[n];
            float total = Mathf.Max(0f, totalReserve);

            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float c = Mathf.Max(0f, frontCriticalities[i]);
                result[i] = c; // 一旦クランプ済みの切迫度を入れておく
                sum += c;
            }

            if (sum <= 0f)
            {
                // 行き先なし＝どこも切迫していない＝予備は温存（全0）。
                for (int i = 0; i < n; i++) result[i] = 0f;
                return result;
            }

            for (int i = 0; i < n; i++)
                result[i] = total * (result[i] / sum);
            return result;
        }

        /// <summary>既定パラメータで予備枯渇の過伸展リスクを返す。</summary>
        public static float OverstretchFromNoReserve(float committedFraction)
            => OverstretchFromNoReserve(committedFraction, StrategicReserveParams.Default);

        /// <summary>
        /// 予備を使い切ると次の危機に対応できないリスク(0..1)。投入率(committedFraction 0..1)が高いほど急増。
        /// overstretchExponent で非線形＝既定2.0なら序盤は緩く・全投入(1.0)で最大1.0（次の危機に裸）。
        /// </summary>
        public static float OverstretchFromNoReserve(float committedFraction, StrategicReserveParams p)
        {
            float committed = Mathf.Clamp01(committedFraction);
            return Mathf.Clamp01(Mathf.Pow(committed, p.overstretchExponent));
        }

        /// <summary>既定パラメータで中央配置の価値を返す。</summary>
        public static float CentralPositionValue(float centrality, int frontCount)
            => CentralPositionValue(centrality, frontCount, StrategicReserveParams.Default);

        /// <summary>
        /// 中央配置の価値(0..1)。中央(centrality 0..1)に置くほど・戦線が多い(frontCount)ほど内線の利が活きる。
        /// 戦線1つなら中央配置の旨味は薄く、多戦線ほど一つの予備が複数方向へ効く。
        /// </summary>
        public static float CentralPositionValue(float centrality, int frontCount, StrategicReserveParams p)
        {
            float c = Mathf.Clamp01(centrality);
            int fronts = frontCount < 1 ? 1 : frontCount;
            // 戦線数の効き：1で0・増えるほど1へ漸近（(fronts-1)/fronts）。
            float multiFrontFactor = (float)(fronts - 1) / fronts;
            float value = c * multiFrontFactor * p.centralityWeight;
            return Mathf.Clamp01(value);
        }

        /// <summary>既定しきい値で戦略予備を投入すべきか判定する。</summary>
        public static bool ShouldDeployReserve(float frontCriticality, float reserveReadiness)
            => ShouldDeployReserve(frontCriticality, reserveReadiness, StrategicReserveParams.Default.deployThreshold);

        /// <summary>
        /// 戦略予備を投入すべきか(bool)。戦線の切迫度×即応度が threshold 以上なら投入＝即応できる予備で切迫を救う。
        /// 切迫していても即応度が低ければ間に合わず、即応できても切迫していなければ温存する。
        /// </summary>
        public static bool ShouldDeployReserve(float frontCriticality, float reserveReadiness, float threshold)
        {
            float criticality = Mathf.Clamp01(frontCriticality);
            float readiness = Mathf.Clamp01(reserveReadiness);
            float th = Mathf.Clamp01(threshold);
            return criticality * readiness >= th;
        }
    }
}
