using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 組織内の稟議（部下→上司）：人物が上司へ稟議を挙げられる／宛先の上司だけが「自分のところに来た稟議」を決裁できる。
    /// 状態遷移は <see cref="WorkflowRules"/> へ委譲し、ここは人物の資格ゲートと宛先（addresseeId）の振る舞いを固定する。
    /// </summary>
    public class PersonRingiRulesTests
    {
        private static Person P(int id, Faction f, int tier, CaptiveStatus cap = CaptiveStatus.自由)
            => new Person { id = id, faction = f, rankTier = tier, captiveStatus = cap };

        [Test]
        public void IsSuperior_RequiresSameFaction_HigherTier_Available()
        {
            var boss = P(1, Faction.同盟, 8);
            var sub = P(2, Faction.同盟, 6);
            Assert.IsTrue(PersonRingiRules.IsSuperior(boss, sub));
            Assert.IsFalse(PersonRingiRules.IsSuperior(sub, boss));            // 下位は上司でない
            Assert.IsFalse(PersonRingiRules.IsSuperior(boss, P(3, Faction.同盟, 8))); // 同 tier は上司でない
            Assert.IsFalse(PersonRingiRules.IsSuperior(boss, P(4, Faction.帝国, 6))); // 別勢力
            Assert.IsFalse(PersonRingiRules.IsSuperior(boss, boss));            // 同一人物
            // 捕虜/死亡は任に就けない＝上下に絡まない
            Assert.IsFalse(PersonRingiRules.IsSuperior(P(5, Faction.同盟, 8, CaptiveStatus.捕虜), sub));
        }

        [Test]
        public void RaiseTo_BuildsPetition_AddressedToSuperior()
        {
            var boss = P(1, Faction.同盟, 8);
            var sub = P(2, Faction.同盟, 6);
            var pet = PersonRingiRules.RaiseTo(10, "前線増援を", sub, boss, "tax.hike");
            Assert.IsNotNull(pet);
            Assert.AreEqual(2, pet.drafterId);     // 起案者＝部下
            Assert.AreEqual(2, pet.carrierId);     // 初期中継者＝部下
            Assert.AreEqual(1, pet.addresseeId);   // 宛先＝上司
            Assert.AreEqual(Faction.同盟, pet.faction);
            Assert.AreEqual(PetitionOrigin.建白, pet.origin);
            Assert.AreEqual(PetitionStatus.起案, pet.status);
        }

        [Test]
        public void RaiseTo_ToNonSuperior_ReturnsNull()
        {
            var boss = P(1, Faction.同盟, 8);
            var sub = P(2, Faction.同盟, 6);
            Assert.IsNull(PersonRingiRules.RaiseTo(10, "越権", boss, sub, "tax.cut")); // 上司が部下へは挙げない
        }

        [Test]
        public void Adjudicate_OnlyAddresseeCanDecide()
        {
            var boss = P(1, Faction.同盟, 8);
            var other = P(3, Faction.同盟, 9); // もっと上位でも宛先でなければ決裁できない
            var sub = P(2, Faction.同盟, 6);
            var pet = PersonRingiRules.RaiseTo(10, "案", sub, boss, "tax.hike");
            pet.status = PetitionStatus.決裁待ち;

            Assert.IsFalse(PersonRingiRules.CanAdjudicate(other, pet)); // 宛先でない
            Assert.IsFalse(PersonRingiRules.Adjudicate(other, pet, true));
            Assert.AreEqual(PetitionStatus.決裁待ち, pet.status);       // 動かない

            Assert.IsTrue(PersonRingiRules.CanAdjudicate(boss, pet));
            Assert.IsTrue(PersonRingiRules.Adjudicate(boss, pet, approve: true));
            Assert.AreEqual(PetitionStatus.承認, pet.status);          // 宛先が承認
        }

        [Test]
        public void Adjudicate_NotAwaiting_IsRejected()
        {
            var boss = P(1, Faction.同盟, 8);
            var sub = P(2, Faction.同盟, 6);
            var pet = PersonRingiRules.RaiseTo(10, "案", sub, boss, "tax.hike"); // status=起案＝まだ決裁待ちでない
            Assert.IsFalse(PersonRingiRules.CanAdjudicate(boss, pet));
            Assert.IsFalse(PersonRingiRules.Adjudicate(boss, pet, true));
        }

        [Test]
        public void Adjudicate_BoxAddressed_NotForPersons()
        {
            var boss = P(1, Faction.同盟, 8);
            // 箱宛て（addresseeId=0）の陳情は人物決裁の対象外（プレイヤーの目安箱は WorkflowRules.Decide）
            var pet = new Petition(11, "箱宛て", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            pet.status = PetitionStatus.決裁待ち;
            Assert.IsFalse(PersonRingiRules.CanAdjudicate(boss, pet));
        }

        [Test]
        public void Ledger_AwaitingDecisionFor_IsPersonalInbox()
        {
            var boss = P(1, Faction.同盟, 8);
            var sub = P(2, Faction.同盟, 6);
            var led = new PetitionLedger();

            var mine = PersonRingiRules.RaiseTo(0, "自分宛て", sub, boss, "tax.hike");
            Assert.IsTrue(RingiPipeline.Submit(led, mine)); // 起案→伝播中・在庫へ
            RingiPipeline.SendToDecision(mine);             // 決裁待ちへ

            // 別の上司宛ての決裁待ちは混ざらない
            var boss2 = P(5, Faction.同盟, 9);
            var sub2 = P(6, Faction.同盟, 7);
            var theirs = PersonRingiRules.RaiseTo(0, "他人宛て", sub2, boss2, "tax.cut");
            RingiPipeline.Submit(led, theirs);
            RingiPipeline.SendToDecision(theirs);

            var inbox = led.AwaitingDecisionFor(boss.id);
            Assert.AreEqual(1, inbox.Count);
            Assert.AreEqual(mine.id, inbox[0].id);
            Assert.AreEqual(0, led.AwaitingDecisionFor(0).Count); // 箱宛て扱いは拾わない
        }
    }
}
