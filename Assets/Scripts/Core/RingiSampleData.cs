using System.Collections.Generic;

namespace Ginei
{
    /// <summary>稟議のサンプル1件（決裁フローのテスト用データ）。題目・効果キー・宛先の箱・本文。
    /// 効果キーは必ず <see cref="PetitionEffects"/> に登録済みのものを使う（<see cref="RingiSampleData"/> がテストで担保）。</summary>
    public readonly struct RingiSample
    {
        public readonly string title;
        public readonly string effectKey;
        public readonly BoxKind box;
        public readonly string body;

        public RingiSample(string title, string effectKey, BoxKind box, string body)
        {
            this.title = title ?? "";
            this.effectKey = effectKey ?? "";
            this.box = box;
            this.body = body ?? "";
        }
    }

    /// <summary>
    /// 稟議の決裁フローを動かす<b>サンプルデータ</b>（建白の見本一覧）。<see cref="RingiDirector"/> が生起に使い、
    /// EditMode/PlayMode テストが決裁フロー（建白→伝播→決裁→執行→効果）を回すのに使う。効果キーは
    /// すべて <see cref="PetitionEffects"/> に登録済み（<c>RingiSampleDataTests</c> が整合を固定）＝サンプルだけで
    /// 世界が実際に動くことを保証する。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RingiSampleData
    {
        /// <summary>サンプル建白の一覧（決裁すると登録済み効果で世界が動く）。</summary>
        public static readonly IReadOnlyList<RingiSample> All = new List<RingiSample>
        {
            new RingiSample("減税の建白", "tax.cut", BoxKind.政治家,
                "重税に民が苦しんでいると政治家箱へ建白が上がった。減税すれば民心は和らぐが歳入は細る。財務官僚は難色。"),
            new RingiSample("増税の建白", "tax.hike", BoxKind.政治家,
                "国庫の窮迫を受け、政治家箱へ増税の建白が上がった。歳入は潤うが民の不満は高まる。"),
            new RingiSample("社会保障の拡充", "welfare.up", BoxKind.政治家,
                "困窮する民への手当を厚くせよ、との建白。民心は上向くが歳出は膨らむ。民部省は財源を問う。"),
            new RingiSample("専制の緩和（包摂）", "reform.inclusive", BoxKind.国王,
                "収奪に偏った統治を改め、民を政に与らせよ、との諫言が国王箱へ。包摂は安定の礎だが既得権益が抵抗する。"),
            new RingiSample("治安の強化", "order.tighten", BoxKind.国王,
                "辺境の騒擾を鎮めよ、との建白が国王箱へ。秩序は保たれるが力による抑圧は民心を蝕む。"),
        };

        public static int Count => All.Count;

        /// <summary>index 番目のサンプル（範囲外は先頭へ丸め）。</summary>
        public static RingiSample At(int index)
        {
            if (All.Count == 0) return default;
            if (index < 0 || index >= All.Count) index = 0;
            return All[index];
        }
    }
}
