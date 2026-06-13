using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督のコンディション（疲労・負傷）の純ロジック（ADM-5 #2306）。連戦の消耗・負傷で実効能力が一時低下し、
    /// 休養で回復する＝常に全力ではない。旗艦撃破時の運命（捕虜/戦死/離脱・`BattleMetaRules`#2260）と地続き
    /// （負傷＝離脱の軽度版）。基準フィールドは非破壊＝実効能力に倍率として乗る（実効値パターン）。test-first。
    /// </summary>
    public static class ConditionRules
    {
        /// <summary>疲労100での能力低下の最大割合。</summary>
        public const float MaxFatiguePenalty = 0.2f;
        /// <summary>負傷100での能力低下の最大割合。</summary>
        public const float MaxWoundPenalty = 0.3f;
        /// <summary>コンディションによる能力倍率の下限（どんなに消耗しても完全停止しない）。</summary>
        public const float MinConditionFactor = 0.4f;

        /// <summary>
        /// コンディションによる実効能力倍率（1.0=万全）。疲労・負傷で下がり、下限でクランプ。基準非破壊。
        /// </summary>
        public static float ConditionFactor(int fatigue, int woundSeverity)
        {
            float f = Mathf.Clamp(fatigue, 0, 100) / 100f;
            float w = Mathf.Clamp(woundSeverity, 0, 100) / 100f;
            float factor = 1f - f * MaxFatiguePenalty - w * MaxWoundPenalty;
            return Mathf.Clamp(factor, MinConditionFactor, 1f);
        }

        /// <summary>戦闘などで疲労を加える（0..100クランプ）。</summary>
        public static int AddFatigue(int fatigue, int amount)
            => Mathf.Clamp(fatigue + Mathf.Max(0, amount), 0, 100);

        /// <summary>休養で疲労を回復（rate×日数ぶん減・0..100クランプ）。</summary>
        public static int Recover(int fatigue, float restDays, float ratePerDay)
            => Mathf.Clamp(fatigue - Mathf.RoundToInt(Mathf.Max(0f, restDays) * Mathf.Max(0f, ratePerDay)), 0, 100);

        /// <summary>負傷の自然治癒（休養で軽くなる）。</summary>
        public static int Heal(int woundSeverity, float restDays, float ratePerDay)
            => Mathf.Clamp(woundSeverity - Mathf.RoundToInt(Mathf.Max(0f, restDays) * Mathf.Max(0f, ratePerDay)), 0, 100);

        /// <summary>戦闘不能（負傷が重く実質離脱扱い）か。</summary>
        public static bool IsIncapacitated(int woundSeverity, int threshold = 90)
            => woundSeverity >= threshold;
    }
}
