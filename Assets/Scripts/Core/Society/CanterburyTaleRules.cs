namespace Ginei
{
    /// <summary>
    /// 社交場の「語り」（カンタベリー物語のエッセンス）。チョーサー『カンタベリー物語』に倣い、
    /// 宿に集った様々な身分の者が、各々の人となりを映した一席を語る——という枠物語を、社交場の
    /// 出席者に与える。語りの内容は提督の<b>得意素養</b>から物語のジャンル（騎士の物語／学僧の物語…）を選び、
    /// 名と素養から<b>決定論</b>で寸言を引く（同じ人物は常に同じ語り＝再現可能）。純フレーバー・状態は持たない。
    /// 観測層 <see cref="SocialVenueOverlay"/> が描画する。test-first。
    /// </summary>
    public static class CanterburyTaleRules
    {
        /// <summary>カンタベリー物語の語りのジャンル（代表的な巡礼者の物語から抜粋）。</summary>
        public enum TaleGenre
        {
            騎士の物語,       // Knight's Tale＝武勲・騎士道・名誉と恋
            郷士の物語,       // Franklin's Tale＝高潔・約束・気前のよさ
            粉屋の物語,       // Miller's Tale＝粗野で滑稽な悪戯話
            学僧の物語,       // Clerk's Tale＝忍耐・学識（グリゼルダの貞節）
            商人の物語,       // Merchant's Tale＝世故・結婚の機微
            免罪符売りの物語, // Pardoner's Tale＝強欲は諸悪の根源（Radix malorum est cupiditas）
            バースの女房の物語, // Wife of Bath's Tale＝主導権・人の真の望み
            尼僧院長の物語,   // Prioress's Tale＝敬虔・慈悲
        }

        /// <summary>ジャンルの表示名（『』なし）。</summary>
        public static string GenreName(TaleGenre g)
        {
            switch (g)
            {
                case TaleGenre.騎士の物語: return "騎士の物語";
                case TaleGenre.郷士の物語: return "郷士の物語";
                case TaleGenre.粉屋の物語: return "粉屋の物語";
                case TaleGenre.学僧の物語: return "学僧の物語";
                case TaleGenre.商人の物語: return "商人の物語";
                case TaleGenre.免罪符売りの物語: return "免罪符売りの物語";
                case TaleGenre.バースの女房の物語: return "バースの女房の物語";
                default: return "尼僧院長の物語";
            }
        }

        // 得意素養ごとに似合うジャンル2種（seed で択一）。
        private static readonly TaleGenre[] Valor   = { TaleGenre.騎士の物語, TaleGenre.粉屋の物語 };       // 武勇
        private static readonly TaleGenre[] Cunning = { TaleGenre.学僧の物語, TaleGenre.免罪符売りの物語 }; // 知略
        private static readonly TaleGenre[] Command = { TaleGenre.騎士の物語, TaleGenre.郷士の物語 };       // 統率
        private static readonly TaleGenre[] Govern  = { TaleGenre.商人の物語, TaleGenre.バースの女房の物語 }; // 政務

        /// <summary>得意素養に似合う語りのジャンルを seed で選ぶ（決定論）。</summary>
        public static TaleGenre GenreFor(TalentAspect dominant, int seed)
        {
            int s = seed & 0x7fffffff;
            switch (dominant)
            {
                case TalentAspect.武勇: return Valor[s % Valor.Length];
                case TalentAspect.知略: return Cunning[s % Cunning.Length];
                case TalentAspect.統率: return Command[s % Command.Length];
                default: return Govern[s % Govern.Length];
            }
        }

        // ジャンルごとの寸言（モラル/あらまし）。seed で択一＝同一人物は同一の語り。
        private static string[] Fragments(TaleGenre g)
        {
            switch (g)
            {
                case TaleGenre.騎士の物語: return new[]
                {
                    "誉れは死よりも重く、二人の勇者が一人の姫を争うて、運命の采は神の手にありと説く。",
                    "戦場で示すべきは勇のみにあらず、敗者への礼節こそ騎士の証と語る。",
                };
                case TaleGenre.郷士の物語: return new[]
                {
                    "交わした約束は岩より固し——気前のよさと信義が、ついには三者を救うと結ぶ。",
                    "真の貴さは血筋でなく行いに宿る、と穏やかに諭す。",
                };
                case TaleGenre.粉屋の物語: return new[]
                {
                    "酒場の悪戯と取り違えの一夜を、下世話に、しかし陽気に笑い飛ばす。",
                    "賢しらぶる者ほど足をすくわれる、と粗野な笑い話で締める。",
                };
                case TaleGenre.学僧の物語: return new[]
                {
                    "忍耐は時に過酷な試練となるが、貞節は最後に報われると静かに語る。",
                    "学識は誇るためでなく仕えるためにある、と書を引いて説く。",
                };
                case TaleGenre.商人の物語: return new[]
                {
                    "老いた夫と若い妻の機微を、世故に長けた皮肉で描いてみせる。",
                    "甘い言葉の裏を読めぬ者は損をする、と商人らしく勘定する。",
                };
                case TaleGenre.免罪符売りの物語: return new[]
                {
                    "「強欲は諸悪の根源」と説きつつ、宝を求めた三人が互いに滅ぶ顛末を語る。",
                    "死を探しに出た者が見つけたのは黄金、そして黄金こそが死であった、と警句を残す。",
                };
                case TaleGenre.バースの女房の物語: return new[]
                {
                    "人が真に望むのは己が事を己で決める自由だ——主導権の物語を快活に語る。",
                    "経験こそ最良の師、と五度の結婚を引いて笑う。",
                };
                default: return new[]
                {
                    "幼き者の無垢な祈りが奇跡を呼ぶ、敬虔と慈悲の小話を捧げる。",
                    "驕る強さより、ささやかな善意が世を照らすと結ぶ。",
                };
            }
        }

        /// <summary>その語り手の一席（決定論）。「『ジャンル』を語る——寸言」を返す。teller 空でも安全。</summary>
        public static string Tale(TaleGenre genre, int seed)
        {
            var frags = Fragments(genre);
            int s = (seed >> 3) & 0x7fffffff;
            return $"『{GenreName(genre)}』を語る——{frags[s % frags.Length]}";
        }

        /// <summary>得意素養から語りを一発で組む（ジャンル選択＋寸言）。</summary>
        public static string Tale(TalentAspect dominant, int seed)
            => Tale(GenreFor(dominant, seed), seed);

        // 宿の主人（Host・ハリー・ベイリー）の口上。seed で択一。
        private static readonly string[] HostLines =
        {
            "宿の主人が一同に呼びかける——「道中の徒然に、各々ひとつ物語を披露されよ。最も見事な語りには一献進ぜよう」",
            "亭主が杯を掲げて言う——「身分も旗も今宵は脇に置き、語りの妙で勝負と参ろう」",
            "主人がにこやかに促す——「巡礼の習いに倣い、銘々の一席を。退屈は最大の敵なれば」",
        };

        /// <summary>宿の主人の口上（語りの会の枠＝Host のフレーミング）。決定論。</summary>
        public static string HostRemark(int seed) => HostLines[(seed & 0x7fffffff) % HostLines.Length];
    }
}
