using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 提督命名システム（#523）の合成名 API を仕様として固定するテスト（test-first）。
    /// 名→姓の順・中黒区切り・貴族の前置詞・世数・異名＋短縮名。
    /// すべて任意フィールドで、未設定なら admiralName にフォールバック（後方互換の核）。
    /// </summary>
    public class AdmiralNameTests
    {
        private AdmiralData Make(string admiralName = "提督名")
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = admiralName;
            return a;
        }

        // ───────── FullName ─────────

        [Test]
        public void FullName_NoStructuredName_FallsBackToAdmiralName()
        {
            var a = Make("ヤン");
            Assert.AreEqual("ヤン", a.FullName);
        }

        [Test]
        public void FullName_Commoner_GivenAndFamily_NoParticle()
        {
            var a = Make();
            a.givenName = "ヤン";
            a.familyName = "ウェンリー";
            Assert.AreEqual("ヤン・ウェンリー", a.FullName);
        }

        [Test]
        public void FullName_Noble_InsertsParticleBeforeFamily()
        {
            var a = Make();
            a.givenName = "ラインハルト";
            a.nobleParticle = "フォン";
            a.familyName = "ローエングラム";
            Assert.AreEqual("ラインハルト・フォン・ローエングラム", a.FullName);
        }

        [Test]
        public void FullName_WithMiddleName_BetweenGivenAndFamily()
        {
            var a = Make();
            a.givenName = "ジーク";
            a.middleName = "カイザー";
            a.familyName = "ミューゼル";
            Assert.AreEqual("ジーク・カイザー・ミューゼル", a.FullName);
        }

        [Test]
        public void FullName_Monarch_GivenPlusRegnal_NoFamily()
        {
            var a = Make();
            a.givenName = "フリードリヒ";
            a.regnalNumber = 3;
            Assert.AreEqual("フリードリヒ三世", a.FullName);
        }

        [Test]
        public void FullName_Regnal_CompoundNumber()
        {
            var a = Make();
            a.givenName = "ルイ";
            a.regnalNumber = 14;
            Assert.AreEqual("ルイ十四世", a.FullName);
        }

        [Test]
        public void FullName_Regnal_AppendedAfterFamilyWithoutSeparator()
        {
            var a = Make();
            a.givenName = "オットー";
            a.familyName = "ハイネセン";
            a.regnalNumber = 2;
            Assert.AreEqual("オットー・ハイネセン二世", a.FullName);
        }

        // ───────── ShortName ─────────

        [Test]
        public void ShortName_PrefersCallName()
        {
            var a = Make();
            a.callName = "ヤン";
            a.familyName = "ウェンリー";
            a.givenName = "ヤン・ウェン＝リー";
            Assert.AreEqual("ヤン", a.ShortName);
        }

        [Test]
        public void ShortName_FallsToFamily_ThenGiven_ThenAdmiral()
        {
            var fam = Make("提督名");
            fam.familyName = "ロイエンタール";
            Assert.AreEqual("ロイエンタール", fam.ShortName);

            var giv = Make("提督名");
            giv.givenName = "オスカー";
            Assert.AreEqual("オスカー", giv.ShortName);

            var none = Make("ただの提督");
            Assert.AreEqual("ただの提督", none.ShortName);
        }

        // ───────── EpithetName（頭上ラベル用） ─────────

        [Test]
        public void EpithetName_WithEpithet_PrefixesShortName()
        {
            var a = Make();
            a.epithet = "疾風";
            a.callName = "ウォルフ";
            Assert.AreEqual("疾風ウォルフ", a.EpithetName);
        }

        [Test]
        public void EpithetName_NoEpithet_EqualsShortName()
        {
            var a = Make();
            a.familyName = "ミッターマイヤー";
            Assert.AreEqual("ミッターマイヤー", a.EpithetName);
        }

        // ───────── RegnalSuffix（int→漢数字＋世） ─────────

        [Test]
        public void RegnalSuffix_Boundaries_ReturnEmpty()
        {
            Assert.AreEqual("", AdmiralData.RegnalSuffix(0));
            Assert.AreEqual("", AdmiralData.RegnalSuffix(-1));
            Assert.AreEqual("", AdmiralData.RegnalSuffix(100));
        }

        [Test]
        public void RegnalSuffix_KanjiConversion()
        {
            Assert.AreEqual("一世", AdmiralData.RegnalSuffix(1));
            Assert.AreEqual("九世", AdmiralData.RegnalSuffix(9));
            Assert.AreEqual("十世", AdmiralData.RegnalSuffix(10));
            Assert.AreEqual("十四世", AdmiralData.RegnalSuffix(14));
            Assert.AreEqual("二十世", AdmiralData.RegnalSuffix(20));
            Assert.AreEqual("二十一世", AdmiralData.RegnalSuffix(21));
            Assert.AreEqual("九十九世", AdmiralData.RegnalSuffix(99));
        }
    }
}
