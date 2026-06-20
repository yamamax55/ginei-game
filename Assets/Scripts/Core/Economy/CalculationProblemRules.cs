using UnityEngine;

namespace Ginei
{
    /// <summary>経済計算問題の調整係数（ミーゼス・ハイエク型・HAYK-2 #1544）。</summary>
    public readonly struct CalculationProblemParams
    {
        /// <summary>市場価格が歪みで失う質の効き（歪み1でこのぶん価格シグナルが劣化）。</summary>
        public readonly float distortionScale;
        /// <summary>計画化が計算能力を削る効きの強さ（計画化が増すほど計算が暗中模索に＝非線形）。</summary>
        public readonly float planningPenaltyScale;
        /// <summary>配分効率の下駄（計算能力0でも残る最低効率＝この値〜1.0の幅で計算能力に比例）。</summary>
        public readonly float efficiencyFloor;
        /// <summary>誤配分が累積する速さ/秒（計画化が進むほど過剰生産と不足が溜まる）。</summary>
        public readonly float misallocationRate;
        /// <summary>非効率な配分が生産性を削る最大ペナルティ（配分効率0でこのぶん生産性が落ちる）。</summary>
        public readonly float productivityPenaltyMax;

        public CalculationProblemParams(float distortionScale, float planningPenaltyScale,
            float efficiencyFloor, float misallocationRate, float productivityPenaltyMax)
        {
            this.distortionScale = Mathf.Clamp01(distortionScale);
            this.planningPenaltyScale = Mathf.Max(0f, planningPenaltyScale);
            this.efficiencyFloor = Mathf.Clamp01(efficiencyFloor);
            this.misallocationRate = Mathf.Max(0f, misallocationRate);
            this.productivityPenaltyMax = Mathf.Clamp01(productivityPenaltyMax);
        }

        /// <summary>
        /// 既定＝歪み効き1・計画ペナルティ1・効率下駄0.2・誤配分累積0.1/秒・生産性ペナルティ最大0.6。
        /// 効率下駄0.2＝計算能力ゼロでも市場の残滓で2割は配分できる／計画ペナルティ1＝計画化が二乗で
        /// 計算能力を削る＝<b>計画化が進むほど価格なき配分が暗中模索になる</b>非線形を数値に固定。
        /// </summary>
        public static CalculationProblemParams Default
            => new CalculationProblemParams(1f, 1f, 0.2f, 0.1f, 0.6f);
    }

    /// <summary>
    /// 社会主義経済計算問題の純ロジック（ミーゼス・ハイエク型・HAYK-2 #1544）。経済計算論争＝<b>価格メカニズムが
    /// なければ計画当局は資源の希少性を計算できず、効率的な配分が不可能になる</b>が核。価格は分散した知識を集約する
    /// シグナルであり、それなしの計画は暗中模索になる＝計画化が進むほど計算能力が落ち、資源配分が非効率になり、
    /// 生産性が削られる。中央計画は現場の局所知識を取りこぼし（ハイエクの知識問題）、誤配分が累積して「過剰生産」と
    /// 「不足」が同時に起きる（計画経済の慢性病）。
    /// <see cref="MarketRules"/>（価格発見と需給均衡＝価格が機能する世界）／同EPIC HAYK の
    /// SpontaneousOrderRules（自生的秩序＝設計されない秩序の侵食）／PlanningDriftRules（計画ドリフト＝計画が
    /// 現実から乖離）とは分担し、ここは<b>価格なき計画の情報問題と、それに伴う配分効率・生産性の損失</b>に専念する
    /// （需給価格でも秩序の侵食でも計画乖離でもなく、計算能力の喪失と誤配分の累積が主役）。
    /// すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CalculationProblemRules
    {
        /// <summary>
        /// 価格シグナルの質（0..1）＝市場価格がどれだけ希少性を正しく伝えるか。市場価格決定度×(1−歪み×効き)＝
        /// <b>統制で価格が歪むほどシグナルが劣化する</b>（価格は希少性を伝える情報。歪めば情報が死ぬ）。
        /// 市場価格決定が満点で歪みゼロなら1、歪みが効くほど痩せる。
        /// </summary>
        public static float PriceSignalQuality(float marketPricing, float distortion, CalculationProblemParams p)
        {
            float mp = Mathf.Clamp01(marketPricing);
            float dist = Mathf.Clamp01(distortion);
            return Mathf.Clamp01(mp * (1f - dist * p.distortionScale));
        }

        public static float PriceSignalQuality(float marketPricing, float distortion)
            => PriceSignalQuality(marketPricing, distortion, CalculationProblemParams.Default);

        /// <summary>
        /// 経済計算能力（0..1）＝価格シグナルなき計画は希少性を計算できない。価格シグナルの質×(1−計画化²×効き)＝
        /// <b>計画化が進むほど計算能力が非線形に落ちる</b>（計画化の二乗＝軽い計画は害が小さいが全面計画は暗中模索）。
        /// 価格シグナルが死んでいれば計画化が低くても計算能力は出ない（価格こそが計算の前提）。
        /// </summary>
        public static float CalculationCapacity(float priceSignalQuality, float centralPlanning, CalculationProblemParams p)
        {
            float psq = Mathf.Clamp01(priceSignalQuality);
            float cp = Mathf.Clamp01(centralPlanning);
            float planningDrag = Mathf.Clamp01(cp * cp * p.planningPenaltyScale);
            return Mathf.Clamp01(psq * (1f - planningDrag));
        }

        public static float CalculationCapacity(float priceSignalQuality, float centralPlanning)
            => CalculationCapacity(priceSignalQuality, centralPlanning, CalculationProblemParams.Default);

        /// <summary>
        /// 資源配分効率（0..1）＝計算能力が低いほど資源配分が非効率（暗中模索）。efficiencyFloor〜1.0 を計算能力で
        /// 線形補間＝<b>計算能力が配分効率の源泉</b>（計算能力0でも下駄ぶんは残り、満点で効率1）。
        /// 呼び出し側が産出倍率へ掛ける。
        /// </summary>
        public static float AllocationEfficiency(float calculationCapacity, CalculationProblemParams p)
        {
            float cc = Mathf.Clamp01(calculationCapacity);
            return Mathf.Lerp(p.efficiencyFloor, 1f, cc);
        }

        public static float AllocationEfficiency(float calculationCapacity)
            => AllocationEfficiency(calculationCapacity, CalculationProblemParams.Default);

        /// <summary>
        /// 生産性ペナルティ（0..1＝生産性をどれだけ削るか）＝非効率な配分が生産性を削る（価格なき計画のコスト）。
        /// (1−配分効率)×productivityPenaltyMax＝<b>配分が非効率なほど生産性が落ちる</b>（配分効率1なら罰なし、
        /// 0で最大ペナルティ）。呼び出し側が産出倍率を (1−penalty) で削る。
        /// </summary>
        public static float ProductivityPenalty(float allocationEfficiency, CalculationProblemParams p)
        {
            float ae = Mathf.Clamp01(allocationEfficiency);
            return Mathf.Clamp01((1f - ae) * p.productivityPenaltyMax);
        }

        public static float ProductivityPenalty(float allocationEfficiency)
            => ProductivityPenalty(allocationEfficiency, CalculationProblemParams.Default);

        /// <summary>
        /// 誤配分の累積（dt後の misallocation 0..1）＝計画化が進むほど資源の誤配分が累積する（過剰生産と不足の共存）。
        /// 累積量＝累積率×計画化×(1−現誤配分)×dt＝<b>計画化が高いほど誤配分が溜まる</b>（伸びしろに比例して飽和へ）。
        /// 計画化0なら市場が配分するので誤配分は増えない。
        /// </summary>
        public static float MisallocationTick(float misallocation, float planningLevel, float dt, CalculationProblemParams p)
        {
            float m = Mathf.Clamp01(misallocation);
            float pl = Mathf.Clamp01(planningLevel);
            float step = Mathf.Max(0f, dt);
            float growth = p.misallocationRate * pl * (1f - m) * step;
            return Mathf.Clamp01(m + growth);
        }

        public static float MisallocationTick(float misallocation, float planningLevel, float dt)
            => MisallocationTick(misallocation, planningLevel, dt, CalculationProblemParams.Default);

        /// <summary>
        /// 情報損失（0..1）＝中央計画が分散した局所知識を取りこぼす（<b>ハイエクの知識問題</b>）。
        /// 中央計画×分散知識＝計画化が高く現場の知識が豊かなほど取りこぼしが大きい（上からの設計は分散知識を
        /// 再現できない）。中央計画0なら現場で知識が活かされ損失ゼロ、分散知識0なら失うものがない。
        /// </summary>
        public static float InformationLoss(float centralPlanning, float dispersedKnowledge)
        {
            float cp = Mathf.Clamp01(centralPlanning);
            float dk = Mathf.Clamp01(dispersedKnowledge);
            return Mathf.Clamp01(cp * dk);
        }

        /// <summary>
        /// 不足と過剰在庫の同時発生度（0..1）＝計算失敗が同時に「不足」と「過剰在庫」を生む（計画経済の慢性病）。
        /// 誤配分×(1−誤配分)×4＝<b>誤配分が中庸のとき最大</b>（一部の財が過剰・一部が不足という乖離が最も顕著）。
        /// 誤配分0は均衡配分で乖離なし、誤配分1は全面破綻で「不足」一色になり乖離としては鈍る（山なり）。
        /// </summary>
        public static float ShortageAndSurplus(float misallocation)
        {
            float m = Mathf.Clamp01(misallocation);
            return Mathf.Clamp01(m * (1f - m) * 4f);
        }

        /// <summary>
        /// 経済計算が破綻した混沌か（true＝価格なき計画で計算が破綻）。計算能力が threshold を下回ると成立＝
        /// <b>価格メカニズムを失い希少性を計算できなくなった状態</b>。計算能力が閾値以上なら（不完全でも）
        /// 配分は機能している。
        /// </summary>
        public static bool IsCalculationChaos(float calculationCapacity, float threshold)
        {
            float cc = Mathf.Clamp01(calculationCapacity);
            float th = Mathf.Clamp01(threshold);
            return cc < th;
        }
    }
}
