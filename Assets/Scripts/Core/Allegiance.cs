using UnityEngine;

namespace Ginei
{
    /// <summary>旗幟（はたじるし）＝関ヶ原型「戦う前に決まる戦い」における一諸侯/部隊の態度（#817 SEKI）。</summary>
    public enum Stance
    {
        未定,   // まだ決めていない（カスケード解決前）
        戦う,   // 自軍のために全力で戦う
        静観,   // 山上で動かない＝フリーライダー（#820・実働しない）
        寝返り  // 敵方へ寝返る（#818 ナッシュ均衡崩壊の実行）
    }

    /// <summary>
    /// 関ヶ原型「戦う前に決まる戦い」の最小単位＝一諸侯（部隊）の旗幟状態（#817・SEKI-1〜3）。
    /// 兵力は名目だが、実際に戦うかは <see cref="loyalty"/>（自軍への忠誠）と <see cref="intrigue"/>
    /// （敵の調略の浸透・#819）と趨勢で決まる。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Allegiance
    {
        public int id;
        public Faction side;     // 名目上の所属側
        public int strength;     // 兵力（名目）
        public float loyalty;    // 自軍への忠誠 0..1
        public float intrigue;   // 敵の調略の浸透 0..1（高いほど寝返りやすい・#819 家康の手紙）
        public bool locked;      // 旗幟を確定済み（カスケードの対象外＝既に動いた）
        public Stance stance = Stance.未定;

        public Allegiance() { }

        public Allegiance(int id, Faction side, int strength, float loyalty = 1f, float intrigue = 0f)
        {
            this.id = id;
            this.side = side;
            this.strength = Mathf.Max(0, strength);
            this.loyalty = Mathf.Clamp01(loyalty);
            this.intrigue = Mathf.Clamp01(intrigue);
        }

        /// <summary>side 陣営のために実際に戦う兵力に数えるか（自軍として戦う、または敵側からの寝返り）。</summary>
        public bool FightsFor(Faction s)
            => (stance == Stance.戦う && side == s) || (stance == Stance.寝返り && side != s);
    }
}
