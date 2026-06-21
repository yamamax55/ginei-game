using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// ウィンドウ化会戦（WIN-4 #2571）の UI 帰属レジストリ。各 <see cref="BattleWindow"/> が自分の会戦シーンと
    /// 「窓内の UI 親矩形（RawImage 矩形に重なる・RectMask2D でクリップ）」を登録し、会戦シーン側の UI
    /// （<see cref="FleetHUDManager"/>/<see cref="CommandMenu"/>/<see cref="Minimap"/>）が自分のシーンの親矩形を引いて
    /// 配下へ親替えする。これにより会戦 UI がフルスクリーンへ広がらず、対応する窓の中に収まる（複数窓で重ならない）。
    /// 非ウィンドウ（フルスクリーン会戦）では登録が無く、各 UI は従来どおり全画面に出る（後方互換）。
    /// </summary>
    public static class BattleWindowUI
    {
        // 会戦シーン → 窓内 UI 親矩形（BattleWindow が登録）。
        private static readonly Dictionary<Scene, RectTransform> roots = new Dictionary<Scene, RectTransform>();

        /// <summary>会戦シーンと、その窓内 UI 親矩形を登録する（BattleWindow が呼ぶ）。</summary>
        public static void Register(Scene scene, RectTransform uiRoot)
        {
            if (!scene.IsValid() || uiRoot == null) return;
            roots[scene] = uiRoot;
        }

        /// <summary>登録解除（窓を閉じたとき）。</summary>
        public static void Unregister(Scene scene)
        {
            roots.Remove(scene);
        }

        /// <summary>指定シーンの窓内 UI 親矩形を返す（未登録＝フルスクリーンなら false）。</summary>
        public static bool TryGetRoot(Scene scene, out RectTransform uiRoot)
        {
            if (roots.TryGetValue(scene, out uiRoot) && uiRoot != null) return true;
            uiRoot = null;
            return false;
        }

        /// <summary>当該シーンがウィンドウ化会戦か（登録された窓 UI 親矩形を持つか）。</summary>
        public static bool IsWindowed(Scene scene) => TryGetRoot(scene, out _);

        /// <summary>
        /// 会戦シーンの UI パネルを、その窓内 UI 親矩形へ親替えする共通ヘルパ。親矩形がまだ未登録なら false
        /// （呼び出し側は次フレーム再試行）。RawImage 矩形に重なる親へ移すので、パネルのアンカー（右上/中央/右下）が
        /// そのまま「窓の中の右上/中央/右下」になる。
        /// </summary>
        public static bool TryAttach(Scene scene, RectTransform panel)
        {
            if (panel == null) return true; // 対象なし＝完了扱い
            if (!TryGetRoot(scene, out RectTransform root)) return false;
            panel.SetParent(root, false);
            panel.SetAsLastSibling();
            return true;
        }

        /// <summary>
        /// 指定シーンに属する同型コンポーネントを優先して返す（複数会戦が同時にロードされていても自分の会戦の
        /// 相手を掴む）。同シーンに無ければ任意の1つへフォールバック＝フルスクリーン会戦では従来動作と同一。
        /// </summary>
        public static T FindInSceneOrAny<T>(Scene scene) where T : Component
        {
            T[] all = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            T any = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (any == null) any = all[i];
                if (all[i].gameObject.scene == scene) return all[i];
            }
            return any;
        }
    }
}
