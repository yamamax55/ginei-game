using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ（C-1 #34）の最小ビジュアライズ。C-1 の純ロジック
    /// （GalaxyMap / StrategicFleet / GalaxyPathfinder / StrategicFleetRegistry / StrategyRules）を
    /// 画面につなぐデモ：星系・回廊・艦隊マーカーを描画し、クリックで艦隊選択→星系クリックでワープ指示。
    /// 銀河時間を進め、到着で占領（色変化）、同一回廊の敵対遭遇を会戦トリガーとして表示。
    ///
    /// 操作：左クリック=艦隊選択（Shiftで追加）／星系クリック=選択艦隊をワープ／
    ///       Space=停止・再開／1・2・3=速度（0.5x/1x/2x）。
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
        public Color frontlineColor = new Color(0.9f, 0.25f, 0.2f, 0.95f); // 前線（FTL不可）
        public Color selectColor = new Color(1f, 0.95f, 0.4f);

        [Header("時間")]
        public float galaxySpeed = 1f;

        private GalaxyMap map;
        private StrategicFleetRegistry reg;
        private Camera cam;
        private Sprite disc;
        private Material lineMat;
        private bool paused;
        private float occupyTimer;
        private string battleMsg = "";
        private float battleMsgTimer;

        private readonly List<StrategicFleet> selectedFleets = new List<StrategicFleet>();
        private readonly Dictionary<int, SpriteRenderer> systemDots = new Dictionary<int, SpriteRenderer>();
        private readonly List<LineRenderer> corridorLines = new List<LineRenderer>();
        private readonly Dictionary<StrategicFleet, SpriteRenderer> fleetMarks = new Dictionary<StrategicFleet, SpriteRenderer>();
        private readonly Dictionary<StrategicFleet, SpriteRenderer> fleetRings = new Dictionary<StrategicFleet, SpriteRenderer>();
        private readonly Dictionary<StrategicFleet, TextMesh> fleetEta = new Dictionary<StrategicFleet, TextMesh>();
        private readonly List<LineRenderer> routeLines = new List<LineRenderer>();
        private TextMesh banner;
        private TextMesh helpLine;

        private void Start()
        {
            cam = Camera.main;
            if (cam == null) cam = new GameObject("GalaxyCamera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);

            disc = MakeDiscSprite(64);
            lineMat = new Material(Shader.Find("Sprites/Default"));

            BuildDemoGalaxy();
            BuildVisuals();

            // 実会戦（Battleシーン）から戻ってきた結果を戦略へ反映
            if (BattleHandoff.Resolved && StrategyRules.ApplyHandoffResult(reg))
            {
                battleMsg = "実会戦の結果を反映しました";
                battleMsgTimer = 3f;
            }
        }

        private void Update()
        {
            HandleKeys();

            float dt = paused ? 0f : Time.deltaTime * Mathf.Max(0f, galaxySpeed);
            reg.Tick(dt);

            // 回廊で接触した敵対艦隊があれば、実会戦（Battleシーン）へ遷移して決着する
            if (StrategyRules.TryFindCollision(reg, out var fa, out var fb))
            {
                BattleHandoff.Queue(fa, fb, "Strategy");
                SceneManager.LoadScene("Battle");
                return;
            }

            occupyTimer += dt;
            if (occupyTimer >= 0.4f) { StrategyRules.ResolveAllOccupations(map, reg); occupyTimer = 0f; }

            if (battleMsgTimer > 0f) battleMsgTimer -= Time.deltaTime; // 実時間で表示

            HandleMouse();
            Refresh();
        }

        // ===== デモ銀河 =====

        private void BuildDemoGalaxy()
        {
            // 戦略↔実会戦の往復で世界状態を保持（あれば再利用）
            if (StrategySession.HasState) { map = StrategySession.Map; reg = StrategySession.Reg; return; }

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
            reg.Add(new StrategicFleet(1, 2, Faction.帝国, 1.5f) { strength = 250 });
            reg.Add(new StrategicFleet(2, 1, Faction.同盟, 1.5f) { strength = 300 });
            reg.Add(new StrategicFleet(3, 4, Faction.同盟, 1.2f) { strength = 150 });
            reg.Add(new StrategicFleet(4, 3, Faction.帝国, 1.3f) { strength = 200 }); // ドラコ防衛・前線で衝突用

            StrategySession.Set(map, reg);
        }

        // ===== 描画 =====

        private void BuildVisuals()
        {
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                StarSystem a = map.GetSystem(c.aId);
                StarSystem b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;

                var lr = NewLine($"Corridor_{c.aId}_{c.bId}", 0);
                lr.positionCount = 2;
                lr.SetPosition(0, a.position);
                lr.SetPosition(1, b.position);
                bool choke = c.type == CorridorType.要衝;
                lr.startWidth = lr.endWidth = choke ? 0.16f : 0.08f;
                lr.startColor = lr.endColor = choke ? chokeColor : corridorColor;
                corridorLines.Add(lr);
            }

            foreach (var s in map.systems)
            {
                if (s == null) continue;
                var go = new GameObject($"System_{s.id}_{s.systemName}");
                go.transform.SetParent(transform, false);
                go.transform.position = s.position;
                go.transform.localScale = Vector3.one * systemScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = disc; sr.color = OwnerColor(s.owner); sr.sortingOrder = 2;
                systemDots[s.id] = sr;
                MakeLabel(go.transform, s.systemName, new Vector3(0f, systemScale * 0.9f, 0f), 0.9f);
            }

            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                var go = new GameObject($"Fleet_{f.id}");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * fleetScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = disc; sr.color = FactionColor(f.faction); sr.sortingOrder = 4;
                fleetMarks[f] = sr;

                // 選択リング（子・既定オフ）
                var ringGo = new GameObject("Ring");
                ringGo.transform.SetParent(go.transform, false);
                ringGo.transform.localScale = Vector3.one * 1.8f;
                var ring = ringGo.AddComponent<SpriteRenderer>();
                ring.sprite = disc;
                ring.color = new Color(selectColor.r, selectColor.g, selectColor.b, 0.35f);
                ring.sortingOrder = 3;
                ring.enabled = false;
                fleetRings[f] = ring;

                // ETA ラベル（移動中のみ表示）
                var eta = MakeLabel(go.transform, "", new Vector3(0f, 0.9f, 0f), 0.7f).GetComponent<TextMesh>();
                eta.color = selectColor;
                fleetEta[f] = eta;
            }

            banner = MakeLabel(transform, "", new Vector3(0f, 7.3f, 0f), 1.0f).GetComponent<TextMesh>();
            helpLine = MakeLabel(transform, "左クリック:選択(Shift追加) / 右クリック:星系へ進軍 or 回廊上で停止保持(前線で待ち伏せ) / Space:停止 / 1・2・3:速度",
                new Vector3(0f, -7.4f, 0f), 0.75f).GetComponent<TextMesh>();
            helpLine.color = new Color(0.7f, 0.7f, 0.8f);
        }

        private void Refresh()
        {
            // 回廊色：前線（両端が敵対所有＝FTL不可）は赤、要衝は金、その他は通常
            for (int i = 0; i < corridorLines.Count && i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                Color col = StrategyRules.IsFtlBlocked(map, c) ? frontlineColor
                    : (c.type == CorridorType.要衝 ? chokeColor : corridorColor);
                corridorLines[i].startColor = corridorLines[i].endColor = col;
            }

            // 除去された艦隊（戦闘で消滅）のマーカーを片付ける
            List<StrategicFleet> gone = null;
            foreach (var kv in fleetMarks)
                if (!reg.fleets.Contains(kv.Key)) (gone ??= new List<StrategicFleet>()).Add(kv.Key);
            if (gone != null)
                foreach (var f in gone)
                {
                    if (fleetMarks[f] != null) Destroy(fleetMarks[f].gameObject);
                    fleetMarks.Remove(f); fleetRings.Remove(f); fleetEta.Remove(f); selectedFleets.Remove(f);
                }

            foreach (var kv in systemDots)
            {
                StarSystem s = map.GetSystem(kv.Key);
                if (s != null && kv.Value != null) kv.Value.color = OwnerColor(s.owner);
            }

            foreach (var kv in fleetMarks)
            {
                StrategicFleet f = kv.Key;
                if (f == null || kv.Value == null) continue;
                kv.Value.transform.position = FleetWorldPos(f);
                kv.Value.color = FactionColor(f.faction);

                if (fleetRings.TryGetValue(f, out var ring)) ring.enabled = selectedFleets.Contains(f);
                if (fleetEta.TryGetValue(f, out var eta))
                    eta.text = f.IsMoving ? $"ETA {f.Eta:F1}" : (f.IsOnCorridor ? "保持" : $"{f.strength}");
            }

            DrawSelectedRoutes();
            UpdateBanner();
        }

        /// <summary>選択中の移動艦隊について、現在位置→残り経路の終点までをハイライト表示。</summary>
        private void DrawSelectedRoutes()
        {
            int li = 0;
            for (int s = 0; s < selectedFleets.Count; s++)
            {
                StrategicFleet f = selectedFleets[s];
                if (f == null || !f.IsMoving) continue;

                var pts = new List<Vector3>();
                pts.Add(FleetWorldPos(f));
                var path = GalaxyPathfinder.FindPath(map, f.destinationSystemId, f.FinalDestinationId);
                if (path.Count == 0)
                {
                    StarSystem dst = map.GetSystem(f.destinationSystemId);
                    if (dst != null) pts.Add(dst.position);
                }
                else
                {
                    foreach (int sid in path)
                    {
                        StarSystem sys = map.GetSystem(sid);
                        if (sys != null) pts.Add(sys.position);
                    }
                }
                if (pts.Count < 2) continue;

                LineRenderer lr = GetRouteLine(li++);
                lr.positionCount = pts.Count;
                lr.SetPositions(pts.ToArray());
                lr.enabled = true;
            }
            for (; li < routeLines.Count; li++) routeLines[li].enabled = false;
        }

        private LineRenderer GetRouteLine(int i)
        {
            while (routeLines.Count <= i)
            {
                var lr = NewLine("Route", 1);
                lr.startWidth = lr.endWidth = 0.06f;
                lr.startColor = lr.endColor = new Color(selectColor.r, selectColor.g, selectColor.b, 0.85f);
                routeLines.Add(lr);
            }
            return routeLines[i];
        }

        private void UpdateBanner()
        {
            if (battleMsgTimer > 0f)
            {
                banner.text = battleMsg;
                banner.color = new Color(1f, 0.6f, 0.3f);
                return;
            }

            var enc = StrategyRules.FindEncounters(reg);
            if (enc.Count > 0)
            {
                banner.text = $"⚔ 会戦発生：回廊で敵対艦隊が遭遇（{enc.Count}件）";
                banner.color = new Color(1f, 0.5f, 0.3f);
                return;
            }
            string speed = paused ? "停止" : $"x{galaxySpeed:0.#}";
            banner.text = $"速度 {speed}　選択 {selectedFleets.Count}隻";
            banner.color = Color.white;
        }

        // ===== 入力 =====

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) paused = !paused;
            if (kb.digit1Key.wasPressedThisFrame) { galaxySpeed = 0.5f; paused = false; }
            if (kb.digit2Key.wasPressedThisFrame) { galaxySpeed = 1f; paused = false; }
            if (kb.digit3Key.wasPressedThisFrame) { galaxySpeed = 2f; paused = false; }
        }

        private void HandleMouse()
        {
            if (Mouse.current == null || cam == null) return;

            // 左クリック：選択（Shiftで追加/トグル、空白で解除）
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 w = WorldMouse();
                bool additive = ShiftHeld();
                StrategicFleet nf = NearestFleet(w, 0.7f);
                if (nf != null)
                {
                    if (additive) { if (!selectedFleets.Remove(nf)) selectedFleets.Add(nf); }
                    else { selectedFleets.Clear(); selectedFleets.Add(nf); }
                }
                else if (!additive) selectedFleets.Clear();
            }
            // 右クリック：クリックに近い方を採用。星系の点が近ければ進軍、回廊の線が近ければ
            // その位置で停止保持（端点に居る選択艦のみ）。
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (selectedFleets.Count == 0) return;
                Vector2 w = WorldMouse();

                int sysId = NearestSystemDist(w, out float sysD);
                bool hasCorr = NearestCorridor(w, out Corridor c, out float fracFromA, out float corrD);

                if (sysId >= 0 && (!hasCorr || sysD <= corrD) && sysD <= 1.6f)
                {
                    // 星系へ進軍
                    foreach (var f in selectedFleets) if (f != null) f.WarpTo(map, sysId);
                }
                else if (hasCorr && corrD <= 0.6f)
                {
                    // 回廊上のクリック位置で停止保持
                    foreach (var f in selectedFleets)
                    {
                        if (f == null) continue;
                        if (f.currentSystemId == c.aId) f.HoldOnCorridor(map, c.bId, fracFromA);
                        else if (f.currentSystemId == c.bId) f.HoldOnCorridor(map, c.aId, 1f - fracFromA);
                    }
                }
            }
        }

        private Vector2 WorldMouse()
        {
            Vector3 sp = Mouse.current.position.ReadValue();
            return cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
        }

        private static bool ShiftHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        private StrategicFleet NearestFleet(Vector2 w, float radius)
        {
            StrategicFleet best = null; float bestD = radius;
            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                float d = Vector2.Distance(FleetWorldPos(f), w);
                if (d <= bestD) { bestD = d; best = f; }
            }
            return best;
        }

        /// <summary>最も近い星系IDとその距離を返す（無ければ -1）。</summary>
        private int NearestSystemDist(Vector2 w, out float dist)
        {
            int best = -1; dist = float.MaxValue;
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                float d = Vector2.Distance(s.position, w);
                if (d < dist) { dist = d; best = s.id; }
            }
            return best;
        }

        /// <summary>クリック点に最も近い回廊（線分）と、その上の位置 fracFromA（aId→bId で0..1）と距離を返す。</summary>
        private bool NearestCorridor(Vector2 w, out Corridor best, out float fracFromA, out float dist)
        {
            best = null; fracFromA = 0f; dist = float.MaxValue;
            foreach (var c in map.corridors)
            {
                StarSystem a = map.GetSystem(c.aId), b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;
                Vector2 pa = a.position, ab = b.position - a.position;
                float len2 = ab.sqrMagnitude;
                float t = (len2 > 0f) ? Mathf.Clamp01(Vector2.Dot(w - pa, ab) / len2) : 0f;
                float d = Vector2.Distance(w, pa + ab * t);
                if (d < dist) { dist = d; best = c; fracFromA = t; }
            }
            return best != null;
        }

        // ===== ヘルパ =====

        private Vector2 FleetWorldPos(StrategicFleet f)
        {
            StarSystem cur = map.GetSystem(f.currentSystemId);
            if (cur == null) return Vector2.zero;
            if (!f.IsOnCorridor) return cur.position; // 停泊中は星系。回廊上（前進・保持）は補間
            StarSystem dst = map.GetSystem(f.destinationSystemId);
            if (dst == null) return cur.position;
            return Vector2.Lerp(cur.position, dst.position, f.Progress);
        }

        private Color OwnerColor(Faction f) => (f == Faction.帝国) ? empireColor : allianceColor;
        private Color FactionColor(Faction f) => Color.Lerp((f == Faction.帝国) ? empireColor : allianceColor, Color.white, 0.35f);

        private LineRenderer NewLine(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = lineMat;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.sortingOrder = order;
            return lr;
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
