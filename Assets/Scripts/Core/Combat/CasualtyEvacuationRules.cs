using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 傷病兵後送（メディバック）のパラメータ。基準値は ctor で全クランプ。
    /// </summary>
    public readonly struct CasualtyEvacuationParams
    {
        /// <summary>救命率の時間減衰係数（大きいほど治療の遅れで急落＝1/(1+k·t)）。</summary>
        public readonly float goldenHourK;
        /// <summary>敵の阻止が後送路の安全を削る重み（0..1）。</summary>
        public readonly float interdictionWeight;
        /// <summary>戦線維持と後送のジレンマ強度（後送に人手を割く痛み）。</summary>
        public readonly float tradeoffWeight;
        /// <summary>救護隊の損耗の最大率（不安全な経路でもこれ以上は失わない）。</summary>
        public readonly float maxAttrition;
        /// <summary>生存とみなす既定しきい値（生存率がこれ以上で「後送成功」）。</summary>
        public readonly float survivalThreshold;

        public CasualtyEvacuationParams(
            float goldenHourK,
            float interdictionWeight,
            float tradeoffWeight,
            float maxAttrition,
            float survivalThreshold)
        {
            this.goldenHourK = Mathf.Clamp(goldenHourK, 0.01f, 10f);
            this.interdictionWeight = Mathf.Clamp01(interdictionWeight);
            this.tradeoffWeight = Mathf.Clamp01(tradeoffWeight);
            this.maxAttrition = Mathf.Clamp01(maxAttrition);
            this.survivalThreshold = Mathf.Clamp01(survivalThreshold);
        }

        /// <summary>既定値（減衰係数1.0／敵阻止フル反映／ジレンマ等倍／損耗上限0.8／生存しきい値0.5）。</summary>
        public static CasualtyEvacuationParams Default =>
            new CasualtyEvacuationParams(1f, 1f, 1f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 傷病兵後送（メディバック＝負傷者の救護と搬送）の純ロジック（盤面非依存・plain引数）。
    /// 戦場で倒れた将兵を後方医療へ運ぶ営み：治療までのゴールデンタイム、後送路の安全、
    /// 後方への搬送能力、遅延による「防げたはずの死」、救護隊の損耗、そして戦線を保つ手と
    /// 後送に割く手のジレンマ。早く・安全に・十分な手段で運べた者だけが生き延びる。
    ///
    /// 分担：
    /// - <c>AttritionExchangeRules</c>（損耗の交換比）が「どれだけ倒れたか」、本ルールは
    ///   「倒れた者をどれだけ救えるか」を担う＝消耗の後段（救護）に特化。
    /// - 時間減衰は Log/Exp を使わず 1/(1+k·t)（規約）。
    /// すべて Mathf のみ・LINQ/乱数なし・実効値パターン（入力は非破壊）。
    /// </summary>
    public static class CasualtyEvacuationRules
    {
        private const float Eps = 0.0001f;

        // ---- ゴールデンタイム（救命率） ----

        /// <summary>治療までの時間から救命率（早いほど助かる＝1/(1+k·t)で急落）。Params版。</summary>
        public static float GoldenHourFactor(float timeToTreatment, CasualtyEvacuationParams p)
        {
            float t = Mathf.Max(timeToTreatment, 0f);
            return Mathf.Clamp01(1f / (1f + p.goldenHourK * t));
        }

        /// <summary>治療までの時間から救命率（既定Params）。</summary>
        public static float GoldenHourFactor(float timeToTreatment) =>
            GoldenHourFactor(timeToTreatment, CasualtyEvacuationParams.Default);

        // ---- 後送路の安全 ----

        /// <summary>経路支配×(1-敵の阻止)で後送路の安全（0..1）。Params版。</summary>
        public static float EvacuationRouteSafety(float routeControl, float enemyInterdiction, CasualtyEvacuationParams p)
        {
            float control = Mathf.Clamp01(routeControl);
            float interdict = Mathf.Clamp01(enemyInterdiction) * p.interdictionWeight;
            return Mathf.Clamp01(control * (1f - interdict));
        }

        /// <summary>後送路の安全（既定Params）。</summary>
        public static float EvacuationRouteSafety(float routeControl, float enemyInterdiction) =>
            EvacuationRouteSafety(routeControl, enemyInterdiction, CasualtyEvacuationParams.Default);

        // ---- 搬送能力 ----

        /// <summary>後送手段/(手段+傷病者量)で搬送能力（0..1・需要過多で逼迫）。Params版。</summary>
        public static float TransportCapacity(float medevacUnits, float casualtyVolume, CasualtyEvacuationParams p)
        {
            float units = Mathf.Max(medevacUnits, 0f);
            float volume = Mathf.Max(casualtyVolume, 0f);
            float total = units + volume;
            if (total <= Eps) return 1f; // 傷病者も手段もゼロ＝逼迫なし
            return Mathf.Clamp01(units / total);
        }

        /// <summary>搬送能力（既定Params）。</summary>
        public static float TransportCapacity(float medevacUnits, float casualtyVolume) =>
            TransportCapacity(medevacUnits, casualtyVolume, CasualtyEvacuationParams.Default);

        // ---- 後送の遅延 ----

        /// <summary>(1-搬送能力)×(1-安全)で後送の遅延（能力も安全も欠くほど遅れる）。Params版。</summary>
        public static float EvacuationDelay(float transportCapacity, float routeSafety, CasualtyEvacuationParams p)
        {
            float cap = Mathf.Clamp01(transportCapacity);
            float safety = Mathf.Clamp01(routeSafety);
            return Mathf.Clamp01((1f - cap) * (1f - safety));
        }

        /// <summary>後送の遅延（既定Params）。</summary>
        public static float EvacuationDelay(float transportCapacity, float routeSafety) =>
            EvacuationDelay(transportCapacity, routeSafety, CasualtyEvacuationParams.Default);

        // ---- 防げたはずの死 ----

        /// <summary>遅延×(1-救命率)で防げたはずの死（遅延が大きく救命率が低いほど増える）。Params版。</summary>
        public static float PreventableDeaths(float evacuationDelay, float goldenHourFactor, CasualtyEvacuationParams p)
        {
            float delay = Mathf.Clamp01(evacuationDelay);
            float ghf = Mathf.Clamp01(goldenHourFactor);
            return Mathf.Clamp01(delay * (1f - ghf));
        }

        /// <summary>防げたはずの死（既定Params）。</summary>
        public static float PreventableDeaths(float evacuationDelay, float goldenHourFactor) =>
            PreventableDeaths(evacuationDelay, goldenHourFactor, CasualtyEvacuationParams.Default);

        // ---- 救護隊の損耗 ----

        /// <summary>(1-安全)×後送量で救護隊の損耗（不安全な経路を多く往復するほど失う）。Params版。</summary>
        public static float MedevacAttrition(float routeSafety, float evacuationVolume, CasualtyEvacuationParams p)
        {
            float safety = Mathf.Clamp01(routeSafety);
            float volume = Mathf.Clamp01(evacuationVolume);
            return Mathf.Clamp((1f - safety) * volume, 0f, p.maxAttrition);
        }

        /// <summary>救護隊の損耗（既定Params）。</summary>
        public static float MedevacAttrition(float routeSafety, float evacuationVolume) =>
            MedevacAttrition(routeSafety, evacuationVolume, CasualtyEvacuationParams.Default);

        // ---- 戦線維持と後送のジレンマ ----

        /// <summary>後送に割く人手×戦闘の必要で戦線維持のジレンマ（大きいほど後送が戦線を削る痛み）。Params版。</summary>
        public static float FrontlineTradeoff(float troopsDivertedToEvac, float combatNeed, CasualtyEvacuationParams p)
        {
            float diverted = Mathf.Clamp01(troopsDivertedToEvac);
            float need = Mathf.Clamp01(combatNeed);
            return Mathf.Clamp01(diverted * need * p.tradeoffWeight);
        }

        /// <summary>戦線維持のジレンマ（既定Params）。</summary>
        public static float FrontlineTradeoff(float troopsDivertedToEvac, float combatNeed) =>
            FrontlineTradeoff(troopsDivertedToEvac, combatNeed, CasualtyEvacuationParams.Default);

        // ---- 生存率 ----

        /// <summary>
        /// 救命率×搬送−防げた死で生存率（0..1）。早く救命でき（救命率）十分に運べた（搬送能力）ほど
        /// 高く、防げたはずの死がそれを差し引く。Params版。
        /// </summary>
        public static float SurvivalRate(float goldenHourFactor, float transportCapacity, float preventableDeaths, CasualtyEvacuationParams p)
        {
            float ghf = Mathf.Clamp01(goldenHourFactor);
            float cap = Mathf.Clamp01(transportCapacity);
            float preventable = Mathf.Clamp01(preventableDeaths);
            return Mathf.Clamp01(ghf * cap - preventable);
        }

        /// <summary>生存率（既定Params）。</summary>
        public static float SurvivalRate(float goldenHourFactor, float transportCapacity, float preventableDeaths) =>
            SurvivalRate(goldenHourFactor, transportCapacity, preventableDeaths, CasualtyEvacuationParams.Default);

        /// <summary>その後送は生存を確保できたか（生存率がしきい値以上）。Params版。</summary>
        public static bool EvacuationSucceeded(float survivalRate, CasualtyEvacuationParams p) =>
            Mathf.Clamp01(survivalRate) >= p.survivalThreshold;

        /// <summary>後送成功判定（既定Params）。</summary>
        public static bool EvacuationSucceeded(float survivalRate) =>
            EvacuationSucceeded(survivalRate, CasualtyEvacuationParams.Default);
    }
}
