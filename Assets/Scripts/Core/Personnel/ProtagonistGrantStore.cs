namespace Ginei
{
    /// <summary>
    /// 主人公が具申で勝ち取った効果（<see cref="PetitionGrants"/>）の単一保管庫（<see cref="FleetPool"/> 同様の static ストア）。
    /// 月次評定で採用された具申が <see cref="PetitionEffectRules.Apply"/> でここへ反映され、執務机・艦隊管理が読む。
    /// 主人公は1人ゆえ単一インスタンス。会戦セットアップ/テスト/一代記の幕切れで <see cref="Clear"/>。
    /// </summary>
    public static class ProtagonistGrantStore
    {
        private static PetitionGrants grants;

        /// <summary>主人公の拝領効果（無ければ生成）。</summary>
        public static PetitionGrants Grants => grants ?? (grants = new PetitionGrants());

        /// <summary>拝領効果を初期化する。</summary>
        public static void Clear() => grants?.Clear();
    }
}
