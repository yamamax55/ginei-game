using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宗教の数値解決の調整値（純構造体・既定 .Default）。マジックナンバーを1か所へ集約する。
    /// </summary>
    public readonly struct ReligionParams
    {
        /// <summary>改宗圧力の最大（信仰差=1のときの圧力上限）。</summary>
        public readonly float conversionMax;
        /// <summary>改宗が均衡へ寄る速さ（/戦略秒。GovernanceRules.StabilitySpeed 相当）。</summary>
        public readonly float conversionSpeed;
        /// <summary>思想親和が一致する勢力の改宗圧力倍率（>1＝速い）。</summary>
        public readonly float affinityBoost;
        /// <summary>社会効果の基準係数（devotion=0 でこの値）。</summary>
        public readonly float socialBase;
        /// <summary>社会効果の信仰寄与（devotion=1 で socialBase+socialGain）。</summary>
        public readonly float socialGain;
        /// <summary>聖地係争時に上乗せする聖戦圧力。</summary>
        public readonly float holySiteBonus;

        public ReligionParams(float conversionMax, float conversionSpeed, float affinityBoost,
            float socialBase, float socialGain, float holySiteBonus)
        {
            this.conversionMax = conversionMax;
            this.conversionSpeed = conversionSpeed;
            this.affinityBoost = affinityBoost;
            this.socialBase = socialBase;
            this.socialGain = socialGain;
            this.holySiteBonus = holySiteBonus;
        }

        /// <summary>既定の調整値。</summary>
        public static ReligionParams Default => new ReligionParams(
            conversionMax: 1f,
            conversionSpeed: 0.05f,
            affinityBoost: 1.5f,
            socialBase: 0.9f,
            socialGain: 0.3f,
            holySiteBonus: 0.4f);
    }

    /// <summary>
    /// 宗教の数値解決（#172-175・R-1 創発とPOP宗教＝#173 中心・純ロジック test-first）。
    /// GovernanceRules に倣い「目標値へ時間で収束」する：改宗圧力から均衡 devotion を出し、
    /// <see cref="Tick"/> で住民信仰を均衡へ寄せる（占領しても即は変わらず時間で改宗＝#173）。
    /// 聖戦/神権は最小スタブ（聖地係争による圧力のみ）。調整値は <see cref="ReligionParams"/> に集約。
    /// Game層（GameSettings/FleetRegistry 等）非依存＝Core 純ロジック。
    /// </summary>
    public static class ReligionRules
    {
        // --- 調整値（ReligionParams に持たせない固定の境界） ---
        public const float IdeologyDefaultDevotion = 0.5f;  // 思想親和の判定が無いときの中立信仰
        public const float HeresyThreshold = 0.5f;          // 公式信仰との乖離が大きいと異端

        /// <summary>
        /// 改宗圧力(0..1)：支配勢力の信仰の強さと住民信仰の差から、改宗が進む勢いを出す純関数。
        /// 支配側のほうが信仰が強い（rulerFaith>localFaith）ほど圧力が高い＝住民が支配信仰へ寄る。
        /// 住民のほうが強ければ圧力0（無理に改宗しない）。affinity一致なら圧力を底上げ。
        /// </summary>
        public static float ConversionPressure(float localFaith, float rulerFaith, bool affinityMatch, ReligionParams p)
        {
            float lf = Mathf.Clamp01(localFaith);
            float rf = Mathf.Clamp01(rulerFaith);
            float diff = rf - lf;                       // 支配側が強いほど正
            if (diff <= 0f) return 0f;                  // 住民のほうが強ければ改宗しない
            float pressure = diff * p.conversionMax;
            if (affinityMatch) pressure *= p.affinityBoost;
            return Mathf.Clamp01(pressure);
        }

        /// <summary>
        /// 改宗が進む先の均衡 devotion(0..1)：改宗圧力のぶんだけ住民信仰が支配信仰へ寄る到達点。
        /// 圧力0＝現状維持（localFaith）、圧力1＝支配信仰（rulerFaith）まで寄る。
        /// </summary>
        public static float EquilibriumDevotion(float localFaith, float rulerFaith, bool affinityMatch, ReligionParams p)
        {
            float lf = Mathf.Clamp01(localFaith);
            float rf = Mathf.Clamp01(rulerFaith);
            float pressure = ConversionPressure(lf, rf, affinityMatch, p);
            return Mathf.Clamp01(Mathf.Lerp(lf, rf, pressure));
        }

        /// <summary>
        /// 1tick の改宗更新：住民信仰の devotion を均衡へ寄せる（戦略時間に dt 比例＝GovernanceRules.Tick と同じ作法）。
        /// affinityMatch＝支配勢力の思想と <see cref="Religion.ideologyAffinity"/> が一致するか。
        /// </summary>
        public static void Tick(Religion r, float rulerFaithDevotion, bool affinityMatch, float deltaTime, ReligionParams p)
        {
            if (r == null || deltaTime <= 0f) return;
            float target = EquilibriumDevotion(r.devotion, rulerFaithDevotion, affinityMatch, p);
            r.devotion = Mathf.MoveTowards(r.devotion, target, p.conversionSpeed * deltaTime);
            r.devotion = Mathf.Clamp01(r.devotion);
        }

        /// <summary>
        /// 異端判定：住民信仰と公式信仰が別物か（名称が異なり、双方が無信仰でない）。
        /// 同名＝正統、どちらか空＝判定対象外（false）。神権の弾圧フックの最小スタブ。
        /// </summary>
        public static bool IsHeresy(string localFaith, string officialFaith)
        {
            if (string.IsNullOrEmpty(localFaith) || string.IsNullOrEmpty(officialFaith)) return false;
            return localFaith != officialFaith;
        }

        /// <summary>
        /// 社会効果係数（GovernanceRules.OutputFactor 風・実効値パターン）：信仰の強さが安定度/士気へ与える倍率。
        /// devotion=0 で socialBase、devotion=1 で socialBase+socialGain（基準を上書きしない実効倍率）。
        /// </summary>
        public static float SocialEffect(Religion r, ReligionParams p)
        {
            if (r == null) return p.socialBase;
            float t = Mathf.Clamp01(r.devotion);
            return p.socialBase + p.socialGain * t;
        }

        /// <summary>
        /// 聖戦圧力(0..1)の最小スタブ：両勢力の信仰の強さの積を基準に、聖地が係争中なら上乗せする。
        /// 双方が熱狂的＝対立が激しい。聖地非係争なら信仰の積のみ（低め）。
        /// </summary>
        public static float HolyWarPressure(float faithA, float faithB, bool holySiteContested, ReligionParams p)
        {
            float fa = Mathf.Clamp01(faithA);
            float fb = Mathf.Clamp01(faithB);
            float pressure = fa * fb;                   // 双方が強いほど対立が激しい
            if (holySiteContested) pressure += p.holySiteBonus;
            return Mathf.Clamp01(pressure);
        }
    }
}
