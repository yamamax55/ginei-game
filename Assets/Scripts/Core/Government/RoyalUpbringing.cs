namespace Ginei
{
    /// <summary>
    /// 王家の養育状態（#王家教育・帝王学）。王族は<b>生まれた瞬間にネームド化</b>され、既存の教育システム
    /// （士官学校#155/科挙#156/大学）を経ず、家庭教師の帝王学で育つ＝その養育の記録を持つ純データ。
    /// <b>子供時代と大人時代で能力は別</b>＝ここには「大人時代に到達しうる素養（遺伝的天井）」を持ち、
    /// 子供時代の能力は素養の未成熟分、大人時代の能力は帝王学の習得度で素養から実現される（<see cref="RoyalEducationRules"/>）。
    /// </summary>
    [System.Serializable]
    public class RoyalUpbringing
    {
        /// <summary>生年（ネームド化＝出生の年）。</summary>
        public int bornYear;

        // 大人時代に到達しうる素養（遺伝的天井・0..100）。子供時代の能力はこの未成熟分。
        public int potLeadership;
        public int potAttack;
        public int potDefense;
        public int potMobility;
        public int potOperation;
        public int potIntelligence;

        /// <summary>家庭教師の人物id（帝王学を授ける・0=なし）。</summary>
        public int tutorId;

        /// <summary>元勲の薫陶を与える人物id（いれば教育にボーナス・0=なし）。</summary>
        public int genroId;

        /// <summary>帝王学の習得度（0..1・子供時代に家庭教師×元勲で漸増）。</summary>
        public float education;

        /// <summary>成人して大人時代の能力が確定したか（一度きり）。</summary>
        public bool matured;

        public RoyalUpbringing() { }

        public RoyalUpbringing(int bornYear, int potLeadership, int potAttack, int potDefense,
                               int potMobility, int potOperation, int potIntelligence)
        {
            this.bornYear = bornYear;
            this.potLeadership = potLeadership;
            this.potAttack = potAttack;
            this.potDefense = potDefense;
            this.potMobility = potMobility;
            this.potOperation = potOperation;
            this.potIntelligence = potIntelligence;
        }
    }
}
