using UnityEngine;

namespace Ginei
{
    /// <summary>創造的破壊の調整係数。ctor で全てクランプ。</summary>
    public readonly struct CreativeDestructionParams
    {
        /// <summary>破壊力の感度（革新×陳腐化に乗算）。</summary>
        public readonly float destructionScale;
        /// <summary>創造（新産業の成長）の感度（革新×吸収能力に乗算）。</summary>
        public readonly float creationScale;
        /// <summary>置換ショックの感度（破壊力×(1−労働移動性)に乗算）。</summary>
        public readonly float shockScale;
        /// <summary>適応の遅れの感度（ショック×(1−再訓練)に乗算＝摩擦の長さ）。</summary>
        public readonly float lagScale;
        /// <summary>シュンペーターのレント感度（革新×(1−模倣の速さ)に乗算＝束の間の超過利潤）。</summary>
        public readonly float rentScale;
        /// <summary>ディスラプション判定の既定しきい値（破壊力がこれを超えると破壊的革新）。</summary>
        public readonly float disruptionThreshold;

        public CreativeDestructionParams(float destructionScale, float creationScale, float shockScale,
                                         float lagScale, float rentScale, float disruptionThreshold)
        {
            this.destructionScale = Mathf.Max(0f, destructionScale);
            this.creationScale = Mathf.Max(0f, creationScale);
            this.shockScale = Mathf.Max(0f, shockScale);
            this.lagScale = Mathf.Max(0f, lagScale);
            this.rentScale = Mathf.Max(0f, rentScale);
            this.disruptionThreshold = Mathf.Clamp01(disruptionThreshold);
        }

        /// <summary>
        /// 既定＝破壊感度1・創造感度1・ショック感度1・遅れ感度1・レント感度0.5・ディスラプション閾値0.5。
        /// </summary>
        public static CreativeDestructionParams Default => new CreativeDestructionParams(1f, 1f, 1f, 1f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 創造的破壊＝新陳代謝の破壊側の純ロジック（SCHU-1 #1581・シュンペーター）。
    /// 資本主義の本質は「絶え間ない革新が古いものを内側から破壊し新しいもので置き換える嵐」＝
    /// 成長は創造であると同時に破壊。革新の大きさ×旧産業の陳腐化度が破壊力を生み
    /// （<see cref="DestructionForce"/>）、新興が旧産業のシェアを時間で食い（<see cref="IncumbentDecayTick"/>）、
    /// 一方で革新は吸収能力に受け止められて新産業の成長を生む（<see cref="CreationGain"/>）。
    /// 嵐の収支＝創造−破壊が純成長（<see cref="NetGrowth"/>）。淘汰された雇用は置換ショックとして社会を揺らし
    /// （<see cref="DisplacementShock"/>＝労働移動性が高いほど和らぐ）、再訓練が追いつくまで摩擦が遅れ
    /// （<see cref="AdaptationLag"/>）、革新者は模倣される前に束の間の超過利潤を得る
    /// （<see cref="SchumpeterianRent"/>）。破壊力が閾値を超えればディスラプション（<see cref="IsDisruption"/>）。
    /// 分担：`InnovationDiffusionRules`＝技術が国から国へ漏れる伝播／`ResearchRules`＝研究の進捗／
    /// **本クラス＝新陳代謝の破壊側**（旧市場の淘汰と置換ショック）。置換ショックの行き先＝
    /// `CompetitiveDemocracyRules`（同EPIC SCHU・社会への波及）。乱数なし決定論・全入力クランプ・
    /// 基準値非破壊（実効値パターン）。調整値は <see cref="CreativeDestructionParams"/>（既定
    /// <see cref="CreativeDestructionParams.Default"/>）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CreativeDestructionRules
    {
        /// <summary>破壊力（既定 Params）。</summary>
        public static float DestructionForce(float innovationMagnitude, float incumbentObsolescence)
            => DestructionForce(innovationMagnitude, incumbentObsolescence, CreativeDestructionParams.Default);

        /// <summary>
        /// 破壊力（0..1）＝革新の大きさ×旧産業の陳腐化度×感度。革新が小さいか旧産業が陳腐化していなければ
        /// 破壊は起きない（片方0なら0＝陳腐な産業ほど新技術に淘汰される）。
        /// </summary>
        public static float DestructionForce(float innovationMagnitude, float incumbentObsolescence, CreativeDestructionParams p)
        {
            float innov = Mathf.Clamp01(innovationMagnitude);
            float obs = Mathf.Clamp01(incumbentObsolescence);
            return Mathf.Clamp01(innov * obs * p.destructionScale);
        }

        /// <summary>
        /// 旧産業シェアの1tick更新＝新興が旧産業を時間で食う（古いものが内側から萎縮する）。
        /// 破壊力が大きいほど速くシェアを失い0へ向かう。新しいシェアを返す（引数非破壊）。
        /// </summary>
        public static float IncumbentDecayTick(float incumbentShare, float destructionForce, float dt)
        {
            float s = Mathf.Clamp01(incumbentShare);
            float force = Mathf.Clamp01(destructionForce);
            float loss = s * force * Mathf.Max(0f, dt); // シェアに比例＝大きい旧産業ほど失う量も大きい
            return Mathf.Clamp01(s - loss);
        }

        /// <summary>創造の成長（既定 Params）。</summary>
        public static float CreationGain(float innovationMagnitude, float absorptiveCapacity)
            => CreationGain(innovationMagnitude, absorptiveCapacity, CreativeDestructionParams.Default);

        /// <summary>
        /// 創造の成長（0..1）＝革新の大きさ×吸収能力×感度。革新があっても社会が受け止める力（資本・人材・
        /// 制度＝absorptiveCapacity）が無ければ新産業は育たない（片方0なら0＝嵐は受け止める器を要る）。
        /// </summary>
        public static float CreationGain(float innovationMagnitude, float absorptiveCapacity, CreativeDestructionParams p)
        {
            float innov = Mathf.Clamp01(innovationMagnitude);
            float cap = Mathf.Clamp01(absorptiveCapacity);
            return Mathf.Clamp01(innov * cap * p.creationScale);
        }

        /// <summary>
        /// 純成長＝創造−破壊（嵐の収支）。創造が破壊を上回れば正の成長、下回れば負（破壊が創造を食う）。
        /// 創造と破壊はどちらも正なので結果は −1..1 にクランプ（創造的破壊は差し引きで効く）。
        /// </summary>
        public static float NetGrowth(float creationGain, float destructionLoss)
        {
            float gain = Mathf.Clamp01(creationGain);
            float loss = Mathf.Clamp01(destructionLoss);
            return Mathf.Clamp(gain - loss, -1f, 1f);
        }

        /// <summary>置換ショック（既定 Params）。</summary>
        public static float DisplacementShock(float destructionForce, float laborMobility)
            => DisplacementShock(destructionForce, laborMobility, CreativeDestructionParams.Default);

        /// <summary>
        /// 置換ショック（0..1）＝淘汰された雇用が社会へ与える衝撃。破壊力×(1−労働移動性)×感度＝
        /// 労働移動性（転職・再配置のしやすさ）が高いほど和らぐ。完全に流動的（mobility=1）なら衝撃0、
        /// 硬直的（mobility=0）なら破壊力がそのまま社会を揺らす。`CompetitiveDemocracyRules` への入力。
        /// </summary>
        public static float DisplacementShock(float destructionForce, float laborMobility, CreativeDestructionParams p)
        {
            float force = Mathf.Clamp01(destructionForce);
            float mobility = Mathf.Clamp01(laborMobility);
            return Mathf.Clamp01(force * (1f - mobility) * p.shockScale);
        }

        /// <summary>適応の遅れ（既定 Params）。</summary>
        public static float AdaptationLag(float displacementShock, float retraining)
            => AdaptationLag(displacementShock, retraining, CreativeDestructionParams.Default);

        /// <summary>
        /// 適応の遅れ（0..1）＝再訓練が追いつくまでの摩擦の長さ。ショック×(1−再訓練)×感度＝
        /// 再訓練（職業訓練・教育投資）が充実するほど遅れは縮む。再訓練が万全（retraining=1）なら遅れ0、
        /// 皆無（retraining=0）ならショックぶんの摩擦がそのまま残る（置換が片付くまで社会が痛む期間）。
        /// </summary>
        public static float AdaptationLag(float displacementShock, float retraining, CreativeDestructionParams p)
        {
            float shock = Mathf.Clamp01(displacementShock);
            float retrain = Mathf.Clamp01(retraining);
            return Mathf.Clamp01(shock * (1f - retrain) * p.lagScale);
        }

        /// <summary>シュンペーターのレント（既定 Params）。</summary>
        public static float SchumpeterianRent(float innovationMagnitude, float imitationDelay)
            => SchumpeterianRent(innovationMagnitude, imitationDelay, CreativeDestructionParams.Default);

        /// <summary>
        /// シュンペーターのレント（0..1）＝模倣される前の革新者の一時的超過利潤（独占の束の間）。
        /// 革新の大きさ×模倣の遅さ×感度＝模倣が速い（imitationDelay=0）と利潤は即座に競争で消え、
        /// 模倣が遅い（imitationDelay=1）ほど革新者は長く超過利潤を享受できる（一時的独占の果実）。
        /// </summary>
        public static float SchumpeterianRent(float innovationMagnitude, float imitationDelay, CreativeDestructionParams p)
        {
            float innov = Mathf.Clamp01(innovationMagnitude);
            float delay = Mathf.Clamp01(imitationDelay);
            return Mathf.Clamp01(innov * delay * p.rentScale);
        }

        /// <summary>ディスラプション判定（既定 Params のしきい値）。</summary>
        public static bool IsDisruption(float destructionForce)
            => IsDisruption(destructionForce, CreativeDestructionParams.Default.disruptionThreshold);

        /// <summary>
        /// 破壊的革新（ディスラプション）の判定。破壊力が threshold を超えたら true＝旧市場を根こそぎ
        /// 置き換える嵐。漸進的改良（破壊力小）と非連続な破壊（破壊力大）を分ける。
        /// </summary>
        public static bool IsDisruption(float destructionForce, float threshold)
            => Mathf.Clamp01(destructionForce) > Mathf.Clamp01(threshold);
    }
}
