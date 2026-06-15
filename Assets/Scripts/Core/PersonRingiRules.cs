namespace Ginei
{
    /// <summary>
    /// 組織内の稟議（部下→上司）の唯一の窓口（稟議基盤整備）。目安箱（プレイヤーの越階回路・<see cref="WorkflowRules"/>/
    /// <see cref="BoxKind"/>）に対し、こちらは<b>ネームド人物どうし</b>の稟議＝部下が上司へ起案し（建白）、宛先の上司だけが
    /// 決裁できる。上下関係は階級 tier（#14）で判定（同勢力・上位 tier が上司）。状態遷移は <see cref="WorkflowRules"/> へ
    /// 委譲し、人物の「挙げられるか／決裁できるか」の資格ゲートだけを持つ（並行新設しない）。宛先は <see cref="Petition.addresseeId"/>
    /// （0=箱宛て＝従来動作）。共通窓口 <see cref="ICharacter"/> 越しに <see cref="Person"/>/<see cref="AdmiralData"/> どちらでも動く。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PersonRingiRules
    {
        /// <summary>superior が subordinate の上司か＝同勢力・両者が任に就ける・superior の階級が上位（tier 大）。別人であること。</summary>
        public static bool IsSuperior(ICharacter superior, ICharacter subordinate)
        {
            if (superior == null || subordinate == null) return false;
            if (superior.Id == subordinate.Id) return false;
            if (superior.Faction != subordinate.Faction) return false;
            if (!superior.IsAvailable || !subordinate.IsAvailable) return false;
            return superior.RankTier > subordinate.RankTier;
        }

        /// <summary>drafter が superior へ稟議を挙げられるか（＝superior が drafter の上司）。</summary>
        public static bool CanRaiseTo(ICharacter drafter, ICharacter superior)
            => IsSuperior(superior, drafter);

        /// <summary>
        /// 部下が上司へ稟議を起案する（建白・status=起案）。挙げられなければ null。
        /// 宛先 <see cref="Petition.addresseeId"/>＝上司、起案者/初期中継者＝部下。以後は
        /// <see cref="RingiPipeline.Submit"/> で在庫へ載せ、官僚機構を経て上司が <see cref="Adjudicate"/> する。
        /// box はプレイヤーの目安箱とは独立の粗い分類ヒント（既定 国王・宛先は addresseeId が真）。
        /// </summary>
        public static Petition RaiseTo(int id, string title, ICharacter drafter, ICharacter superior,
            string effectKey = "", BoxKind box = BoxKind.国王, string regionKey = "")
        {
            if (!CanRaiseTo(drafter, superior)) return null;
            var pet = new Petition(id, title, drafter.Faction, box, PetitionOrigin.建白, effectKey, regionKey)
            {
                drafterId = drafter.Id,
                addresseeId = superior.Id,
                carrierId = drafter.Id,
            };
            return pet;
        }

        /// <summary>decider が「自分のところに来た稟議」を決裁できるか＝決裁待ちで、宛先が decider 本人で、任に就ける。</summary>
        public static bool CanAdjudicate(ICharacter decider, Petition pet)
        {
            if (decider == null || pet == null) return false;
            if (pet.addresseeId == 0 || pet.addresseeId != decider.Id) return false;
            if (!decider.IsAvailable) return false;
            return pet.status == PetitionStatus.決裁待ち;
        }

        /// <summary>
        /// 宛先の上司が決裁する（承認/却下）＝<see cref="WorkflowRules.Decide"/> へ委譲。資格（宛先本人・任に就ける・決裁待ち）が
        /// 無ければ何もせず false。＝「自分のところに来た稟議を決裁できる」。
        /// </summary>
        public static bool Adjudicate(ICharacter decider, Petition pet, bool approve)
        {
            if (!CanAdjudicate(decider, pet)) return false;
            return WorkflowRules.Decide(pet, approve);
        }
    }
}
