namespace Ginei
{
    /// <summary>
    /// 動作確認用のサンプルイベント（#116）。通知1本＋2択1本。横断基盤がデータ駆動で「条件発火→選択肢→効果適用」
    /// まで通しで動くことを示す（内政 P-6 #115・政治 #14・戦略イベントの雛形）。効果は <see cref="EventContext.payload"/> に
    /// 渡した <see cref="ResourceStockpile"/> を増減する例。実イベントは各機能側が定義する。
    /// </summary>
    public static class SampleEvents
    {
        /// <summary>英雄登場（通知のみ・確認）。重み低め・一回限り。</summary>
        public static GameEventDef HeroAppears()
        {
            return new GameEventDef("hero_appears", "英雄、現る", "若き提督が頭角を現した。")
            {
                weight = 0.5f,
                repeatable = false
            }
            .AddChoice("おお！");
        }

        /// <summary>
        /// 補給危機（2択）。前線の備蓄が払底気味のとき発火。緊急増産（弾薬+）か、節約（消費減＝物資温存）。
        /// payload に前線の <see cref="ResourceStockpile"/> を渡す前提（無ければ効果は無害にスキップ）。
        /// </summary>
        public static GameEventDef SupplyCrisis()
        {
            return new GameEventDef("supply_crisis", "補給危機", "前線の備蓄が乏しい。いかにする。")
            {
                weight = 1f,
                repeatable = true,
                cooldown = 30f
            }
            .When(ctx => ctx != null && ctx.payload is ResourceStockpile s && s.IsDepleted)
            .AddChoice("緊急増産（弾薬+50）", ctx =>
            {
                if (ctx.payload is ResourceStockpile s) s.Add(ResourceType.弾薬, 50f);
            })
            .AddChoice("節約して耐える（物資+20）", ctx =>
            {
                if (ctx.payload is ResourceStockpile s) s.Add(ResourceType.物資, 20f);
            });
        }
    }
}
