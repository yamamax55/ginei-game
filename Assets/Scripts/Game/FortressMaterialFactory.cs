using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 要塞系3Dモデル（球状要塞・浮遊砲台）のマテリアルを作る<b>唯一の窓口</b>。
    ///
    /// このプロジェクトの URP は 2D Renderer で 3D ライトが効かないため、自己陰影シェーダー
    /// <c>Ginei/FortressSelfLit</c> を使う。スロット名（Blender の材質名）ごとの色・金属感・発光を
    /// ここ1か所で決めることで、<b>要塞本体と浮遊砲台が同じ質感</b>になる（別々に作ると別素材に見える）。
    ///
    /// 納品モデルのスロット：Armor_Silver / Armor_Light / Armor_Dark / Recess_Graphite /
    /// Gunmetal / Reactor_Cyan / Windows_Amber。
    /// </summary>
    public static class FortressMaterialFactory
    {
        /// <summary>既定のキーライト方向（自己陰影シェーダーの擬似光源）。</summary>
        public static readonly Vector3 DefaultKeyLightDir = new Vector3(-0.45f, 0.72f, -0.53f);

        private static readonly Dictionary<string, Material> shared = new Dictionary<string, Material>();
        private static Material wreckMaterial;

        /// <summary>スロット名の正規化（Unity が付ける " (Instance)" を落とす）。</summary>
        public static string NormalizeSlot(string slotName)
        {
            if (string.IsNullOrEmpty(slotName)) return "Armor_Silver";
            int paren = slotName.IndexOf(" (");
            return paren > 0 ? slotName.Substring(0, paren) : slotName;
        }

        /// <summary>
        /// スロット名から<b>新しい</b>マテリアルを作る（キャッシュしない）。
        /// 呼び手が自分のキャッシュに入れて、破棄まで面倒を見る（実行時生成の Material はリークするため）。
        /// </summary>
        public static Material Create(string slotName, Vector3 keyLightDir)
        {
            slotName = NormalizeSlot(slotName);

            Shader shader = Resources.Load<Shader>("Shaders/FortressSelfLit");
            if (shader == null) shader = Shader.Find("Ginei/FortressSelfLit");
            if (shader == null) shader = Shader.Find("Sprites/Default"); // 最後の保険（陰影なしでも形は出る）
            if (shader == null) return null;

            var m = new Material(shader) { name = "Fortress_" + slotName };
            Color baseColor; float metallic, smoothness; Color emission = Color.black;
            switch (slotName)
            {
                case "Recess_Graphite":
                    baseColor = new Color(0.13f, 0.15f, 0.18f); metallic = 0.30f; smoothness = 0.25f; break;
                case "Armor_Light":
                    baseColor = new Color(0.82f, 0.85f, 0.89f); metallic = 0.60f; smoothness = 0.62f; break;
                case "Armor_Dark":
                    baseColor = new Color(0.42f, 0.46f, 0.53f); metallic = 0.70f; smoothness = 0.45f; break;
                case "Gunmetal":
                    baseColor = new Color(0.30f, 0.34f, 0.40f); metallic = 0.85f; smoothness = 0.50f; break;
                case "Reactor_Cyan":
                    baseColor = new Color(0.78f, 0.93f, 1f); metallic = 0f; smoothness = 0.85f;
                    emission = new Color(0.55f, 0.85f, 1f) * 3.0f; break;
                case "Windows_Amber":
                    baseColor = new Color(1f, 0.82f, 0.52f); metallic = 0f; smoothness = 0.60f;
                    emission = new Color(1f, 0.66f, 0.30f) * 2.0f; break;
                default:
                    baseColor = new Color(0.72f, 0.75f, 0.80f); metallic = 0.65f; smoothness = 0.55f; break;
            }

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_LightDir"))
                m.SetVector("_LightDir", new Vector4(keyLightDir.x, keyLightDir.y, keyLightDir.z, 0f));

            if (emission != Color.black)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
                if (m.HasProperty("_AmbientColor")) m.SetColor("_AmbientColor", new Color(0.9f, 0.9f, 0.9f));
                if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0.1f);
            }
            return m;
        }

        /// <summary>共有キャッシュから引く（浮遊砲台のように同じモデルが多数出るもの向け）。</summary>
        public static Material Shared(string slotName)
        {
            slotName = NormalizeSlot(slotName);
            if (shared.TryGetValue(slotName, out Material cached) && cached != null) return cached;
            Material m = Create(slotName, DefaultKeyLightDir);
            if (m != null) shared[slotName] = m;
            return m;
        }

        /// <summary>そのレンダラのマテリアルを要塞系の質感へ差し替える（共有キャッシュを使う）。</summary>
        public static void Apply(Renderer r, Color tint)
        {
            if (r == null) return;
            Material[] slots = r.sharedMaterials;
            if (slots == null || slots.Length == 0) return;

            var next = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                string slotName = slots[i] != null ? slots[i].name : "Armor_Silver";
                next[i] = Shared(slotName) ?? slots[i];
            }
            r.sharedMaterials = next;
        }

        /// <summary>沈黙した砲台の見た目＝発光しない暗い残骸（破壊されたことが分かる）。</summary>
        public static void ApplyWreck(Renderer r)
        {
            if (r == null) return;
            if (wreckMaterial == null)
            {
                wreckMaterial = Create("Recess_Graphite", DefaultKeyLightDir);
                if (wreckMaterial != null)
                {
                    wreckMaterial.name = "Fortress_Wreck";
                    if (wreckMaterial.HasProperty("_BaseColor"))
                        wreckMaterial.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.18f));
                    else if (wreckMaterial.HasProperty("_Color"))
                        wreckMaterial.SetColor("_Color", new Color(0.16f, 0.16f, 0.18f));
                    wreckMaterial.DisableKeyword("_EMISSION");
                    if (wreckMaterial.HasProperty("_EmissionColor"))
                        wreckMaterial.SetColor("_EmissionColor", Color.black);
                }
            }
            if (wreckMaterial == null) return;

            Material[] slots = r.sharedMaterials;
            if (slots == null || slots.Length == 0) return;
            var next = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++) next[i] = wreckMaterial;
            r.sharedMaterials = next;
        }

        /// <summary>共有マテリアルを破棄する（会戦を閉じるときに呼ぶ＝実行時生成はリークするため）。</summary>
        public static void ReleaseAll()
        {
            foreach (var kv in shared)
            {
                if (kv.Value == null) continue;
                if (Application.isPlaying) Object.Destroy(kv.Value); else Object.DestroyImmediate(kv.Value);
            }
            shared.Clear();
            if (wreckMaterial != null)
            {
                if (Application.isPlaying) Object.Destroy(wreckMaterial); else Object.DestroyImmediate(wreckMaterial);
                wreckMaterial = null;
            }
        }
    }
}
