using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 稟議の決裁フローを<b>PlayMode（実ランタイム）</b>で通す自動テスト：<see cref="RingiDirector"/>（MonoBehaviour）が
    /// 建白を起こし、<see cref="DecisionDeck"/> の決裁（<see cref="DecisionDeck.Resolved"/> 合流）で <see cref="RingiPipeline"/> が
    /// 執行し、世界（<see cref="FactionState.taxRate"/>）が動く／見送りでは動かないことを固定する。EditMode 純ロジックでは
    /// 検証できない「MonoBehaviour の購読→執行」配線を実機で担保する。
    /// </summary>
    public class RingiFlowPlayModeTests
    {
        private GameObject directorGo;

        private static FactionState SetupCampaign()
        {
            DecisionDeck.Queue.items.Clear();   // 前テストの積み残しを掃く（id 衝突回避）
            RingiDirector.Ledger.Clear();
            var camp = new CampaignState();
            var fs = new FactionState(Faction.同盟); // taxRate 0.3
            fs.credibility.globalDeference = 1f;
            CredibilityRules.Adjust(fs.credibility, BoxKind.政治家, 1f); // heed を最大化＝建白が浮上しやすい
            camp.states = new List<FactionState> { fs };
            StrategySession.Campaign = camp;
            if (GameSettings.Instance != null) GameSettings.Instance.playerFaction = Faction.同盟;
            return fs;
        }

        [TearDown]
        public void TearDown()
        {
            if (directorGo != null) Object.Destroy(directorGo);
            StrategySession.Campaign = null;
            DecisionDeck.Queue.items.Clear();
            RingiDirector.Ledger.Clear();
        }

        private RingiDirector NewDirector()
        {
            directorGo = new GameObject("RingiDirector_Test");
            return directorGo.AddComponent<RingiDirector>(); // Awake が DecisionDeck.Resolved を購読
        }

        /// <summary>サンプルを浮上するまで起案し続ける（生存ロールは確率的なのでリトライ）。決裁id（&lt;0=失敗）。</summary>
        private static int RaiseUntilSurfaced(RingiDirector dir, int sampleIndex, int maxTries = 400)
        {
            for (int i = 0; i < maxTries; i++)
            {
                int id = dir.ForceRaise(sampleIndex);
                if (id >= 0) return id;
            }
            return -1;
        }

        [UnityTest]
        public IEnumerator Approve_TaxCut_MovesWorld()
        {
            var fs = SetupCampaign();
            var dir = NewDirector();
            yield return null; // Awake/購読を確定

            float before = fs.taxRate; // 0.3
            int decisionId = RaiseUntilSurfaced(dir, sampleIndex: 0); // 減税
            Assert.GreaterOrEqual(decisionId, 0, "建白が決裁デスクまで浮上しなかった");

            Assert.IsTrue(DecisionDeck.Resolve(decisionId, 0), "決裁できなかった"); // 裁可
            Assert.Less(fs.taxRate, before, "裁可で税率が下がるはず（官僚に骨抜きされつつも世界が動く）");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Decline_TaxCut_LeavesWorldUnchanged()
        {
            var fs = SetupCampaign();
            var dir = NewDirector();
            yield return null;

            float before = fs.taxRate;
            int decisionId = RaiseUntilSurfaced(dir, sampleIndex: 0);
            Assert.GreaterOrEqual(decisionId, 0);

            Assert.IsTrue(DecisionDeck.Resolve(decisionId, 1)); // 見送る（現状維持）
            Assert.AreEqual(before, fs.taxRate, 1e-4f, "見送りは世界を動かさない");
            yield return null;
        }
    }
}
