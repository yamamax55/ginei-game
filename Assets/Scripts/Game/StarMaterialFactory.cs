using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 恒星3Dモデルのマテリアルを作る<b>唯一の窓口</b>（#恒星モデル）。
    ///
    /// URP の 2D Renderer では 3D ライトが効かないため、恒星は自己シェーディングのシェーダー
    /// （<c>Ginei/StarSelfLit</c>）で陰影を作る。色はモデルの頂点カラーが持つのでここでは付けない。
    ///
    /// 戦略MAPの盤面（<see cref="GalaxyView"/>）と、艦隊メニューの行き先プレビュー
    /// （<see cref="ModelPreviewLibrary"/>）の<b>両方が同じ見た目</b>になるよう、
    /// マテリアルの作り方をここ1か所に集約する（別々に作ると同じ恒星が別物に見える）。
    ///
    /// スロット名ごとに1つ作って使い回す（実行時生成のマテリアルはリークしないよう
    /// <see cref="ReleaseAll"/> で破棄する）。
    /// </summary>
    public static class StarMaterialFactory
    {
        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        /// <summary>そのレンダラのマテリアルを恒星用へ差し替える（スロット名で作り分ける）。</summary>
        public static void Apply(Renderer r)
        {
            if (r == null) return;
            Material[] slots = r.sharedMaterials;
            if (slots == null || slots.Length == 0) return;

            var next = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                string slotName = slots[i] != null ? slots[i].name : (i == 1 ? "Prominence" : "Photosphere");
                next[i] = For(slotName) ?? slots[i];
            }
            r.sharedMaterials = next;
        }

        /// <summary>スロット名→恒星マテリアル（生成して使い回す）。</summary>
        public static Material For(string slotName)
        {
            if (string.IsNullOrEmpty(slotName)) slotName = "Photosphere";
            // Unity は実行時に "Foo (Instance)" と付けるので素の名前へ戻す。
            int paren = slotName.IndexOf(" (");
            if (paren > 0) slotName = slotName.Substring(0, paren);

            if (cache.TryGetValue(slotName, out Material cached) && cached != null) return cached;

            Shader shader = Resources.Load<Shader>("Shaders/StarSelfLit");
            if (shader == null) shader = Shader.Find("Ginei/StarSelfLit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var m = new Material(shader) { name = "Star_" + slotName };
            bool prominence = slotName.IndexOf("Prominence", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (m.HasProperty("_Brightness")) m.SetFloat("_Brightness", prominence ? 1.35f : 1.15f);
            if (m.HasProperty("_LimbPower")) m.SetFloat("_LimbPower", prominence ? 0.45f : 0.85f);
            if (m.HasProperty("_LimbFloor")) m.SetFloat("_LimbFloor", prominence ? 0.55f : 0.30f);
            if (m.HasProperty("_CoreBoost")) m.SetFloat("_CoreBoost", prominence ? 0.20f : 0.45f);
            if (m.HasProperty("_SoftKnee")) m.SetFloat("_SoftKnee", 0.55f);

            cache[slotName] = m;
            return m;
        }

        /// <summary>
        /// 生成したマテリアルを破棄する（実行時生成の Material は自動では解放されない＝規約）。
        /// 盤面を組み直すときに呼ぶ。次に <see cref="For"/> が呼ばれれば作り直される。
        /// </summary>
        public static void ReleaseAll()
        {
            foreach (var kv in cache)
            {
                if (kv.Value == null) continue;
                if (Application.isPlaying) Object.Destroy(kv.Value);
                else Object.DestroyImmediate(kv.Value);
            }
            cache.Clear();
        }
    }
}
