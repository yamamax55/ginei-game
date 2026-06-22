using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 尉官〜佐官の道（TKO-12 #2489）を固定する：完全ラダー名（<see cref="RankSystem.FullLadderName"/>／
    /// <see cref="RankSystem.CareerRankName"/>）の尉官/佐官＋将官フォールバック、士官学校卒の初任階級
    /// （<see cref="MilitaryAcademyRules.CommissionTier"/>）、既存 <see cref="RankSystem.DefaultRankName"/> の不変。
    /// </summary>
    public class OfficerLadderRulesTests
    {
        [Test]
        public void FullLadderName_JuniorRanksThenFlagFallback()
        {
            Assert.AreEqual("少尉", RankSystem.FullLadderName(1));
            Assert.AreEqual("中尉", RankSystem.FullLadderName(2));
            Assert.AreEqual("大尉", RankSystem.FullLadderName(3));
            Assert.AreEqual("少佐", RankSystem.FullLadderName(4));
            Assert.AreEqual("中佐", RankSystem.FullLadderName(5));
            Assert.AreEqual("大佐", RankSystem.FullLadderName(6));
            // 7-12 は将官（DefaultRankName 委譲）
            Assert.AreEqual("准将", RankSystem.FullLadderName(7));
            Assert.AreEqual("元帥", RankSystem.FullLadderName(12));
            // 範囲外
            Assert.AreEqual("", RankSystem.FullLadderName(0));
            Assert.AreEqual("", RankSystem.FullLadderName(13));
        }

        [Test]
        public void DefaultRankName_StaysFlagOnly_Unchanged()
        {
            // DefaultRankName は将官（7-12）のみ＝尉官/佐官帯は将官フォールバックでは空のまま
            Assert.AreEqual("", RankSystem.DefaultRankName(6));
            Assert.AreEqual("", RankSystem.DefaultRankName(1));
            Assert.AreEqual("准将", RankSystem.DefaultRankName(7));
        }

        [Test]
        public void CareerRankName_PrefersFactionTable_ElseFullLadder()
        {
            // 階級表に尉官が無い勢力 → FullLadder へフォールバック
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry> { new FactionData.RankEntry(7, "准将") };
            Assert.AreEqual("少尉", RankSystem.CareerRankName(f, 1), "表に無ければ完全ラダー");
            Assert.AreEqual("准将", RankSystem.CareerRankName(f, 7), "表にあれば表優先");
            Assert.AreEqual("", RankSystem.CareerRankName(f, 0));

            // 表が独自名を持てばそれを使う
            var f2 = ScriptableObject.CreateInstance<FactionData>();
            f2.ranks = new List<FactionData.RankEntry> { new FactionData.RankEntry(1, "見習士官") };
            Assert.AreEqual("見習士官", RankSystem.CareerRankName(f2, 1));
        }

        [Test]
        public void CommissionTier_ByDegree()
        {
            Assert.AreEqual(1, MilitaryAcademyRules.CommissionTier(MilitaryDegree.士官学校卒, 1), "士官学校卒＝少尉");
            Assert.AreEqual(3, MilitaryAcademyRules.CommissionTier(MilitaryDegree.大学校卒, 1), "大学校卒＝大尉（fast-track）");
            Assert.AreEqual(0, MilitaryAcademyRules.CommissionTier(MilitaryDegree.幼年学校卒, 1), "未任官");
            Assert.AreEqual(0, MilitaryAcademyRules.CommissionTier(MilitaryDegree.無資格, 1), "退校＝未任官");
        }

        [Test]
        public void JuniorLadder_PromotesStepwiseToFlag()
        {
            // 完全12段ラダーを持つ勢力で 少尉(1) から段階昇進＝准将(7)まで尉官/佐官を1段ずつ
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(1, "少尉"), new FactionData.RankEntry(2, "中尉"),
                new FactionData.RankEntry(3, "大尉"), new FactionData.RankEntry(4, "少佐"),
                new FactionData.RankEntry(5, "中佐"), new FactionData.RankEntry(6, "大佐"),
                new FactionData.RankEntry(7, "准将"), new FactionData.RankEntry(8, "少将"),
            };
            Assert.AreEqual(2, RankSystem.NextRankTier(f, 1), "少尉→中尉");
            Assert.AreEqual(3, RankSystem.NextRankTier(f, 2));
            Assert.AreEqual(6, RankSystem.NextRankTier(f, 5), "中佐→大佐");
            Assert.AreEqual(7, RankSystem.NextRankTier(f, 6), "大佐→准将（将官へ）");
            Assert.AreEqual(8, RankSystem.NextRankTier(f, 7));
        }
    }
}
