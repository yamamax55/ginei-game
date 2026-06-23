using UnityEngine;

namespace Ginei
{
    /// <summary>電子対抗手段（ECM/ECCM）の調整係数。マジックナンバー禁止＝ここに集約。全フィールド ctor で Clamp。</summary>
    public readonly struct ElectronicCountermeasuresParams
    {
        /// <summary>妨害出力×範囲を妨害強度へ写す倍率（0..1）。</summary>
        public readonly float jammingScale;
        /// <summary>対妨害技術×周波数敏捷性を対妨害へ写す倍率（0..1）。</summary>
        public readonly float counterScale;
        /// <summary>偽目標×信号模倣を欺瞞妨害へ写す倍率（0..1）。</summary>
        public readonly float deceptionScale;
        /// <summary>双方の投資をエスカレーション度へ写す感度（0..1・大きいほど投資でいたちごっこが激化）。</summary>
        public readonly float escalationSensitivity;
        /// <summary>電子的に優越と判定する優越度の既定閾値（−1..1）。</summary>
        public readonly float dominanceThreshold;

        public ElectronicCountermeasuresParams(float jammingScale, float counterScale, float deceptionScale,
                                               float escalationSensitivity, float dominanceThreshold)
        {
            this.jammingScale = Mathf.Clamp01(jammingScale);
            this.counterScale = Mathf.Clamp01(counterScale);
            this.deceptionScale = Mathf.Clamp01(deceptionScale);
            this.escalationSensitivity = Mathf.Clamp01(escalationSensitivity);
            this.dominanceThreshold = Mathf.Clamp(dominanceThreshold, -1f, 1f);
        }

        /// <summary>
        /// 既定＝妨害倍率1.0/対妨害倍率1.0/欺瞞倍率1.0/エスカレーション感度1.0/優越閾値0.2。
        /// </summary>
        public static ElectronicCountermeasuresParams Default =>
            new ElectronicCountermeasuresParams(1f, 1f, 1f, 1f, 0.2f);
    }

    /// <summary>
    /// 電子対抗手段（ECM＝電子妨害／ECCM＝対電子妨害）の応酬の純ロジック。妨害の強度、対妨害技術、
    /// 欺瞞（デコイ＝偽目標）、周波数ホッピングによる回避、妨害と対妨害のいたちごっこ（エスカレーション）、
    /// 電子的優越を扱う＝撃てば相手が対抗し、対抗すれば相手が更に強化する技術的応酬。
    /// <see cref="JammingWarfareRules"/>（能動的な電波妨害そのもの）とは別＝こちらは ECM/ECCM の技術的かけ引き。
    /// 盤面非依存の plain 引数。値は徹底して clamp（符号付きは −1..1）・乱数なし決定論・実効値パターン。
    /// 純ロジック（非 MonoBehaviour・test-first）。各メソッドは Params 明示版＋Default 委譲版を持つ。
    /// </summary>
    public static class ElectronicCountermeasuresRules
    {
        /// <summary>
        /// 妨害の強度 0..1：妨害出力 jammerPower × 妨害範囲 jammerCoverage × jammingScale。
        /// 出力が高く広く覆うほど妨害が強い。
        /// </summary>
        public static float JammingStrength(float jammerPower, float jammerCoverage, ElectronicCountermeasuresParams p)
        {
            float power = Mathf.Clamp01(jammerPower);
            float coverage = Mathf.Clamp01(jammerCoverage);
            return Mathf.Clamp01(power * coverage * p.jammingScale);
        }

        public static float JammingStrength(float jammerPower, float jammerCoverage)
            => JammingStrength(jammerPower, jammerCoverage, ElectronicCountermeasuresParams.Default);

        /// <summary>
        /// 対妨害 0..1：対妨害技術 eccmTech × 周波数敏捷性 frequencyAgility × counterScale。
        /// 技術が高く周波数を素早く切り替えられるほど妨害を打ち消せる。
        /// </summary>
        public static float CounterJamming(float eccmTech, float frequencyAgility, ElectronicCountermeasuresParams p)
        {
            float tech = Mathf.Clamp01(eccmTech);
            float agility = Mathf.Clamp01(frequencyAgility);
            return Mathf.Clamp01(tech * agility * p.counterScale);
        }

        public static float CounterJamming(float eccmTech, float frequencyAgility)
            => CounterJamming(eccmTech, frequencyAgility, ElectronicCountermeasuresParams.Default);

        /// <summary>
        /// 妨害の実効 0..1：妨害強度 jammingStrength と対妨害 counterJamming の綱引き。
        /// jamming / (jamming + counter)。対妨害がゼロなら 1（完全有効）、両者ゼロなら 0（妨害なし）。
        /// Params 非依存（純粋な比）。
        /// </summary>
        public static float JammingEffectiveness(float jammingStrength, float counterJamming)
        {
            float jamming = Mathf.Clamp01(jammingStrength);
            float counter = Mathf.Clamp01(counterJamming);
            float denom = jamming + counter;
            if (denom <= 0f) return 0f; // 妨害も対妨害も無ければ実効ゼロ
            return Mathf.Clamp01(jamming / denom);
        }

        /// <summary>
        /// 欺瞞妨害 0..1：偽目標 falseTargets × 信号模倣 signalMimicry × deceptionScale。
        /// 多数のデコイを撒き本物そっくりの信号を真似るほど敵を欺ける。
        /// </summary>
        public static float DeceptionJamming(float falseTargets, float signalMimicry, ElectronicCountermeasuresParams p)
        {
            float targets = Mathf.Clamp01(falseTargets);
            float mimicry = Mathf.Clamp01(signalMimicry);
            return Mathf.Clamp01(targets * mimicry * p.deceptionScale);
        }

        public static float DeceptionJamming(float falseTargets, float signalMimicry)
            => DeceptionJamming(falseTargets, signalMimicry, ElectronicCountermeasuresParams.Default);

        /// <summary>
        /// 周波数回避 0..1：ホッピング速度 hoppingRate と敵の追従速度 enemyTrackingSpeed の綱引き。
        /// hopping / (hopping + tracking)。素早く周波数を飛び移るほど敵の追従を振り切れる。
        /// 敵追従ゼロなら 1（完全回避）、両者ゼロなら 0。Params 非依存（純粋な比）。
        /// </summary>
        public static float FrequencyHopEvasion(float hoppingRate, float enemyTrackingSpeed)
        {
            float hopping = Mathf.Clamp01(hoppingRate);
            float tracking = Mathf.Clamp01(enemyTrackingSpeed);
            float denom = hopping + tracking;
            if (denom <= 0f) return 0f; // どちらも動かなければ回避効果なし
            return Mathf.Clamp01(hopping / denom);
        }

        /// <summary>
        /// 電子的優越 −1..1：自軍の妨害実効 jammingEffectiveness × 周波数回避 frequencyHopEvasion から
        /// 敵の対妨害 counterJamming を差し引いた電子戦の優劣。正＝電子的に優勢／負＝劣勢／0＝拮抗。
        /// (jammingEffectiveness × frequencyHopEvasion) − counterJamming を −1..1 へクランプ。Params 非依存。
        /// </summary>
        public static float ElectronicSuperiority(float jammingEffectiveness, float counterJamming, float frequencyHopEvasion)
        {
            float effect = Mathf.Clamp01(jammingEffectiveness);
            float counter = Mathf.Clamp01(counterJamming);
            float evasion = Mathf.Clamp01(frequencyHopEvasion);
            return Mathf.Clamp(effect * evasion - counter, -1f, 1f);
        }

        /// <summary>
        /// いたちごっこのエスカレーション 0..1：自軍 ECM 投資 ecmInvestment と敵 ECCM 投資 eccmInvestment の
        /// 双方が高いほど妨害⇄対妨害の応酬が激化する＝両者の幾何平均（どちらかが低いと螺旋は鈍る）に感度を掛ける。
        /// sqrt(ecm × eccm) × escalationSensitivity。
        /// </summary>
        public static float EscalationSpiral(float ecmInvestment, float eccmInvestment, ElectronicCountermeasuresParams p)
        {
            float ecm = Mathf.Clamp01(ecmInvestment);
            float eccm = Mathf.Clamp01(eccmInvestment);
            float spiral = Mathf.Sqrt(ecm * eccm) * p.escalationSensitivity;
            return Mathf.Clamp01(spiral);
        }

        public static float EscalationSpiral(float ecmInvestment, float eccmInvestment)
            => EscalationSpiral(ecmInvestment, eccmInvestment, ElectronicCountermeasuresParams.Default);

        /// <summary>
        /// 電子的に優越か：電子的優越 electronicSuperiority が threshold 以上なら優越（true）。
        /// </summary>
        public static bool IsElectronicallyDominant(float electronicSuperiority, float threshold)
        {
            return Mathf.Clamp(electronicSuperiority, -1f, 1f) >= Mathf.Clamp(threshold, -1f, 1f);
        }

        /// <summary>既定閾値（<see cref="ElectronicCountermeasuresParams.dominanceThreshold"/>）での電子的優越判定。</summary>
        public static bool IsElectronicallyDominant(float electronicSuperiority)
            => IsElectronicallyDominant(electronicSuperiority, ElectronicCountermeasuresParams.Default.dominanceThreshold);
    }
}
