using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 職業の中分類エントリ（<b>日本標準職業分類 JSOC の中分類を参考</b>・#110 標準化・純データ）。
    /// 1つの中分類＝コード（01〜73）＋名称＋親の大分類（<see cref="OccupationCategory"/>）。
    /// <b>これは参照用の分類辞書（lookup taxonomy）であって、POP のシミュレーション状態ではない</b>＝Province に 73 幅の配列は持たせない
    /// （集約・タイクン回避＝シミュは6種〔<see cref="Occupation"/>〕のまま大分類で回す）。台帳は <see cref="JsocMiddleClassification"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class JsocMiddleGroup
    {
        public int code;                 // JSOC 中分類コード（1〜73）
        public string name;              // 中分類名（JSOC 準拠）
        public OccupationCategory major; // 親の大分類

        public JsocMiddleGroup() { }

        public JsocMiddleGroup(int code, string name, OccupationCategory major)
        {
            this.code = code;
            this.name = name;
            this.major = major;
        }

        /// <summary>ゼロ詰め2桁のコード文字列（"01"〜"73"）。</summary>
        public string CodeString => code.ToString("00");

        public override string ToString() => $"{CodeString} {name}";
    }
}
