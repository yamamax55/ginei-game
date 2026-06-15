using UnityEngine;

namespace Ginei
{
    /// <summary>技術水準→盤面効果の換算係数。係数（1段あたりの伸び）と上限（飽和）を分野ごとに持つ。</summary>
    public readonly struct TechEffectParams
    {
        /// <summary>技術1段あたりの戦力倍率の伸び（線形係数・非負）。</summary>
        public readonly float combatPerLevel;
        /// <summary>戦力倍率の上限（飽和＝技術だけで無限に強くならない・1以上）。</summary>
        public readonly float combatCap;
        /// <summary>技術1段あたりの建艦速度倍率の伸び（線形係数・非負）。</summary>
        public readonly float buildPerLevel;
        /// <summary>建艦速度倍率の上限（飽和・1以上）。</summary>
        public readonly float buildCap;
        /// <summary>技術1段あたりの生産性倍率の伸び（線形係数・非負）。</summary>
        public readonly float productivityPerLevel;
        /// <summary>生産性倍率の上限（飽和・1以上）。</summary>
        public readonly float productivityCap;

        public TechEffectParams(
            float combatPerLevel, float combatCap,
            float buildPerLevel, float buildCap,
            float productivityPerLevel, float productivityCap)
        {
            this.combatPerLevel = Mathf.Max(0f, combatPerLevel);
            this.combatCap = Mathf.Max(1f, combatCap);
            this.buildPerLevel = Mathf.Max(0f, buildPerLevel);
            this.buildCap = Mathf.Max(1f, buildCap);
            this.productivityPerLevel = Mathf.Max(0f, productivityPerLevel);
            this.productivityCap = Mathf.Max(1f, productivityCap);
        }

        /// <summary>
        /// 既定＝戦力 +5%/段（上限2.0倍）・建艦速度 +4%/段（上限1.8倍）・生産性 +3%/段（上限1.6倍）。
        /// いずれも飽和で頭打ち＝技術だけで無限に強くならない（バランス規律）。
        /// </summary>
        public static TechEffectParams Default => new TechEffectParams(
            0.05f, 2.0f,
            0.04f, 1.8f,
            0.03f, 1.6f);
    }

    /// <summary>
    /// 技術水準（<see cref="ResearchState.techLevel"/>＝<see cref="ResearchProgramRules"/> が積み上げる集約段）を
    /// 盤面効果＝戦力／建艦速度／生産性の倍率へ変換する薄い橋渡し（純ロジック・唯一の窓口・test-first）。
    /// 「研究が進む（techLevel↑）→艦が強く・造船が速く・産業が効率的になる」を式で固定する。
    /// 倍率は 1 + techLevel×係数 の線形で、上限（飽和）でクランプ＝技術だけで無限に伸びない（バランス・タイクン回避）。
    /// techLevel=0 で全て1.0（後方互換＝技術未進展なら従来挙動）。負の techLevel は0扱い＝1.0以上を保証。
    /// 係数・上限は <see cref="TechEffectParams"/>。乱数なし・決定論。個体粒度へ降りない（集約のみ）。
    /// <see cref="TechTreeRules"/>（前提依存グラフ＝どの技術が解禁されるか）とは別系統＝こちらは集約段→倍率。
    /// 倍率の合成は呼び出し側が <see cref="ModifierStack"/>（<see cref="CombatModifiers"/>）で積む（ここは単一公式）。
    /// </summary>
    public static class TechEffectRules
    {
        /// <summary>
        /// 戦力倍率＝1 + techLevel×<see cref="TechEffectParams.combatPerLevel"/>（上限 <see cref="TechEffectParams.combatCap"/> で飽和）。
        /// techLevel=0 で1.0（後方互換）・負は0扱い＝1.0以上。技術が進むほど艦・部隊が強い（#106 の係数チャネルへ橋渡し）。
        /// </summary>
        public static float CombatStrengthFactor(float techLevel, TechEffectParams p)
        {
            float lv = Mathf.Max(0f, techLevel);
            return Mathf.Min(p.combatCap, 1f + lv * p.combatPerLevel);
        }

        public static float CombatStrengthFactor(float techLevel)
            => CombatStrengthFactor(techLevel, TechEffectParams.Default);

        /// <summary>
        /// 建艦速度倍率＝1 + techLevel×<see cref="TechEffectParams.buildPerLevel"/>（上限 <see cref="TechEffectParams.buildCap"/> で飽和）。
        /// techLevel=0 で1.0（後方互換）・負は0扱い＝1.0以上。技術が進むほど造船が速い（<c>ShipyardRules</c> の生産係数へ橋渡し）。
        /// </summary>
        public static float BuildSpeedFactor(float techLevel, TechEffectParams p)
        {
            float lv = Mathf.Max(0f, techLevel);
            return Mathf.Min(p.buildCap, 1f + lv * p.buildPerLevel);
        }

        public static float BuildSpeedFactor(float techLevel)
            => BuildSpeedFactor(techLevel, TechEffectParams.Default);

        /// <summary>
        /// 生産性倍率＝1 + techLevel×<see cref="TechEffectParams.productivityPerLevel"/>（上限 <see cref="TechEffectParams.productivityCap"/> で飽和）。
        /// techLevel=0 で1.0（後方互換）・負は0扱い＝1.0以上。技術が進むほど産業が効率的（#93 産出の係数へ橋渡し）。
        /// </summary>
        public static float ProductivityFactor(float techLevel, TechEffectParams p)
        {
            float lv = Mathf.Max(0f, techLevel);
            return Mathf.Min(p.productivityCap, 1f + lv * p.productivityPerLevel);
        }

        public static float ProductivityFactor(float techLevel)
            => ProductivityFactor(techLevel, TechEffectParams.Default);
    }
}
