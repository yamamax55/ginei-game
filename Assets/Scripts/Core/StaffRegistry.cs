using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 参謀本部の台帳（参謀本部基盤・static）。<b>部隊参謀</b>（艦隊長〜軍団長のポスト鍵ごと）と
    /// <b>大本営参謀本部</b>（勢力ごとに1つ）を保持する唯一の窓口。`OrderOfBattle`#147（梯団ツリー）／
    /// `GovernmentRegistry`#142（政府人事）とは別レイヤー＝幕僚団の編制台帳。
    /// 数値ロジックは持たず <see cref="StaffRules"/> を読むだけ。会戦/戦役の開始時に <see cref="Clear"/>。
    /// </summary>
    public static class StaffRegistry
    {
        // 部隊参謀：ポスト鍵（例「帝国/第1艦隊」）→ Staff
        private static readonly Dictionary<string, Staff> fieldStaffs = new Dictionary<string, Staff>();
        // 大本営参謀本部：勢力 → Staff
        private static readonly Dictionary<Faction, Staff> generalStaffs = new Dictionary<Faction, Staff>();

        public static int FieldStaffCount => fieldStaffs.Count;
        public static int GeneralStaffCount => generalStaffs.Count;

        /// <summary>
        /// 部隊参謀を取得（無ければ作成）。RequiresFieldStaff を満たす梯団のみ作る（満たさなければ null）。
        /// </summary>
        public static Staff GetOrCreateFieldStaff(Faction faction, EchelonType echelon, string postKey)
        {
            if (string.IsNullOrEmpty(postKey)) return null;
            if (!StaffRules.RequiresFieldStaff(echelon)) return null; // 艦隊長〜軍団長のみ
            if (fieldStaffs.TryGetValue(postKey, out Staff s)) return s;
            s = new Staff(faction, StaffLevel.部隊参謀, echelon, postKey);
            fieldStaffs[postKey] = s;
            return s;
        }

        /// <summary>部隊参謀をポスト鍵で取得（無ければ null）。</summary>
        public static Staff GetFieldStaff(string postKey)
            => (!string.IsNullOrEmpty(postKey) && fieldStaffs.TryGetValue(postKey, out Staff s)) ? s : null;

        /// <summary>大本営参謀本部を取得（無ければ作成）。勢力ごとに1つ。</summary>
        public static Staff GetOrCreateGeneralStaff(Faction faction)
        {
            if (generalStaffs.TryGetValue(faction, out Staff s)) return s;
            s = new Staff(faction, StaffLevel.大本営参謀本部, EchelonType.宇宙艦隊, faction.ToString());
            generalStaffs[faction] = s;
            return s;
        }

        /// <summary>大本営参謀本部を取得（無ければ null）。</summary>
        public static Staff GeneralStaff(Faction faction)
            => generalStaffs.TryGetValue(faction, out Staff s) ? s : null;

        /// <summary>全台帳を空にする（会戦/戦役の作り直し）。</summary>
        public static void Clear()
        {
            fieldStaffs.Clear();
            generalStaffs.Clear();
        }
    }
}
