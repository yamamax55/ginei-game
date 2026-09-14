using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 星系情報パネルを開いたまま統治政策が変わった時、上段（上申欄）と下段（本文）が同じ政策を示すか。
    /// GalaxyView は置かない＝パネルは上申欄と同じ <see cref="StrategySession"/> の地図/内政を読み直す経路で試す。
    /// 政策は稟議を通さず直接書き換える（判定・執行はこの試験の対象外＝表示の更新だけを見る）。
    /// </summary>
    public class SystemDetailPanelRefreshPlayModeTests
    {
        private const int SystemId = 7;
        private const float WaitRealtimeSeconds = 1.0f; // 既定の更新間隔 0.5 秒より長く待つ

        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;

        [SetUp]
        public void SetUp()
        {
            savedMap = StrategySession.Map;
            savedProvinces = StrategySession.Provinces;
        }

        [TearDown]
        public void TearDown()
        {
            SystemDetailPanel panel = Object.FindAnyObjectByType<SystemDetailPanel>();
            if (panel != null) Object.DestroyImmediate(panel.gameObject);
            StrategySession.Map = savedMap;
            StrategySession.Provinces = savedProvinces;
        }

        [UnityTest]
        public IEnumerator OpenPanel_PolicyChanged_BodyMatchesGovernanceBlockAfterRefresh()
        {
            var system = new StarSystem { id = SystemId, systemName = "テスト星系", owner = Faction.同盟 };
            var map = new GalaxyMap();
            map.AddSystem(system);
            var prov = new Province(SystemId, "") { governancePolicy = GovernancePolicy.民生 };
            StrategySession.Map = map;
            StrategySession.Provinces = new Dictionary<int, Province> { { SystemId, prov } };

            SystemDetailPanel.Show(system, prov, 0, "");
            yield return null;
            StringAssert.Contains("統治政策: 民生", SystemDetailPanel.BodyTextForTest);
            StringAssert.Contains("現在「民生」", SystemDetailPanel.GovernanceTextForTest);

            prov.governancePolicy = GovernancePolicy.動員;
            yield return new WaitForSecondsRealtime(WaitRealtimeSeconds);

            StringAssert.Contains("現在「動員」", SystemDetailPanel.GovernanceTextForTest, "上段が最新の政策を示す");
            StringAssert.Contains("統治政策: 動員", SystemDetailPanel.BodyTextForTest, "開いたままでも本文が最新の政策に追従する");
            StringAssert.DoesNotContain("統治政策: 民生", SystemDetailPanel.BodyTextForTest, "本文に古い政策が残らない");
        }
    }
}
