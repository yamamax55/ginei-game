using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍事司法＝懲罰体制（STSH-2 #2357）の純ロジック。軍紀を恐怖（外部コスト）で支える側面を解決する。
    /// 苛烈さ×執行で脱走を抑止し（恐怖の抑止力）、執行×適用範囲で部隊結束を保ち、
    /// 苛烈な公開処刑は反感を生む（恐怖統治の代償）。苛烈さは「抑止が足りない」と「反感が募る」の
    /// 谷＝最適点を持つ。乱数なし・決定論・基準非破壊（実効値パターン）・null/clamp 安全。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    ///
    /// // 区別：本ルールは <see cref="NormalizationRules"/>（規律権力＝規範の内面化で自発的に規律を保つ）とは別の機構。
    ///   こちらは「外部コスト抑止＝懲罰の恐怖」で規律を強いる（罰せられるから従う）。
    ///   NormalizationRules は「規範の内面化＝従順な身体」で規律が立つ（信じるから従う）。
    ///   恐怖は速効だが反感（<see cref="ResentmentAccrual"/>）を生み、内面化は遅効だが反感を伴わない。
    /// 分担：<see cref="SecurityRules.RepressionSupportPenalty"/>＝政治的弾圧の支持低下（民間向け）、
    ///   <see cref="LoyaltyRules"/>＝忠誠の総合解決。本ルールは軍内の懲罰体制に特化する。
    /// </summary>
    public static class MilitaryJusticeRules
    {
        // --- 調整値（const に集約・マジックナンバー禁止） ---
        /// <summary>懲罰体制が無い（null）ときの中立的な脱走リスク（抑止も反感も無い既定値）。</summary>
        public const float NeutralDeserterRisk = 0.5f;
        /// <summary>脱走リスクが抑止しきれず残る最小値（恐怖でも完全には消せない＝0 にしない）。</summary>
        public const float MinDeserterRisk = 0.02f;
        /// <summary>結束ボーナスの最大上乗せ（執行×適用範囲が完全なときの倍率上限＝1+この値）。</summary>
        public const float MaxCohesionBonus = 0.15f;
        /// <summary>反感蓄積の最大値（苛烈な公開処刑が完全なときの反感）。</summary>
        public const float MaxResentment = 1.0f;
        /// <summary>最適苛烈さの粗探索の刻み数（0..1 をこの分割で走査する）。</summary>
        public const int OptimalSearchSteps = 100;

        /// <summary>
        /// 脱走リスク(0..1)：苛烈さ×執行（＝確実に重く罰せられる恐怖）が高いほど減る（抑止力）。
        /// 中立 <see cref="NeutralDeserterRisk"/> から抑止ぶんを引き、<see cref="MinDeserterRisk"/> を下限とする。
        /// 体制が無い（null）なら中立値を返す。
        /// </summary>
        public static float DeserterRisk(PunishmentRegime r)
        {
            if (r == null) return NeutralDeserterRisk;
            float deterrence = Mathf.Clamp01(r.severity) * Mathf.Clamp01(r.enforcement);
            float risk = NeutralDeserterRisk * (1f - deterrence);
            return Mathf.Clamp(risk, MinDeserterRisk, 1f);
        }

        /// <summary>
        /// 結束ボーナス（倍率・FleetMorale 用）：執行×適用範囲（＝末端まで規律が確実に及ぶ）が高いほど
        /// 部隊が崩れず線を保つ。1.0（無効果）〜1+<see cref="MaxCohesionBonus"/>。null なら 1.0。
        /// </summary>
        public static float CohesionBonus(PunishmentRegime r)
        {
            if (r == null) return 1f;
            float discipline = Mathf.Clamp01(r.enforcement) * Mathf.Clamp01(r.coverageScope);
            return 1f + MaxCohesionBonus * discipline;
        }

        /// <summary>
        /// 反感蓄積(0..1)：苛烈さ×公開性（＝見せしめの公開処刑）が高いほど反感が募る（恐怖統治の代償）。
        /// null なら 0（体制が無ければ反感も生まれない）。
        /// </summary>
        public static float ResentmentAccrual(PunishmentRegime r)
        {
            if (r == null) return 0f;
            float harshPublic = Mathf.Clamp01(r.severity) * Mathf.Clamp01(r.publicVisibility);
            return Mathf.Clamp01(MaxResentment * harshPublic);
        }

        /// <summary>
        /// 最適苛烈さ(0..1)：脱走リスクのコストと反感のコストの和を最小化する苛烈さの均衡点。
        /// 苛烈さを上げると脱走リスクは下がる（執行 enforcement に比例した抑止）が、反感は上がる
        /// （公開性 publicVisibility に比例）。両コストの谷を 0..1 の粗探索で返す。
        /// 執行が高いほど少ない苛烈さで抑止でき、公開性が高いほど苛烈さを抑えるのが得（反感を避ける）。
        /// </summary>
        public static float OptimalSeverity(float enforcement, float publicVisibility)
        {
            float enf = Mathf.Clamp01(enforcement);
            float pub = Mathf.Clamp01(publicVisibility);

            float bestSeverity = 0f;
            float bestCost = float.MaxValue;
            for (int i = 0; i <= OptimalSearchSteps; i++)
            {
                float s = i / (float)OptimalSearchSteps;
                float cost = CombinedCost(s, enf, pub);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSeverity = s;
                }
            }
            return bestSeverity;
        }

        /// <summary>適用範囲は反感に効かないため公開性のみで最適化する簡易オーバーロード（適用範囲不問）。</summary>
        public static float OptimalSeverity(float enforcement)
            => OptimalSeverity(enforcement, 1f);

        /// <summary>
        /// 苛烈さ s（0..1）における結合コスト＝脱走リスクコスト＋反感コスト。
        /// <see cref="OptimalSeverity"/> が最小化する目的関数（テストで谷を検証する窓口）。
        /// 脱走コストは苛烈さに対し逓減（最初の罰がいちばん効き、追加の苛烈さは抑止に効きにくい＝√で凹）、
        /// 反感コストは苛烈さに対し逓増（公開の重罰は加速度的に恨みを買う＝二乗で凸）。
        /// 凹＋凸ゆえ和は内点で谷を持つ＝過少（抑止不足）と過剰（反感過多）の中間が最適になる。
        /// </summary>
        public static float CombinedCost(float severity, float enforcement, float publicVisibility)
        {
            float s = Mathf.Clamp01(severity);
            float enf = Mathf.Clamp01(enforcement);
            float pub = Mathf.Clamp01(publicVisibility);
            // 脱走コスト：苛烈さ×執行で抑止が立つが逓減（√s で凹＝最初の一罰が最も効く）。
            float deterrence = Mathf.Sqrt(s) * enf;
            float deserterCost = NeutralDeserterRisk * (1f - deterrence);
            // 反感コスト：苛烈さ×公開性で逓増（s² で凸＝重罰の見せしめは加速度的に恨みを買う）。
            float resentmentCost = (s * s) * pub;
            return deserterCost + resentmentCost;
        }
    }
}
