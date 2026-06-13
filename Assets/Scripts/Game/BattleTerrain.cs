using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 戦場の地形／危険地帯（#2181・小惑星帯/星雲）。`BlackHole` パターン踏襲＝静的レジストリ `All`＋自動配置。
    /// 効果は押し出しでなく「その地点の倍率を返す」クエリ方式：`SpeedFactorAt`（FleetMovement が機動に乗算）／
    /// `RangeFactorAt`（FleetWeapon/EscortShip が実効射程に乗算）。数値は `TerrainRules`（Core）。
    /// 重なりは最も制限の強い（最小）倍率を採る。ビジュアルはランタイム生成の半透明ディスク。
    /// </summary>
    public class BattleTerrain : MonoBehaviour
    {
        public TerrainType type = TerrainType.小惑星帯;
        public float radius = 8f;

        public static readonly List<BattleTerrain> All = new List<BattleTerrain>();
        public static bool AutoSpawnEnabled = true;

        private static Sprite discSprite;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TrySpawn(scene);

        private static void TrySpawn(Scene scene)
        {
            if (!AutoSpawnEnabled || scene.name != "Battle") return;
            // 惑星攻城/システムビューなど特殊モードでは出さない。
            if (BattleHandoff.IsPlanetSiege || BattleHandoff.IsSystemView) return;
            if (FindAnyObjectByType<BattleTerrain>() != null) return;
            Create(TerrainType.小惑星帯, new Vector2(-12f, 6f), 8f);
            Create(TerrainType.星雲, new Vector2(12f, -6f), 10f);
        }

        private static BattleTerrain Create(TerrainType type, Vector2 pos, float radius)
        {
            var go = new GameObject($"BattleTerrain_{type}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var t = go.AddComponent<BattleTerrain>();
            t.type = type;
            t.radius = radius;
            t.BuildVisual();
            return t;
        }

        private void BuildVisual()
        {
            if (discSprite == null) discSprite = BuildDiscSprite();
            var vis = new GameObject("TerrainVisual");
            vis.transform.SetParent(transform, false);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sprite = discSprite;
            sr.color = type == TerrainType.小惑星帯
                ? new Color(0.6f, 0.45f, 0.3f, 0.18f)   // 茶系（瓦礫）
                : new Color(0.55f, 0.35f, 0.75f, 0.18f); // 紫系（星雲）
            sr.sortingLayerName = "Background";
            sr.sortingOrder = -40;
            float d = radius * 2f / (discSprite.bounds.size.x <= 0f ? 1f : discSprite.bounds.size.x);
            vis.transform.localScale = new Vector3(d, d, 1f);
        }

        private static Sprite BuildDiscSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - dist); // 中心ほど濃く端は透明
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>pos における機動倍率（最も制限の強い地形を採用）。</summary>
        public static float SpeedFactorAt(Vector2 pos)
        {
            float factor = 1f;
            for (int i = 0; i < All.Count; i++)
            {
                BattleTerrain t = All[i];
                if (t == null) continue;
                if (((Vector2)t.transform.position - pos).sqrMagnitude > t.radius * t.radius) continue;
                factor = Mathf.Min(factor, TerrainRules.SpeedFactor(t.type));
            }
            return factor;
        }

        /// <summary>pos における射程/索敵倍率（最も制限の強い地形を採用）。</summary>
        public static float RangeFactorAt(Vector2 pos)
        {
            float factor = 1f;
            for (int i = 0; i < All.Count; i++)
            {
                BattleTerrain t = All[i];
                if (t == null) continue;
                if (((Vector2)t.transform.position - pos).sqrMagnitude > t.radius * t.radius) continue;
                factor = Mathf.Min(factor, TerrainRules.RangeFactor(t.type));
            }
            return factor;
        }
    }
}
