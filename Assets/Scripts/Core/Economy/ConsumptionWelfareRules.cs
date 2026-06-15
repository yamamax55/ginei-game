using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 生活水準・支持への接続（POPDEM-4・#2042・#181/#113/#403 連携・純ロジック）。
    /// <b>マズロー階層</b>＝下位必需が満たされて初めて上位需要が効く：飢えていれば奢侈がいくらあっても生活水準は上がらない
    /// （上位カテゴリの寄与を下位充足で<b>ゲート</b>する＝厳格な階層）。「いま最も足りない欲求」は <see cref="NeedsRules.DominantNeed"/>（#403）を窓口に。
    /// 生活水準#181→支持#113。実効値パターン。test-first。
    /// </summary>
    public static class ConsumptionWelfareRules
    {
        public const float NecessityWeight = 0.6f; // 必需の寄与
        public const float ComfortWeight = 0.3f;   // 快適の寄与（必需が満たされた分だけ）
        public const float LuxuryWeight = 0.1f;    // 奢侈の寄与（必需・快適が満たされた分だけ）

        /// <summary>3カテゴリ充足→マズロー6段の充足配列（必需→生理/安全・快適→所属/承認・奢侈→自己実現/自己超越）。#403 の窓口へ渡す。</summary>
        public static float[] BuildSatisfaction(float necessity, float comfort, float luxury)
        {
            var s = new float[6];
            s[(int)NeedLevel.生理] = s[(int)NeedLevel.安全] = Mathf.Clamp01(necessity);
            s[(int)NeedLevel.所属] = s[(int)NeedLevel.承認] = Mathf.Clamp01(comfort);
            s[(int)NeedLevel.自己実現] = s[(int)NeedLevel.自己超越] = Mathf.Clamp01(luxury);
            return s;
        }

        /// <summary>
        /// 生活水準（0..1）＝必需×重み + (必需×快適)×重み + (必需×快適×奢侈)×重み。
        /// <b>上位財は下位充足で乗算ゲート</b>＝飢餓時は快適/奢侈が寄与しない（マズロー階層）。
        /// </summary>
        public static float LivingStandard(float necessity, float comfort, float luxury)
        {
            float n = Mathf.Clamp01(necessity);
            float c = Mathf.Clamp01(comfort);
            float l = Mathf.Clamp01(luxury);
            return Mathf.Clamp01(NecessityWeight * n + ComfortWeight * (n * c) + LuxuryWeight * (n * c * l));
        }

        /// <summary>いま最も足りない欲求段（#403 DominantNeed を窓口に＝飢餓なら生理）。イベント/表示の火種。</summary>
        public static NeedLevel DominantUnmetNeed(float necessity, float comfort, float luxury)
            => NeedsRules.DominantNeed(BuildSatisfaction(necessity, comfort, luxury));

        /// <summary>支持の増減＝(生活水準−基準)×スケール（高い生活水準で支持↑・困窮で↓・#113）。</summary>
        public static float SupportDelta(float livingStandard, float baseline, float scale)
            => (Mathf.Clamp01(livingStandard) - baseline) * Mathf.Max(0f, scale);
    }
}
