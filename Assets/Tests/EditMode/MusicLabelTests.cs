using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>音楽レーベル（#2025・<see cref="MusicLabelRules"/>）：ストリーミング印税(MUS-1)・カタログ価値(MUS-2)・前払金未回収(MUS-3)・利益(MUS-4)。</summary>
    public class MusicLabelTests
    {
        [Test]
        public void Streaming_AndCatalog()
        {
            Assert.AreEqual(50000f, MusicLabelRules.StreamingRoyalty(10000000f, 0.005f), 1e-1f); // 1再生は極小
            Assert.AreEqual(500000f, MusicLabelRules.CatalogValue(50000f, 10f), 1e-1f); // 旧譜は資産
        }

        [Test]
        public void Recoupment_AndProfit()
        {
            Assert.AreEqual(40000f, MusicLabelRules.ArtistAdvanceRecoupment(100000f, 60000f), 1e-1f); // 未回収40000
            Assert.AreEqual(0f, MusicLabelRules.ArtistAdvanceRecoupment(100000f, 120000f), 1e-1f);    // 回収済み
            Assert.AreEqual(40000f, MusicLabelRules.MusicLabelProfit(200000f, 80000f, 50000f, 30000f), 1e-1f);
        }
    }
}
