using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 自艦隊への予算増額の具申（建白）の effectKey を扱う純ロジック（<see cref="FleetEstablishmentRules"/> の
    /// effectKey 直列化パターンを踏襲）。執務机（<see cref="ProtagonistDeskOverlay"/>）からの具申を稟議
    /// （<see cref="PersonRingiRules.RaiseTo"/>）へ載せるとき、対象艦隊番号と増額を effectKey に直列化し、
    /// 採用時に decode して反映できるようにする唯一の窓口。数値（兵力等）は呼び出し側が渡す＝二重実装しない。test-first。
    /// </summary>
    public static class FleetBudgetPetitionRules
    {
        /// <summary>effectKey の接頭辞（"fleet.budget:番号:増額"）。番号0＝自艦隊（特定しない）。</summary>
        public const string Prefix = "fleet.budget:";

        /// <summary>増額の既定割合（兵力ベースの運用予算に対し ＋20% を目安に具申）。</summary>
        public const float DefaultIncreaseFraction = 0.2f;

        /// <summary>effectKey を組む（"fleet.budget:3:5000"）。増額は非負。</summary>
        public static string EncodeEffectKey(int fleetNumber, int amount)
            => Prefix + Mathf.Max(0, fleetNumber) + ":" + Mathf.Max(0, amount);

        /// <summary>この effectKey が艦隊予算具申か。</summary>
        public static bool IsBudgetPetition(string effectKey)
            => !string.IsNullOrEmpty(effectKey) && effectKey.StartsWith(Prefix);

        /// <summary>effectKey から艦隊番号と増額を取り出す（不正は false・out は0）。</summary>
        public static bool TryDecode(string effectKey, out int fleetNumber, out int amount)
        {
            fleetNumber = 0; amount = 0;
            if (!IsBudgetPetition(effectKey)) return false;
            string rest = effectKey.Substring(Prefix.Length);
            string[] parts = rest.Split(':');
            if (parts.Length != 2) return false;
            return int.TryParse(parts[0], out fleetNumber) & int.TryParse(parts[1], out amount);
        }

        /// <summary>具申する増額の目安＝運用予算（兵力相当）×割合（非負整数）。</summary>
        public static int RecommendedIncrease(int strengthBudget, float fraction)
            => Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, strengthBudget) * Mathf.Max(0f, fraction)));

        public static int RecommendedIncrease(int strengthBudget)
            => RecommendedIncrease(strengthBudget, DefaultIncreaseFraction);
    }
}
