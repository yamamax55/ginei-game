using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 寝返りの純ロジック（#寝返り・史実＝小早川秀秋）。関ヶ原で家康の調略（問鉄砲で追い込まれ「はしごを外され」）に屈し、
    /// <b>布陣後に寝返って</b>西軍を崩壊させた裏切りを再現する。要点：
    /// ①調略・圧力に弱く土壇場で寝返る、②その寝返りは決定的（裏切られた側の戦線が崩れる＝松尾山から大谷隊へ）、
    /// ③<b>布陣後の寝返りはご法度</b>ゆえ<b>名誉が大幅に下がる</b>（裏切り者の汚名・秀秋は2年後に夭折）。
    /// 戦前に決まる関ヶ原型 `BattleAllegianceRules`#817（静観/旗幟）に対し、本クラスは<b>布陣後の禁じ手</b>と名誉の罰を扱う。
    /// 名声は `RenownRules`#2304、士気衝撃は `MoraleShockRules`#2176 へ橋渡し。実効値パターン・決定論・test-first。
    /// </summary>
    public static class TurncoatRules
    {
        /// <summary>布陣後の寝返り（ご法度）の名誉ペナルティ＝大幅減。</summary>
        public const int TabooFamePenalty = 50;
        /// <summary>布陣前の旗幟変更（戦う前に決まる #817）の名誉ペナルティ＝軽微。</summary>
        public const int PreDeployFamePenalty = 10;
        /// <summary>布陣後の寝返りが裏切られた側へ与える士気衝撃（戦線崩壊）。</summary>
        public const float TabooBetrayalMoraleShock = 0.4f;
        /// <summary>布陣前の離反の士気衝撃（軽微）。</summary>
        public const float PreDeployMoraleShock = 0.15f;

        /// <summary>布陣後の寝返りか（＝ご法度。布陣前は戦う前に決まる旗幟で許容範囲）。</summary>
        public static bool IsTabooDefection(bool afterDeployment) => afterDeployment;

        /// <summary>寝返りの名誉ペナルティ（布陣後＝大幅50／布陣前＝軽微10）。</summary>
        public static int FamePenalty(bool afterDeployment)
            => afterDeployment ? TabooFamePenalty : PreDeployFamePenalty;

        /// <summary>名誉を寝返りで減じた値（`RenownRules`#2304 の fame に適用・0..100）。布陣後は大幅マイナス。</summary>
        public static int ApplyHonorPenalty(int currentFame, bool afterDeployment)
            => Mathf.Clamp(currentFame - FamePenalty(afterDeployment), 0, 100);

        /// <summary>
        /// 寝返る確率。調略の浸透 intrigue(0..1) と圧力 pressure(0..1・問鉄砲) で上がる。
        /// 寝返り型は土壇場で靡きやすい（base高め）・並は調略のみで低い。
        /// </summary>
        public static float SwayChance(bool isTurncoat, float intrigue, float pressure)
        {
            float i = Mathf.Clamp01(intrigue);
            float p = Mathf.Clamp01(pressure);
            if (isTurncoat) return Mathf.Clamp01(0.2f + i * 0.4f + p * 0.4f);
            return Mathf.Clamp01(i * 0.4f);
        }

        /// <summary>寝返るか（roll∈[0,1) を注入・決定論）。</summary>
        public static bool Betrays(float swayChance, float roll) => roll < Mathf.Clamp01(swayChance);

        /// <summary>裏切られた側が受ける士気衝撃（布陣後＝戦線崩壊の大ショック／布陣前＝軽微）。</summary>
        public static float BetrayalMoraleShock(bool afterDeployment)
            => afterDeployment ? TabooBetrayalMoraleShock : PreDeployMoraleShock;
    }
}
