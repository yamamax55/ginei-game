namespace Ginei
{
    /// <summary>
    /// 動作確認用のサンプル開示連鎖（FND-4 #495）。秘史の断片→古代の真相→エンディング解放、という
    /// 「条件→開示→（前提が満ちて）次の開示」を示す。実際の世界観（秘史#450・エンディング#458 等）は
    /// 各EPICがこの形で <see cref="DisclosureEntry"/> を足すだけで乗る。
    /// 一貫した payload（<see cref="Chronicle"/>）で条件入力と効果出力を扱う（条件＝<c>fragmentFound</c>、効果＝<c>endingUnlocked</c>）。
    /// </summary>
    public static class SampleDisclosures
    {
        public const string Fragment = "secret_fragment";
        public const string Truth = "ancient_truth";
        public const string Ending = "ending_unlocked";

        /// <summary>断片の発見（条件：<see cref="Chronicle.fragmentFound"/> が立つ＝探索などで真に）。</summary>
        public static DisclosureEntry SecretFragment()
        {
            return new DisclosureEntry(Fragment, "秘史の断片", "古き記録の一片が見つかった。", "秘史")
                .When(ctx => ctx != null && ctx.payload is Chronicle c && c.fragmentFound);
        }

        /// <summary>古代の真相（前提：断片が開示）。前提さえ満ちれば自動で開示。</summary>
        public static DisclosureEntry AncientTruth()
        {
            return new DisclosureEntry(Truth, "古代の真相", "断片はひとつの真実を指し示していた。", "真相")
                .Requires(Fragment);
        }

        /// <summary>エンディング解放（前提：真相が開示）。開示時に効果でエンディングを記録する。</summary>
        public static DisclosureEntry EndingUnlock()
        {
            return new DisclosureEntry(Ending, "新たな歴史へ", "真実を知った者たちは、次の時代を選び取った。", "エンディング")
                .Requires(Truth)
                .OnReveal(ctx => { if (ctx != null && ctx.payload is Chronicle c) c.endingUnlocked = true; });
        }

        /// <summary>サンプルの入出力（条件入力＝fragmentFound／効果出力＝endingUnlocked）。実装では世界状態が担う。</summary>
        public class Chronicle
        {
            public bool fragmentFound;
            public bool endingUnlocked;
        }
    }
}
