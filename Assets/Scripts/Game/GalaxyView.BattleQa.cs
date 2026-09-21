namespace Ginei
{
    public partial class GalaxyView
    {
        /// <summary>WIN-4 の結果直列反映を、シーン構築なしで実経路検証するための入口。</summary>
        public void BindBattleRegistryForQa(StrategicFleetRegistry registry)
        {
            reg = registry;
            map = registry != null ? registry.map : null;
        }

        /// <summary>キュー先頭の会戦結果を、実ゲームと同じ経路で1件だけ反映する。</summary>
        public void DrainOneBattleResultForQa() => DrainBattleResults();
    }
}
