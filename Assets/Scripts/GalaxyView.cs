using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ（C-1 #34）の最小ビジュアライズ。C-1 の純ロジック
    /// （GalaxyMap / StrategicFleet / GalaxyPathfinder / StrategicFleetRegistry / StrategyRules）を
    /// 画面につなぐデモ：星系・回廊・艦隊マーカーを描画し、左クリックで艦隊選択→星系クリックでワープ指示。
    /// 銀河時間を毎フレーム進め、到着で星系を占領（色が変わる）、同一回廊の敵対遭遇を会戦トリガーとして表示。
    ///
    /// 実機確認用。`Ginei/戦略マップ デモを開く` でデモシーンに配置 → 再生。
    /// </summary>
    public class GalaxyView : MonoBehaviour
    {
        [Header("見た目")]
        public float systemScale = 0.8f;
        public float fleetScale = 0.4f;
        public Color empireColor = new Color(0.85f, 0.3f, 0.25f);
        public Color allianceColor = new Color(0.3f, 0.5f, 0.9f);
        public Color corridorColor = new Color(0.5f, 0.55f, 0.7f, 0.9f);
        public Color chokeColor = new Color(0.9f, 0.8f, 0.3f, 0.95f);
        public Color selectColor = new Color(1f, 0.95f, 0.4f);

        private GalaxyMap map;
        private StrategicFleetRegistry reg;
        private Camera cam;
        private Sprite disc;
        private Material lineMat;
        private StrategicFleet selected;
        private float occupyTimer;

        private readonly Dictionary<int, SpriteRenderer> systemDots = new Dictionary<int, SpriteRenderer>();
        private readonly List<LineRenderer> corridorLines = new List<LineRenderer>();
        private readonly Dictionary<StrategicFleet, SpriteRenderer> fleetMarks = new Dictionary<StrategicFleet, SpriteRenderer>();
        private SpriteRenderer selectionRing;
        private TextMesh banner;

        private void Start()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("GalaxyCamera");
                cam = camObj.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);

            disc = MakeDiscSprite(64);
            lineMat = new Material(Shader.Find("Sprites/Default"));

            BuildDemoGalaxy();
            BuildVisuals();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            reg.Tick(dt);

            // 一定間隔で占領を解決（無防備な敵星系を確保＝色が変わる）
            occupyTimer += dt;
            if (occupyTimer >= 0.4f)
            {
                StrategyRules.ResolveAllOccupations(map, reg);
                occupyTimer = 0f;
            }

            HandleInput();
            Refresh();
        }

        // ===== デモ銀河 =====

        private void BuildDemoGalaxy()
        {
            map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "アスタ", new Vector2(0f, 3f), Faction.帝国));
            map.AddSystem(new StarSystem(1, "ベガ", new Vector2(-5f, -3f), Faction.同盟));
            map.AddSystem(new StarSystem(2, "ケレス", new Vector2(5f, 3f), Faction.帝国));
            map.AddSystem(new StarSystem(3, "ドラコ", new Vector2(0f, -0.5f), Faction.帝国));
            map.AddSystem(new StarSystem(4, "エリス", new Vector2(-2.5f, 1f), Faction.同盟));
            map.AddSystem(new StarSystem(5, "フェニクス", new Vector2(3.5f, -2.5f), Faction.帝国));

            map.AddCorridor(new Corridor(2, 0, 4f, CorridorType.要衝));
            map.AddCorridor(new Corridor(0, 3, 5f));
            map.AddCorridor(new Corridor(3, 1, 4f));
            map.AddCorridor(new Corridor(3, 4, 3f));
            map.AddCorridor(new Corridor(4, 1, 2f));
            map.AddCorridor(new Corridor(0, 5, 3f));
            map.AddCorridor(new Corridor(5, 3, 2f));

            reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(1, 2, Faction.帝国, 1.5f));  // ケレスの帝国艦隊
            reg.Add(new StrategicFleet(2, 1, Faction.同盟, 1.5f));  // ベガの同盟艦隊
            reg.Add(new StrategicFleet(3, 4, Faction.同盟, 1.2f));  // エリスの同盟艦隊
        }

        // ===== 描画 =====

        private void BuildVisuals()
        {
            // 回廊（線）
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                StarSystem a = map.GetSystem(c.aId);
                StarSystem b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;

                var go = new GameObject($"Corridor_{c.aId}_{c.bId}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = lineMat;
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, a.position);
                lr.SetPosition(1, b.position);
                bool choke = c.type == CorridorType.要衝;
                float w = choke ? 0.16f : 0.08f;
                lr.startWidth = lr.endWidth = w;
                Color col = choke ? chokeColor : corridorColor;
                lr.startColor = lr.endColor = col;
                lr.numCapVertices = 2;
                lr.sortingOrder = 0;
                corridorLines.Add(lr);
            }

            // 星系（円＋名前ラベル）
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                var go = new GameObject($"System_{s.id}_{s.systemName}");
                go.transform.SetParent(transform, false);
                go.transform.position = s.position;
                go.transform.localScale = Vector3.one * systemScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = disc;
                sr.color = OwnerColor(s.owner);
                sr.sortingOrder = 2;
                systemDots[s.id] = sr;

                MakeLabel(go.transform, s.systemName, new Vector3(0f, systemScale * 0.9f, 0f), 0.9f);
            }

            // 艦隊マーカー
            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                var go = new GameObject($"Fleet_{f.id}");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * fleetScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = disc;
                sr.color = FactionColor(f.faction);
                sr.sortingOrder = 4;
                fleetMarks[f] = sr;
            }

            // 選択リング
            var ringGo = new GameObject("SelectionRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localScale = Vector3.one * (fleetScale * 1.8f);
            selectionRing = ringGo.AddComponent<SpriteRenderer>();
            selectionRing.sprite = disc;
            selectionRing.color = new Color(selectColor.r, selectColor.g, selectColor.b, 0.35f);
            selectionRing.sortingOrder = 3;
            selectionRing.enabled = false;

            // バナー（操作説明・状態）
            banner = MakeLabel(transform, "左クリック:艦隊選択 → 星系クリック:ワープ指示", new Vector3(0f, 7.2f, 0f), 1.0f).GetComponent<TextMesh>();
            banner.transform.position = new Vector3(0f, 7.2f, 0f);
        }

        private void Refresh()
        {
            // 星系色（占領で変化）
            foreach (var kv in systemDots)
            {
                StarSystem s = map.GetSystem(kv.Key);
                if (s != null && kv.Value != null) kv.Value.color = OwnerColor(s.owner);
            }

            // 艦隊位置
            foreach (var kv in fleetMarks)
            {
                StrategicFleet f = kv.Key;
                if (f == null || kv.Value == null) continue;
                kv.Value.transform.position = FleetWorldPos(f);
                kv.Value.color = FactionColor(f.faction);
            }

            // 選択リング
            if (selected != null && fleetMarks.ContainsKey(selected))
            {
                selectionRing.enabled = true;
                selectionRing.transform.position = FleetWorldPos(selected);
            }
            else selectionRing.enabled = false;

            // 会戦トリガー（同一回廊の敵対遭遇）
            var enc = StrategyRules.FindEncounters(reg);
            if (enc.Count > 0)
            {
                var e0 = enc[0];
                banner.text = $"⚔ 会戦発生：回廊で敵対艦隊が遭遇（{enc.Count}件）";
                banner.color = new Color(1f, 0.5f, 0.3f);
            }
            else
            {
                banner.text = "左クリック:艦隊選択 → 星系クリック:ワープ指示";
                banner.color = Color.white;
            }
        }

        // ===== 入力 =====

        private void HandleInput()
        {
            if (Mouse.current == null || cam == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            Vector3 sp = Mouse.current.position.ReadValue();
            Vector3 wp = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
            Vector2 w = wp;

            // まず艦隊を拾う
            StrategicFleet nf = NearestFleet(w, 0.7f);
            if (nf != null)
            {
                selected = nf;
                return;
            }

            // 艦隊が選択済みなら、クリックした星系へワープ
            int sysId = NearestSystem(w, systemScale + 0.3f);
            if (sysId >= 0 && selected != null)
            {
                selected.WarpTo(map, sysId);
            }
        }

        private StrategicFleet NearestFleet(Vector2 w, float radius)
        {
            StrategicFleet best = null;
            float bestD = radius;
            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                float d = Vector2.Distance(FleetWorldPos(f), w);
                if (d <= bestD) { bestD = d; best = f; }
            }
            return best;
        }

        private int NearestSystem(Vector2 w, float radius)
        {
            int best = -1;
            float bestD = radius;
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                float d = Vector2.Distance(s.position, w);
                if (d <= bestD) { bestD = d; best = s.id; }
            }
            return best;
        }

        // ===== ヘルパ =====

        private Vector2 FleetWorldPos(StrategicFleet f)
        {
            StarSystem cur = map.GetSystem(f.currentSystemId);
            if (cur == null) return Vector2.zero;
            if (!f.IsMoving) return cur.position;
            StarSystem dst = map.GetSystem(f.destinationSystemId);
            if (dst == null) return cur.position;
            return Vector2.Lerp(cur.position, dst.position, f.Progress);
        }

        private Color OwnerColor(Faction f) => (f == Faction.帝国) ? empireColor : allianceColor;

        private Color FactionColor(Faction f)
        {
            Color c = (f == Faction.帝国) ? empireColor : allianceColor;
            return Color.Lerp(c, Color.white, 0.35f); // 星系より明るく
        }

        private GameObject MakeLabel(Transform parent, string text, Vector3 localOffset, float charSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = FontProvider.JapaneseFont;
            tm.fontSize = 48;
            tm.characterSize = charSize * 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = 6;
            return go;
        }

        private static Sprite MakeDiscSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            var cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    cols[y * size + x] = (d <= r - 1f) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(cols);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void OnDestroy()
        {
            if (lineMat != null) Destroy(lineMat);
            if (disc != null && disc.texture != null) Destroy(disc.texture);
        }
    }
}
