using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文化・宗教・イデオロギーの薄いオーケストレータ（合成層・純ロジック test-first）。
    /// 勢力レベルの <see cref="CultureState"/>（結束/信仰/真摯さ）を、既存の未配線 Core
    /// （<see cref="CultureRules"/>＝同化・ナショナリズム、<see cref="CivicFaithRules"/>＝市民宗教）へ橋渡しして
    /// 年次収束させ、安定度・占領同化の背景modifierを返す唯一の窓口。数式は委譲先へ委ね二重実装しない。
    /// 同名の <see cref="CultureTickRules"/>（#194・惑星単位の民族 Tick）とは別物＝こちらは勢力全体の薄い合成。
    /// 決定論・bounded・null安全。基準値は非破壊（read-only で利用、状態は <see cref="CultureState"/> のみ更新）。
    /// </summary>
    public static class CultureSynthesisTickRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---
        /// <summary>結束が均衡へ寄る速さ（/年）。</summary>
        public const float CohesionSpeed = 0.06f;
        /// <summary>結束が安定度へ与える加点の振れ幅（±この値＝結束1.0で+、0.0で−）。bounded。</summary>
        public const float StabilityModifierScale = 8f;
        /// <summary>占領同化速度の基準（文化結束が高いほど速く同化させる）。</summary>
        public const float AssimilationBaseRate = 0.04f;

        /// <summary>
        /// 勢力の文化・市民宗教を1年ぶん進める（収束更新）。
        /// 結束は「正統性×包摂」の目標へ <see cref="CohesionSpeed"/> で寄り、信仰は <see cref="CivicFaithRules"/> で
        /// 包摂＝国策の推進力として製造され、真摯さは <see cref="CivicFaithRules.RitualizationDrift"/> で形骸化する。
        /// 数式は委譲先のみ＝ここでは合成と収束の配線に徹する。
        /// </summary>
        /// <param name="s">対象の文化状態。null なら何もしない。</param>
        /// <param name="legitimacy">勢力の正統性(0..1)＝結束の目標を引き上げる。</param>
        /// <param name="inclusiveness">統治の包摂度(0..1)＝収奪0↔包摂1。結束の目標と市民宗教の推進力。</param>
        /// <param name="dt">経過年（通常1）。0以下なら何もしない。</param>
        public static void TickYear(CultureState s, float legitimacy, float inclusiveness, float dt)
        {
            if (s == null || dt <= 0f) return;

            float leg = Mathf.Clamp01(legitimacy);
            float inc = Mathf.Clamp01(inclusiveness);

            // 結束の目標＝正統性×包摂（どちらも要る＝積）。基準（cohesion）は MoveTowards で収束＝非破壊的に更新。
            float cohesionTarget = leg * inc;
            s.cohesion = Mathf.Clamp01(Mathf.MoveTowards(s.cohesion, cohesionTarget, CohesionSpeed * dt));

            // 市民宗教＝包摂を国策の推進力として信奉を製造（CivicFaithRules へ委譲・天井あり）。
            s.faith = CivicFaithRules.ManufacturedFaithTick(s.faith, inc, dt);

            // 真摯さは儀礼活力（＝信奉を外形の活力とみなす）で形骸化（CivicFaithRules へ委譲）。
            s.sincerity = CivicFaithRules.RitualizationDrift(s.sincerity, s.faith, dt);
        }

        /// <summary>
        /// 文化結束が安定度へ与える背景加点（±・bounded）。read-only（状態を変更しない）。
        /// 結束が高い（1.0）と +<see cref="StabilityModifierScale"/>、低い（0.0）と −<see cref="StabilityModifierScale"/>、
        /// 中庸（0.5）で 0＝バラバラな社会は安定を蝕み、一体な社会は支える。null なら 0（modifier 無効＝未配線を明示）。
        /// </summary>
        /// <param name="s">対象の文化状態。null なら 0。</param>
        public static float StabilityModifier(CultureState s)
        {
            if (s == null) return 0f;
            // 政治的結束(信奉×真摯さ＝形骸化を割り引いた実効結束)を CivicFaithRules へ委譲し、文化結束と平均する。
            float civicCohesion = CivicFaithRules.CivicCohesion(s.faith, s.sincerity);
            float effective = 0.5f * (Mathf.Clamp01(s.cohesion) + civicCohesion);
            // 0..1 を −1..+1 へ写して振れ幅を掛ける＝中庸で 0・両端で ±Scale。
            return (effective * 2f - 1f) * StabilityModifierScale;
        }

        /// <summary>
        /// 占領地の同化速度(/年・0..)。read-only。文化結束が強いほど被支配民を速く同化させる（強い文化は同化を牽引）。
        /// 統合が進む(integration 高)ほど同化の余地は小さくなる（残りの未統合ぶんに比例）。
        /// 速度の単位係数は <see cref="CultureRules"/> 系の同化速度（<see cref="CultureParams.assimilationSpeed"/>）と整合させる。
        /// </summary>
        /// <param name="s">対象の文化状態。null なら 0（同化なし）。</param>
        /// <param name="integration">占領統合度(0=占領直後 .. 1=完全統合)。残りの未統合ぶんに比例して同化が進む。</param>
        public static float AssimilationRate(CultureState s, float integration)
        {
            if (s == null) return 0f;
            float cohesion = Mathf.Clamp01(s.cohesion);
            float headroom = 1f - Mathf.Clamp01(integration); // 未統合ぶん＝同化の余地
            // 単位速度は CultureRules 系の同化速度（CultureParams.Default.assimilationSpeed）に整合させ、
            // 結束が強い文化ほど速く・未統合ぶんに比例して同化させる（基準 state は非破壊）。
            float unitSpeed = CultureParams.Default.assimilationSpeed;
            float rate = unitSpeed * cohesion * headroom;
            return Mathf.Max(0f, rate);
        }
    }
}
