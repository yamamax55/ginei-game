using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍産複合体＝造船省益による過剰建艦バイアスの純ロジック（#1389 MCN-4・唯一の窓口・test-first）。
    /// 造船所・軍需産業が自らの利権（省益・雇用）のために**軍事的必要を超えて過剰に建艦を推し進める**力学：
    /// 造船省益×産業の政治影響が調達を必要超に膨らませ（<see cref="MilitaryIndustrialRules.ProcurementBias"/>）、
    /// 実際の軍事的必要が低くても建艦圧力が止まらず（<see cref="MilitaryIndustrialRules.OverbuildPressure"/>＝必要なくても造り続ける）、
    /// 予算を取るために脅威を誇張し（<see cref="MilitaryIndustrialRules.ThreatExaggeration"/>＝脅威がないと予算が減る）、
    /// 平時でも一度動いた生産ラインが慣性で建艦を続け（<see cref="MilitaryIndustrialRules.PeacetimeMomentum"/>）、
    /// 過剰な造船能力が固定化して雇用・利権が建艦を要求し続け（<see cref="MilitaryIndustrialRules.CapacityLockIn"/>）、
    /// 過剰建艦が国庫を食い（<see cref="MilitaryIndustrialRules.FiscalDrainFromOverbuild"/>）、
    /// 天下り・癒着が調達を歪める（<see cref="MilitaryIndustrialRules.RevolvingDoorCorruption"/>）。
    /// 軍事的必要から乖離した建艦バブルが成立する（<see cref="MilitaryIndustrialRules.IsArmamentBubble"/>）
    /// ＝**軍事的合理でなく産業の自己利益が艦隊規模を膨らませる**。
    /// 分担：`WarIndustryRules`＝軍需が**講和・軍縮に抵抗**する戦争利得のロビー（平和は失業）／
    /// `ShipyardRules`＝造船そのものの生産（船渠・建艦速度・就役）／`LobbyRules`＝業種を問わないロビー一般／
    /// `MinistryRules`＝省益(institutionalInterest)＝縦割り強度の供給源／
    /// **本クラス＝造船省益による過剰建艦バイアス**（必要を超えた調達・平時の建艦慣性・建艦バブルの層。生産量・講和抵抗そのものは扱わない）。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。調整値は <see cref="MilitaryIndustrialParams"/>（既定 <see cref="MilitaryIndustrialParams.Default"/>）。
    /// </summary>
    public static class MilitaryIndustrialRules
    {
        /// <summary>調達バイアスの上限（軍事的必要を超える上乗せの最大倍率。0..2）。</summary>
        public const float MaxBiasOver = 1f;

        /// <summary>調達バイアスの最大値（軍事的必要を超える過剰調達倍率＝1＋bias の bias 部）。</summary>
        public static float ProcurementBias(float industryInfluence, float institutionalInterest)
            => ProcurementBias(industryInfluence, institutionalInterest, MilitaryIndustrialParams.Default);

        /// <summary>
        /// 調達バイアス（0..1）＝造船省益(institutionalInterest)×産業の政治影響(industryInfluence)で、
        /// 調達が軍事的必要を超えて膨らむ上乗せ率。省益が縦割りで予算を抱え込み、産業の影響がそれを政治へ通す＝
        /// 両者が**揃って初めて**必要超の調達が成立する（積＝どちらか0なら膨らまない）。感度 biasScale で増幅。
        /// </summary>
        public static float ProcurementBias(float industryInfluence, float institutionalInterest, MilitaryIndustrialParams p)
        {
            float inf = Mathf.Clamp01(industryInfluence);
            float interest = Mathf.Clamp01(institutionalInterest);
            return Mathf.Clamp01(inf * interest * p.biasScale);
        }

        /// <summary>過剰建艦の圧力（既定 Params）。</summary>
        public static float OverbuildPressure(float procurementBias, float actualNeed)
            => OverbuildPressure(procurementBias, actualNeed, MilitaryIndustrialParams.Default);

        /// <summary>
        /// 過剰建艦の圧力（0..1）＝調達バイアス×(1−軍事的必要)。**実際の軍事的必要が低いほど過剰建艦が際立つ**：
        /// 必要が高ければ調達は正当で過剰ではない（need=1で圧力0）、必要が無いのに建艦を続ける（need=0で圧力＝bias）＝
        /// 産業の論理が軍事の論理から乖離した分だけ「過剰」になる。呼び出し側は艦隊規模・財政へ響かせる想定（基準非破壊）。
        /// </summary>
        public static float OverbuildPressure(float procurementBias, float actualNeed, MilitaryIndustrialParams p)
        {
            float bias = Mathf.Clamp01(procurementBias);
            float need = Mathf.Clamp01(actualNeed);
            return Mathf.Clamp01(bias * (1f - need));
        }

        /// <summary>脅威の誇張誘因（既定 Params）。</summary>
        public static float ThreatExaggeration(float industryInfluence, float budgetStake)
            => ThreatExaggeration(industryInfluence, budgetStake, MilitaryIndustrialParams.Default);

        /// <summary>
        /// 脅威の誇張誘因（0..1）＝産業の政治影響×予算の懸かり具合(budgetStake)×感度。
        /// **脅威がないと予算が減る**から、予算が多く懸かるほど敵を大きく見せる誘因が強まる（`WarIndustryRules.ThreatInflationIncentive` と同型）。
        /// 呼び出し側が諜報#119/脅威評価の信頼度に (1−exaggeration) を掛ける想定＝軍産は脅威認識の汚染源になる。
        /// </summary>
        public static float ThreatExaggeration(float industryInfluence, float budgetStake, MilitaryIndustrialParams p)
        {
            float inf = Mathf.Clamp01(industryInfluence);
            float stake = Mathf.Clamp01(budgetStake);
            return Mathf.Clamp01(inf * stake * p.exaggerationScale);
        }

        /// <summary>平時の建艦慣性の1tick更新（既定 Params）。</summary>
        public static float PeacetimeMomentum(float procurementBias, float peace, float dt)
            => PeacetimeMomentum(procurementBias, peace, dt, MilitaryIndustrialParams.Default);

        /// <summary>
        /// 平時の建艦慣性（0..1）の1tick更新＝平時(peace 0..1 が高い)でも建艦が慣性で続く度合いへ漸近する。
        /// **一度動いた生産ラインは止まらない**：目標慣性＝調達バイアス×平時度（平和なほど「過剰さ」が露わ）、
        /// そこへ momentumRate×dt で MoveTowards。引数非破壊で新しい慣性値を返す。
        /// </summary>
        public static float PeacetimeMomentum(float procurementBias, float peace, float dt, MilitaryIndustrialParams p)
        {
            float bias = Mathf.Clamp01(procurementBias);
            float pc = Mathf.Clamp01(peace);
            float target = Mathf.Clamp01(bias * pc);                                   // 平時ほど過剰建艦が際立つ
            // 慣性自体を状態として持たない簡易形＝目標へ rate ぶん寄せた値（呼び出し側が前値を渡せば状態追従にもなる）
            return Mathf.MoveTowards(0f, target, p.momentumRate * Mathf.Max(0f, dt));
        }

        /// <summary>過剰造船能力の固定化（既定 Params）。</summary>
        public static float CapacityLockIn(float overbuildPressure)
            => CapacityLockIn(overbuildPressure, MilitaryIndustrialParams.Default);

        /// <summary>
        /// 過剰造船能力の固定化（0..1）＝過剰建艦圧力の lockInExponent 乗。**過剰な造船能力が一度できると雇用・利権が建艦を要求し続ける**：
        /// 浅い過剰はまだほどけるが、深く過剰な能力ほど超線形に固定化する（連続炉のように止められない＝`ContinuousOperationRules` と接続）。
        /// 呼び出し側は建艦縮小の難しさ・転換費用へ掛ける想定。
        /// </summary>
        public static float CapacityLockIn(float overbuildPressure, MilitaryIndustrialParams p)
            => Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(overbuildPressure), p.lockInExponent));

        /// <summary>過剰建艦による財政圧迫（既定 Params）。</summary>
        public static float FiscalDrainFromOverbuild(float overbuildPressure, float fiscalCapacity)
            => FiscalDrainFromOverbuild(overbuildPressure, fiscalCapacity, MilitaryIndustrialParams.Default);

        /// <summary>
        /// 過剰建艦による財政圧迫（0..1）＝過剰建艦圧力×(1.5−0.5×財政余力)×感度。**軍拡が国庫を食う**：
        /// 財政余力(fiscalCapacity 0..1)が薄いほど同じ過剰建艦が重くのしかかる（余力1で軽く、余力0で1.5倍痛い）。
        /// 呼び出し側は財政#163/`FiscalRules` の歳出側へ積む想定（基準非破壊）。
        /// </summary>
        public static float FiscalDrainFromOverbuild(float overbuildPressure, float fiscalCapacity, MilitaryIndustrialParams p)
        {
            float over = Mathf.Clamp01(overbuildPressure);
            float cap = Mathf.Clamp01(fiscalCapacity);
            float burden = 1.5f - 0.5f * cap;                                          // 余力が薄いほど重い(1.0..1.5)
            return Mathf.Clamp01(over * burden * p.drainScale);
        }

        /// <summary>天下りの癒着（既定 Params）。</summary>
        public static float RevolvingDoorCorruption(float industryGovernmentTies)
            => RevolvingDoorCorruption(industryGovernmentTies, MilitaryIndustrialParams.Default);

        /// <summary>
        /// 天下り・癒着（0..1）＝発注側(政府)と受注側(造船産業)の結びつき(industryGovernmentTies)×感度。
        /// 回転ドアで一体化するほど調達が競争でなく癒着で決まり、必要超の発注が通りやすくなる。
        /// 呼び出し側は調達効率#867・腐敗へ響かせる想定（`WarIndustryRules.RevolvingDoorCorruption` の造船省益版）。
        /// </summary>
        public static float RevolvingDoorCorruption(float industryGovernmentTies, MilitaryIndustrialParams p)
            => Mathf.Clamp01(Mathf.Clamp01(industryGovernmentTies) * p.corruptionScale);

        /// <summary>建艦バブルの判定（既定の閾値 Params.bubbleThreshold）。</summary>
        public static bool IsArmamentBubble(float overbuildPressure, float actualNeed)
            => IsArmamentBubble(overbuildPressure, actualNeed, MilitaryIndustrialParams.Default.bubbleThreshold);

        /// <summary>
        /// 軍事的必要から乖離した建艦バブルの判定＝過剰建艦圧力×(1−軍事的必要)が threshold 以上か。
        /// **必要が低いのに過剰建艦圧力が高い**ほど成立する（産業の論理だけで膨らんだ艦隊規模＝実需なきバブル）。
        /// 軍事的必要が十分あれば過剰圧力が高くてもバブルではない（正当な軍拡）。
        /// </summary>
        public static bool IsArmamentBubble(float overbuildPressure, float actualNeed, float threshold)
        {
            float over = Mathf.Clamp01(overbuildPressure);
            float need = Mathf.Clamp01(actualNeed);
            float th = Mathf.Clamp01(threshold);
            return over * (1f - need) >= th;
        }
    }

    /// <summary>
    /// 軍産複合体（造船省益による過剰建艦バイアス・#1389）の調整値（各感度・固定化指数・バブル閾値）。ctor で全てクランプ。
    /// </summary>
    public readonly struct MilitaryIndustrialParams
    {
        /// <summary>調達バイアスの感度（≥0）。</summary>
        public readonly float biasScale;
        /// <summary>脅威誇張誘因の感度（≥0）。</summary>
        public readonly float exaggerationScale;
        /// <summary>平時建艦慣性の追従速度（/単位時間。≥0）。</summary>
        public readonly float momentumRate;
        /// <summary>過剰造船能力固定化の非線形指数（≥1。深い過剰ほど超線形に固定化）。</summary>
        public readonly float lockInExponent;
        /// <summary>財政圧迫の感度（≥0）。</summary>
        public readonly float drainScale;
        /// <summary>天下り癒着の感度（≥0）。</summary>
        public readonly float corruptionScale;
        /// <summary>建艦バブル判定の閾値（過剰圧×必要不足がこれ以上でバブル。0..1）。</summary>
        public readonly float bubbleThreshold;

        public MilitaryIndustrialParams(
            float biasScale, float exaggerationScale, float momentumRate,
            float lockInExponent, float drainScale, float corruptionScale, float bubbleThreshold)
        {
            this.biasScale = Mathf.Max(0f, biasScale);
            this.exaggerationScale = Mathf.Max(0f, exaggerationScale);
            this.momentumRate = Mathf.Max(0f, momentumRate);
            this.lockInExponent = Mathf.Max(1f, lockInExponent);                       // 線形未満にしない＝固定化の超線形を保証
            this.drainScale = Mathf.Max(0f, drainScale);
            this.corruptionScale = Mathf.Max(0f, corruptionScale);
            this.bubbleThreshold = Mathf.Clamp01(bubbleThreshold);
        }

        /// <summary>
        /// 既定＝調達感度1・誇張感度1・慣性速度0.5・固定化指数2・財政感度1・癒着感度1・バブル閾値0.3。
        /// </summary>
        public static MilitaryIndustrialParams Default
            => new MilitaryIndustrialParams(1f, 1f, 0.5f, 2f, 1f, 1f, 0.3f);
    }
}
