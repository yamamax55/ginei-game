using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 律令制の形骸化の段階（日本の律令制・官僚制基盤・史実参考）。朝廷の権威が落ちるほど官職・位階は
    /// 実権を失い名誉職化する。律令制（中央が機能）→摂関政治→院政→武家政権→戦国（官職は完全に名誉職）。
    /// <see cref="RitsuryoFormalizationRules.PhaseOf"/> が <see cref="CourtAuthority"/> から導く。
    /// </summary>
    public enum RitsuryoPhase
    {
        律令制,     // 中央集権が機能＝官職に実権が伴う
        摂関政治,   // 朝廷内の実権移動（官職と実力が緩み始める）
        院政,       // 公的官職の外に実権（名実の乖離が進む）
        武家政権,   // 実権は武家（封建）へ＝官職は権威付け
        戦国        // 官職は完全に名誉職＝実権は在地の封建領主のみ
    }

    /// <summary>
    /// 朝廷の権威（純データ・官僚制基盤）。律令の官職・位階（名）にどれだけ実権（実）が伴うかを左右する
    /// <b>盤面で唯一の中央権威</b>。1＝律令が完全に機能（任官＝実際の統治）、0＝完全に形骸化（官職は名誉職で、
    /// 実権は封建領主のみ＝<b>封建制のみ有効</b>）。諸侯（各勢力）の <see cref="Regime"/> 正統性とは別＝諸侯が
    /// 共有する中央朝廷の権威。解決は <see cref="RitsuryoFormalizationRules"/>（名実の乖離）が窓口。
    /// 史実では摂関政治・院政・武家政権・戦国と下がり続けた。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class CourtAuthority
    {
        /// <summary>朝廷の権威 0..1（1＝律令が機能・0＝形骸化＝封建のみ実効）。</summary>
        public float authority = 1f;

        public CourtAuthority() { }

        public CourtAuthority(float authority)
        {
            this.authority = Mathf.Clamp01(authority);
        }

        /// <summary>権威を増減（武家台頭・院政で下がる／中興で上がる）。0..1 にクランプ。</summary>
        public void Shift(float delta)
        {
            authority = Mathf.Clamp01(authority + delta);
        }
    }
}
