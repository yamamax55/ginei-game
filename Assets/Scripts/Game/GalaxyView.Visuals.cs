using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    public partial class GalaxyView
    {
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

                // 防衛惑星は攻城状態（制空権/侵略値）を星系の下にコンパクト表示
                if (s.planet != null)
                {
                    var sl = MakeLabel(go.transform, "", new Vector3(0f, -systemScale * 0.95f, 0f), 0.7f).GetComponent<TextMesh>();
                    siegeLabels[s.id] = sl;
                }
            }

            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                var go = new GameObject($"Fleet_{f.id}");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * fleetScale;

                // 本体スプライト（移動方向へ回転する子）。リング/ETA ラベルは回らないよう親（go）直下に置く。
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(go.transform, false);
                var sr = bodyGo.AddComponent<SpriteRenderer>();
                Sprite fs = FleetSpriteFor(f.faction);
                if (fs != null) { sr.sprite = fs; sr.color = Color.white; }            // 専用画像：陣営色で着色しない
                else { sr.sprite = disc; sr.color = FactionColor(f.faction); }          // 画像が無い勢力はマル
                sr.sortingOrder = 4;
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
            // S5：プレイヤー勢力の税率/国庫/民心/安定度の読み取り表示（バナー直下）
            policyLine = MakeLabel(transform, "", new Vector3(0f, 6.6f, 0f), 0.7f).GetComponent<TextMesh>();
            policyLine.color = new Color(0.85f, 0.9f, 0.7f);
            helpLine = MakeLabel(transform, "左ク:選択(Shift追加) / 回廊ダブルクリック:潜行 / 星系ダブルクリック:システムビュー / 右ク:進軍 / I:星系情報 / +/-・1・2・3:速度 / Space:停止",
                new Vector3(0f, -7.4f, 0f), 0.7f).GetComponent<TextMesh>();
            helpLine.color = new Color(0.7f, 0.7f, 0.8f);
        }

        // ===== S5/S6：財政スライス（税率レバー・国庫・支持低下イベント）=====

        /// <summary>
        /// 浮きHUD（税率行・操作ヒント）を抑制するか。<see cref="StrategyMapWindow"/> が上メニューへ集約する間 true。
        /// banner（戦況/速度/選択）は動的なため抑制しない。
        /// </summary>
        public static bool HideWorldHud = false;

        /// <summary>プレイヤー勢力の税率/国庫/民心/安定度を読み取り表示する（S5・毎フレーム）。</summary>
        private void UpdatePolicyLine()
        {
            if (policyLine == null) return;
            // 上メニューへ集約中は浮き表示を消す（税率行・操作ヒントとも）
            if (HideWorldHud)
            {
                policyLine.text = "";
                if (helpLine != null) helpLine.text = "";
                return;
            }
            FactionState s = PlayerState();
            if (s == null) { policyLine.text = ""; return; }
            float hope = s.community != null ? s.community.hope : 0f;
            float stab = CampaignRules.EffectiveStability(StrategySession.Campaign,
                GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国);
            // 読み取り表示（税率/国庫/民心/安定度）は観測＝常時。税率レバーのヒントはデバッグモード時のみ（` で切替）。
            string tag = debugMode ? "【DEBUG】 " : "";
            string lever = debugMode ? "　[ ] で税率" : "";
            policyLine.text = $"{tag}税率 {s.taxRate * 100f:0}%　国庫 {s.treasury:0}　民心 {hope * 100f:0}%　安定度 {stab * 100f:0}%{lever}";
            // 民心が閾値割れで警告色
            policyLine.color = hope < hopeEventThreshold ? new Color(1f, 0.5f, 0.4f) : new Color(0.85f, 0.9f, 0.7f);
        }

        private void Refresh()
        {
            UpdatePolicyLine(); // S5：プレイヤー勢力の税率/国庫/民心/安定度の読み取り表示
            // 回廊色：交戦中は戦闘色で点滅、前線（両端が敵対所有＝FTL不可）は赤、要衝は金、その他は通常
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < corridorLines.Count && i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                Color col;
                if (IsEngagedCorridor(c)) col = Color.Lerp(combatColor, Color.white, pulse * 0.6f);
                else col = StrategyRules.IsFtlBlocked(map, c) ? frontlineColor
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
                    if (fleetMarks[f] != null) Destroy(fleetMarks[f].transform.parent.gameObject);
                    fleetMarks.Remove(f); fleetRings.Remove(f); fleetEta.Remove(f); selectedFleets.Remove(f);
                }

            foreach (var kv in systemDots)
            {
                StarSystem s = map.GetSystem(kv.Key);
                if (s != null && kv.Value != null) kv.Value.color = OwnerColor(s.owner);
            }

            // 攻城状態：制空権健在は ⛨残量%（橙）、ドメイン・ダウン中は 侵略%（赤）
            foreach (var kv in siegeLabels)
            {
                StarSystem s = map.GetSystem(kv.Key);
                TextMesh sl = kv.Value;
                if (s == null || s.planet == null || sl == null) continue;
                Planet p = s.planet;
                if (!p.DomainDown)
                {
                    sl.text = $"制空{Mathf.CeilToInt(100f * p.orbitalDefense / Mathf.Max(1f, p.maxOrbitalDefense))}%";
                    sl.color = defenseColor;
                }
                else
                {
                    sl.text = $"侵攻{Mathf.FloorToInt(100f * p.invasionProgress / Mathf.Max(1f, p.invasionThreshold))}%";
                    sl.color = invadeColor;
                }
            }

            foreach (var kv in fleetMarks)
            {
                StrategicFleet f = kv.Key;
                if (f == null || kv.Value == null) continue;
                var anchor = kv.Value.transform.parent;                      // 移動アンカー（go）。本体（kv.Value）は子。
                anchor.position = FleetWorldPos(f);

                bool hasSprite = fleetSprites.ContainsKey(f.faction);
                kv.Value.color = hasSprite ? Color.white : FactionColor(f.faction);
                // 移動方向（回廊の向き）へ本体だけ回す。停泊中は最後の向きを保つ。専用画像のみ回転。
                if (hasSprite && TryFleetHeading(f, out var dir))
                    kv.Value.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

                if (fleetRings.TryGetValue(f, out var ring)) ring.enabled = selectedFleets.Contains(f);
                if (fleetEta.TryGetValue(f, out var eta))
                    eta.text = f.engaged ? "◆交戦" : (f.IsMoving ? $"ETA {f.Eta:F1}" : (f.IsOnCorridor ? "保持" : $"{f.strength}"));
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
            // イベント通知は左下フィード（NotificationFeed・#964）へ集約。バナーは現在状態のみ表示。
            if (AnyEngaged())
            {
                double total = currentAutoResolveSeconds > 0.0 ? currentAutoResolveSeconds : autoResolveDelay;
                float remain = Mathf.Max(0f, (float)total - engagedElapsed);
                banner.text = $"◆ 回廊で交戦中：ダブルクリックで潜行（手動指揮）／放置で自動解決（残り{remain:0.0}）";
                banner.color = combatColor;
                return;
            }
            if (TryBesiegeStatus(out string bt, out Color bc)) { banner.text = bt; banner.color = bc; return; }
            // 平時は浮きバナーを出さない（速度/選択数は上メニューに集約済み＝重複表示を廃止）。
            banner.text = "";
        }

        /// <summary>
        /// 選択中の艦隊が敵の防衛惑星に停泊していれば、攻城の状況（制空権制圧/侵攻/係争中）を返す。
        /// 「敵惑星に入ったのに何も起きない」を防ぐ説明用フィードバック（#131）。
        /// </summary>
        private bool TryBesiegeStatus(out string text, out Color col)
        {
            text = ""; col = Color.white;
            for (int i = 0; i < selectedFleets.Count; i++)
            {
                StrategicFleet f = selectedFleets[i];
                if (f == null || f.IsOnCorridor) continue;
                StarSystem s = map.GetSystem(f.currentSystemId);
                if (s == null || s.planet == null) continue;
                Planet p = s.planet;
                if (!FactionRelations.IsHostile(null, f.faction, null, p.owner)) continue; // 自国/友軍の惑星

                bool contested = false;
                var present = reg.FleetsAt(s.id);
                for (int k = 0; k < present.Count; k++)
                {
                    StrategicFleet g = present[k];
                    if (g != null && !FactionRelations.IsHostile(null, g.faction, null, p.owner)) { contested = true; break; }
                }

                if (contested)
                {
                    text = $"{s.systemName}：係争中（敵守備隊あり）＝攻城停止。守備隊を排除せよ";
                    col = combatColor;
                }
                else if (!p.DomainDown)
                {
                    text = $"{s.systemName} を攻城中：制空権 {Mathf.CeilToInt(100f * p.orbitalDefense / Mathf.Max(1f, p.maxOrbitalDefense))}%（S-AVが制圧）／ダブルクリックで突入";
                    col = defenseColor;
                }
                else if (!p.Captured)
                {
                    text = $"{s.systemName} へ侵攻中：侵略 {Mathf.FloorToInt(100f * p.invasionProgress / Mathf.Max(1f, p.invasionThreshold))}%／ダブルクリックで突入";
                    col = invadeColor;
                }
                else continue;
                return true;
            }
            return false;
        }

        // ===== 入力 =====

        /// <summary>背景星雲（galaxy_backdrop）を生成（#2384）。画像が無ければ何もしない＝後方互換。Multiple 設定でも確実なよう Texture2D から動的生成。</summary>
        private void SetupBackdrop()
        {
            if (backdropAlpha <= 0f) return;
            Texture2D tex = Resources.Load<Texture2D>("Textures/galaxy_backdrop");
            if (tex == null) return;
            var go = new GameObject("GalaxyBackdrop");
            backdrop = go.AddComponent<SpriteRenderer>();
            backdrop.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            backdrop.sortingOrder = -200; // 星系ドット/回廊/艦隊より背後
            // 明るさを落として盤面（星系ドット/回廊/艦隊）を読みやすくする（白×brightness で減光）。
            float b = Mathf.Clamp01(backdropBrightness);
            backdrop.color = new Color(b, b, b, backdropAlpha);
        }

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

        /// <summary>勢力別の艦隊スプライトを Resources から読み込む（帝国/同盟）。無い勢力はマルのまま。</summary>
        private void LoadFleetSprites()
        {
            fleetSprites.Clear();
            var imperial = Resources.Load<Sprite>("Ships/ImperialFlagship");
            if (imperial != null) fleetSprites[Faction.帝国] = imperial;
            var alliance = Resources.Load<Sprite>("Ships/AllianceFlagship");
            if (alliance != null) fleetSprites[Faction.同盟] = alliance;
        }

        /// <summary>この勢力の艦隊スプライト（無ければ null＝マル表示）。</summary>
        private Sprite FleetSpriteFor(Faction f) => fleetSprites.TryGetValue(f, out var s) ? s : null;

        /// <summary>艦隊の進行方向（回廊の向き）。回廊上のときだけ true。停泊中は向きを変えない。</summary>
        private bool TryFleetHeading(StrategicFleet f, out Vector2 dir)
        {
            dir = Vector2.zero;
            if (f == null || map == null || !f.IsOnCorridor) return false;
            StarSystem cur = map.GetSystem(f.currentSystemId);
            StarSystem dst = map.GetSystem(f.destinationSystemId);
            if (cur == null || dst == null) return false;
            Vector2 d = dst.position - cur.position;
            if (d.sqrMagnitude < 1e-6f) return false;
            dir = d.normalized;
            return true;
        }

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

    }
}
