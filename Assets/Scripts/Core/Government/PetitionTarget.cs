namespace Ginei
{
    /// <summary>決裁が名指しする対象の種類。</summary>
    public enum PetitionTargetKind
    {
        /// <summary>対象を名指ししない（税率など、勢力全体に効くもの）。</summary>
        なし,
        星系,
        惑星,
        勢力,
        艦隊,
        造船所,
    }

    /// <summary>
    /// 決裁が<b>提案の時点で名指しした対象</b>（星系・惑星・相手勢力など）。
    ///
    /// <b>なぜ要るか</b>：従来は「大攻勢」を承認したあと、執行の瞬間に改めて目標を選んでいた。
    /// これだと決裁前に見せた対象と、実際に攻めた先が食い違いうる＝<b>承認した内容と違うことが起きる</b>。
    /// 対象は提案の時点で決めてここに固定し、執行はこの対象だけを使う。
    /// 執行時に対象が失われていたら<b>別の対象へ勝手に振り替えず、失敗として返す</b>。
    /// </summary>
    public readonly struct PetitionTarget
    {
        public readonly PetitionTargetKind kind;
        /// <summary>対象のID（星系ID・艦隊ID など）。<see cref="PetitionTargetKind.勢力"/> は (int)Faction。</summary>
        public readonly int id;
        /// <summary>表示名（決裁前後で同じものを見せる）。</summary>
        public readonly string name;

        public PetitionTarget(PetitionTargetKind kind, int id, string name)
        {
            this.kind = kind;
            this.id = id;
            this.name = name ?? "";
        }

        /// <summary>対象を名指ししていない（勢力全体に効く決裁）。</summary>
        public static PetitionTarget None => new PetitionTarget(PetitionTargetKind.なし, 0, "");

        /// <summary>名指しの対象があるか。</summary>
        public bool HasTarget => kind != PetitionTargetKind.なし;

        /// <summary>画面に出す1行（「対象：○○」の右側）。</summary>
        public string Label => HasTarget
            ? (string.IsNullOrEmpty(name) ? $"{kind} #{id}" : $"{name}（{kind}）")
            : "勢力全体";

        /// <summary>直列化用に "kind:id:name" へ（保存はこの1本を通す）。</summary>
        public string Encode() => $"{(int)kind}:{id}:{name}";

        /// <summary>直列化文字列から戻す（壊れていれば <see cref="None"/>）。</summary>
        public static PetitionTarget Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return None;
            int a = text.IndexOf(':');
            if (a <= 0) return None;
            int b = text.IndexOf(':', a + 1);
            if (b <= a) return None;

            if (!int.TryParse(text.Substring(0, a), out int k)) return None;
            if (!int.TryParse(text.Substring(a + 1, b - a - 1), out int id)) return None;
            return new PetitionTarget((PetitionTargetKind)k, id, text.Substring(b + 1));
        }
    }
}
