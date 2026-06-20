using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 世界観の開示連鎖（IHER-5 #2401『星を継ぐもの』着想・本作オリジナル）。
    /// 「散らばった証拠が収束して確証になる」(IHER-1 <see cref="EvidenceRules"/>) と、開示の連鎖
    /// (<see cref="DisclosureLedger"/>) を橋渡しする lore データ＝先行文明の発見→失われた惑星→
    /// 起源としての移民→宇宙の孤独、という 4 段の真相を連鎖開示する。
    /// 専用ロジックは書かず、<see cref="DisclosureEntry"/> を組み立てて返すだけ（<see cref="SampleDisclosures"/> に倣う）。
    /// 起点 <see cref="Precursor"/> の条件は、<see cref="EventContext.payload"/> に積まれた証拠群
    /// (<see cref="EvidenceBody"/>) が確証 (<see cref="EvidenceRules.IsConclusive"/>) になったとき真。
    /// 著作物の固有名詞・引用は用いず、原案の概念のみを汎用的に表現する。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IherDisclosures
    {
        // 開示 id（セーブ直列化のキー＝安定させる）。
        public const string Precursor = "iher.precursor";
        public const string LostPlanet = "iher.lost-planet";
        public const string Migration = "iher.migration";
        public const string Solitude = "iher.solitude";

        /// <summary>確証とみなす実効信頼性の既定閾値（<see cref="EvidenceRules.IsConclusive"/> へ渡す）。</summary>
        public const float ConclusiveThreshold = 1.0f;

        /// <summary>
        /// 4 段の lore 開示を構築して返す（登録順は不問＝<see cref="DisclosureLedger"/> が前提を見て連鎖開示）。
        /// </summary>
        public static IEnumerable<DisclosureEntry> All()
        {
            return All(ConclusiveThreshold);
        }

        /// <summary>確証閾値を指定して構築する版（テスト・チューニング用）。</summary>
        public static IEnumerable<DisclosureEntry> All(float conclusiveThreshold)
        {
            var list = new List<DisclosureEntry>(4);

            // 1. 先行文明の発見（前提なし。条件：渡された証拠群が確証になったとき）。
            list.Add(new DisclosureEntry(
                    Precursor,
                    "先史の証",
                    "銀河には、人類より前に星々を渡った文明が存在した。散らばっていた証拠が、ひとつの真実へ収束した。",
                    "秘史")
                .When(ctx => ctx != null
                             && ctx.payload is EvidenceBody body
                             && EvidenceRules.IsConclusive(body, conclusiveThreshold)));

            // 2. 失われた惑星（前提：先行文明）。
            list.Add(new DisclosureEntry(
                    LostPlanet,
                    "砕けた世界",
                    "ある星系の小惑星帯は、かつてひとつの惑星だった——何かがそれを砕いたのだ。",
                    "真相")
                .Requires(Precursor));

            // 3. 移民としての起源（前提：先行文明＋失われた惑星）。
            list.Add(new DisclosureEntry(
                    Migration,
                    "遠き故郷",
                    "ある勢力の起源は、この銀河の外から渡ってきた移民の末裔なのかもしれない。",
                    "真相")
                .Requires(Precursor, LostPlanet));

            // 4. 宇宙の孤独（前提：移民としての起源）。
            list.Add(new DisclosureEntry(
                    Solitude,
                    "静寂の宇宙",
                    "宇宙はかつて、無数の声で賑わっていた。いま私たちは、その広さの中で独りきりだ。",
                    "真相")
                .Requires(Migration));

            return list;
        }
    }
}
