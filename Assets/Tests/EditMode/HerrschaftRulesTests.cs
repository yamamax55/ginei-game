using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>支配の三類型（ウェーバー・#1525）の純ロジックの担保。類型別の正統性・安定・脆さ・日常化・侵食・官僚制・崩壊・継承。</summary>
    public class HerrschaftRulesTests
    {
        private const float Eps = 0.0001f;

        /// <summary>類型ごとに正統性の源が異なる＝伝統的は慣習・カリスマ的は資質・合法的は規則を主因にする。</summary>
        [Test]
        public void LegitimacyStrength_類型ごとに主因が変わる()
        {
            // tradition=1, charisma=0, legality=0 のとき各類型を比較。
            float trad = HerrschaftRules.LegitimacyStrength(HerrschaftType.伝統的, 1f, 0f, 0f);
            float chari = HerrschaftRules.LegitimacyStrength(HerrschaftType.カリスマ的, 1f, 0f, 0f);
            float legal = HerrschaftRules.LegitimacyStrength(HerrschaftType.合法的, 1f, 0f, 0f);
            // 伝統的は慣習を主因にするので最も高い。
            Assert.Greater(trad, chari);
            Assert.Greater(trad, legal);
            // 主因＝慣習(1)×0.7＝0.7。
            Assert.AreEqual(0.7f, trad, Eps);
        }

        /// <summary>安定プロファイル＝合法的が最も安定・伝統的が中・カリスマ的は高いが（脆さは別）。</summary>
        [Test]
        public void StabilityProfile_合法が最安定_既定値()
        {
            Assert.AreEqual(0.8f, HerrschaftRules.StabilityProfile(HerrschaftType.合法的), Eps);
            Assert.AreEqual(0.6f, HerrschaftRules.StabilityProfile(HerrschaftType.伝統的), Eps);
            Assert.AreEqual(0.75f, HerrschaftRules.StabilityProfile(HerrschaftType.カリスマ的), Eps);
            Assert.Greater(HerrschaftRules.StabilityProfile(HerrschaftType.合法的),
                HerrschaftRules.StabilityProfile(HerrschaftType.伝統的));
        }

        /// <summary>カリスマ的支配は脆い＝カリスマが強く日常化が進んでいないほど脆く、日常化完了で脆さが消える。</summary>
        [Test]
        public void CharismaticFragility_日常化で脆さが消える()
        {
            // charisma=1, routinization=0 ＝最も脆い＝1×1×0.7＝0.7。
            float raw = HerrschaftRules.CharismaticFragility(1f, 0f);
            Assert.AreEqual(0.7f, raw, Eps);
            // 日常化が完了(1)すれば脆さ0。
            float routinized = HerrschaftRules.CharismaticFragility(1f, 1f);
            Assert.AreEqual(0f, routinized, Eps);
            // 日常化が進むほど単調に減る。
            Assert.Greater(raw, HerrschaftRules.CharismaticFragility(1f, 0.5f));
        }

        /// <summary>カリスマの日常化（ルーティン化）＝制度化と強いカリスマほど速く進む。</summary>
        [Test]
        public void CharismaRoutinization_制度化とカリスマで進む()
        {
            // charisma=1, institutionalization=1, dt=1 ＝1×1×0.15×1＝0.15。
            float full = HerrschaftRules.CharismaRoutinization(1f, 1f, 1f);
            Assert.AreEqual(0.15f, full, Eps);
            // 制度化が無ければ進まない。
            Assert.AreEqual(0f, HerrschaftRules.CharismaRoutinization(1f, 0f, 1f), Eps);
        }

        /// <summary>近代化が伝統的権威を蝕む＝近代化が進むほど伝統が削られる。</summary>
        [Test]
        public void TraditionErosion_近代化で伝統が削られる()
        {
            // tradition=1, modernization=1, dt=1 ＝1 - 1×0.2×1＝0.8。
            float eroded = HerrschaftRules.TraditionErosion(1f, 1f, 1f);
            Assert.AreEqual(0.8f, eroded, Eps);
            // 近代化が無ければ削られない。
            Assert.AreEqual(1f, HerrschaftRules.TraditionErosion(1f, 0f, 1f), Eps);
        }

        /// <summary>合法的支配は官僚制の規則一貫性で安定する＝一貫性が高いほど高い。</summary>
        [Test]
        public void LegalRationalBureaucracy_規則一貫性で安定()
        {
            // legality=1, ruleConsistency=1 ＝1×(0.4+0.6)＝1.0。
            Assert.AreEqual(1f, HerrschaftRules.LegalRationalBureaucracy(1f, 1f), Eps);
            // 一貫性0でも合法性の素地で0.4。
            Assert.AreEqual(0.4f, HerrschaftRules.LegalRationalBureaucracy(1f, 0f), Eps);
            Assert.Greater(HerrschaftRules.LegalRationalBureaucracy(1f, 1f),
                HerrschaftRules.LegalRationalBureaucracy(1f, 0f));
        }

        /// <summary>崩壊モードは類型ごとに異なる＝伝統＝近代化侵食・カリスマ＝指導者の死・合法＝硬直。</summary>
        [Test]
        public void CollapseMode_類型ごとに崩壊の仕方が異なる()
        {
            Assert.AreEqual(HerrschaftCollapseMode.近代化侵食, HerrschaftRules.CollapseMode(HerrschaftType.伝統的));
            Assert.AreEqual(HerrschaftCollapseMode.指導者の死失敗, HerrschaftRules.CollapseMode(HerrschaftType.カリスマ的));
            Assert.AreEqual(HerrschaftCollapseMode.正統性危機硬直, HerrschaftRules.CollapseMode(HerrschaftType.合法的));
        }

        /// <summary>継承の安定＝合法は制度で円滑・カリスマは後継者問題で危機（明確でも合法に及ばない）。</summary>
        [Test]
        public void SuccessionStability_合法は円滑_カリスマは危機()
        {
            // 後継者が明確(1)でも：合法0.7+0.3＝1.0／カリスマ0.15+0.45＝0.6。
            float legal = HerrschaftRules.SuccessionStability(HerrschaftType.合法的, 1f);
            float chari = HerrschaftRules.SuccessionStability(HerrschaftType.カリスマ的, 1f);
            Assert.AreEqual(1f, legal, Eps);
            Assert.AreEqual(0.6f, chari, Eps);
            Assert.Greater(legal, chari);
            // 後継者が不明確(0)のカリスマは特に低い＝0.15。
            Assert.AreEqual(0.15f, HerrschaftRules.SuccessionStability(HerrschaftType.カリスマ的, 0f), Eps);
        }
    }
}
