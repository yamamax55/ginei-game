namespace Ginei
{
    /// <summary>
    /// 海賊狩りの具申（下級士官の腕試し）の effectKey と報酬を扱う純ロジック。少尉/大尉が執務机から出す
    /// 「海賊掃討」の任を稟議（<see cref="PersonRingiRules.RaiseTo"/>）へ載せ、採用されれば武名が上がる。
    /// 他の具申（<see cref="FleetBudgetPetitionRules"/> 等）と同じ effectKey 直列化の作法。test-first。
    /// </summary>
    public static class PirateHuntPetitionRules
    {
        /// <summary>海賊狩り具申の effectKey（パラメータ無しの定数）。</summary>
        public const string EffectKey = "mission.pirate";

        /// <summary>採用されたときに上がる武名（若手の腕試し＝名を売る）。</summary>
        public const int FameReward = 3;

        /// <summary>この effectKey が海賊狩り具申か。</summary>
        public static bool IsPiratePetition(string effectKey) => effectKey == EffectKey;
    }
}
