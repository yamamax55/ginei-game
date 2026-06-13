using UnityEngine;

namespace Ginei
{
    /// <summary>内戦の調整係数。</summary>
    public readonly struct CivilWarParams
    {
        /// <summary>経済崩壊の進行速度（per duration×強度・双方が痩せる）。</summary>
        public readonly float collapseRate;
        /// <summary>経済崩壊の上限（焦土にも底はある）。</summary>
        public readonly float maxCollapse;
        /// <summary>対外防衛の空き係数（1超＝内戦は注いだ戦力以上に外を空ける）。</summary>
        public readonly float vulnerabilityScale;
        /// <summary>厭戦の蓄積速度（per duration）。</summary>
        public readonly float exhaustionRate;
        /// <summary>優勢側への雪崩（バンドワゴン）の急峻さ（指数・1以上）。</summary>
        public readonly float bandwagonExponent;

        public CivilWarParams(float collapseRate, float maxCollapse, float vulnerabilityScale, float exhaustionRate, float bandwagonExponent)
        {
            this.collapseRate = Mathf.Max(0f, collapseRate);
            this.maxCollapse = Mathf.Clamp01(maxCollapse);
            this.vulnerabilityScale = Mathf.Max(0f, vulnerabilityScale);
            this.exhaustionRate = Mathf.Max(0f, exhaustionRate);
            this.bandwagonExponent = Mathf.Max(1f, bandwagonExponent);
        }

        /// <summary>既定＝崩壊速度0.02・崩壊上限0.8・対外空き係数1.25・厭戦0.01・雪崩指数2。</summary>
        public static CivilWarParams Default => new CivilWarParams(0.02f, 0.8f, 1.25f, 0.01f, 2f);
    }

    /// <summary>
    /// 内戦の純ロジック（リップシュタット型＝国内二分の長期分裂戦争）。国が二つに割れると経済は双方まとめて
    /// 痩せ（内戦に勝者の無傷はない）、注ぎ込んだ戦力以上に対外防衛が空く＝外敵にとって最大の好機。
    /// 勝者は総取りだが、長い内戦の果ての勝者は荒野の王＝統一の実りは戦った年月で目減りする。
    /// 優勢が見えれば諸侯は雪崩を打って勝ち馬に乗る（バンドワゴン）。
    /// クーデター（<see cref="CoupRules"/>＝一夜の権力奪取・短期決着）とは別系統＝こちらは月年単位の消耗戦。
    /// 旗幟・寝返りの個別解決は <see cref="LoyaltyRules"/>（関ヶ原型）が担い、ここは国家規模の帰結のみ扱う。
    /// 倍率は産出・防衛係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CivilWarRules
    {
        /// <summary>
        /// 経済崩壊度（0..maxCollapse）＝継続時間×戦闘強度(0..1)×崩壊速度。内戦が長く激しいほど
        /// 国土が荒れる。双方共通＝どちらの陣営の経済もこの分だけ痩せる（内戦に無傷の側はない）。
        /// </summary>
        public static float EconomicCollapse(float duration, float intensity, CivilWarParams p)
        {
            return Mathf.Min(p.maxCollapse, p.collapseRate * Mathf.Max(0f, duration) * Mathf.Clamp01(intensity));
        }

        public static float EconomicCollapse(float duration, float intensity)
            => EconomicCollapse(duration, intensity, CivilWarParams.Default);

        /// <summary>双方の産出倍率（1−経済崩壊度）。両陣営の産出係数に掛けて使う。</summary>
        public static float EconomicOutputFactor(float duration, float intensity, CivilWarParams p)
        {
            return 1f - EconomicCollapse(duration, intensity, p);
        }

        public static float EconomicOutputFactor(float duration, float intensity)
            => EconomicOutputFactor(duration, intensity, CivilWarParams.Default);

        /// <summary>
        /// 対外的な無防備度（0..1）＝内戦に注いだ戦力比(0..1)×空き係数。係数が1超なので
        /// 注いだ分「以上」に外が空く＝内戦中の国は外敵にとって最大の好機（既定では8割投入で全開）。
        /// </summary>
        public static float ExternalVulnerability(float committedForces, CivilWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(committedForces) * p.vulnerabilityScale);
        }

        public static float ExternalVulnerability(float committedForces)
            => ExternalVulnerability(committedForces, CivilWarParams.Default);

        /// <summary>対外防衛倍率（1−無防備度）。国境防衛の係数に掛けて使う。</summary>
        public static float BorderDefenseFactor(float committedForces, CivilWarParams p)
        {
            return 1f - ExternalVulnerability(committedForces, p);
        }

        public static float BorderDefenseFactor(float committedForces)
            => BorderDefenseFactor(committedForces, CivilWarParams.Default);

        /// <summary>厭戦度（0..1）＝継続時間×蓄積速度。長引く内戦は双方の民を疲れさせる。</summary>
        public static float WarExhaustion(float duration, CivilWarParams p)
        {
            return Mathf.Clamp01(Mathf.Max(0f, duration) * p.exhaustionRate);
        }

        public static float WarExhaustion(float duration) => WarExhaustion(duration, CivilWarParams.Default);

        /// <summary>
        /// 勝者の統一実効度（0..1）＝勝者の趨勢シェア(0..1)×（1−厭戦度）。勝者総取りだが、
        /// 長い内戦の勝者が手にするのは荒野＝即決の勝者だけが無傷の国を継ぐ。
        /// </summary>
        public static float VictorConsolidation(float victorShare, float warDuration, CivilWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(victorShare) * (1f - WarExhaustion(warDuration, p)));
        }

        public static float VictorConsolidation(float victorShare, float warDuration)
            => VictorConsolidation(victorShare, warDuration, CivilWarParams.Default);

        /// <summary>
        /// 優勢側への雪崩圧力（0..1）＝拮抗(0.5)では0、優勢が見えるほど指数で加速＝バンドワゴン。
        /// winningSideShare は優勢側の趨勢シェア(0..1)。0.5以下（劣勢・拮抗）は雪崩なし。
        /// </summary>
        public static float DefectionMomentum(float winningSideShare, CivilWarParams p)
        {
            float lead = (Mathf.Clamp01(winningSideShare) - 0.5f) * 2f;
            if (lead <= 0f) return 0f;
            return Mathf.Pow(lead, p.bandwagonExponent);
        }

        public static float DefectionMomentum(float winningSideShare)
            => DefectionMomentum(winningSideShare, CivilWarParams.Default);
    }
}
