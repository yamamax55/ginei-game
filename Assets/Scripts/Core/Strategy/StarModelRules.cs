namespace Ginei
{
    /// <summary>
    /// 星系名から恒星3Dモデルを選ぶ（#恒星モデル）。名前ごとに固有の FBX
    /// （<c>Models/Stars/Star_00</c>〜）を用意してあり、<see cref="MountainSystemNames.Names"/> の
    /// <b>並び順がそのまま連番</b>になっている。ここは「どの名前がどの番号か」だけを決める純ロジック
    /// ＝星系id・所有・回廊・セーブには一切触れない（見た目の選択のみ）。
    ///
    /// 名前は<b>完全一致</b>で引く。プールに無い名前（将来の追加名・セーブ由来の別名）は
    /// 名前のハッシュから決定論的に1つへ割り当てる＝同じ名前なら毎回同じ恒星に見える（起動ごとに変わらない）。
    /// </summary>
    public static class StarModelRules
    {
        /// <summary>Resources 配下の恒星モデルの置き場（末尾に連番が付く）。</summary>
        public const string ResourceFolder = "Models/Stars/Star_";

        /// <summary>モデルの総数（名前プールと1対1）。</summary>
        public static int ModelCount => MountainSystemNames.Count;

        /// <summary>
        /// 星系名に対応するモデル番号。プールにあれば その並び順、無ければ名前から決定論的に選ぶ。
        /// 空名やプールが空のときは 0（先頭）＝必ず何かに解決して描画が欠けないようにする。
        /// </summary>
        public static int IndexForName(string systemName)
        {
            int count = ModelCount;
            if (count <= 0) return 0;
            if (string.IsNullOrEmpty(systemName)) return 0;

            string[] names = MountainSystemNames.Names;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], systemName)) return i;

            return StableIndex(systemName, count);
        }

        /// <summary>名前から決定論的に 0..count-1 を作る（FNV-1a・起動やロード順に依存しない）。</summary>
        public static int StableIndex(string text, int count)
        {
            if (count <= 0) return 0;
            if (string.IsNullOrEmpty(text)) return 0;

            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    h ^= text[i];
                    h *= 16777619u;
                }
                return (int)(h % (uint)count);
            }
        }

        /// <summary>モデル番号から Resources のパス（例：3 → "Models/Stars/Star_03"）。範囲外は巡回。</summary>
        public static string ResourcePathFor(int index)
        {
            int count = ModelCount;
            int idx = count > 0 ? ((index % count) + count) % count : 0;
            return ResourceFolder + idx.ToString("00");
        }

        /// <summary>星系名から直接 Resources のパスを引く（呼び出し側の1行窓口）。</summary>
        public static string ResourcePathForName(string systemName) => ResourcePathFor(IndexForName(systemName));
    }
}
