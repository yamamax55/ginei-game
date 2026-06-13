using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍産複合体＝戦争利得のロビー力学の純ロジック（唯一の窓口・test-first）。
    /// 戦争が長引くほど経済の軍需依存が深まり（<see cref="WarIndustryRules.IndustryShareTick"/>）、
    /// 軍需が雇用を握ると「平和は失業」になって講和に抵抗し（<see cref="WarIndustryRules.PeaceResistance"/>）、
    /// 予算のために敵を大きく見せる誘因が情報を汚染し（<see cref="WarIndustryRules.ThreatInflationIncentive"/>）、
    /// 監督の弱さ×産業規模が天下りの癒着を生み（<see cref="WarIndustryRules.RevolvingDoorCorruption"/>）、
    /// 深く依存した経済ほど平和経済への転換が痛い（<see cref="WarIndustryRules.ConversionCost"/>）。
    /// これらが監督の抑止を上回ると恒久戦争が「合理的」になる
    /// （<see cref="WarIndustryRules.WarContinuationPressure"/>／<see cref="WarIndustryRules.PerpetualWarEquilibrium"/>）
    /// ＝**戦争で食う者が増えるほど、平和は高くつく**。
    /// 分担：`StockMarketRules`＝企業一般の株価・配当（業種を問わない市場）／`LobbyRules`(バックログ)＝利益集団のロビー一般／
    /// **本クラス＝戦争利得に特化したロビー力学**（軍需シェアが講和・軍縮・情報を歪める層。株価・需給そのものは扱わない）。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。調整値は <see cref="WarIndustryParams"/>（既定 <see cref="WarIndustryParams.Default"/>）。
    /// </summary>
    public static class WarIndustryRules
    {
        /// <summary>軍産複合体の調整値（平時シェア・依存上限・飽和時間・各感度・均衡閾値）。ctor で全てクランプ。</summary>
        public readonly struct WarIndustryParams
        {
            /// <summary>平時の自然な軍需シェア（戦争が無いときの落ち着き先。0..0.9）。</summary>
            public readonly float peacetimeShare;
            /// <summary>戦争で深まる依存の上限シェア（peacetimeShare..1）。</summary>
            public readonly float maxShare;
            /// <summary>戦争長期化の飽和定数（warDuration がこの値で目標シェアが中間に達する。&gt;0）。</summary>
            public readonly float warSaturation;
            /// <summary>シェアの成長速度（/単位時間。≥0）。</summary>
            public readonly float growthRate;
            /// <summary>縮小速度の比率（成長比 0..1。依存は深まりやすく解けにくい＝縮小は遅い）。</summary>
            public readonly float shrinkRatio;
            /// <summary>講和抵抗の感度（≥0）。</summary>
            public readonly float resistanceScale;
            /// <summary>脅威誇張誘因の感度（≥0）。</summary>
            public readonly float inflationScale;
            /// <summary>天下り癒着の感度（≥0）。</summary>
            public readonly float corruptionScale;
            /// <summary>転換費用の非線形指数（≥1。深く依存するほど軍縮が超線形に痛い）。</summary>
            public readonly float conversionExponent;
            /// <summary>転換費用の感度（≥0）。</summary>
            public readonly float conversionScale;
            /// <summary>恒久戦争均衡の閾値（継続圧−実効監督がこれ以上で均衡成立。0..2）。</summary>
            public readonly float equilibriumThreshold;

            public WarIndustryParams(
                float peacetimeShare, float maxShare, float warSaturation, float growthRate, float shrinkRatio,
                float resistanceScale, float inflationScale, float corruptionScale,
                float conversionExponent, float conversionScale, float equilibriumThreshold)
            {
                this.peacetimeShare = Mathf.Clamp(peacetimeShare, 0f, 0.9f);          // 1だと誇張誘因の超過域が消える
                this.maxShare = Mathf.Clamp(maxShare, this.peacetimeShare, 1f);        // 平時シェア未満にしない
                this.warSaturation = Mathf.Max(0.01f, warSaturation);                  // 0除算防止
                this.growthRate = Mathf.Max(0f, growthRate);
                this.shrinkRatio = Mathf.Clamp01(shrinkRatio);
                this.resistanceScale = Mathf.Max(0f, resistanceScale);
                this.inflationScale = Mathf.Max(0f, inflationScale);
                this.corruptionScale = Mathf.Max(0f, corruptionScale);
                this.conversionExponent = Mathf.Max(1f, conversionExponent);           // 線形未満にしない＝超線形の痛みを保証
                this.conversionScale = Mathf.Max(0f, conversionScale);
                this.equilibriumThreshold = Mathf.Clamp(equilibriumThreshold, 0f, 2f);
            }

            /// <summary>
            /// 既定＝平時シェア0.05・上限0.6・飽和20・成長0.1・縮小比0.5（解けにくい）・
            /// 抵抗感度1・誇張感度1・癒着感度1・転換指数2・転換感度1・均衡閾値0.5。
            /// </summary>
            public static WarIndustryParams Default
                => new WarIndustryParams(0.05f, 0.6f, 20f, 0.1f, 0.5f, 1f, 1f, 1f, 2f, 1f, 0.5f);
        }

        /// <summary>軍需シェアの1tick更新（既定 Params）。</summary>
        public static float IndustryShareTick(float share, float warDuration, float dt)
            => IndustryShareTick(share, warDuration, dt, WarIndustryParams.Default);

        /// <summary>
        /// 軍需シェアの1tick更新＝戦争が長いほど経済の軍需依存が深まる。目標シェアは戦争継続時間の飽和関数
        /// （warDuration/(warDuration+warSaturation) で peacetimeShare→maxShare へ）、warDuration=0（平時）なら平時シェアへ戻る。
        /// ただし縮小は成長の shrinkRatio 倍の速さしか出ない＝**依存は深まりやすく解けにくい**。新しいシェアを返す（引数非破壊）。
        /// </summary>
        public static float IndustryShareTick(float share, float warDuration, float dt, WarIndustryParams p)
        {
            float s = Mathf.Clamp01(share);
            float w = Mathf.Max(0f, warDuration);
            float t = w / (w + p.warSaturation);                                 // 戦争長期化の飽和(0..1)
            float target = Mathf.Lerp(p.peacetimeShare, p.maxShare, t);
            float rate = p.growthRate * (target > s ? 1f : p.shrinkRatio);       // 縮小だけ遅い＝構造の粘性
            return Mathf.MoveTowards(s, target, rate * Mathf.Max(0f, dt));
        }

        /// <summary>講和への抵抗力（既定 Params）。</summary>
        public static float PeaceResistance(float industryShare, float employmentDependence)
            => PeaceResistance(industryShare, employmentDependence, WarIndustryParams.Default);

        /// <summary>
        /// 講和への抵抗力（0..1）＝軍需シェア×(0.5＋0.5×雇用依存)×感度。利潤動機だけでも半分の抵抗が立ち、
        /// 軍需が雇用まで握る（employmentDependence 0..1）と「平和は失業」になって抵抗が倍化する。
        /// 呼び出し側は講和受諾度（`WarGoalRules.PeaceAcceptance` 等）からこの分を差し引く想定（基準非破壊）。
        /// </summary>
        public static float PeaceResistance(float industryShare, float employmentDependence, WarIndustryParams p)
        {
            float share = Mathf.Clamp01(industryShare);
            float emp = Mathf.Clamp01(employmentDependence);
            return Mathf.Clamp01(share * (0.5f + 0.5f * emp) * p.resistanceScale);
        }

        /// <summary>脅威誇張の誘因（既定 Params）。</summary>
        public static float ThreatInflationIncentive(float industryShare)
            => ThreatInflationIncentive(industryShare, WarIndustryParams.Default);

        /// <summary>
        /// 脅威誇張の誘因（0..1）＝予算のために敵を大きく見せるインセンティブ。平時シェア以下では0
        /// （守る予算が無ければ誇張する理由も無い）、超過分の正規化値×感度で増える。
        /// 呼び出し側が諜報#119/情報の信頼度に (1−incentive) を掛ける想定＝**軍産は情報の汚染源**になる。
        /// </summary>
        public static float ThreatInflationIncentive(float industryShare, WarIndustryParams p)
        {
            float share = Mathf.Clamp01(industryShare);
            if (share <= p.peacetimeShare) return 0f;
            float t = (share - p.peacetimeShare) / (1f - p.peacetimeShare);       // 平時超過の正規化(0..1)
            return Mathf.Clamp01(t * p.inflationScale);
        }

        /// <summary>天下りの癒着（既定 Params）。</summary>
        public static float RevolvingDoorCorruption(float industryShare, float oversightStrength)
            => RevolvingDoorCorruption(industryShare, oversightStrength, WarIndustryParams.Default);

        /// <summary>
        /// 天下りの癒着（0..1）＝軍需シェア×(1−監督の強さ)×感度。産業が大きく監督が弱いほど、
        /// 発注者と受注者が回転ドアで一体化する。呼び出し側は腐敗#867/調達効率に響かせる想定で、
        /// <see cref="WarContinuationPressure"/> では監督の実効性を (1−corruption) で骨抜きにする。
        /// </summary>
        public static float RevolvingDoorCorruption(float industryShare, float oversightStrength, WarIndustryParams p)
            => Mathf.Clamp01(Mathf.Clamp01(industryShare) * (1f - Mathf.Clamp01(oversightStrength)) * p.corruptionScale);

        /// <summary>平和経済への転換費用（既定 Params）。</summary>
        public static float ConversionCost(float industryShare)
            => ConversionCost(industryShare, WarIndustryParams.Default);

        /// <summary>
        /// 平和経済への転換費用（0..1）＝シェアの conversionExponent 乗×感度。浅い依存のうちの軍縮は安く、
        /// 深く依存した経済ほど超線形に痛い＝**軍縮の最大の敵は経済構造**。
        /// 呼び出し側は軍縮時に財政#163/安定#109 からこの分を差し引く想定（基準非破壊）。
        /// </summary>
        public static float ConversionCost(float industryShare, WarIndustryParams p)
            => Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(industryShare), p.conversionExponent) * p.conversionScale);

        /// <summary>戦争継続圧（既定 Params）。</summary>
        public static float WarContinuationPressure(float industryShare, float employmentDependence, float oversightStrength)
            => WarContinuationPressure(industryShare, employmentDependence, oversightStrength, WarIndustryParams.Default);

        /// <summary>
        /// 戦争継続圧（−1..2）＝「平和の値段」を一つの数に出す：
        /// 講和抵抗＋転換費用（戦争を続けたい力）から実効監督（監督の強さ×(1−天下り癒着)＝歯止め）を引いた正味。
        /// 正に大きいほど講和が政治的に高くつき、負なら歯止めが勝っている。
        /// </summary>
        public static float WarContinuationPressure(
            float industryShare, float employmentDependence, float oversightStrength, WarIndustryParams p)
        {
            float oversight = Mathf.Clamp01(oversightStrength);
            float corruption = RevolvingDoorCorruption(industryShare, oversight, p);
            float effectiveOversight = oversight * (1f - corruption);            // 癒着が監督を骨抜きにする
            return PeaceResistance(industryShare, employmentDependence, p)
                 + ConversionCost(industryShare, p)
                 - effectiveOversight;
        }

        /// <summary>恒久戦争均衡の判定（既定 Params）。</summary>
        public static bool PerpetualWarEquilibrium(float industryShare, float employmentDependence, float oversightStrength)
            => PerpetualWarEquilibrium(industryShare, employmentDependence, oversightStrength, WarIndustryParams.Default);

        /// <summary>
        /// 恒久戦争が「合理化」される条件判定＝戦争継続圧が均衡閾値以上か。
        /// 軍需が深く（シェア大）・広く（雇用依存大）・野放し（監督弱）なら、誰も悪意なく講和が割に合わなくなる
        /// ＝戦争の長期化が経済構造として固定される。監督が強ければ高シェアでも均衡は崩せる（歯止めは効く）。
        /// </summary>
        public static bool PerpetualWarEquilibrium(
            float industryShare, float employmentDependence, float oversightStrength, WarIndustryParams p)
            => WarContinuationPressure(industryShare, employmentDependence, oversightStrength, p) >= p.equilibriumThreshold;
    }
}
