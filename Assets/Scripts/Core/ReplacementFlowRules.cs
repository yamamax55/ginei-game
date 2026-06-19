using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 損耗補充の流れ（消耗した部隊への新兵/新造艦の補充）の調整値。
    /// 補充の供給効率の効き・練度希釈の効き・新兵が戦力化する統合の遅さ・空洞化の進む速さ・戦力化のしきい値。
    /// </summary>
    public readonly struct ReplacementFlowParams
    {
        /// <summary>補充プールから需要へ流す効率（0..1。1で需要を満たせる・小さいほど絞られる）。</summary>
        public readonly float supplyEfficiency;
        /// <summary>新兵が戦力化するまでの基準統合時間（補充1単位あたりのgame-time秒・大きいほど遅い）。</summary>
        public readonly float integrationBase;
        /// <summary>補充不足が部隊を空洞化させる速さ（不足×期間に対する係数・大きいほど早く枯れる）。</summary>
        public readonly float hollowingRate;
        /// <summary>幹部（古参）の再建加速の効き（古参比率→再建速度の効き・大きいほど幹部温存が効く）。</summary>
        public readonly float cadreWeight;
        /// <summary>補充部隊が戦力化したとみなす統合進捗の既定しきい値（0..1）。</summary>
        public readonly float readyThreshold;

        public ReplacementFlowParams(float supplyEfficiency, float integrationBase, float hollowingRate, float cadreWeight, float readyThreshold)
        {
            this.supplyEfficiency = Mathf.Clamp01(supplyEfficiency);
            this.integrationBase = Mathf.Max(0f, integrationBase);
            this.hollowingRate = Mathf.Max(0f, hollowingRate);
            this.cadreWeight = Mathf.Clamp01(cadreWeight);
            this.readyThreshold = Mathf.Clamp01(readyThreshold);
        }

        /// <summary>既定：供給効率0.8・基準統合時間2.0・空洞化速度0.5・幹部の効き0.5・戦力化しきい値0.6。</summary>
        public static ReplacementFlowParams Default
            => new ReplacementFlowParams(DefaultSupplyEfficiency, DefaultIntegrationBase, DefaultHollowingRate, DefaultCadreWeight, DefaultReadyThreshold);

        public const float DefaultSupplyEfficiency = 0.8f;
        public const float DefaultIntegrationBase = 2.0f;
        public const float DefaultHollowingRate = 0.5f;
        public const float DefaultCadreWeight = 0.5f;
        public const float DefaultReadyThreshold = 0.6f;
    }

    /// <summary>
    /// 損耗補充の流れ（Replacement Flow＝戦闘で消耗した部隊に新兵/新造艦を補充する）の純ロジック。
    /// 補充を流すと頭数は回復するが、新兵・新造艦が混じり平均練度（練度）が薄まる。
    /// 補充が需要に追いつかないと部隊は空洞化して枯れる。古参（幹部）を残せば再建が速い。
    ///
    /// <b>分担</b>：
    /// - <see cref="ReinforcementRules"/>（増援の時間差到着・既存）とは別＝こちらは<b>損耗補充と練度希釈</b>（既存部隊の頭数を埋め戻す）。
    /// - <see cref="VeterancyRules"/>（練度→会戦倍率・既存）とは別＝本ルールは<b>補充による平均練度の低下そのもの</b>を扱う。
    /// 盤面非依存の plain 引数のみ（艦隊・シーンに触れない）。test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class ReplacementFlowRules
    {
        /// <summary>既定パラメータで補充の供給率を返す。</summary>
        public static float ReplacementRate(float replacementPool, float demandRate)
            => ReplacementRate(replacementPool, demandRate, ReplacementFlowParams.Default);

        /// <summary>
        /// 補充の供給率（実際に流れる頭数/秒）。需要(demandRate)に対し供給効率で流すが、
        /// プール(replacementPool)が薄ければ需要を満たせず頭打ちになる＝min(需要×効率, プール)。
        /// </summary>
        public static float ReplacementRate(float replacementPool, float demandRate, ReplacementFlowParams p)
        {
            float pool = Mathf.Max(0f, replacementPool);
            float demand = Mathf.Max(0f, demandRate);
            float wanted = demand * p.supplyEfficiency;
            return Mathf.Min(wanted, pool);
        }

        /// <summary>
        /// 頭数の回復量（この期間に埋め戻された兵力）。供給率×経過時間。
        /// 現有兵力は情報として受け取るが回復は供給率と時間で決まる（負の dt は0）。
        /// </summary>
        public static float StrengthRecovery(float currentStrength, float replacementRate, float dt)
        {
            float rate = Mathf.Max(0f, replacementRate);
            float time = Mathf.Max(0f, dt);
            return rate * time;
        }

        /// <summary>
        /// 補充で薄まった平均練度(0..1)。古参(veteranCount)の練度(currentVeterancy)に練度0の補充(replacementCount)が混じり、
        /// 全体の加重平均が下がる＝veteran×V /(veteran+replacement)。母数0なら0（人がいない）。
        /// </summary>
        public static float VeterancyDilution(float currentVeterancy, float veteranCount, float replacementCount)
        {
            float v = Mathf.Clamp01(currentVeterancy);
            float vets = Mathf.Max(0f, veteranCount);
            float repl = Mathf.Max(0f, replacementCount);
            float total = vets + repl;
            if (total <= 0f) return 0f;
            // 補充は練度0と仮定＝古参ぶんだけが練度に寄与する。
            return Mathf.Clamp01((vets * v) / total);
        }

        /// <summary>既定パラメータで新兵の統合時間を返す。</summary>
        public static float IntegrationTime(float replacementCount, float unitCohesion)
            => IntegrationTime(replacementCount, unitCohesion, ReplacementFlowParams.Default);

        /// <summary>
        /// 新兵が戦力化するまでの統合時間(game-time秒)。補充が多いほど長くかかり、
        /// 部隊の結束(unitCohesion 0..1)が高いほど早く馴染む＝結束で時間を割り引く。
        /// </summary>
        public static float IntegrationTime(float replacementCount, float unitCohesion, ReplacementFlowParams p)
        {
            float repl = Mathf.Max(0f, replacementCount);
            float cohesion = Mathf.Clamp01(unitCohesion);
            // 結束1.0で半減、結束0で満額（馴染みにくい）。
            float cohesionFactor = 1f - 0.5f * cohesion;
            return repl * p.integrationBase * cohesionFactor;
        }

        /// <summary>
        /// 補充不足（追いつかない度合い 0..1）。需要に対し供給が足りないぶんの比率＝(需要-供給)/需要。
        /// 供給が需要以上なら0（充足）。需要0なら不足なし（0）。
        /// </summary>
        public static float ReplacementShortfall(float demandRate, float replacementRate)
        {
            float demand = Mathf.Max(0f, demandRate);
            float supply = Mathf.Max(0f, replacementRate);
            if (demand <= 0f) return 0f;
            float deficit = demand - supply;
            if (deficit <= 0f) return 0f;
            return Mathf.Clamp01(deficit / demand);
        }

        /// <summary>既定パラメータで部隊の空洞化を返す。</summary>
        public static float UnitHollowing(float replacementShortfall, float duration)
            => UnitHollowing(replacementShortfall, duration, ReplacementFlowParams.Default);

        /// <summary>
        /// 補充不足で部隊が枯れる度合い（空洞化 0..1）。不足(0..1)が続いた期間(duration)に応じて進む。
        /// 不足が無ければ進まず、長く続くほど1へ漸近する＝1-exp 風だが Exp 不可ゆえ飽和線形でクランプ。
        /// </summary>
        public static float UnitHollowing(float replacementShortfall, float duration, ReplacementFlowParams p)
        {
            float shortfall = Mathf.Clamp01(replacementShortfall);
            float time = Mathf.Max(0f, duration);
            return Mathf.Clamp01(shortfall * p.hollowingRate * time);
        }

        /// <summary>既定パラメータで幹部温存による再建速度を返す。</summary>
        public static float CadrePreservation(float veteranCount, float totalStrength)
            => CadrePreservation(veteranCount, totalStrength, ReplacementFlowParams.Default);

        /// <summary>
        /// 幹部（古参）を残すと再建が速い度合い(0..1)。残存兵力に占める古参比率(veteran/total)が高いほど速い。
        /// cadreWeight で効きを調整＝古参の骨格があれば補充を素早く戦力化できる（核を残す）。母数0なら0。
        /// </summary>
        public static float CadrePreservation(float veteranCount, float totalStrength, ReplacementFlowParams p)
        {
            float vets = Mathf.Max(0f, veteranCount);
            float total = Mathf.Max(0f, totalStrength);
            if (total <= 0f) return 0f;
            float cadreRatio = Mathf.Clamp01(vets / total);
            return Mathf.Clamp01(cadreRatio * p.cadreWeight);
        }

        /// <summary>既定しきい値で補充部隊が戦力化したか判定する。</summary>
        public static bool IsUnitCombatReady(float strengthRecovery, float integrationTime, float elapsed)
            => IsUnitCombatReady(strengthRecovery, integrationTime, elapsed, ReplacementFlowParams.Default.readyThreshold);

        /// <summary>
        /// 補充部隊が戦力になったか(bool)。頭数の回復(strengthRecovery)が進み、かつ統合時間(integrationTime)に対し
        /// 経過(elapsed)が threshold 割合に達していれば戦力化。頭数が無い／馴染んでいないなら未だ戦力にならない。
        /// </summary>
        public static bool IsUnitCombatReady(float strengthRecovery, float integrationTime, float elapsed, float threshold)
        {
            float recovery = Mathf.Max(0f, strengthRecovery);
            float integ = Mathf.Max(0f, integrationTime);
            float time = Mathf.Max(0f, elapsed);
            float th = Mathf.Clamp01(threshold);
            if (recovery <= 0f) return false;
            // 統合時間0なら即馴染む（補充が無い等）。それ以外は経過/統合時間が th 以上で戦力化。
            if (integ <= 0f) return true;
            return (time / integ) >= th;
        }
    }
}
