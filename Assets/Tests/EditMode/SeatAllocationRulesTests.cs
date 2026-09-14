using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 議席配分（最大剰余法）を固定する：総数保存・端数の配り方・同点の決着（剰余→得票→ID）・得票0と空入力の安全。
    /// </summary>
    public class SeatAllocationRulesTests
    {
        [Test]
        public void ExactQuotas_AreAllocatedAsIs()
        {
            int[] r = SeatAllocationRules.LargestRemainder(new[] { 1, 2, 3 }, new[] { 50f, 30f, 20f }, 10);
            CollectionAssert.AreEqual(new[] { 5, 3, 2 }, r);
        }

        [Test]
        public void Remainder_GoesToLargestFraction()
        {
            // 3.29 / 2.31 / 1.40 → 床 3/2/1、残り1議席は剰余0.40の党3
            int[] r = SeatAllocationRules.LargestRemainder(new[] { 1, 2, 3 }, new[] { 47f, 33f, 20f }, 7);
            CollectionAssert.AreEqual(new[] { 3, 2, 2 }, r);
        }

        [Test]
        public void RemainderAndVotesTie_SmallerIdWins_RegardlessOfOrder()
        {
            // 並びは ID 5, 2。1.5 / 1.5 → 残り1議席は ID の小さい 2 へ
            int[] r = SeatAllocationRules.LargestRemainder(new[] { 5, 2 }, new[] { 10f, 10f }, 3);
            CollectionAssert.AreEqual(new[] { 1, 2 }, r);
        }

        [Test]
        public void RemainderTie_MoreVotesWinsBeforeId()
        {
            // 150 / 112.5 / 37.5 → 床 150/112/37、剰余0.5同士は得票の多い党2へ
            int[] r = SeatAllocationRules.LargestRemainder(new[] { 1, 2, 3 }, new[] { 0.5f, 0.375f, 0.125f }, 300);
            CollectionAssert.AreEqual(new[] { 150, 113, 37 }, r);
        }

        [Test]
        public void TotalIsConserved_WithRepeatingFractions()
        {
            int[] r = SeatAllocationRules.LargestRemainder(new[] { 3, 1, 2 }, new[] { 1f, 1f, 1f }, 100);
            Assert.AreEqual(100, r[0] + r[1] + r[2]);
            CollectionAssert.AreEqual(new[] { 33, 34, 33 }, r); // 端数の1議席は ID 1
        }

        [Test]
        public void ZeroVoteParty_GetsNoSeat()
        {
            int[] r = SeatAllocationRules.LargestRemainder(new[] { 1, 2 }, new[] { 0f, 5f }, 3);
            CollectionAssert.AreEqual(new[] { 0, 3 }, r);
        }

        [Test]
        public void NoVotes_NoSeats_NullSafe()
        {
            CollectionAssert.AreEqual(new[] { 0, 0 }, SeatAllocationRules.LargestRemainder(new[] { 1, 2 }, new[] { 0f, 0f }, 10));
            CollectionAssert.AreEqual(new[] { 0, 0 }, SeatAllocationRules.LargestRemainder(new[] { 1, 2 }, new[] { 1f, 1f }, 0));
            Assert.AreEqual(0, SeatAllocationRules.LargestRemainder(null, null, 10).Length);
        }
    }
}
