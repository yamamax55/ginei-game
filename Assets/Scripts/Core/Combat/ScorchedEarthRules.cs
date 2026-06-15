using UnityEngine;

namespace Ginei
{
    /// <summary>焦土戦術の調整係数。</summary>
    public readonly struct ScorchedEarthParams
    {
        /// <summary>焼却の徹底度（焼いた範囲のうち実際に敵へ渡らなくなる割合。隠匿・燃え残りで漏れる）。</summary>
        public readonly float denialEfficiency;
        /// <summary>侵攻減速の最大幅（全面焼却のとき。現地調達を断たれた敵は補給線の長さに縛られる）。</summary>
        public readonly float slowdownScale;
        /// <summary>自国民の恨みの最大量（住民ごと焼いた全面焼却のとき）。</summary>
        public readonly float resentmentScale;
        /// <summary>住民退避による恨みの軽減率（全力退避でも財産は焼ける＝恨みゼロにはならない）。</summary>
        public readonly float evacuationMitigation;
        /// <summary>焼却範囲→荒廃への変換係数（生じる荒廃＝範囲×これ）。</summary>
        public readonly float devastationScale;
        /// <summary>焼いた資産1あたりの復興費の比率（奪還後に自分で払う）。</summary>
        public readonly float reconstructionCostRatio;
        /// <summary>敵の資産活用が飽和する失陥期間（これ以上長く奪われると拒否価値が満額になる）。</summary>
        public readonly float recaptureHorizon;

        public ScorchedEarthParams(float denialEfficiency, float slowdownScale, float resentmentScale,
            float evacuationMitigation, float devastationScale, float reconstructionCostRatio, float recaptureHorizon)
        {
            this.denialEfficiency = Mathf.Clamp01(denialEfficiency);
            this.slowdownScale = Mathf.Clamp01(slowdownScale);
            this.resentmentScale = Mathf.Max(0f, resentmentScale);
            this.evacuationMitigation = Mathf.Clamp01(evacuationMitigation);
            this.devastationScale = Mathf.Clamp01(devastationScale);
            this.reconstructionCostRatio = Mathf.Max(0f, reconstructionCostRatio);
            this.recaptureHorizon = Mathf.Max(0f, recaptureHorizon);
        }

        /// <summary>既定＝徹底度0.8・減速幅0.5・恨み0.6・退避軽減0.7・荒廃変換1.0・復興費比0.6・飽和期間100。</summary>
        public static ScorchedEarthParams Default => new ScorchedEarthParams(0.8f, 0.5f, 0.6f, 0.7f, 1f, 0.6f, 100f);
    }

    /// <summary>
    /// 焦土戦術の純ロジック（破壊側）。撤退時に自領の資産を焼いて敵の利得を消す＝敵の侵攻は鈍るが、
    /// ①自国民の恨み（住民を退避させてから焼けば軽い・住民ごと焼けば深い）、②荒廃＝奪還後に自分で払う
    /// 復興費が残る。「焦土は時間を買って未来を売る」＝すぐ取り返すなら焼くだけ損・長期失陥なら焼き得。
    /// 生じた荒廃の再建は <see cref="ReconstructionRules"/>（再建側）が対になって扱う
    /// （<see cref="DevastationCreated(float)"/> がその devastation 入力）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ScorchedEarthRules
    {
        /// <summary>
        /// 敵に渡らなくなる資産＝資産価値×焼却範囲 burnScope(0..1)×徹底度。
        /// 「全部焼く」と命じても拒否できるのは徹底度の分だけ（隠匿・燃え残りは敵が拾う）。
        /// </summary>
        public static float DeniedValue(float assetValue, float burnScope, ScorchedEarthParams p)
        {
            return Mathf.Max(0f, assetValue) * Mathf.Clamp01(burnScope) * p.denialEfficiency;
        }

        public static float DeniedValue(float assetValue, float burnScope)
            => DeniedValue(assetValue, burnScope, ScorchedEarthParams.Default);

        /// <summary>
        /// 侵攻速度の低下量（0..slowdownScale）＝焼却範囲に比例。現地調達を断たれた敵の
        /// 侵攻速度に 1−これ を掛ける（焼くほど敵は補給線の長さに縛られて鈍る）。
        /// </summary>
        public static float InvaderSlowdown(float burnScope, ScorchedEarthParams p)
        {
            return Mathf.Clamp01(burnScope) * p.slowdownScale;
        }

        public static float InvaderSlowdown(float burnScope) => InvaderSlowdown(burnScope, ScorchedEarthParams.Default);

        /// <summary>
        /// 自国民の恨み（0..resentmentScale）＝焼却範囲×（1−退避努力 evacuationEffort(0..1)×退避軽減）。
        /// 住民を退避させてから焼けば軽く、住民ごと焼けば深い。ただし全力退避でも財産は焼ける＝ゼロにはならない。
        /// </summary>
        public static float PopulationResentment(float burnScope, float evacuationEffort, ScorchedEarthParams p)
        {
            float mitigation = 1f - Mathf.Clamp01(evacuationEffort) * p.evacuationMitigation;
            return Mathf.Clamp01(burnScope) * p.resentmentScale * mitigation;
        }

        public static float PopulationResentment(float burnScope, float evacuationEffort)
            => PopulationResentment(burnScope, evacuationEffort, ScorchedEarthParams.Default);

        /// <summary>
        /// 生じる荒廃（0..1）＝焼却範囲×荒廃変換係数。<see cref="ReconstructionRules"/> の devastation 入力
        /// ＝奪還後に自分で払う復興費の種。焼いた者が後で再建費を払う。
        /// </summary>
        public static float DevastationCreated(float burnScope, ScorchedEarthParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(burnScope) * p.devastationScale);
        }

        public static float DevastationCreated(float burnScope) => DevastationCreated(burnScope, ScorchedEarthParams.Default);

        /// <summary>
        /// 焼く損益（資産価値の単位）＝拒否価値×時間係数−復興費。「焦土は時間を買って未来を売る」を式に出す：
        /// 利得＝敵に使わせない価値で、失陥期間 expectedRecaptureTime が飽和期間 recaptureHorizon に近づくほど満額。
        /// 損＝焼いた資産の復興費（奪還後に自分で払う固定費）。すぐ取り返すなら焼くだけ損・長期失陥なら焼き得。
        /// </summary>
        public static float NetValue(float assetValue, float burnScope, float expectedRecaptureTime, ScorchedEarthParams p)
        {
            if (assetValue <= 0f) return 0f;
            float timeFactor = p.recaptureHorizon <= 0f
                ? 1f
                : Mathf.Clamp01(Mathf.Max(0f, expectedRecaptureTime) / p.recaptureHorizon);
            float gain = DeniedValue(assetValue, burnScope, p) * timeFactor;
            float loss = assetValue * Mathf.Clamp01(burnScope) * p.reconstructionCostRatio;
            return gain - loss;
        }

        public static float NetValue(float assetValue, float burnScope, float expectedRecaptureTime)
            => NetValue(assetValue, burnScope, expectedRecaptureTime, ScorchedEarthParams.Default);

        /// <summary>
        /// 損益分岐の失陥期間＝これより長く奪われる見込みなら焼き得（NetValue&gt;0）。
        /// 飽和期間×復興費比÷徹底度。徹底度0、または飽和してもなお復興費が上回るなら無限大＝焼いても元が取れない。
        /// </summary>
        public static float BreakEvenTime(ScorchedEarthParams p)
        {
            if (p.denialEfficiency <= 0f || p.reconstructionCostRatio > p.denialEfficiency)
                return float.PositiveInfinity;
            return p.recaptureHorizon * p.reconstructionCostRatio / p.denialEfficiency;
        }

        public static float BreakEvenTime() => BreakEvenTime(ScorchedEarthParams.Default);
    }
}
