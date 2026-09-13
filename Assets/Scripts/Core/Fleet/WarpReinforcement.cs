using System;

namespace Ginei
{
    /// <summary>
    /// 戦場の同定キー（#38 C-5 援軍＝ワープイン）。回廊戦場は「両端星系IDの無順序ペア」、
    /// 星系（惑星上）戦場は「同一IDのペア」で表す。順序は正規化する（小さいほうが <see cref="systemA"/>）ので
    /// {3,7} と {7,3} は同じ戦場になる＝<b>別戦場への混入を型で防ぐ</b>のが役目。
    ///
    /// GalaxyView の CorridorKey（long エンコード）と同じ同定規則を型にしたもの＝Core 側の唯一の窓口。
    /// 既存の long キーとは <see cref="Encode"/>/<see cref="Decode"/> で相互変換できる（並行する同定規則を作らない）。
    /// <c>default(BattlefieldKey)</c> は「無効」＝星系0の戦場とは区別される（<see cref="IsValid"/>）。
    /// </summary>
    public readonly struct BattlefieldKey : IEquatable<BattlefieldKey>
    {
        /// <summary>正規化済みの小さいほうの星系ID。</summary>
        public readonly int systemA;

        /// <summary>正規化済みの大きいほうの星系ID（星系戦場では <see cref="systemA"/> と同じ）。</summary>
        public readonly int systemB;

        /// <summary>既定値（default）と実キーを区別するフラグ。</summary>
        private readonly bool assigned;

        private BattlefieldKey(int a, int b)
        {
            systemA = Math.Min(a, b);
            systemB = Math.Max(a, b);
            assigned = true;
        }

        /// <summary>回廊戦場のキー（両端星系ID・順不同）。</summary>
        public static BattlefieldKey Corridor(int aId, int bId) => new BattlefieldKey(aId, bId);

        /// <summary>星系（惑星上）戦場のキー。</summary>
        public static BattlefieldKey System(int systemId) => new BattlefieldKey(systemId, systemId);

        /// <summary>有効なキーか（default は無効）。</summary>
        public bool IsValid => assigned;

        /// <summary>星系上の戦場か（回廊でなく1星系）。</summary>
        public bool IsSystemBattle => assigned && systemA == systemB;

        /// <summary>この戦場が指定星系に接しているか（回廊の端／星系そのもの）。</summary>
        public bool Contains(int systemId) => assigned && (systemId == systemA || systemId == systemB);

        /// <summary>回廊の反対側の端（接していなければ -1・星系戦場は自身）。</summary>
        public int Other(int systemId)
        {
            if (!assigned) return -1;
            if (systemId == systemA) return systemB;
            if (systemId == systemB) return systemA;
            return -1;
        }

        /// <summary>GalaxyView の CorridorKey と同じ long エンコード（min×100000＋max）。</summary>
        public long Encode() => assigned ? (long)systemA * 100000L + systemB : -1L;

        /// <summary>long エンコードから復元する（負値は無効キー）。</summary>
        public static BattlefieldKey Decode(long key)
        {
            if (key < 0L) return default;
            return Corridor((int)(key / 100000L), (int)(key % 100000L));
        }

        public bool Equals(BattlefieldKey other)
            => assigned == other.assigned && systemA == other.systemA && systemB == other.systemB;

        public override bool Equals(object obj) => obj is BattlefieldKey k && Equals(k);

        public override int GetHashCode() => assigned ? (systemA * 397) ^ systemB : 0;

        public static bool operator ==(BattlefieldKey a, BattlefieldKey b) => a.Equals(b);
        public static bool operator !=(BattlefieldKey a, BattlefieldKey b) => !a.Equals(b);

        public override string ToString()
        {
            if (!assigned) return "(無効な戦場)";
            return IsSystemBattle ? $"星系{systemA}" : $"回廊{systemA}-{systemB}";
        }
    }

    /// <summary>
    /// 派遣した援軍1件（#38 C-5）。戦略側で「どの戦場へ・どの勢力の・どの艦隊が・いつ着くか」を持つ平データ。
    /// 到着時刻は<b>絶対 game-秒</b>（<see cref="arrivalTime"/>）＝倍速/ポーズでずれない（相対の残り時間で持たない）。
    /// UnityEngine.Object を持たない純データ＝セーブ・比較・テストが素直（提督は fleetId から Game 側で解決する）。
    /// </summary>
    public struct WarpReinforcement
    {
        /// <summary>台帳の採番ID（1以上・0＝無効）。</summary>
        public long id;

        /// <summary>参戦先の戦場。</summary>
        public BattlefieldKey battlefield;

        /// <summary>援軍の勢力（自陣側の入口から出現する）。</summary>
        public Faction faction;

        /// <summary>戦略艦隊ID（差し戻し・盤面照合用）。</summary>
        public int fleetId;

        /// <summary>戦略兵力（会戦へは BattleHandoff.StrengthScale で換算する）。</summary>
        public int strength;

        /// <summary>派遣した game-秒（絶対時刻）。</summary>
        public double dispatchTime;

        /// <summary>到着する game-秒（絶対時刻）。</summary>
        public double arrivalTime;

        /// <summary>所要時間（game-秒）。</summary>
        public float TravelSeconds => (float)Math.Max(0.0, arrivalTime - dispatchTime);

        /// <summary>採番済みの有効な派遣か。</summary>
        public bool IsValid => id > 0L;
    }
}
