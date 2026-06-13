namespace Ginei
{
    /// <summary>
    /// 職業の小分類エントリ（<b>日本標準職業分類 JSOC の小分類を参考</b>・#110 標準化・純データ）。
    /// 1つの小分類＝コード（中分類プレフィックスの3桁文字列）＋名称＋親の中分類コード（1〜73）＋本作固有フラグ。
    /// <b>全329小分類を網羅せず、現在この作品に存在する職業＋この宇宙設定でありえる職業に絞った curated な参照辞書</b>
    /// （大分類11→中分類73→小分類の階層を保つ lookup taxonomy であって、POP のシミュレーション状態ではない＝タイクン回避）。
    /// 台帳は <see cref="JsocMinorClassification"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class JsocMinorGroup
    {
        public string code;     // 小分類コード（中分類2桁＋連番1桁の3桁文字列・例 "531"）
        public string name;     // 小分類名
        public int middleCode;  // 親の中分類コード（1〜73・JsocMiddleClassification）
        public bool isSetting;  // 本作固有（この宇宙設定で足した職業＝JSOC 由来でない拡張）

        public JsocMinorGroup() { }

        public JsocMinorGroup(string code, string name, int middleCode, bool isSetting = false)
        {
            this.code = code;
            this.name = name;
            this.middleCode = middleCode;
            this.isSetting = isSetting;
        }

        public override string ToString() => $"{code} {name}";
    }
}
