using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍事司法＝懲罰体制（STSH-2 #2357）の状態。各値は 0..1 にクランプして保持する。
    /// <list type="bullet">
    /// <item><description><see cref="severity"/>＝懲罰の苛烈さ（軽微な減給〜死刑）。</description></item>
    /// <item><description><see cref="enforcement"/>＝執行の確実さ（違反が確実に裁かれるか）。</description></item>
    /// <item><description><see cref="coverageScope"/>＝適用範囲（末端まで規律が及ぶか）。</description></item>
    /// <item><description><see cref="publicVisibility"/>＝懲罰の公開性（見せしめか内々か）。</description></item>
    /// </list>
    /// 数式は <see cref="MilitaryJusticeRules"/> が解決する（本クラスは値の入れ物）。直列化可。
    /// </summary>
    [System.Serializable]
    public class PunishmentRegime
    {
        /// <summary>懲罰の苛烈さ（0..1）。高いほど重罰（脱走抑止に効くが資源外コスト＝反感を生む）。</summary>
        public float severity;
        /// <summary>執行の確実さ（0..1）。高いほど違反が確実に裁かれる（恐怖の信頼性）。</summary>
        public float enforcement;
        /// <summary>適用範囲（0..1）。高いほど末端まで規律が行き渡る（部隊結束に効く）。</summary>
        public float coverageScope;
        /// <summary>懲罰の公開性（0..1）。高いほど見せしめ＝抑止も効くが反感も増幅する。</summary>
        public float publicVisibility;

        public PunishmentRegime() { }

        public PunishmentRegime(float severity, float enforcement, float coverageScope, float publicVisibility)
        {
            this.severity = Mathf.Clamp01(severity);
            this.enforcement = Mathf.Clamp01(enforcement);
            this.coverageScope = Mathf.Clamp01(coverageScope);
            this.publicVisibility = Mathf.Clamp01(publicVisibility);
        }
    }
}
