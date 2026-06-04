using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力（陣営）の定義データ（ScriptableObject）。将来的に enum Faction を置き換える土台。
    /// 名前・色・思想・敵対関係を持ち、3勢力以上を扱えるようにする。
    ///
    /// 後方互換：FactionData を割り当てない艦は従来どおり enum Faction で動作する
    /// （敵対判定・色は FactionData が無ければ enum にフォールバックする）。
    /// アセットは エディタメニュー `Ginei/Create Faction Data` で生成できる（`Resources/Factions/`）。
    /// </summary>
    [CreateAssetMenu(fileName = "NewFaction", menuName = "Ginei/Faction Data")]
    public class FactionData : ScriptableObject
    {
        [Header("基本情報")]
        [Tooltip("勢力名（表示・結果画面の勝者名に使用）")]
        public string factionName = "勢力名";

        [Tooltip("陣営色（艦体・ビーム・HUD・マーカーハロー等の唯一の出所）")]
        public Color color = Color.white;

        [Tooltip("思想・系統（王党派/民主派/共産/軍閥 等。表示・将来ロジック用）")]
        public string ideology = "";

        [Header("敵対関係")]
        [Tooltip("この勢力と敵対しない（味方・中立）勢力。ここに無い他勢力は既定で敵対する")]
        public List<FactionData> nonHostileFactions = new List<FactionData>();

        [Header("後方互換")]
        [Tooltip("旧 enum Faction との対応（既存UI・セーブ・プレイヤー陣営選択の橋渡し用）")]
        public Faction legacyFaction = Faction.帝国;

        /// <summary>
        /// other と敵対するか。自分自身・非敵対リスト内の勢力とは敵対しない。
        /// それ以外の異勢力は既定で敵対（従来の「陣営違い＝敵」を踏襲）。
        /// </summary>
        public bool IsHostileTo(FactionData other)
        {
            if (other == null || other == this) return false;
            if (nonHostileFactions != null && nonHostileFactions.Contains(other)) return false;
            return true;
        }
    }
}
