namespace Ginei
{
    /// <summary>
    /// 性別（人物 <see cref="Person"/>・POP <see cref="Population"/> の属性）。男性／女性。
    /// 人物は個別に持ち（既定=男性＝後方互換）、POP はマクロな男女比（<see cref="Population.femaleShare"/>）で持つ。
    /// 解決は <see cref="SexRules"/>。性的指向は別軸（隠しパラメータ案・<c>docs/gender-orientation-design.md</c> の検討項目＝現状未実装）。
    /// </summary>
    public enum Sex { 男性, 女性 }
}
