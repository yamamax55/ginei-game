using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 提督の武名（fame・ADM-3 #2304）の実行時台帳（static）。人物（AdmiralData）id→fame。会戦戦功で<b>動的に上がる</b>武名を
    /// 一時オブジェクトに捨てず id キーで蓄える（<see cref="GrowthRegistry"/> と同型）。基準の <c>AdmiralData.fame</c>（設計時）とは別系統で、
    /// 会戦の実効武名は「max(基準, 実行時)」で読む（基準非破壊＝実効値パターン）。キーは呼び出し側が与える（GetInstanceID 等）＝
    /// Core 純ロジック（UnityEngine 非依存）。永続は安定キーで別途（主人公分は ProtagonistCareerSave.fame）。test-first。
    /// </summary>
    public static class FameRegistry
    {
        private static readonly Dictionary<int, int> byPerson = new Dictionary<int, int>();

        /// <summary>id の武名（無ければ0）。</summary>
        public static int Get(int personId) => byPerson.TryGetValue(personId, out int v) ? v : 0;

        /// <summary>id の武名を設定（0..100 クランプ）。</summary>
        public static void Set(int personId, int fame)
            => byPerson[personId] = fame < 0 ? 0 : (fame > 100 ? 100 : fame);

        /// <summary>id の実行時武名が登録済みか。</summary>
        public static bool Has(int personId) => byPerson.ContainsKey(personId);

        /// <summary>登録人数。</summary>
        public static int Count => byPerson.Count;

        /// <summary>台帳を空にする（戦役の作り直し・テスト）。</summary>
        public static void Clear() => byPerson.Clear();
    }
}
