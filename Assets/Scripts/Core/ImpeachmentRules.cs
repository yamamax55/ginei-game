using UnityEngine;

namespace Ginei
{
    /// <summary>弾劾・不信任の調整係数。</summary>
    public readonly struct ImpeachmentParams
    {
        /// <summary>罷免に必要な議席（票）の割合（0..1・既定2/3＝特別多数）。</summary>
        public readonly float requiredShare;
        /// <summary>党派忠誠の壁の強さ（0..1）。1なら完全忠誠の与党が壁となり証拠が完璧でも成立0。</summary>
        public readonly float loyaltyWall;
        /// <summary>弱い訴追の失敗が政権を強化する逆風の最大量（魔女狩り効果）。</summary>
        public readonly float backlashScale;
        /// <summary>強い訴追の失敗が世論に燻り続ける（政権を蝕む）最大量。</summary>
        public readonly float smolderScale;

        public ImpeachmentParams(float requiredShare, float loyaltyWall,
                                 float backlashScale, float smolderScale)
        {
            this.requiredShare = Mathf.Clamp01(requiredShare);
            this.loyaltyWall = Mathf.Clamp01(loyaltyWall);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.smolderScale = Mathf.Max(0f, smolderScale);
        }

        /// <summary>既定＝必要議席2/3・党派の壁0.7・魔女狩り逆風0.5・燻り0.2。</summary>
        public static ImpeachmentParams Default => new ImpeachmentParams(2f / 3f, 0.7f, 0.5f, 0.2f);
    }

    /// <summary>
    /// 弾劾・不信任の純ロジック＝<b>合法的な政権打倒経路</b>。<see cref="CoupRules"/>（非合法打倒）の制度版。
    /// 要件は二つ＝証拠（訴追の強さ）と議席（特別多数）。だが法廷は政治の中にある：
    /// 証拠が強くても党派忠誠が壁となり成立を阻む（政治的裁判）。そして
    /// **弾劾は外科手術＝失敗すれば患者でなく執刀医が死ぬ**：弱い訴追の失敗は「魔女狩り」と映り
    /// 政権をむしろ強化し、強い訴追の失敗だけが世論に燻って政権を蝕む（非対称な逆風）。
    /// 成立した罷免の正統性は手続きの正しさがゲート＝次の政権の足場になる。
    /// 乱数は呼び出し側が roll(0..1) を渡す＝決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ImpeachmentRules
    {
        /// <summary>
        /// 訴追の強さ（0..1）＝証拠の質 evidenceQuality(0..1)×罪の重さ offenseSeverity(0..1)。
        /// 完璧な証拠でも軽微な罪なら弱く、大罪でも証拠が無ければ弱い＝両方そろって初めて立つ。
        /// </summary>
        public static float CaseStrength(float evidenceQuality, float offenseSeverity)
        {
            return Mathf.Clamp01(evidenceQuality) * Mathf.Clamp01(offenseSeverity);
        }

        /// <summary>
        /// 議席要件（特別多数）を満たすか＝賛成割合 supportShare(0..1) ≥ 必要割合 requiredShare(0..1)。
        /// 弾劾は単純多数では足りない設計が普通＝既定2/3。
        /// </summary>
        public static bool VoteThresholdMet(float supportShare, float requiredShare)
        {
            return Mathf.Clamp01(supportShare) >= Mathf.Clamp01(requiredShare);
        }

        public static bool VoteThresholdMet(float supportShare)
            => VoteThresholdMet(supportShare, ImpeachmentParams.Default.requiredShare);

        /// <summary>
        /// 成立確率（0..1）＝議席要件を満たさなければ0。満たせば
        /// 訴追の強さ caseStrength(0..1)×(1−loyaltyWall×党派忠誠 partisanLoyalty(0..1))。
        /// 証拠が完璧でも与党が結束していれば成立は遠い＝弾劾は法廷でなく政治的裁判。
        /// </summary>
        public static float ConvictionChance(float caseStrength, float supportShare,
                                             float partisanLoyalty, ImpeachmentParams p)
        {
            if (!VoteThresholdMet(supportShare, p.requiredShare)) return 0f;
            float wall = 1f - p.loyaltyWall * Mathf.Clamp01(partisanLoyalty);
            return Mathf.Clamp01(caseStrength) * wall;
        }

        public static float ConvictionChance(float caseStrength, float supportShare, float partisanLoyalty)
            => ConvictionChance(caseStrength, supportShare, partisanLoyalty, ImpeachmentParams.Default);

        /// <summary>
        /// 罷免成立か＝roll(0..1) &lt; chance。乱数は呼び出し側が渡す決定論判定。
        /// </summary>
        public static bool Convicted(float chance, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 失敗した弾劾の逆風（符号付き・政権の強化量）＝
        /// backlashScale×(1−caseStrength) − smolderScale×caseStrength。
        /// 弱い訴追の失敗ほど「魔女狩り」と映り政権を強化（正・最大 backlashScale）。
        /// 強い訴追の失敗だけが世論に燻って政権を蝕む（負・最大 −smolderScale）。
        /// 既定では強化幅＞燻り幅＝失敗の代償は執刀医（訴追側）に大きく非対称＝
        /// 中途半端な訴追の失敗でもなお政権は強くなる（外科手術の掟）。
        /// </summary>
        public static float FailedImpeachmentBacklash(float caseStrength, ImpeachmentParams p)
        {
            float s = Mathf.Clamp01(caseStrength);
            return p.backlashScale * (1f - s) - p.smolderScale * s;
        }

        public static float FailedImpeachmentBacklash(float caseStrength)
            => FailedImpeachmentBacklash(caseStrength, ImpeachmentParams.Default);

        /// <summary>
        /// 罷免の正統性（0..1）＝訴追の強さ caseStrength(0..1)×手続きの正しさ dueProcess(0..1)。
        /// 数の力で押し切った罷免（dueProcess=0）はクーデターの法服版＝次の政権の足場にならない。
        /// 手続きが正しいほど、罷免は報復でなく法の執行として受け入れられる。
        /// </summary>
        public static float LegitimacyOfRemoval(float caseStrength, float dueProcess)
        {
            return Mathf.Clamp01(caseStrength) * Mathf.Clamp01(dueProcess);
        }
    }
}
