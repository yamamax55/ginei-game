using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 星系内の惑星に与える固有名のプール（日本神話ベース・#惑星名）。`HeritageShipNames`（旗艦名）の惑星版＝
    /// 神々や神話的存在の名を惑星名として払い出す。割り当ては決定論（同じ星系・同じ軌道なら常に同じ名）で、
    /// 1つの星系の中では惑星名が重複しない。純データ＋純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MythicPlanetNames
    {
        /// <summary>惑星名プール（日本神話の神名・神話的存在。重複なし）。</summary>
        public static readonly string[] Names =
        {
            "イザナギ", "イザナミ", "アマテラス", "ツクヨミ", "スサノオ",
            "アメノウズメ", "サルタヒコ", "ニニギ", "コノハナサクヤ", "イワナガ",
            "ホオリ", "ホデリ", "トヨタマ", "タマヨリ", "ウガヤフキアエズ",
            "オオクニヌシ", "スクナビコナ", "タケミカヅチ", "フツヌシ", "アメノオシホミミ",
            "オモイカネ", "タヂカラオ", "アメノコヤネ", "フトダマ", "タケミナカタ",
            "コトシロヌシ", "オオヤマツミ", "ワタツミ", "カグツチ", "ククリヒメ",
            "アメノミナカヌシ", "タカミムスヒ", "カミムスヒ", "クニノトコタチ", "トヨウケ",
            "ウケモチ", "オオゲツヒメ", "ニギハヤヒ", "アメノホアカリ", "セオリツ",
            "シナツヒコ", "カヤノヒメ", "ハニヤス", "ミヅハノメ", "アメノワカヒコ",
            "シタテルヒメ", "ヤマトタケル", "オキナガタラシ", "タケウチ", "アヂスキタカヒコネ",
        };

        /// <summary>プールの件数。</summary>
        public static int Count => Names.Length;

        /// <summary>i 番目の名（負・超過は巡回）。プール空なら空文字。</summary>
        public static string At(int i)
        {
            if (Count == 0) return "";
            int idx = ((i % Count) + Count) % Count;
            return Names[idx];
        }

        /// <summary>
        /// 星系（<paramref name="systemId"/>）と軌道インデックス（<paramref name="orbitIndex"/>＝第1惑星=0）から
        /// 決定論的に惑星名を払い出す。<b>同じ星系の中では軌道ごとに異なる名</b>（プール件数 &gt; 惑星数の前提）。
        /// 星系が違えば開始位置がずれて別の名の並びになる（変化）。
        /// </summary>
        public static string For(int systemId, int orbitIndex)
        {
            if (Count == 0) return "";
            // 星系ごとに開始オフセットを散らす（決定論ハッシュ）。同一星系内は orbitIndex で連番＝重複しない。
            uint h = (uint)Mathf.Abs(systemId) * 2654435761u;
            int baseOffset = (int)(h % (uint)Count);
            return At(baseOffset + orbitIndex);
        }
    }
}
