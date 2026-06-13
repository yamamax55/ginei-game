using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍産複合体のロジック（MCN-4 #1389・CAP-3 #204・純ロジック・唯一の窓口）。アイゼンハワーの警告＝軍需産業・軍と政治が
    /// 癒着し<b>構造的な戦争バイアス</b>を生む。造船利権（省益 #158 `MinistryRules.SectionalismFriction`）が政治圧力となり、
    /// 補助金で建艦を加速し（歳出 #163・建艦 #884）、過剰建艦（過剰拡張 #1321）と調達腐敗を招き、軍縮＝平和が経済問題になる
    /// 倒錯（#204）を表現する。既存モジュールへ係数入力で接続（read-only/接続のみ）。マクロ近似（個社経営は持たない）。test-first。
    /// </summary>
    public static class MilitaryIndustrialRules
    {
        /// <summary>政治圧力が最大化する造船所数の基準（これ以上は頭打ち）。</summary>
        public const float ReferenceShipyards = 10f;

        /// <summary>政治圧力→歳出補助金の係数（建艦生産力に乗る上乗せ）。</summary>
        public const float DefaultSubsidySlope = 0.5f;

        /// <summary>政治圧力→調達コスト膨張の係数（キックバック・割高調達）。</summary>
        public const float DefaultCorruptionSlope = 0.3f;

        /// <summary>軍産複合化（複合体成立）とみなす政治圧力の閾値。</summary>
        public const float DefaultComplexThreshold = 0.6f;

        /// <summary>政治圧力→戦争バイアスの重み。</summary>
        public const float DefaultWarBiasWeight = 1f;

        // ===== 政治圧力（ロビー） =====

        /// <summary>
        /// 軍需ロビーの政治圧力（0..1）＝造船所数（軍需の規模）×省益の縦割り強度（既得権益の固さ）。造船所が増えるほど・
        /// 省益が強いほど圧力が高い。<see cref="MinistryRules.SectionalismFriction"/>（#158）を入力に使う想定。
        /// </summary>
        public static float LobbyingPressure(float shipyardCount, float sectionalismFriction)
        {
            float scale = Mathf.Clamp01(Mathf.Max(0f, shipyardCount) / ReferenceShipyards);
            return Mathf.Clamp01(Mathf.Clamp01(sectionalismFriction) * scale);
        }

        // ===== 軍事費の押し上げ・調達腐敗 =====

        /// <summary>生産補助金係数＝政治圧力×補助金係数（軍事費・建艦生産力を上乗せ＝歳出 #163/建艦 #884 へ）。</summary>
        public static float ProductionSubsidy(float pressure)
            => Mathf.Clamp01(pressure) * DefaultSubsidySlope;

        /// <summary>調達コスト膨張係数＝政治圧力×腐敗係数（割高調達・キックバックで調達費が膨らむ）。</summary>
        public static float CorruptionGain(float pressure)
            => Mathf.Clamp01(pressure) * DefaultCorruptionSlope;

        // ===== 過剰建艦 =====

        /// <summary>
        /// 過剰建艦比率（0以上）＝(配分戦力−戦略的最適量)/最適量（必要以上の建艦＝軍需利権が押し込む過剰）。
        /// <see cref="OverstretchRules"/>（#1321）の造船ルート入力。最適量0以下は0。
        /// </summary>
        public static float OverkillRisk(float allocatedFleetStrength, float strategicOptimum)
            => strategicOptimum <= 0f ? 0f : Mathf.Max(0f, allocatedFleetStrength - strategicOptimum) / strategicOptimum;

        // ===== 戦争バイアス・軍産複合化（#204） =====

        /// <summary>戦争バイアス（0..1）＝政治圧力×重み（開戦圧力・厭戦抑制・軍縮反対へ歪める力）。</summary>
        public static float WarBias(float pressure)
            => Mathf.Clamp01(Mathf.Clamp01(pressure) * DefaultWarBiasWeight);

        /// <summary>軍産複合化が成立したか＝政治圧力が閾値以上（経済・政治を支配する構造的戦争バイアス）。</summary>
        public static bool IsComplex(float pressure, float threshold)
            => pressure >= threshold;

        /// <summary>軍産複合化が成立したか（既定閾値）。</summary>
        public static bool IsComplex(float pressure) => IsComplex(pressure, DefaultComplexThreshold);

        /// <summary>
        /// 平和の経済ショック＝軍需依存度×軍縮率（軍縮・平和で軍需依存の経済が被るダメージ＝「平和が経済問題になる倒錯」#204）。
        /// 軍需に依存するほど・軍縮が深いほど痛い。
        /// </summary>
        public static float PeaceEconomicShock(float defenseRevenueShare, float disarmamentRatio)
            => Mathf.Clamp01(defenseRevenueShare) * Mathf.Clamp01(disarmamentRatio);
    }
}
