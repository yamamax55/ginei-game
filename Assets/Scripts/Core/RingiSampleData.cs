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

            // ----- 軍事・外交（決裁ゲーム MVP・戦略レイヤーへ効く） -----
            new RingiSample("全軍動員令", "mil.mobilize", BoxKind.国王,
                "参謀本部が総動員を上申。軍は厚みを増すが、国庫は軍費に削られ、徴兵に民は倦む。"),
            new RingiSample("前線への大攻勢", "mil.offensive", BoxKind.国王,
                "好機なり、と前線司令部が一大攻勢を具申。決まれば版図は広がるが、戦力を磨り減らし戦費も嵩む。"),
            new RingiSample("防衛線の強化", "mil.defend", BoxKind.地方,
                "要衝の守りを固めよ、との建白。前線は安定し統制は保たれるが、築城と駐留に費用がかかる。"),
            new RingiSample("敵国との講和", "diplo.ceasefire", BoxKind.政治家,
                "長きにわたる戦に疲れた民から、停戦を求める声。和せば民心は和らぐが、得た版図の一部は手放すことになる。"),
            new RingiSample("綱紀粛正（粛清）", "purge", BoxKind.国王,
                "中央に不穏分子あり、と密偵が報告。粛清すれば統制は固まるが、恐怖は民心を凍らせる。"),
            new RingiSample("大恩赦", "amnesty", BoxKind.政治家,
                "新帝の即位に際し恩赦を、との奏上。民心は大きく上向くが、罪を許すことは中央の統制を緩める。"),
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
