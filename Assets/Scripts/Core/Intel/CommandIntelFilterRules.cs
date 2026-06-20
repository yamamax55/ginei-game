using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指揮系統の情報フィルタ（ENDR-2 #2352・縦方向の情報遮断の唯一の窓口）。
    /// <para>
    /// <see cref="EspionageRules"/> は「対外諜報（敵から情報を得る）」だが、本ルールはその縦方向の逆＝
    /// 組織内で上位司令部が前線指揮官へ開示する情報を意図的に絞る「情報遮断」を解く純ロジック。
    /// 「演習だと思っていたら実は本物の艦隊戦だった」というねじれ＝前線の<b>認識</b>と実際の<b>戦略状況</b>の
    /// ギャップを生み、判断誤差を与え、閾値超過で <see cref="EventEngine"/>（#116）の「真相開示」イベント候補に乗せる。
    /// </para>
    /// 概念的にのみ EventEngine / Espionage 系へ橋渡しする＝本ルール自体は自己完結した純数学（float/bool のみ返す）。
    /// 基準値は非破壊（実効値パターン）。すべて clamp で安全側に倒し、test-first。
    /// </summary>
    public static class CommandIntelFilterRules
    {
        // === 調整値（マジックナンバー禁止＝ここに集約）===

        /// <summary>フィルタ段階1段あたりの情報品質の減衰量（完全開示→部分開示→隠蔽で逓減）。</summary>
        public const float QualityLossPerLevel = 0.35f;

        /// <summary>指揮系統の階層1段（中継1ホップ）あたりの追加減衰量（伝言ゲーム劣化）。</summary>
        public const float QualityLossPerDepth = 0.08f;

        /// <summary>情報将校の情報能力が品質に与える最大寄与（0..1。鋭い参謀ほど遮断・劣化を補える）。</summary>
        public const float IntelligenceQualityScale = 0.30f;

        /// <summary>能力スコアの基準値（この値で寄与ゼロ＝中庸）。</summary>
        public const float IntelligenceMidpoint = 50f;

        /// <summary>能力スコアの想定上限（正規化の分母）。</summary>
        public const float IntelligenceMax = 100f;

        /// <summary>認識ギャップ1.0あたりの判断誤差係数の増分（ギャップが判断を狂わせる強さ）。</summary>
        public const float GapErrorScale = 1.0f;

        /// <summary>判断誤差係数の上限（暴走防止）。1.0=ギャップなしの正常判断。</summary>
        public const float MaxErrorFactor = 3.0f;

        /// <summary>「真相開示」イベント候補に乗せるギャップ閾値（これを超えると true）。</summary>
        public const float DefaultRevealThreshold = 0.5f;

        // === 情報品質 ===

        /// <summary>
        /// フィルタ後の情報品質（0..1）。フィルタ段階が高いほど・指揮系統が深いほど落ち、
        /// 情報将校の情報能力が高いほど一部を補う。
        /// </summary>
        /// <param name="fullInfo">無遮断・無劣化なら届くはずの情報の充足度（0..1）。</param>
        /// <param name="filterLevel">上位司令部のフィルタ段階。</param>
        /// <param name="admiralIntelligence">受け手（前線指揮官/参謀）の情報能力（0..100想定・clamp）。</param>
        /// <param name="hierarchyDepth">情報が経由した指揮系統の段数（中継ホップ数・0以上・負は0扱い）。</param>
        public static float FilteredIntelQuality(float fullInfo, InfoFilterLevel filterLevel, float admiralIntelligence, int hierarchyDepth)
        {
            float baseInfo = Mathf.Clamp01(fullInfo);
            int level = Mathf.Clamp((int)filterLevel, 0, (int)InfoFilterLevel.隠蔽);
            int depth = hierarchyDepth > 0 ? hierarchyDepth : 0;

            // フィルタ段階・階層深度による減衰（信号の目減り）。
            float retention = 1f
                - QualityLossPerLevel * level
                - QualityLossPerDepth * depth;
            retention = Mathf.Clamp01(retention);

            // 情報能力による補填（中庸=50で寄与0、上限で IntelligenceQualityScale まで）。
            float intelNorm = Mathf.Clamp01((admiralIntelligence - IntelligenceMidpoint) / IntelligenceMax);
            float intelBonus = intelNorm * IntelligenceQualityScale;

            return Mathf.Clamp01(baseInfo * retention + intelBonus * baseInfo);
        }

        /// <summary>階層深度ゼロ（直属）での品質。</summary>
        public static float FilteredIntelQuality(float fullInfo, InfoFilterLevel filterLevel, float admiralIntelligence)
            => FilteredIntelQuality(fullInfo, filterLevel, admiralIntelligence, 0);

        // === 認識ギャップ → 判断誤差 ===

        /// <summary>
        /// 認識ギャップの大きさ（0..1）。前線指揮官の認識と実際の戦略状況の差の絶対値。
        /// 入力は同一スケール（例:0..1）の状況指標を想定し、clamp して安全に差を取る。
        /// </summary>
        public static float GapMagnitude(float perceivedSituation, float actualSituation)
        {
            float perceived = Mathf.Clamp01(perceivedSituation);
            float actual = Mathf.Clamp01(actualSituation);
            return Mathf.Clamp01(Mathf.Abs(actual - perceived));
        }

        /// <summary>
        /// 認識ギャップ→判断誤差係数（1.0=ギャップなし＝正常／大きいほど誤った前提で動く＝判断が狂う）。
        /// 会戦・戦略の係数として消費する想定（#106 ModifierStack へ橋渡し）。
        /// </summary>
        public static float IntelGapEffect(float perceivedSituation, float actualSituation)
        {
            float gap = GapMagnitude(perceivedSituation, actualSituation);
            return Mathf.Clamp(1f + gap * GapErrorScale, 1f, MaxErrorFactor);
        }

        // === 真相開示の閾値判定 ===

        /// <summary>ギャップが閾値を超過すれば真相開示イベント候補に登録（true）。閾値は clamp01。</summary>
        public static bool RevealThreshold(float gapMagnitude, float threshold)
        {
            float gap = Mathf.Clamp01(gapMagnitude);
            float th = Mathf.Clamp01(threshold);
            return gap > th;
        }

        /// <summary>既定閾値での真相開示判定。</summary>
        public static bool RevealThreshold(float gapMagnitude)
            => RevealThreshold(gapMagnitude, DefaultRevealThreshold);
    }
}
