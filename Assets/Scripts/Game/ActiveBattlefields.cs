using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// いま<b>戦術マップで戦っている戦場</b>の一覧（Game 層・static）。
    ///
    /// 戦略側の自動解決（<see cref="GalaxyView"/> の要塞力攻め・回廊会戦の自動決着）が、
    /// プレイヤーが潜行中の戦場を横取りしないようにするための唯一の窓口。
    ///
    /// <b>なぜ <see cref="BattleHandoff"/> を見てはいけないか</b>：ウィンドウ化会戦（既定 ON）では
    /// <see cref="BattleDirector"/> がロード完了時に <c>BattleHandoff.Clear()</c> を呼んで global を空ける。
    /// そのため「潜行中か」を global の受け渡しで判定すると、ウィンドウ会戦では必ず false になり、
    /// 戦っている最中に戦略側が同じ要塞を力攻めで片付けてしまう（実機QA：戦術中に
    /// 「攻略に失敗＝難攻不落」が出て、突入中の艦隊が盤面から消えた）。
    ///
    /// 登録・解除は会戦シーン側（<see cref="BattleManager"/>）が行い、シーンごとに1件持つ。
    /// シーンをまたぐので static。会戦が異常終了してもシーン破棄で必ず解除される。
    /// </summary>
    public static class ActiveBattlefields
    {
        // シーン → その会戦が扱っている戦場キー。シーン単位なので複数同時会戦でも取り違えない。
        private static readonly Dictionary<UnityEngine.SceneManagement.Scene, BattlefieldKey> active
            = new Dictionary<UnityEngine.SceneManagement.Scene, BattlefieldKey>();

        /// <summary>その会戦シーンが扱っている戦場を登録する（同じシーンは上書き）。</summary>
        public static void Register(UnityEngine.SceneManagement.Scene scene, BattlefieldKey key)
        {
            if (!key.IsValid) return;
            active[scene] = key;
        }

        /// <summary>その会戦シーンの登録を外す（戦略へ戻るとき・シーン破棄時）。</summary>
        public static void Unregister(UnityEngine.SceneManagement.Scene scene) => active.Remove(scene);

        /// <summary>いまその戦場を誰かが戦術マップで戦っているか。</summary>
        public static bool IsActive(BattlefieldKey key)
        {
            if (!key.IsValid) return false;
            foreach (var kv in active) if (kv.Value == key) return true;
            return false;
        }

        /// <summary>回廊（両端の星系ID）を戦っているか。</summary>
        public static bool IsCorridorActive(int aId, int bId) => IsActive(BattlefieldKey.Corridor(aId, bId));

        /// <summary>登録数（デバッグ・QA 用）。</summary>
        public static int Count => active.Count;

        /// <summary>全消去（シーン初期化・新規戦役）。</summary>
        public static void Clear() => active.Clear();
    }
}
