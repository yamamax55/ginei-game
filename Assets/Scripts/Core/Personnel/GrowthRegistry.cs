using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 提督の成長（経験）の実行時台帳（#2477 P1-b・static）。人物id→<see cref="Growth"/>。会戦で得た経験を一時オブジェクトに
    /// 捨てず id キーで蓄える（<see cref="MedalRegistry"/> と同型の台帳）。基準能力は <c>AdmiralData</c> が持ち、ここは
    /// 経験のみを蓄える（基準非破壊＝実効値パターン）。共有 ScriptableObject を実行時に書き換えないための分離。
    /// キー（GetInstanceID 等）は呼び出し側が与える＝Core 純ロジック（UnityEngine 非依存）。
    /// ※セッション跨ぎの永続（セーブ）は別途＝instance id は不安定なので安定キーが要る（P2-b と合流）。test-first。
    /// </summary>
    public static class GrowthRegistry
    {
        private static readonly Dictionary<int, Growth> byPerson = new Dictionary<int, Growth>();

        /// <summary>id の成長を取得（無ければ <paramref name="archetype"/> で生成して登録）。</summary>
        public static Growth GetOrCreate(int personId, GrowthArchetype archetype)
        {
            if (!byPerson.TryGetValue(personId, out Growth g))
            {
                g = new Growth(archetype);
                byPerson[personId] = g;
            }
            return g;
        }

        /// <summary>id の成長（無ければ null）。</summary>
        public static Growth Get(int personId)
            => byPerson.TryGetValue(personId, out Growth g) ? g : null;

        /// <summary>id の成長が登録済みか。</summary>
        public static bool Has(int personId) => byPerson.ContainsKey(personId);

        /// <summary>経験を加算する（無ければ生成）。数式は <see cref="GrowthRules.GainExperience"/> へ委譲（二重実装しない）。</summary>
        public static void GainExperience(int personId, GrowthArchetype archetype, float amount, float dt)
            => GrowthRules.GainExperience(GetOrCreate(personId, archetype), amount, dt);

        /// <summary>登録人数。</summary>
        public static int Count => byPerson.Count;

        /// <summary>台帳を空にする（戦役の作り直し・テスト）。</summary>
        public static void Clear() => byPerson.Clear();
    }
}
