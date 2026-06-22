using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 立身出世の具申（執務机）の追加 effectKey と報酬量を扱う純ロジック（決定論）。既存の effectKey 直列化
    /// （<see cref="FleetBudgetPetitionRules"/> 等）と同作法で、採用時に <see cref="ProtagonistCareerDirector"/> が
    /// decode して<b>実効果</b>へ反映する：増援＝艦艇プール増、恩賞＝武名、減税＝税率減、名誉回復＝不満減。test-first。
    /// </summary>
    public static class CareerPetitionRules
    {
        // ── 増援要請（軍事・予算ゲート）：艦艇プールへ増援 ──
        public const string ReinforcePrefix = "fleet.reinforce:";
        /// <summary>増援の目安＝自分の指揮可能規模×割合（既定1/4）。</summary>
        public const float ReinforceFraction = 0.25f;
        public static int RecommendReinforce(int commandCapacity)
            => Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, commandCapacity) * ReinforceFraction));
        public static string EncodeReinforce(int amount) => ReinforcePrefix + Mathf.Max(0, amount);
        public static bool IsReinforce(string key) => !string.IsNullOrEmpty(key) && key.StartsWith(ReinforcePrefix);
        public static bool TryDecodeReinforce(string key, out int amount)
        {
            amount = 0;
            if (!IsReinforce(key)) return false;
            return int.TryParse(key.Substring(ReinforcePrefix.Length), out amount);
        }

        // ── 恩賞の上申（人事ゲート）：採用で武名↑（部下を遇する将は名が上がる） ──
        public const string HonorKey = "hr.honor";
        public const int HonorFameReward = 4;
        public static bool IsHonor(string key) => key == HonorKey;

        // ── 減税の建言（政治ゲート）：採用で自勢力の税率↓ ──
        public const string TaxCutKey = "gov.taxcut";
        public const float TaxCutAmount = 0.02f;
        public static bool IsTaxCut(string key) => key == TaxCutKey;

        // ── 名誉回復の嘆願（個人）：採用で不満↓ ──
        public const string ClearKey = "self.clear";
        public const float ClearGrievanceAmount = 20f;
        public static bool IsClear(string key) => key == ClearKey;
    }
}
