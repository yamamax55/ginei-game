namespace Ginei
{
    /// <summary>具申のタブ分類（執務机のカテゴリ化UI）。数が増えた具申をこの4タブに束ねる。</summary>
    public enum PetitionTab { 軍事, 人事, 政治, 個人 }

    /// <summary>
    /// 具申区分→タブの対応（純・決定論）。UI（<see cref="ProtagonistDeskOverlay"/> のタブ）と分類の唯一の窓口。
    /// 軍事＝予算/編制/作戦/海賊狩り、人事＝人事、政治＝政治、個人＝建白（汎用）。賄賂は区分外ゆえ UI 側で個人へ。test-first。
    /// </summary>
    public static class PetitionTabRules
    {
        /// <summary>具申区分が属するタブ。</summary>
        public static PetitionTab TabOf(PetitionCategory category)
        {
            switch (category)
            {
                case PetitionCategory.予算:
                case PetitionCategory.編制:
                case PetitionCategory.作戦:
                case PetitionCategory.海賊狩り: return PetitionTab.軍事;
                case PetitionCategory.人事: return PetitionTab.人事;
                case PetitionCategory.政治: return PetitionTab.政治;
                default: return PetitionTab.個人; // 建白（汎用）
            }
        }
    }
}
