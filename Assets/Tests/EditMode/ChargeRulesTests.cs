using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>突撃（#突撃）成立判定の純ロジックテスト。正面同士の判定と突撃間合いを担保する。</summary>
    public class ChargeRulesTests
    {
        [Test]
        public void IsHeadOn_互いに正対なら成立()
        {
            // self は原点で +y(上)を向く。enemy は上(0,10)で -y(下=自分の方)を向く＝正面同士。
            bool r = ChargeRules.IsHeadOn(Vector2.zero, Vector2.up, new Vector2(0f, 10f), Vector2.down);
            Assert.IsTrue(r);
        }

        [Test]
        public void IsHeadOn_敵が側面を向いていれば不成立()
        {
            // enemy は上に居るが右(+x)を向く＝側面を晒す＝正面衝突ではない。
            bool r = ChargeRules.IsHeadOn(Vector2.zero, Vector2.up, new Vector2(0f, 10f), Vector2.right);
            Assert.IsFalse(r);
        }

        [Test]
        public void IsHeadOn_自分が敵を向いていなければ不成立()
        {
            // self が右を向き、敵は上＝自分の前方に敵が居ない。
            bool r = ChargeRules.IsHeadOn(Vector2.zero, Vector2.right, new Vector2(0f, 10f), Vector2.down);
            Assert.IsFalse(r);
        }

        [Test]
        public void InChargeRange_間合い内外()
        {
            Assert.IsTrue(ChargeRules.InChargeRange(Vector2.zero, new Vector2(0f, 10f), 14f));
            Assert.IsFalse(ChargeRules.InChargeRange(Vector2.zero, new Vector2(0f, 20f), 14f));
        }
    }
}
