using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 脅威探知のパラメータ（敵の接近を早期にどれだけ察知・識別・警報できるかを量る調整値）。基準値は ctor で全クランプ。
    /// </summary>
    public readonly struct ThreatDetectionParams
    {
        /// <summary>探知の鋭さ（センサーと信号の積をどれだけ探知度へ増幅するか・1=線形）。</summary>
        public readonly float detectionGain;
        /// <summary>識別の最低確度（探知できても識別質が低いとここまでは下がりうる下限の幅・0..1）。</summary>
        public readonly float identificationFloor;
        /// <summary>脅威度における近接の重み（近いほど危険を強める指数・1=線形）。</summary>
        public readonly float proximityWeight;
        /// <summary>奇襲察知の猶予の効き（猶予時間をどれだけ察知へ反映するか・1=線形）。</summary>
        public readonly float warningWeight;
        /// <summary>警報疲れの蓄積感度（誤警報がどれだけ早く疲れへ転ぶか・大きいほど疲れやすい）。</summary>
        public readonly float fatigueSensitivity;
        /// <summary>脅威認識の既定しきい値（警報－疲れ がこれを超えれば認識＝対処できる）。</summary>
        public readonly float recognitionThreshold;

        public ThreatDetectionParams(
            float detectionGain,
            float identificationFloor,
            float proximityWeight,
            float warningWeight,
            float fatigueSensitivity,
            float recognitionThreshold)
        {
            this.detectionGain = Mathf.Clamp(detectionGain, 0.1f, 4f);
            this.identificationFloor = Mathf.Clamp01(identificationFloor);
            this.proximityWeight = Mathf.Clamp(proximityWeight, 0.1f, 4f);
            this.warningWeight = Mathf.Clamp(warningWeight, 0.1f, 4f);
            this.fatigueSensitivity = Mathf.Clamp(fatigueSensitivity, 0.1f, 4f);
            this.recognitionThreshold = Mathf.Clamp01(recognitionThreshold);
        }

        /// <summary>既定値（探知ゲイン1.0・識別下限0.1・近接重み1.0・猶予重み1.0・疲れ感度1.5・認識しきい値0.3）。</summary>
        public static ThreatDetectionParams Default =>
            new ThreatDetectionParams(1f, 0.1f, 1f, 1f, 1.5f, 0.3f);
    }

    /// <summary>
    /// 脅威探知の純ロジック（盤面非依存・plain引数）。敵の接近と意図を早期に探知・識別し、脅威度を評価して
    /// 優先順位を付け、奇襲を察知し、警報を発令する層。過去の誤警報は警報疲れ（オオカミ少年効果）を生み、
    /// 疲れが警報を呑むと脅威を認識・対処できない。
    ///
    /// 分担：<c>ThreatAssessmentRules</c> は「探知済みの敵が危ないか・退くべきか」（AI撤退判断）を量る。
    /// こちらは一段手前＝そもそも敵を「探知・識別・警報」できるかの層（両者は直交）。
    /// すべて Mathf のみ・LINQ/乱数なし・入力非破壊（実効値パターン）・決定論。test-first。
    /// </summary>
    public static class ThreatDetectionRules
    {
        /// <summary>
        /// 脅威の探知度 0..1。センサーカバー率 × 脅威の信号強度 を探知ゲインで増幅。
        /// 一方が0なら探知0＝死角／無信号は見えない。
        /// </summary>
        public static float ThreatDetection(float sensorCoverage, float threatSignature, ThreatDetectionParams p)
        {
            float cov = Mathf.Clamp01(sensorCoverage);
            float sig = Mathf.Clamp01(threatSignature);
            return Mathf.Clamp01(cov * sig * p.detectionGain);
        }

        /// <summary>脅威の探知度（既定Params）。</summary>
        public static float ThreatDetection(float sensorCoverage, float threatSignature) =>
            ThreatDetection(sensorCoverage, threatSignature, ThreatDetectionParams.Default);

        /// <summary>
        /// 敵味方識別の確度 0..1。探知度 × 識別質。識別質が低くても下限 identificationFloor は確保しない
        /// ＝探知できた分のうち識別質で割り引く（探知0なら識別0）。floor は識別質の底上げに使う。
        /// </summary>
        public static float IdentificationConfidence(float detection, float iffQuality, ThreatDetectionParams p)
        {
            float det = Mathf.Clamp01(detection);
            float iff = Mathf.Lerp(p.identificationFloor, 1f, Mathf.Clamp01(iffQuality));
            return Mathf.Clamp01(det * iff);
        }

        /// <summary>敵味方識別の確度（既定Params）。</summary>
        public static float IdentificationConfidence(float detection, float iffQuality) =>
            IdentificationConfidence(detection, iffQuality, ThreatDetectionParams.Default);

        /// <summary>
        /// 脅威度 0..1。敵戦力 × 近接（重み付き）× 敵意。近接は proximityWeight 乗で効く（近いほど跳ね上がる）。
        /// </summary>
        public static float ThreatLevel(
            float threatStrength, float proximity, float hostileIntent, ThreatDetectionParams p)
        {
            float str = Mathf.Clamp01(threatStrength);
            float prox = Mathf.Pow(Mathf.Clamp01(proximity), p.proximityWeight);
            float intent = Mathf.Clamp01(hostileIntent);
            return Mathf.Clamp01(str * prox * intent);
        }

        /// <summary>脅威度（既定Params）。</summary>
        public static float ThreatLevel(float threatStrength, float proximity, float hostileIntent) =>
            ThreatLevel(threatStrength, proximity, hostileIntent, ThreatDetectionParams.Default);

        /// <summary>
        /// 対処優先度 0..1。脅威度 × (1＋自軍脆弱性)/2 ＝弱点を突く脅威ほど優先。
        /// 脆弱性0で脅威度の半分・1で脅威度そのもの＝脅威度を基準に脆弱性で持ち上げる。
        /// </summary>
        public static float ThreatPrioritization(float threatLevel, float vulnerability, ThreatDetectionParams p)
        {
            float lvl = Mathf.Clamp01(threatLevel);
            float vuln = Mathf.Clamp01(vulnerability);
            return Mathf.Clamp01(lvl * (1f + vuln) * 0.5f);
        }

        /// <summary>対処優先度（既定Params）。</summary>
        public static float ThreatPrioritization(float threatLevel, float vulnerability) =>
            ThreatPrioritization(threatLevel, vulnerability, ThreatDetectionParams.Default);

        /// <summary>
        /// 奇襲の察知度 0..1。探知度 × (1－敵隠密) × 猶予（重み付き）。
        /// 敵が隠密で猶予が無いほど察知できない＝奇襲が刺さる。
        /// </summary>
        public static float SurpriseDetection(
            float detection, float enemyStealth, float warningTime, ThreatDetectionParams p)
        {
            float det = Mathf.Clamp01(detection);
            float exposed = 1f - Mathf.Clamp01(enemyStealth);
            float warn = Mathf.Pow(Mathf.Clamp01(warningTime), p.warningWeight);
            return Mathf.Clamp01(det * exposed * warn);
        }

        /// <summary>奇襲の察知度（既定Params）。</summary>
        public static float SurpriseDetection(float detection, float enemyStealth, float warningTime) =>
            SurpriseDetection(detection, enemyStealth, warningTime, ThreatDetectionParams.Default);

        /// <summary>
        /// 警報の発令度 0..1。脅威度 × 識別確度＝「危ない」と「敵と確信」が揃うほど強い警報。
        /// 識別が曖昧だと（味方かもしれず）警報は鈍る。
        /// </summary>
        public static float AlertGeneration(float threatLevel, float identificationConfidence, ThreatDetectionParams p)
        {
            float lvl = Mathf.Clamp01(threatLevel);
            float idc = Mathf.Clamp01(identificationConfidence);
            return Mathf.Clamp01(lvl * idc);
        }

        /// <summary>警報の発令度（既定Params）。</summary>
        public static float AlertGeneration(float threatLevel, float identificationConfidence) =>
            AlertGeneration(threatLevel, identificationConfidence, ThreatDetectionParams.Default);

        /// <summary>
        /// 警報疲れ 0..1（オオカミ少年効果）。過去の誤警報の蓄積を感度で増幅＝誤警報が積もるほど警報を信じなくなる。
        /// </summary>
        public static float AlarmFatigue(float falseAlarmHistory, ThreatDetectionParams p)
        {
            float hist = Mathf.Clamp01(falseAlarmHistory);
            return Mathf.Clamp01(hist * p.fatigueSensitivity);
        }

        /// <summary>警報疲れ（既定Params）。</summary>
        public static float AlarmFatigue(float falseAlarmHistory) =>
            AlarmFatigue(falseAlarmHistory, ThreatDetectionParams.Default);

        /// <summary>
        /// 脅威を認識・対処できるか。警報の発令度が疲れを差し引いてもしきい値を超えるか
        /// ＝疲れが警報を呑むと（オオカミ少年）認識できない。
        /// </summary>
        public static bool IsThreatRecognized(float alertGeneration, float alarmFatigue, float threshold)
        {
            float net = Mathf.Clamp01(alertGeneration) - Mathf.Clamp01(alarmFatigue);
            return net > Mathf.Clamp01(threshold);
        }

        /// <summary>脅威を認識・対処できるか（既定しきい値＝Default.recognitionThreshold）。</summary>
        public static bool IsThreatRecognized(float alertGeneration, float alarmFatigue) =>
            IsThreatRecognized(alertGeneration, alarmFatigue, ThreatDetectionParams.Default.recognitionThreshold);
    }
}
