using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 地方自治体のロジック（#109 内政・中央集権↔地方分権・純ロジック・唯一の窓口）。<b>自治度</b>のトレードオフを解く：
    /// 高自治＝現地に即した統治で<b>地方の安定↑</b>（ただし行政能力に依存）／<b>中央へ送る税収↓</b>（現地に残す）／<b>分離独立リスク↑</b>（低統合だと離れる）。
    /// 中央集権＝税収/統制を中央へ集めるが応答性が落ちる。封建制（<see cref="FeudalRules"/> #168）の近代版・別軸。
    /// 地方安定は <see cref="GovernanceRules"/>、税配分は <see cref="FiscalRules"/>(#163)、分離は <see cref="CultureRules"/>(#194) へ接続する想定。test-first。
    /// </summary>
    public static class LocalGovernmentRules
    {
        /// <summary>有能な自治がもたらす地方安定度ボーナスの上限。</summary>
        public const float MaxLocalStabilityBonus = 15f;

        /// <summary>完全自治でも中央へ残る最低税収割合（国家の体は保つ）。</summary>
        public const float MinCentralShare = 0.4f;

        /// <summary>中央集権度（1−自治度）。</summary>
        public static float Centralization(float autonomy) => 1f - Mathf.Clamp01(autonomy);

        /// <summary>
        /// 地方統治の安定度ボーナス＝行政能力×自治度（現地に即した応答的統治）。自治が高くても無能なら効かない（能力に律速）。
        /// </summary>
        public static float LocalStabilityBonus(float autonomy, float competence)
            => MaxLocalStabilityBonus * Mathf.Clamp01(competence) * Mathf.Clamp01(autonomy);

        /// <summary>中央へ送られる税収割合（自治0＝全額中央 .. 自治1＝<see cref="MinCentralShare"/>。残りは地方に留保）。</summary>
        public static float CentralRevenueShare(float autonomy)
            => Mathf.Lerp(1f, MinCentralShare, Mathf.Clamp01(autonomy));

        /// <summary>地方に留保される税収割合（1−中央割合）。</summary>
        public static float LocalRevenueShare(float autonomy) => 1f - CentralRevenueShare(autonomy);

        /// <summary>分離独立リスク（0..1）＝高自治×低統合（中央から離れ独自に動く）。<see cref="CultureRules"/> 分離と連携。</summary>
        public static float SeparatismRisk(float autonomy, float integration)
            => Mathf.Clamp01(Mathf.Clamp01(autonomy) * (1f - Mathf.Clamp01(integration)));

        /// <summary>政体（思想）に応じた既定の自治度（専制/中央集権は低く・民主/連邦は高い）。</summary>
        public static float DefaultAutonomy(string ideology)
        {
            if (string.IsNullOrEmpty(ideology)) return 0.5f;
            if (ideology.Contains("専制") || ideology.Contains("集権")) return 0.2f;
            if (ideology.Contains("民主") || ideology.Contains("連邦")) return 0.6f;
            return 0.5f;
        }
    }
}
