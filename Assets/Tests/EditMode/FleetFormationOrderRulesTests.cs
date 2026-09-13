using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 確定仕様1：陣形専用の保持・解除と命令の優先順位。
    ///
    /// 要点は「陣形の保持は<b>移動・攻撃と独立</b>」で、
    /// AI（艦隊・軍団とも）が明示指定を上書きしないこと、
    /// 断ったときは<b>何も変わらない</b>こと、
    /// 緊急（敗走・総退却）で解けるのは<b>陣形の保持だけ</b>であること。
    /// </summary>
    public class FleetFormationOrderRulesTests
    {
        private static FleetFormationHold Held(Formation f, FormationOrderSource src, string corps = "A軍団")
            => new FleetFormationHold(true, f, src, corps);

        // ===== 優先順位 =====

        // ===== 撤退の状態（レビュー指摘1） =====

        /// <summary>
        /// ★士気は正常で、総退却の移動中（AI＝撤退・まだ戦場端に着いていない）でも撤退中と見なす。
        /// ここを取りこぼすと、次の軍団周期で解除される陣形を受理して
        /// ちらつきとスキルポイントの無駄遣いが起きる。
        /// </summary>
        [Test]
        public void RetreatingState_CoversCorpsRetreatMovementWithNormalMorale()
        {
            // 離脱前（IsRetreating=false）・士気正常（routed=false）でも、AI が撤退中なら撤退中。
            Assert.IsTrue(FleetFormationOrderRules.IsRetreatingState(
                withdrawing: false, routed: false, aiRetreating: true, corpsRetreatOrdered: false));

            // 軍団が総退却を発令中なら、その艦がまだ撤退へ落ちていなくても撤退中。
            Assert.IsTrue(FleetFormationOrderRules.IsRetreatingState(
                withdrawing: false, routed: false, aiRetreating: false, corpsRetreatOrdered: true));
        }

        /// <summary>従来の2つ（戦場離脱・敗走）も引き続き撤退中。</summary>
        [Test]
        public void RetreatingState_CoversWithdrawAndRout()
        {
            Assert.IsTrue(FleetFormationOrderRules.IsRetreatingState(true, false, false, false));
            Assert.IsTrue(FleetFormationOrderRules.IsRetreatingState(false, true, false, false));
        }

        /// <summary>平時はどれも当てはまらない（普通に陣形を変えられる）。</summary>
        [Test]
        public void RetreatingState_IsFalseWhenNothingApplies()
        {
            Assert.IsFalse(FleetFormationOrderRules.IsRetreatingState(false, false, false, false));
        }

        /// <summary>
        /// ★総退却の発令中は、直接命令を維持している例外の艦でも新規の保持を受け付けない
        /// （移動命令そのものは別＝ここでは止めない）。
        /// </summary>
        [Test]
        public void CorpsRetreatOrdered_RejectsNewHoldEvenForDirectOrder()
        {
            bool retreating = FleetFormationOrderRules.IsRetreatingState(
                withdrawing: false, routed: false, aiRetreating: false, corpsRetreatOrdered: true);

            Assert.AreEqual(FormationOrderResult.撤退中,
                FleetFormationOrderRules.Decide(FleetFormationHold.None, FormationOrderSource.直接命令,
                    sameFormation: false, hasPoints: true, qualified: true, retreating: retreating));
        }

        [Test]
        public void Explicit_IsDirectAndSupportOnly()
        {
            Assert.IsTrue(FleetFormationOrderRules.IsExplicit(FormationOrderSource.直接命令));
            Assert.IsTrue(FleetFormationOrderRules.IsExplicit(FormationOrderSource.支援要請));
            Assert.IsFalse(FleetFormationOrderRules.IsExplicit(FormationOrderSource.軍団AI));
            Assert.IsFalse(FleetFormationOrderRules.IsExplicit(FormationOrderSource.艦隊AI));
            Assert.IsFalse(FleetFormationOrderRules.IsExplicit(FormationOrderSource.なし));
        }

        // ===== 手動保持を AI が上書きしない =====

        /// <summary>★保持中の明示指定は、艦隊AIも軍団AIも上書きできない。</summary>
        [Test]
        public void HeldFormation_IsNotOverriddenByAnyAi()
        {
            var hold = Held(Formation.円陣, FormationOrderSource.直接命令);

            Assert.IsFalse(FleetFormationOrderRules.CanAccept(hold, FormationOrderSource.艦隊AI));
            Assert.IsFalse(FleetFormationOrderRules.CanAccept(hold, FormationOrderSource.軍団AI));

            Assert.AreEqual(FormationOrderResult.保持により拒否,
                FleetFormationOrderRules.Decide(hold, FormationOrderSource.艦隊AI,
                    sameFormation: false, hasPoints: true, qualified: true, retreating: false));
            Assert.AreEqual(FormationOrderResult.保持により拒否,
                FleetFormationOrderRules.Decide(hold, FormationOrderSource.軍団AI,
                    sameFormation: false, hasPoints: true, qualified: true, retreating: false));
        }

        /// <summary>承諾された支援要請も明示指定＝AI に上書きされない。</summary>
        [Test]
        public void SupportOrderedFormation_IsAlsoProtectedFromAi()
        {
            var hold = Held(Formation.方陣, FormationOrderSource.支援要請);
            Assert.IsFalse(FleetFormationOrderRules.CanAccept(hold, FormationOrderSource.軍団AI));
            Assert.IsTrue(FleetFormationOrderRules.CanAccept(hold, FormationOrderSource.直接命令));
        }

        /// <summary>明示指定どうしは「最後に受理したもの」が勝つ（直接と要請に上下はない）。</summary>
        [Test]
        public void ExplicitOrders_LastAcceptedWins()
        {
            var byDirect = Held(Formation.円陣, FormationOrderSource.直接命令);
            Assert.IsTrue(FleetFormationOrderRules.CanAccept(byDirect, FormationOrderSource.支援要請));

            var bySupport = Held(Formation.円陣, FormationOrderSource.支援要請);
            Assert.IsTrue(FleetFormationOrderRules.CanAccept(bySupport, FormationOrderSource.直接命令));

            FleetFormationHold after = FleetFormationOrderRules.Apply(
                byDirect, FormationOrderSource.支援要請, Formation.横陣, "A軍団");
            Assert.IsTrue(after.held);
            Assert.AreEqual(Formation.横陣, after.formation);
            Assert.AreEqual(FormationOrderSource.支援要請, after.source);
        }

        /// <summary>保持していなければ AI も普通に陣形を変えられる（従来動作）。</summary>
        [Test]
        public void WithoutHold_AiCanStillChangeFormation()
        {
            var none = FleetFormationHold.None;
            Assert.IsTrue(FleetFormationOrderRules.CanAccept(none, FormationOrderSource.艦隊AI));
            Assert.AreEqual(FormationOrderResult.受理,
                FleetFormationOrderRules.Decide(none, FormationOrderSource.艦隊AI,
                    false, true, true, false));
        }

        /// <summary>★AI の指定は保持を作らない（AI が陣形を固定してしまわない）。</summary>
        [Test]
        public void AiOrder_DoesNotCreateHold()
        {
            FleetFormationHold after = FleetFormationOrderRules.Apply(
                FleetFormationHold.None, FormationOrderSource.艦隊AI, Formation.鶴翼陣, "A軍団");
            Assert.IsFalse(after.held);

            after = FleetFormationOrderRules.Apply(
                FleetFormationHold.None, FormationOrderSource.軍団AI, Formation.鶴翼陣, "A軍団");
            Assert.IsFalse(after.held);
        }

        [Test]
        public void ExplicitOrder_CreatesHoldWithSourceAndCorps()
        {
            FleetFormationHold after = FleetFormationOrderRules.Apply(
                FleetFormationHold.None, FormationOrderSource.直接命令, Formation.円陣, "A軍団");
            Assert.IsTrue(after.held);
            Assert.AreEqual(Formation.円陣, after.formation);
            Assert.AreEqual(FormationOrderSource.直接命令, after.source);
            Assert.AreEqual("A軍団", after.corpsKey);
        }

        // ===== 拒否したら何も変えない =====

        /// <summary>★資格不足・ポイント不足・撤退中は拒否。旧指定と保持はそのまま。</summary>
        [Test]
        public void Rejections_KeepOldHoldUntouched()
        {
            var hold = Held(Formation.円陣, FormationOrderSource.直接命令);

            Assert.AreEqual(FormationOrderResult.撤退中,
                FleetFormationOrderRules.Decide(hold, FormationOrderSource.直接命令,
                    false, true, true, retreating: true));
            Assert.AreEqual(FormationOrderResult.資格不足,
                FleetFormationOrderRules.Decide(hold, FormationOrderSource.直接命令,
                    false, true, qualified: false, retreating: false));
            Assert.AreEqual(FormationOrderResult.ポイント不足,
                FleetFormationOrderRules.Decide(hold, FormationOrderSource.直接命令,
                    false, hasPoints: false, qualified: true, retreating: false));

            // 拒否のときは Apply を呼ばない＝保持は元のまま、というのが呼び手の契約。
            Assert.AreEqual(Formation.円陣, hold.formation);
            Assert.AreEqual(FormationOrderSource.直接命令, hold.source);
            Assert.IsTrue(hold.held);
        }

        /// <summary>★同一陣形は無料＝ポイントが無くても保持だけ付け直せる。</summary>
        [Test]
        public void SameFormation_IsFreeEvenWithoutPoints()
        {
            Assert.AreEqual(FormationOrderResult.受理,
                FleetFormationOrderRules.Decide(FleetFormationHold.None, FormationOrderSource.直接命令,
                    sameFormation: true, hasPoints: false, qualified: true, retreating: false));
        }

        /// <summary>撤退中は同一陣形でも受け付けない（崩れている隊に指定を入れない）。</summary>
        [Test]
        public void Retreating_RejectsEvenSameFormation()
        {
            Assert.AreEqual(FormationOrderResult.撤退中,
                FleetFormationOrderRules.Decide(FleetFormationHold.None, FormationOrderSource.直接命令,
                    sameFormation: true, hasPoints: true, qualified: true, retreating: true));
        }

        // ===== 緊急（敗走・総退却）で保持だけ解ける =====

        /// <summary>★敗走・総退却では出どころに関係なく保持を解く。</summary>
        [Test]
        public void Emergency_ReleasesHoldRegardlessOfSource()
        {
            Assert.IsTrue(FleetFormationOrderRules.ShouldReleaseForEmergency(routed: true, corpsRetreatOrdered: false));
            Assert.IsTrue(FleetFormationOrderRules.ShouldReleaseForEmergency(routed: false, corpsRetreatOrdered: true));
            Assert.IsFalse(FleetFormationOrderRules.ShouldReleaseForEmergency(false, false),
                "平時に保持が勝手に解けている");
        }

        // ===== 所属変更で解除 =====

        /// <summary>★軍団の所属が変わったら旧指揮系統の保持を解く。</summary>
        [Test]
        public void CorpsChange_ReleasesHold()
        {
            var hold = Held(Formation.円陣, FormationOrderSource.直接命令, "A軍団");
            Assert.IsTrue(FleetFormationOrderRules.ShouldReleaseOnCorpsChange(hold, "B軍団"));
            Assert.IsFalse(FleetFormationOrderRules.ShouldReleaseOnCorpsChange(hold, "A軍団"));

            // 軍団を離れた（所属なし）ときも解く。
            Assert.IsTrue(FleetFormationOrderRules.ShouldReleaseOnCorpsChange(hold, ""));
            Assert.IsTrue(FleetFormationOrderRules.ShouldReleaseOnCorpsChange(hold, null));
        }

        [Test]
        public void CorpsChange_DoesNothingWithoutHold()
        {
            Assert.IsFalse(FleetFormationOrderRules.ShouldReleaseOnCorpsChange(FleetFormationHold.None, "B軍団"));
        }

        // ===== 自動復帰 =====

        /// <summary>★保持している陣形からずれたら戻す（移動・攻撃には触らない＝陣形だけ）。</summary>
        [Test]
        public void Restore_OnlyWhenHeldAndDrifted()
        {
            var hold = Held(Formation.円陣, FormationOrderSource.直接命令);
            Assert.IsTrue(FleetFormationOrderRules.ShouldRestore(hold, Formation.横陣), "ずれたのに戻さない");
            Assert.IsFalse(FleetFormationOrderRules.ShouldRestore(hold, Formation.円陣), "同じなのに戻そうとする");
            Assert.IsFalse(FleetFormationOrderRules.ShouldRestore(FleetFormationHold.None, Formation.横陣),
                "保持していないのに戻している");
        }

        // ===== 表示 =====

        [Test]
        public void HoldText_ShowsSourceAndState()
        {
            var hold = Held(Formation.円陣, FormationOrderSource.直接命令);
            const FormationOrderSource last = FormationOrderSource.直接命令;
            StringAssert.Contains("円陣", FleetFormationOrderRules.HoldText(hold, Formation.円陣, last));
            StringAssert.Contains("保持", FleetFormationOrderRules.HoldText(hold, Formation.円陣, last));
            StringAssert.Contains("直接命令", FleetFormationOrderRules.HoldText(hold, Formation.円陣, last));
            // ずれている間は復帰中と分かる。
            StringAssert.Contains("復帰中", FleetFormationOrderRules.HoldText(hold, Formation.横陣, last));
        }

        /// <summary>
        /// ★保持していないときは<b>最後に決めた出どころ</b>を出す
        /// ＝軍団長の指示で布いているのか自律かを区別できる（レビュー補足）。
        /// </summary>
        [Test]
        public void HoldText_DistinguishesCorpsOrderFromAutonomous()
        {
            string byCorps = FleetFormationOrderRules.HoldText(
                FleetFormationHold.None, Formation.方陣, FormationOrderSource.軍団AI);
            string bySelf = FleetFormationOrderRules.HoldText(
                FleetFormationHold.None, Formation.方陣, FormationOrderSource.艦隊AI);

            StringAssert.Contains("軍団", byCorps);
            StringAssert.Contains("自律", bySelf);
            Assert.AreNotEqual(byCorps, bySelf, "軍団長の指示と自律が同じ表示になっている");

            // まだ誰も指定していない初期状態も区別できる。
            string initial = FleetFormationOrderRules.HoldText(
                FleetFormationHold.None, Formation.紡錘陣, FormationOrderSource.なし);
            Assert.AreNotEqual(byCorps, initial);
            Assert.AreNotEqual(bySelf, initial);
        }

        /// <summary>拒否の文面は「変更していない」と分かること。</summary>
        [Test]
        public void ResultText_SaysNothingChangedOnRejection()
        {
            StringAssert.Contains("変更していません",
                FleetFormationOrderRules.ResultText(FormationOrderResult.ポイント不足, Formation.円陣, "第7艦隊"));
            StringAssert.Contains("変更していません",
                FleetFormationOrderRules.ResultText(FormationOrderResult.資格不足, Formation.車懸かり, "第7艦隊"));
            StringAssert.Contains("撤退中",
                FleetFormationOrderRules.ResultText(FormationOrderResult.撤退中, Formation.円陣, "第7艦隊"));
            StringAssert.Contains("保持中",
                FleetFormationOrderRules.ResultText(FormationOrderResult.保持により拒否, Formation.円陣, "第7艦隊"));
        }

        [Test]
        public void Texts_AreNullSafe()
        {
            Assert.IsFalse(string.IsNullOrEmpty(
                FleetFormationOrderRules.ResultText(FormationOrderResult.受理, Formation.円陣, null)));
            Assert.IsFalse(string.IsNullOrEmpty(FleetFormationOrderRules.ReleaseText(null, null)));
            StringAssert.Contains("敗走", FleetFormationOrderRules.ReleaseText("第7艦隊", "敗走"));
        }

        [Test]
        public void None_HasNoHold()
        {
            Assert.IsFalse(FleetFormationHold.None.held);
            Assert.AreEqual(FormationOrderSource.なし, FleetFormationHold.None.source);
            Assert.AreEqual("", FleetFormationHold.None.corpsKey);
        }

        /// <summary>出どころ「なし」は指定として受け付けない。</summary>
        [Test]
        public void NoneSource_IsNeverAccepted()
        {
            Assert.IsFalse(FleetFormationOrderRules.CanAccept(FleetFormationHold.None, FormationOrderSource.なし));
        }
    }
}
