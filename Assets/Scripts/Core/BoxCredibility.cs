using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 陳情先の「箱」＝権力主体のチャンネル（目安箱 MEYASU #1296）。
    /// <b>官僚箱は無い</b>＝官僚は民意を聞かない立場であり、陳情先ではなく執行の壁（MEYASU-3）。
    /// 地方箱の“中の人”（世襲貴族/選挙知事/任命知事）は地方自治 #1306 が供給する。
    /// </summary>
    public enum BoxKind { 国王, 政治家, 地方 }

    /// <summary>
    /// 目安箱への「信認」ストア（MEYASU-2 #1298）。箱が聞いてくれるのは“借り物の権威”（<see cref="ConsentRules"/>）。
    /// プレイヤーは個人と交友を持たないので、信認は人物ごとでなく<b>箱（身分/権力主体）ごと</b>に持つ
    /// ＝国王箱/政治家箱/地方箱。中央箱（国王/政治家）は勢力につき各1、地方箱は<b>地方スコープごとに疎</b>（触れた地方だけ・lazy）。
    /// <see cref="globalDeference"/> は国家規模の傾聴度（オラクルとしての地位＝失墜の指標）。
    /// 解決は <see cref="CredibilityRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class BoxCredibility
    {
        public Faction faction;

        /// <summary>箱キー→信認 0..1（触れた箱だけ・lazy/sparse）。キーは <see cref="CredibilityRules.Key"/> が生成。</summary>
        public readonly Dictionary<string, float> entries = new Dictionary<string, float>();

        /// <summary>国家規模の箱への傾聴度 0..1（全体の信頼＝オラクルとしての地位）。既定1。全体で枯れると失墜。</summary>
        public float globalDeference = 1f;

        public BoxCredibility() { }

        public BoxCredibility(Faction faction)
        {
            this.faction = faction;
        }
    }
}
