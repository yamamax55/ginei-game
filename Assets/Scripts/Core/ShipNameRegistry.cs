using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 旗艦名の払い出し台帳（#旗艦名・static）。出典＝世界遺産・自然遺産（<see cref="HeritageShipNames"/> の約200プール）。
    /// 艦隊番号 <see cref="FleetRoster"/>#146 の名前版＝<b>使用中は重複させず、退役で返却（再利用可）・撃沈で永久欠番（以後払い出さない）</b>。
    /// 名前体系は勢力非依存（世界遺産名は普遍）。プール順に最小の空き名を払い出す（決定論）。
    /// プール枯渇時は連番サフィックス（"◯◯ II" …）でフォールバックし、命名が止まらない。test-first。
    /// </summary>
    public static class ShipNameRegistry
    {
        private static readonly HashSet<string> inUse = new HashSet<string>();
        private static readonly HashSet<string> retired = new HashSet<string>(); // 永久欠番（撃沈艦）

        /// <summary>台帳を空にする（会戦セットアップやテストの初期化用）。</summary>
        public static void Clear() { inUse.Clear(); retired.Clear(); }

        /// <summary>使用中か。</summary>
        public static bool IsInUse(string name) => name != null && inUse.Contains(name);

        /// <summary>永久欠番か（撃沈艦の名は以後払い出されない＝史実の艦名継承の重み）。</summary>
        public static bool IsRetired(string name) => name != null && retired.Contains(name);

        /// <summary>払い出し可能な名の残数（プール中、使用中でも永久欠番でもないもの）。</summary>
        public static int AvailableCount
        {
            get
            {
                int c = 0;
                var pool = HeritageShipNames.Names;
                for (int i = 0; i < pool.Length; i++)
                    if (!inUse.Contains(pool[i]) && !retired.Contains(pool[i])) c++;
                return c;
            }
        }

        /// <summary>
        /// 次の旗艦名を払い出す（プール順に最小の空き名）。使用中・永久欠番は飛ばす。
        /// プールが尽きたら "◯◯ II"/"◯◯ III" … の連番で空きを探す（命名を止めない）。
        /// </summary>
        public static string Assign()
        {
            var pool = HeritageShipNames.Names;
            for (int i = 0; i < pool.Length; i++)
            {
                string n = pool[i];
                if (!inUse.Contains(n) && !retired.Contains(n)) { inUse.Add(n); return n; }
            }
            // フォールバック：連番サフィックス（II 世以降）。
            for (int suffix = 2; ; suffix++)
            {
                for (int i = 0; i < pool.Length; i++)
                {
                    string n = pool[i] + " " + Roman(suffix);
                    if (!inUse.Contains(n) && !retired.Contains(n)) { inUse.Add(n); return n; }
                }
            }
        }

        /// <summary>名を返却する（退役・安全退却＝再び払い出し可能に戻す）。永久欠番には触れない。</summary>
        public static void Release(string name)
        {
            if (name != null) inUse.Remove(name);
        }

        /// <summary>名を永久欠番にする（撃沈＝以後その名は払い出さない）。使用中からも外す。</summary>
        public static void Retire(string name)
        {
            if (name == null) return;
            inUse.Remove(name);
            retired.Add(name);
        }

        /// <summary>整数を簡易ローマ数字（フォールバックの世数表記用・1〜数十を想定）。</summary>
        private static string Roman(int n)
        {
            if (n <= 0) return n.ToString();
            int[] vals = { 50, 40, 10, 9, 5, 4, 1 };
            string[] syms = { "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < vals.Length && n > 0; i++)
                while (n >= vals[i]) { sb.Append(syms[i]); n -= vals[i]; }
            return sb.ToString();
        }
    }
}
