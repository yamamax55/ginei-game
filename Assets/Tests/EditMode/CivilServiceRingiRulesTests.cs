using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人事（省内職位）を稟議へ載せる橋渡し（<see cref="CivilServiceRingiRules"/>・#141）を固定する：
    /// 効果キーの往復（全行為×全段・版番号つき ASCII・人物名や文面を含まない）と不正キーの拒否（接頭辞違い・未知の版・
    /// 項目数違い・範囲外の行為/段・負数・符号や空白や桁違いの数字・非正規形）、承認を問う段の解決（配属＝一般官僚／
    /// 昇任・降任＝就ける段／異動・解任＝現職の段・未在任は指定のまま）、理由の既定文と整形（空白/改行/長文/見出しの衝突）、
    /// カード本文への埋め込みと取り出し、決裁の効果レジストリが人事キーを実装済みと認識すること。
    /// </summary>
    public class CivilServiceRingiRulesTests
    {
        const int Ministry = 1004, Person = 31;

        static CivilServicePostRequest Req(CivilServiceAction a, BureaucratGrade g, int ministryId = Ministry, int personId = Person)
            => new CivilServicePostRequest(a, ministryId, personId, g);

        static CivilServiceState Ledger(params CivilServiceRecord[] records)
        {
            var st = new CivilServiceState();
            for (int i = 0; i < records.Length; i++) st.records.Add(records[i]);
            return st;
        }

        static CivilServiceRecord Serving(int personId, int ministryId, BureaucratGrade g)
            => new CivilServiceRecord
            {
                ministryId = ministryId, ministryName = "兵部省", personId = personId,
                grade = g, appointedYear = 800, status = CivilServiceStatus.在任,
            };

        // ===== 1. 効果キーの往復 =====

        [Test]
        public void Encode_IsStableAsciiWithVersion_AndCarriesNoNames()
        {
            string key = CivilServiceRingiRules.Encode(Req(CivilServiceAction.昇任, BureaucratGrade.課長級));
            Assert.AreEqual("civilservice.post:1:2:1004:31:1", key, "効果キーの正規形が変わった（保存互換に影響する）");
            Assert.AreEqual(1, CivilServiceRingiRules.KeyVersion);
            for (int i = 0; i < key.Length; i++)
                Assert.Less((int)key[i], 128, "効果キーに非 ASCII が混じった：" + key);
            StringAssert.StartsWith(CivilServiceRingiRules.EffectPrefix + ":", key);
            Assert.IsTrue(CivilServiceRingiRules.IsCivilServiceKey(key));
        }

        [Test]
        public void EncodeDecode_RoundTrips_AllActionsAndGrades()
        {
            foreach (CivilServiceAction a in System.Enum.GetValues(typeof(CivilServiceAction)))
                foreach (BureaucratGrade g in System.Enum.GetValues(typeof(BureaucratGrade)))
                {
                    CivilServicePostRequest req = Req(a, g, 0, 0);
                    string key = CivilServiceRingiRules.Encode(req);
                    Assert.IsTrue(CivilServiceRingiRules.TryDecode(key, out CivilServicePostRequest back), key);
                    Assert.AreEqual(a, back.action, key);
                    Assert.AreEqual(g, back.targetGrade, key);
                    Assert.AreEqual(0, back.ministryId, key);
                    Assert.AreEqual(0, back.personId, key);
                    Assert.IsTrue(back.IsValid, key);
                    Assert.AreEqual(key, CivilServiceRingiRules.Encode(back), "復元してから組み直すと別のキーになる");
                }
        }

        [Test]
        public void Decode_KeepsLargeIds()
        {
            string key = CivilServiceRingiRules.Encode(Req(CivilServiceAction.解任, BureaucratGrade.事務次官級, 999999, 123456));
            Assert.IsTrue(CivilServiceRingiRules.TryDecode(key, out CivilServicePostRequest back));
            Assert.AreEqual(999999, back.ministryId);
            Assert.AreEqual(123456, back.personId);
            Assert.AreEqual(CivilServiceAction.解任, back.action);
            Assert.AreEqual(BureaucratGrade.事務次官級, back.targetGrade);
        }

        // ===== 2. 不正キーの拒否 =====

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("tax.cut")]
        [TestCase("fleet.establish:12000")]
        [TestCase("civilservice.post")]                      // 区切りが無い
        [TestCase("civilservice.post:1:2:1004:31")]          // 項目が足りない
        [TestCase("civilservice.post:1:2:1004:31:1:9")]      // 項目が多い
        [TestCase("civilservice.Post:1:2:1004:31:1")]        // 接頭辞の大小違い
        [TestCase("civilservice.post:0:2:1004:31:1")]        // 未知の版（旧）
        [TestCase("civilservice.post:2:2:1004:31:1")]        // 未知の版（新）
        [TestCase("civilservice.post:1:5:1004:31:1")]        // 範囲外の行為
        [TestCase("civilservice.post:1:2:1004:31:4")]        // 範囲外の段
        [TestCase("civilservice.post:1:-1:1004:31:1")]       // 負の行為
        [TestCase("civilservice.post:1:2:-5:31:1")]          // 負の省
        [TestCase("civilservice.post:1:2:1004:-31:1")]       // 負の人物
        [TestCase("civilservice.post:1:2:1004:31:+1")]       // 符号つき
        [TestCase("civilservice.post:1:2:1004: 31:1")]       // 空白つき
        [TestCase("civilservice.post:1:2:1004:031:1")]       // 非正規形（桁揃え）
        [TestCase("civilservice.post:1:2:1004:31:x")]        // 数字でない
        [TestCase("civilservice.post:1:2:1,004:31:1")]       // 桁区切り
        public void Decode_RejectsMalformedKeys(string key)
        {
            Assert.IsFalse(CivilServiceRingiRules.TryDecode(key, out CivilServicePostRequest req), key ?? "(null)");
            Assert.AreEqual(default(CivilServicePostRequest).ministryId, req.ministryId, "拒否したのに内容が入った");
        }

        [Test]
        public void IsCivilServiceKey_MatchesPrefixOnly()
        {
            Assert.IsTrue(CivilServiceRingiRules.IsCivilServiceKey("civilservice.post:1:0:1:2:0"));
            Assert.IsTrue(CivilServiceRingiRules.IsCivilServiceKey("civilservice.post:こわれた"), "壊れた人事キーも振り分けは人事へ（黙って一般の分野推定へ落とさない）");
            Assert.IsFalse(CivilServiceRingiRules.IsCivilServiceKey("civilservice.post"));
            Assert.IsFalse(CivilServiceRingiRules.IsCivilServiceKey("tax.cut"));
            Assert.IsFalse(CivilServiceRingiRules.IsCivilServiceKey(null));
            Assert.IsFalse(CivilServiceRingiRules.IsCivilServiceKey(""));
        }

        // ===== 3. 承認を問う段 =====

        [Test]
        public void ResolveApprovalGrade_AssignIsEntryGrade_PromotionIsTarget_TransferKeepsCurrent()
        {
            CivilServiceState st = Ledger(Serving(Person, Ministry, BureaucratGrade.局長級));

            Assert.AreEqual(BureaucratGrade.一般官僚, CivilServiceRingiRules.ResolveApprovalGrade(
                st, CivilServiceAction.配属, Person, BureaucratGrade.事務次官級), "配属は入省の段だけを問う");
            Assert.AreEqual(BureaucratGrade.事務次官級, CivilServiceRingiRules.ResolveApprovalGrade(
                st, CivilServiceAction.昇任, Person, BureaucratGrade.事務次官級));
            Assert.AreEqual(BureaucratGrade.課長級, CivilServiceRingiRules.ResolveApprovalGrade(
                st, CivilServiceAction.降任, Person, BureaucratGrade.課長級));
            Assert.AreEqual(BureaucratGrade.局長級, CivilServiceRingiRules.ResolveApprovalGrade(
                st, CivilServiceAction.異動, Person, BureaucratGrade.一般官僚), "異動は段を変えない＝現職の段を問う");
            Assert.AreEqual(BureaucratGrade.局長級, CivilServiceRingiRules.ResolveApprovalGrade(
                st, CivilServiceAction.解任, Person, BureaucratGrade.一般官僚), "解任も現職の段を問う");
        }

        [Test]
        public void ResolveApprovalGrade_FallsBackWhenNotServing_AndIsNullSafe()
        {
            CivilServiceState empty = Ledger();
            Assert.AreEqual(BureaucratGrade.課長級, CivilServiceRingiRules.ResolveApprovalGrade(
                empty, CivilServiceAction.異動, Person, BureaucratGrade.課長級));
            Assert.AreEqual(BureaucratGrade.一般官僚, CivilServiceRingiRules.ResolveApprovalGrade(
                null, CivilServiceAction.解任, Person, BureaucratGrade.一般官僚));
            Assert.AreEqual(BureaucratGrade.一般官僚, CivilServiceRingiRules.ResolveApprovalGrade(
                null, CivilServiceAction.配属, Person, BureaucratGrade.一般官僚));
        }

        // ===== 4. 理由 =====

        [Test]
        public void SafeReason_UsesDefaultWhenBlank_AndNormalizes()
        {
            Assert.AreEqual("所定の人事（配属）", CivilServiceRingiRules.SafeReason(CivilServiceAction.配属, null));
            Assert.AreEqual("所定の人事（昇任）", CivilServiceRingiRules.SafeReason(CivilServiceAction.昇任, ""));
            Assert.AreEqual("所定の人事（解任）", CivilServiceRingiRules.SafeReason(CivilServiceAction.解任, "  \n "));
            Assert.AreEqual("所定の人事（異動）", CivilServiceRingiRules.DefaultReason(CivilServiceAction.異動));

            Assert.AreEqual("前線の要請 による", CivilServiceRingiRules.SafeReason(
                CivilServiceAction.異動, "  前線の要請\nによる  "), "改行は空白へ畳み前後は落とす");

            string longReason = new string('功', CivilServiceRingiRules.MaxReasonLength + 40);
            Assert.AreEqual(CivilServiceRingiRules.MaxReasonLength,
                CivilServiceRingiRules.SafeReason(CivilServiceAction.昇任, longReason).Length, "長文を切り詰めない");

            StringAssert.DoesNotContain(CivilServiceRingiRules.ReasonLabel,
                CivilServiceRingiRules.SafeReason(CivilServiceAction.昇任, "理由：功績"), "本文の見出しと紛れる");
        }

        [Test]
        public void ComposeBody_PutsReasonLast_AndExtractRoundTrips()
        {
            string reason = CivilServiceRingiRules.SafeReason(CivilServiceAction.昇任, "考課上位のため");
            string body = CivilServiceRingiRules.ComposeBody("［人事］試験官僚31 を 兵部課長 へ昇任",
                "兵部省 ／ 試験官僚31（課長級）", "試験官僚35", "兵部大臣 試験政治家12", "権限外：所管大臣が承認する", reason);

            StringAssert.Contains("対象：兵部省", body);
            StringAssert.Contains("提案：試験官僚35", body);
            StringAssert.Contains("決裁：兵部大臣 試験政治家12", body);
            StringAssert.Contains("権限：権限外：所管大臣が承認する", body);
            StringAssert.EndsWith(CivilServiceRingiRules.ReasonLabel + reason, body, "理由は最終行に置く");
            Assert.AreEqual(reason, CivilServiceRingiRules.ExtractReason(body));
        }

        [Test]
        public void ExtractReason_IsSafeOnMissingOrEmptyBody()
        {
            Assert.AreEqual("", CivilServiceRingiRules.ExtractReason(null));
            Assert.AreEqual("", CivilServiceRingiRules.ExtractReason(""));
            Assert.AreEqual("", CivilServiceRingiRules.ExtractReason("理由の無い本文"));
            Assert.AreEqual("後段", CivilServiceRingiRules.ExtractReason("前段\n理由：後段"));
            Assert.AreEqual("後段", CivilServiceRingiRules.ExtractReason("理由：前段\n理由：後段\n"), "最終行の理由を読む");
        }

        // ===== 5. 文面 =====

        [Test]
        public void Describe_UsesGradeTitle_AndFallsBackToPersonId()
        {
            Assert.AreEqual("［人事］試験官僚31 を 兵部課長 へ昇任", CivilServiceRingiRules.Describe(
                Req(CivilServiceAction.昇任, BureaucratGrade.課長級), "兵部省", "試験官僚31", BureaucratGrade.課長級));
            Assert.AreEqual("［人事］兵部課長 人物#31 を解任", CivilServiceRingiRules.Describe(
                Req(CivilServiceAction.解任, BureaucratGrade.課長級), "兵部省", "", BureaucratGrade.課長級));
            Assert.AreEqual("［人事］試験官僚31 を 兵部省 職員 へ配属", CivilServiceRingiRules.Describe(
                Req(CivilServiceAction.配属, BureaucratGrade.一般官僚), "兵部省", "試験官僚31", BureaucratGrade.一般官僚));
        }

        // ===== 6. 決裁の効果レジストリ =====

        [Test]
        public void DecisionEffectRegistry_KnowsCivilServiceKeys_ButNotBrokenOnes()
        {
            string key = CivilServiceRingiRules.Encode(Req(CivilServiceAction.配属, BureaucratGrade.一般官僚));
            Assert.IsTrue(DecisionEffectRegistryRules.IsImplemented(key), "人事キーが未実装扱いになる");
            Assert.AreEqual("", DecisionEffectRegistryRules.NotImplementedText(key));

            Assert.IsFalse(DecisionEffectRegistryRules.IsImplemented("civilservice.post:1:9:1:2:0"), "壊れた人事キーを実装済みにした");
            StringAssert.Contains("実装されていません", DecisionEffectRegistryRules.NotImplementedText("civilservice.post:1:9:1:2:0"));

            // 既存のキーの扱いを変えない
            Assert.IsTrue(DecisionEffectRegistryRules.IsImplemented("tax.cut"));
            Assert.IsFalse(DecisionEffectRegistryRules.IsImplemented("treaty.sign"));
            Assert.IsFalse(DecisionEffectRegistryRules.IsImplemented(""));
        }
    }
}
