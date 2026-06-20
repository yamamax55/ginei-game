using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人事評価9-box＝<b>実績×潜在</b>のマトリクス区分（#995）。人物を「実績（過去の成果）」と
    /// 「潜在能力（将来の伸びしろ）」の<b>2軸</b>で評価し、昇進・配置・育成の判断材料にする。
    /// 「今の成果（実績）と未来の伸び（潜在）は別軸」を式に出す＝高実績でも潜在が低ければ専門職留任、
    /// 低実績でも潜在が高ければ育成投資の対象（有望株）。
    /// 分担：席次vs実力（<see cref="SeniorityRules"/>）は<b>初期序列</b>、成長（<c>GrowthRules</c>）は経験→能力の<b>伸び</b>、
    /// 出自経路（<c>CareerPipelineRules</c>）は<b>入口</b>。本ルールはそれらと別＝就いた後の<b>業績評価</b>で配置・昇進を裁く。
    /// 基準値非破壊（実効値パターン）。乱数なし決定論。test-first。
    /// </summary>
    public enum TalentBox
    {
        問題児,        // 低実績×低潜在
        平凡,          // 中実績×低潜在
        要改善,        // 低実績×中潜在
        有望株,        // 低実績×高潜在（育成投資の最優先）
        中核人材,      // 中実績×中潜在
        スター人材,    // 高実績×高潜在（昇進の最優先）
        熟練者,        // 高実績×中潜在
        安定貢献者,    // 中実績×高潜在
        高位プロ       // 高実績×低潜在（専門職として留任）
    }

    /// <summary>9-box 評価の調整値（軸の区切り・各種感度）。</summary>
    public readonly struct PerformanceReviewParams
    {
        public readonly float lowThreshold;       // この値未満＝低（実績/潜在 共通）
        public readonly float highThreshold;      // この値以上＝高（実績/潜在 共通）
        public readonly float retentionBase;      // 離職リスクの基準感度（市場需要×区分重みに掛ける）
        public readonly float calibrationStrength; // 甘辛補正の強さ（評価者偏りの引き戻し量）

        public PerformanceReviewParams(float lowThreshold, float highThreshold, float retentionBase, float calibrationStrength)
        {
            this.lowThreshold = Mathf.Clamp01(lowThreshold);
            this.highThreshold = Mathf.Clamp01(highThreshold);
            this.retentionBase = Mathf.Clamp01(retentionBase);
            this.calibrationStrength = Mathf.Clamp01(calibrationStrength);
        }

        /// <summary>既定＝低0.33未満/高0.66以上・離職基準0.6・甘辛補正0.5。</summary>
        public static PerformanceReviewParams Default => new PerformanceReviewParams(0.33f, 0.66f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 人事評価9-box の純ロジック（#995）。実績×潜在の3×3マトリクスで人材を区分し、
    /// 昇進適性・育成優先度・離職リスク・後継者準備度・甘辛補正を算出する。
    /// </summary>
    public static class PerformanceReviewRules
    {
        /// <summary>0..1 を 低=0/中=1/高=2 の3段階へ量子化（軸の区切り）。</summary>
        private static int Band(float v, PerformanceReviewParams p)
        {
            v = Mathf.Clamp01(v);
            if (v >= p.highThreshold) return 2;
            if (v >= p.lowThreshold) return 1;
            return 0;
        }

        /// <summary>
        /// 9-box の区分判定（実績軸×潜在軸の3×3＝人材ポートフォリオ）。
        /// 実績(<paramref name="performance"/>) と潜在(<paramref name="potential"/>) は<b>別軸</b>で測る。
        /// </summary>
        public static TalentBox Box(float performance, float potential, PerformanceReviewParams p)
        {
            int perf = Band(performance, p);  // 実績バンド
            int pot = Band(potential, p);     // 潜在バンド

            // 行=実績(0..2)・列=潜在(0..2) の9区分。
            switch (perf)
            {
                case 0: // 低実績
                    if (pot == 2) return TalentBox.有望株;   // 伸びしろ大＝育成価値
                    if (pot == 1) return TalentBox.要改善;
                    return TalentBox.問題児;
                case 1: // 中実績
                    if (pot == 2) return TalentBox.安定貢献者;
                    if (pot == 1) return TalentBox.中核人材;
                    return TalentBox.平凡;
                default: // 高実績
                    if (pot == 2) return TalentBox.スター人材; // 高実績×高潜在＝最優先昇進
                    if (pot == 1) return TalentBox.熟練者;
                    return TalentBox.高位プロ;                // 高実績×低潜在＝専門職留任
            }
        }

        /// <summary>既定 Params 版。</summary>
        public static TalentBox Box(float performance, float potential) => Box(performance, potential, PerformanceReviewParams.Default);

        /// <summary>
        /// 昇進適性（0..1）。高実績×高潜在＝スター人材が最優先（実績6:潜在4 で実績寄り＝今の成果が昇進の主因）。
        /// 高実績×低潜在は専門職として留任ぶん割り引く（上位職へ伸びにくい）。
        /// </summary>
        public static float PromotionReadiness(float performance, float potential)
        {
            float perf = Mathf.Clamp01(performance);
            float pot = Mathf.Clamp01(potential);
            float baseReady = 0.6f * perf + 0.4f * pot;            // 実績寄りの加重和
            float penalty = 0.3f * perf * (1f - pot);             // 高実績×低潜在＝専門職留任ペナルティ
            return Mathf.Clamp01(baseReady - penalty);
        }

        /// <summary>
        /// 育成優先度（0..1）。低実績×高潜在＝有望株が最大（投資価値大）／高実績×低潜在は伸びしろ小で最小。
        /// ＝潜在が上げ・実績が下げ＝「未だ出ていない伸び」へ投資する。
        /// </summary>
        public static float DevelopmentPriority(float performance, float potential)
        {
            float perf = Mathf.Clamp01(performance);
            float pot = Mathf.Clamp01(potential);
            // 潜在が高く実績が低いほど大（伸びしろ＝potential×未実現ぶん(1-performance)）。
            return Mathf.Clamp01(pot * (0.5f + 0.5f * (1f - perf)));
        }

        /// <summary>
        /// 離職リスク（0..1）。区分ほど外から引かれる（スター人材＝高需要で最大）。
        /// 市場需要(<paramref name="marketDemand"/>) が高いほど引き留めが要る＝区分重み×需要×基準感度。
        /// </summary>
        public static float RetentionRisk(TalentBox box, float marketDemand, PerformanceReviewParams p)
        {
            float demand = Mathf.Clamp01(marketDemand);
            float weight = BoxAttractiveness(box); // 区分の「引かれやすさ」
            return Mathf.Clamp01(p.retentionBase * weight * demand);
        }

        /// <summary>既定 Params 版。</summary>
        public static float RetentionRisk(TalentBox box, float marketDemand) => RetentionRisk(box, marketDemand, PerformanceReviewParams.Default);

        /// <summary>区分ごとの市場での魅力度（0..1＝外部から引かれやすさ）。スター人材が最大。</summary>
        private static float BoxAttractiveness(TalentBox box)
        {
            switch (box)
            {
                case TalentBox.スター人材:   return 1.0f;
                case TalentBox.熟練者:       return 0.85f;
                case TalentBox.高位プロ:     return 0.8f;  // 専門職＝即戦力で引かれる
                case TalentBox.安定貢献者:   return 0.7f;
                case TalentBox.有望株:       return 0.6f;  // 将来性を買われる
                case TalentBox.中核人材:     return 0.5f;
                case TalentBox.要改善:       return 0.3f;
                case TalentBox.平凡:         return 0.25f;
                case TalentBox.問題児:       return 0.1f;
                default:                     return 0.5f;
            }
        }

        /// <summary>
        /// 後継者準備度（0..1）。潜在×経験＝次世代リーダー候補（潜在は資質・経験は実戦の場数）。
        /// どちらか欠けると伸びない＝潜在主導(0.6)×経験(0.4) の加重幾何的バランス。
        /// </summary>
        public static float SuccessionReadiness(float potential, float experience)
        {
            float pot = Mathf.Clamp01(potential);
            float exp = Mathf.Clamp01(experience);
            // 潜在を主軸に経験で底上げ。経験ゼロでは潜在の半分（場数を踏んでいない）。
            return Mathf.Clamp01(pot * (0.5f + 0.5f * exp));
        }

        /// <summary>
        /// 評価の甘辛調整（評価者の偏りを補正＝相対評価の公平化）。
        /// 評価者の寛大さ(<paramref name="raterLeniency"/> 0..1、0.5=公平) が高い＝甘い評価ほど引き下げ、辛い評価は引き上げる。
        /// 補正の強さは <see cref="PerformanceReviewParams.calibrationStrength"/>。
        /// </summary>
        public static float CalibrationAdjustment(float rawScore, float raterLeniency, PerformanceReviewParams p)
        {
            float raw = Mathf.Clamp01(rawScore);
            float bias = Mathf.Clamp01(raterLeniency) - 0.5f; // +=甘い / -=辛い
            // 甘い評価者の点は引き下げ、辛い評価者の点は引き上げ＝バイアスの逆方向へ寄せる。
            float adjusted = raw - p.calibrationStrength * bias;
            return Mathf.Clamp01(adjusted);
        }

        /// <summary>既定 Params 版。</summary>
        public static float CalibrationAdjustment(float rawScore, float raterLeniency) => CalibrationAdjustment(rawScore, raterLeniency, PerformanceReviewParams.Default);
    }
}
