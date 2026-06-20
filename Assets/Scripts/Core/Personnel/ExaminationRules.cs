using UnityEngine;

namespace Ginei
{
    /// <summary>考課制度（試験・記録）の調整係数。</summary>
    public readonly struct ExaminationParams
    {
        /// <summary>考課が記録を蓄積する速度（per dt・頻度1のとき）。記録は積み上がるが上限へ漸近。</summary>
        public readonly float recordGrowthRate;
        /// <summary>無考課時に記録が陳腐化する速度（per dt）。記録は更新されねば古びる。</summary>
        public readonly float recordDecayRate;
        /// <summary>昇進反映における考課点の重み（残りが記録一貫性の重み）。</summary>
        public readonly float examWeightForPromotion;
        /// <summary>反乱予兆検出における記録深度の重み（残りが監視統合の重み）。</summary>
        public readonly float recordWeightForDetection;
        /// <summary>規範化（正常/逸脱の線引き）の鋭さ。大きいほど僅かな偏差を逸脱と見なす。</summary>
        public readonly float normalizationSharpness;
        /// <summary>過度な考課が萎縮・疲弊を生む速度（per dt・頻度1のとき）。</summary>
        public readonly float fatigueRate;
        /// <summary>これを超える頻度は「過度」＝疲労を生む閾値。</summary>
        public readonly float fatigueFrequencyThreshold;
        /// <summary>全員が記録で把握される考課国家（ドシエ国家）と見なす記録深度の閾値。</summary>
        public readonly float dossierThreshold;

        public ExaminationParams(float recordGrowthRate, float recordDecayRate,
                                 float examWeightForPromotion, float recordWeightForDetection,
                                 float normalizationSharpness, float fatigueRate,
                                 float fatigueFrequencyThreshold, float dossierThreshold)
        {
            this.recordGrowthRate = Mathf.Max(0f, recordGrowthRate);
            this.recordDecayRate = Mathf.Max(0f, recordDecayRate);
            this.examWeightForPromotion = Mathf.Clamp01(examWeightForPromotion);
            this.recordWeightForDetection = Mathf.Clamp01(recordWeightForDetection);
            this.normalizationSharpness = Mathf.Max(0f, normalizationSharpness);
            this.fatigueRate = Mathf.Max(0f, fatigueRate);
            this.fatigueFrequencyThreshold = Mathf.Clamp01(fatigueFrequencyThreshold);
            this.dossierThreshold = Mathf.Clamp01(dossierThreshold);
        }

        /// <summary>既定＝記録蓄積0.5/陳腐化0.1・昇進反映(考課0.6:一貫性0.4)・検出(記録0.6:統合0.4)・規範化鋭さ4・疲労0.3・過度閾値0.6・ドシエ閾値0.8。</summary>
        public static ExaminationParams Default
            => new ExaminationParams(0.5f, 0.1f, 0.6f, 0.6f, 4f, 0.3f, 0.6f, 0.8f);
    }

    /// <summary>
    /// 考課制度の純ロジック＝フーコー『監獄の誕生』の<b>試験・考課（examination）</b>（PANO-3 #1509）。
    /// 考課は「監視（階層的視線）」と「規範化（賞罰）」を組み合わせ、個人を<b>可視化し・記録し・序列化する</b>権力技術である。
    /// 定期的な記録が個人を「ケース」として把握可能にする＝誰が何をしたかが分かり、標準（規範）からの偏差で序列が付き、
    /// その記録が昇進に反映され、蓄積された記録が普段との逸脱（反乱の予兆）を浮かび上がらせる。
    /// 分担：人事評価9-box（<see cref="PerformanceReviewRules"/>）は<b>実績×潜在の業績裁定</b>、席次（<see cref="SeniorityRules"/>）は<b>初期序列</b>。
    /// 本ルールはそれらと別＝フーコー的な<b>試験・記録の権力</b>（可視化・記録・序列化による把握そのもの）を扱う。
    /// 反乱予兆は <c>MutinyRules</c>（艦隊反乱）の前段＝記録の蓄積が予兆検出精度を上げる。
    /// 監視（階層的視線・一望監視＝パノプティコン）の側は同EPIC PANO の <c>PanoptismRules</c> が担い、本ルールは考課・記録の側を担う。
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ExaminationRules
    {
        /// <summary>
        /// 記録の蓄積（0..1）の1tick後。定期的な考課（頻度 examFrequency）が個人の記録を積み上げ、
        /// 上限へ漸近する（残余 1−深度 に比例して伸びる＝飽和）。考課が止めば記録は陳腐化して薄れる。
        /// ＝<b>定期的な記録が個人をケース化する</b>（記録の更新が把握可能性を保つ）。
        /// </summary>
        public static float RecordTick(float recordDepth, float examFrequency, float dt, ExaminationParams p)
        {
            float depth = Mathf.Clamp01(recordDepth);
            float freq = Mathf.Clamp01(examFrequency);
            float d = Mathf.Max(0f, dt);
            float growth = p.recordGrowthRate * freq * (1f - depth) * d;   // 考課で蓄積（飽和）
            float decay = p.recordDecayRate * (1f - freq) * depth * d;     // 無考課で陳腐化
            return Mathf.Clamp01(depth + growth - decay);
        }

        public static float RecordTick(float recordDepth, float examFrequency, float dt)
            => RecordTick(recordDepth, examFrequency, dt, ExaminationParams.Default);

        /// <summary>
        /// 可視化度（0..1）。記録が深いほど個人が可視化・把握される（誰が何をしたかが分かる）。
        /// 記録深度に単調かつ逓増的に対応＝薄い記録ではぼやけ、深い記録で輪郭が立つ。
        /// </summary>
        public static float Visibility(float recordDepth)
        {
            float depth = Mathf.Clamp01(recordDepth);
            // 深いほど効く逓増（記録が一定量を超えると把握が一気に効く）。
            return Mathf.Clamp01(depth * (0.5f + 0.5f * depth));
        }

        /// <summary>
        /// 昇進への反映（0..1）。考課の点数(examScore)と記録の一貫性(recordConsistency)が昇進に反映される。
        /// ＝<b>記録が序列を決める</b>。一発の高得点でなく、安定した記録（一貫性）が裏打ちする。
        /// </summary>
        public static float PromotionReflection(float examScore, float recordConsistency, ExaminationParams p)
        {
            float score = Mathf.Clamp01(examScore);
            float consistency = Mathf.Clamp01(recordConsistency);
            return Mathf.Clamp01(p.examWeightForPromotion * score
                                 + (1f - p.examWeightForPromotion) * consistency);
        }

        public static float PromotionReflection(float examScore, float recordConsistency)
            => PromotionReflection(examScore, recordConsistency, ExaminationParams.Default);

        /// <summary>
        /// 反乱の予兆検出精度（0..1）。蓄積された記録(recordDepth)と監視の統合度(surveillanceIntegration)が
        /// 普段との逸脱を見えるようにする＝<b>記録が深いほど予兆が見える</b>（平時の基準があるから逸脱が分かる）。
        /// 記録が浅ければ何が異常かの基準が無く、統合が無ければ点在する記録が繋がらない＝両者の加重和。
        /// <c>MutinyRules</c> の不満蓄積を早期に捉える前段。
        /// </summary>
        public static float RebellionPrecursorDetection(float recordDepth, float surveillanceIntegration, ExaminationParams p)
        {
            float depth = Mathf.Clamp01(recordDepth);
            float integration = Mathf.Clamp01(surveillanceIntegration);
            return Mathf.Clamp01(p.recordWeightForDetection * depth
                                 + (1f - p.recordWeightForDetection) * integration);
        }

        public static float RebellionPrecursorDetection(float recordDepth, float surveillanceIntegration)
            => RebellionPrecursorDetection(recordDepth, surveillanceIntegration, ExaminationParams.Default);

        /// <summary>
        /// 規範化（0..1）＝規範（標準的な評価基準 normReference）からの偏差で個人を序列化する（正常/逸脱の線引き）。
        /// 考課点が規範を上回れば正常側（高位）、下回れば逸脱側（低位）へ。偏差を <see cref="ExaminationParams.normalizationSharpness"/>
        /// で増幅し 0.5 中心の連続値へ写す＝<b>正常/逸脱の境界が個人を序列の位置へ割り付ける</b>。
        /// </summary>
        public static float Normalization(float examScore, float normReference, ExaminationParams p)
        {
            float score = Mathf.Clamp01(examScore);
            float reference = Mathf.Clamp01(normReference);
            float deviation = score - reference; // +=規範超え（正常・高位）/ -=逸脱（低位）
            // 0.5 を境界に偏差を鋭さで増幅（鋭いほど僅差でも上下へ振れる＝規律的な線引き）。
            return Mathf.Clamp01(0.5f + 0.5f * p.normalizationSharpness * deviation);
        }

        public static float Normalization(float examScore, float normReference)
            => Normalization(examScore, normReference, ExaminationParams.Default);

        /// <summary>
        /// 能力主義の透明性（0..1）＝記録に基づく評価が情実でなく記録で測る正の面。
        /// 記録が深い(recordDepth)ほど・運用の公正さ(fairness)が高いほど、評価が客観的記録に立脚する＝
        /// <b>誰がどう評価されたかが記録から辿れる</b>。記録が浅ければ恣意の余地が残り、公正さが無ければ記録が歪む＝両者の積。
        /// </summary>
        public static float MeritocraticTransparency(float recordDepth, float fairness)
        {
            float depth = Mathf.Clamp01(recordDepth);
            float fair = Mathf.Clamp01(fairness);
            return Mathf.Clamp01(depth * fair);
        }

        /// <summary>
        /// 考課疲れ（0..1）の1tick後。過度な考課（頻度が <see cref="ExaminationParams.fatigueFrequencyThreshold"/> を超える分）が
        /// 被考課者の萎縮・疲弊を生む＝評価漬けの息苦しさ。閾値以下は蓄積しない（適度な考課は無害）。
        /// 累積疲労として返す（呼び出し側が前tickの疲労を渡して積む）。
        /// </summary>
        public static float SurveillanceFatigue(float currentFatigue, float examFrequency, float dt, ExaminationParams p)
        {
            float fatigue = Mathf.Clamp01(currentFatigue);
            float freq = Mathf.Clamp01(examFrequency);
            float d = Mathf.Max(0f, dt);
            float excess = Mathf.Max(0f, freq - p.fatigueFrequencyThreshold); // 過度ぶんのみ
            float gain = p.fatigueRate * excess * d;
            return Mathf.Clamp01(fatigue + gain);
        }

        public static float SurveillanceFatigue(float currentFatigue, float examFrequency, float dt)
            => SurveillanceFatigue(currentFatigue, examFrequency, dt, ExaminationParams.Default);

        /// <summary>
        /// ドシエ国家（考課国家）判定。記録深度が閾値(threshold)以上＝全員が記録で把握される状態。
        /// ＝<b>定期的な考課が一人残らず個人を可視化・記録・序列化した</b>到達点（フーコー的規律社会）。
        /// </summary>
        public static bool IsDossierState(float recordDepth, float threshold)
        {
            return Mathf.Clamp01(recordDepth) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定 Params 版（<see cref="ExaminationParams.dossierThreshold"/> を閾値に用いる）。</summary>
        public static bool IsDossierState(float recordDepth)
            => IsDossierState(recordDepth, ExaminationParams.Default.dossierThreshold);
    }
}
