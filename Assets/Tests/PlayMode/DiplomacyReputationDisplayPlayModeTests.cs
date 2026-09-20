using System.Collections.Generic;
using NUnit.Framework;

namespace Ginei.Tests
{
    public class DiplomacyReputationDisplayPlayModeTests
    {
        [Test]
        public void ReputationSection_Shows_Doctrine_Dao_And_PairFactors()
        {
            var empire = new FactionState(Faction.帝国)
            {
                foreignDoctrine = ForeignDoctrine.保守,
                daoValue = -0.8f
            };
            var alliance = new FactionState(Faction.同盟)
            {
                foreignDoctrine = ForeignDoctrine.改革,
                daoValue = 0.4f
            };

            string text = DiplomacyObserverOverlay.BuildReputationSection(
                new List<FactionState> { empire, alliance });

            StringAssert.Contains("帝国　主義=保守 ／ 覇道 -0.80", text);
            StringAssert.Contains("同盟　主義=改革 ／ 王道 +0.40", text);
            StringAssert.Contains("帝国 ⇔ 同盟　主義 -1.00 ／ 評判 -0.10", text);
        }

        [Test]
        public void ReputationSection_Handles_MissingCampaignState()
        {
            string text = DiplomacyObserverOverlay.BuildReputationSection(null);
            StringAssert.Contains("国家状態なし", text);
        }
    }
}
