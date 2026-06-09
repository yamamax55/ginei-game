using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 役職の任命台帳（政府役職システム GOV-1 #142・唯一の窓口）。勢力ごとに役職へ人物（<see cref="ICharacter"/>）を
    /// 任命/解任し、保持者・保持役職を引く。スコープ付き役職（星系総督）は<b>対象スコープのキー</b>（星系ID等）と
    /// セットで保持する。資格判定は <see cref="OfficeRules.CanHold"/> に委譲し、定員（<see cref="Office.slots"/>）を守る。
    /// 別レジストリを乱立させない（軍の <see cref="FleetRegistry"/>/<see cref="OrderOfBattle"/> と別物＝こちらは政府人事）。
    /// </summary>
    public static class GovernmentRegistry
    {
        /// <summary>1件の任命（誰が・どの役職に・どのスコープキーで就いているか）。</summary>
        public class Appointment
        {
            public Faction faction;
            public Office office;
            public ICharacter holder;
            public int scopeKey; // 星系総督なら星系ID等。国家スコープは0。
        }

        private static readonly List<Appointment> appointments = new List<Appointment>();

        public static IReadOnlyList<Appointment> Appointments => appointments;

        /// <summary>
        /// 人物を役職へ任命する。資格不足・定員超過・既任なら false。成功で true。
        /// </summary>
        public static bool TryAppoint(Faction faction, Office office, ICharacter holder, int scopeKey = 0)
        {
            if (office == null || holder == null) return false;
            if (!OfficeRules.CanHold(holder, office)) return false;
            if (IsAppointed(office, holder, scopeKey)) return false;

            int slots = office.slots > 0 ? office.slots : 1;
            if (HolderCount(office, scopeKey) >= slots) return false;

            appointments.Add(new Appointment { faction = faction, office = office, holder = holder, scopeKey = scopeKey });
            return true;
        }

        /// <summary>役職保持者を解任する（該当が無ければ false）。</summary>
        public static bool Dismiss(Office office, ICharacter holder, int scopeKey = 0)
        {
            for (int i = appointments.Count - 1; i >= 0; i--)
            {
                Appointment a = appointments[i];
                if (a.office == office && a.holder == holder && a.scopeKey == scopeKey)
                {
                    appointments.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>指定スコープキーで役職の最初の保持者を返す（無ければ null）。</summary>
        public static ICharacter GetHolder(Office office, int scopeKey = 0)
        {
            for (int i = 0; i < appointments.Count; i++)
                if (appointments[i].office == office && appointments[i].scopeKey == scopeKey)
                    return appointments[i].holder;
            return null;
        }

        /// <summary>指定スコープキーの役職保持者を全て返す（定員 N の官僚等）。</summary>
        public static List<ICharacter> GetHolders(Office office, int scopeKey = 0)
        {
            var list = new List<ICharacter>();
            for (int i = 0; i < appointments.Count; i++)
                if (appointments[i].office == office && appointments[i].scopeKey == scopeKey)
                    list.Add(appointments[i].holder);
            return list;
        }

        /// <summary>人物が保持する全役職を返す。</summary>
        public static List<Office> GetOffices(ICharacter holder)
        {
            var list = new List<Office>();
            for (int i = 0; i < appointments.Count; i++)
                if (appointments[i].holder == holder)
                    list.Add(appointments[i].office);
            return list;
        }

        /// <summary>指定スコープキーで役職に就いている人数。</summary>
        public static int HolderCount(Office office, int scopeKey = 0)
        {
            int n = 0;
            for (int i = 0; i < appointments.Count; i++)
                if (appointments[i].office == office && appointments[i].scopeKey == scopeKey)
                    n++;
            return n;
        }

        private static bool IsAppointed(Office office, ICharacter holder, int scopeKey)
        {
            for (int i = 0; i < appointments.Count; i++)
                if (appointments[i].office == office && appointments[i].holder == holder && appointments[i].scopeKey == scopeKey)
                    return true;
            return false;
        }

        /// <summary>全任命を消去（会戦開始/シナリオ構築時にリセット）。</summary>
        public static void Clear() => appointments.Clear();
    }
}
