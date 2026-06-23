namespace Ginei
{
    /// <summary>
    /// 自艦隊の根拠地（母港/展開拠点）変更の具申（建白）の effectKey を扱う純ロジック
    /// （<see cref="FleetBudgetPetitionRules"/> と同じ effectKey 直列化パターン）。執務机
    /// （<see cref="ProtagonistDeskOverlay"/>）からの具申を稟議（<see cref="PersonRingiRules.RaiseTo"/>）へ載せるとき、
    /// 対象艦隊番号と希望根拠地（星系ID）を effectKey に直列化し、採用時に decode して反映できるようにする唯一の窓口。test-first。
    /// </summary>
    public static class FleetBasePetitionRules
    {
        /// <summary>effectKey の接頭辞（"fleet.base:番号:星系ID"）。番号0＝自艦隊、星系ID -1＝司令部一任（前線寄りへ）。</summary>
        public const string Prefix = "fleet.base:";

        /// <summary>一任（具体的な星系を指定せず根拠地変更を上申）を表す星系ID。</summary>
        public const int UnspecifiedSystem = -1;

        /// <summary>effectKey を組む（"fleet.base:0:-1"）。番号は非負、星系IDは -1 を許容。</summary>
        public static string EncodeEffectKey(int fleetNumber, int systemId)
            => Prefix + (fleetNumber < 0 ? 0 : fleetNumber) + ":" + systemId;

        /// <summary>この effectKey が根拠地変更具申か。</summary>
        public static bool IsBasePetition(string effectKey)
            => !string.IsNullOrEmpty(effectKey) && effectKey.StartsWith(Prefix);

        /// <summary>effectKey から艦隊番号と希望星系IDを取り出す（不正は false・out は番号0/星系 -1）。</summary>
        public static bool TryDecode(string effectKey, out int fleetNumber, out int systemId)
        {
            fleetNumber = 0; systemId = UnspecifiedSystem;
            if (!IsBasePetition(effectKey)) return false;
            string rest = effectKey.Substring(Prefix.Length);
            string[] parts = rest.Split(':');
            if (parts.Length != 2) return false;
            return int.TryParse(parts[0], out fleetNumber) & int.TryParse(parts[1], out systemId);
        }
    }
}
