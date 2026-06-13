using UnityEngine;

namespace Ginei
{
    /// <summary>暗号解読の調整係数。</summary>
    public readonly struct CodebreakingParams
    {
        /// <summary>解析進捗の蓄積速度（per dt・傍受量1×解析力1のとき）。</summary>
        public readonly float analysisRate;
        /// <summary>敵企図を読める確率の上限（完全解読でも全部は読めない）。</summary>
        public readonly float maxReadChance;
        /// <summary>解読情報の使用が敵に勘付かれる感度（使用率1のときの発覚率）。</summary>
        public readonly float exploitationSensitivity;

        public CodebreakingParams(float analysisRate, float maxReadChance, float exploitationSensitivity)
        {
            this.analysisRate = Mathf.Max(0f, analysisRate);
            this.maxReadChance = Mathf.Clamp01(maxReadChance);
            this.exploitationSensitivity = Mathf.Clamp01(exploitationSensitivity);
        }

        /// <summary>既定＝解析0.05・読み上限0.8・露見感度0.5。</summary>
        public static CodebreakingParams Default => new CodebreakingParams(0.05f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 暗号解読の純ロジック（信号情報＝ウルトラ型）。敵通信の傍受を解析して進捗を積み、進捗に応じて
    /// 敵企図を先読みできる。だが解読情報を使うほど敵が「漏れている」と勘付き、暗号更新で進捗は
    /// 振り出しに戻る＝**読めることと使えることは別**（ウルトラのジレンマ：使いすぎれば源泉を失う）。
    /// 物理探知（<see cref="ReconRules"/>）・人的諜報（<see cref="EspionageRules"/>）とは別系統＝信号の世界。
    /// 乱数は roll で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CodebreakingRules
    {
        /// <summary>
        /// 解析進捗の1tick後の値（0..1）。傍受量 trafficVolume(0..1)×解析力 skill(0..1)×速度×dt で積む
        /// ＝敵がよく喋るほど・解析班が優秀なほど速い。
        /// </summary>
        public static float AnalysisTick(float progress, float trafficVolume, float skill, float dt, CodebreakingParams p)
        {
            float gain = p.analysisRate * Mathf.Clamp01(trafficVolume) * Mathf.Clamp01(skill) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(progress) + gain);
        }

        public static float AnalysisTick(float progress, float trafficVolume, float skill, float dt)
            => AnalysisTick(progress, trafficVolume, skill, dt, CodebreakingParams.Default);

        /// <summary>敵企図を読める確率（0..maxReadChance）＝進捗に比例。roll∈[0,1) 未満で先読み成立。</summary>
        public static float ReadChance(float progress, CodebreakingParams p)
        {
            return Mathf.Clamp01(progress) * p.maxReadChance;
        }

        public static float ReadChance(float progress) => ReadChance(progress, CodebreakingParams.Default);

        /// <summary>先読み判定（決定論）。</summary>
        public static bool ReadsIntent(float progress, float roll, CodebreakingParams p)
        {
            return roll < ReadChance(progress, p);
        }

        public static bool ReadsIntent(float progress, float roll) => ReadsIntent(progress, roll, CodebreakingParams.Default);

        /// <summary>
        /// 解読情報の使用による発覚率（0..1）＝使用率 usageRate(0..1＝読めた情報のうち行動に使った割合)×感度。
        /// 「読めても動かない」（usageRate低）なら源泉は守られる＝ウルトラのジレンマの定量化。
        /// </summary>
        public static float ExploitationDetectionChance(float usageRate, CodebreakingParams p)
        {
            return Mathf.Clamp01(usageRate) * p.exploitationSensitivity;
        }

        public static float ExploitationDetectionChance(float usageRate)
            => ExploitationDetectionChance(usageRate, CodebreakingParams.Default);

        /// <summary>敵が漏洩に勘付くか（決定論）。勘付けば暗号更新（<see cref="CipherReset"/>）が来る。</summary>
        public static bool EnemyNotices(float usageRate, float roll, CodebreakingParams p)
        {
            return roll < ExploitationDetectionChance(usageRate, p);
        }

        public static bool EnemyNotices(float usageRate, float roll) => EnemyNotices(usageRate, roll, CodebreakingParams.Default);

        /// <summary>暗号更新＝解析進捗は振り出しへ（積み上げた解読資産が一夜で紙屑になる）。</summary>
        public static float CipherReset() => 0f;
    }
}
