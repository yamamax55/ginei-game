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
            systemNameLabels.Clear(); // 戦役の作り直しで古い（破棄済み）ラベル参照を残さない
            systemGlows.Clear(); systemCores.Clear(); systemNameLabelById.Clear(); fortressHitRadius = 0f; starSystems.Clear();
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                StarSystem a = map.GetSystem(c.aId);
                StarSystem b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;

                // #戦略MAP刷新：航路は細く階層化する。通商路は最も細く沈ませ、要衝だけ金でわずかに太く前へ出す
                // ＝「金は選択と要衝だけ」の原則で、画面の意味が一目で読める。
                bool choke = c.type == CorridorType.要衝;
                var lr = NewLine($"Corridor_{c.aId}_{c.bId}", choke ? 1 : 0);
                lr.positionCount = 2;
                lr.SetPosition(0, a.position);
                lr.SetPosition(1, b.position);
                lr.startWidth = lr.endWidth = choke ? chokeCorridorWidth : tradeCorridorWidth;
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

                // #戦略MAP刷新：太いベタ丸をやめ「淡い光暈 → 細いリング → 小さな発光核」の3層で星系を示す。
                // 所有勢力はリングと核の色で読む（陣営色＝青/珊瑚）。金は選択と要衝だけに温存する。
                var glow = MakeChild(go.transform, "Glow", disc, glowRadiusFactor, 1);
                var ring = MakeChild(go.transform, "Ring", ringSprite, 1f, 3);
                var core = MakeChild(go.transform, "Core", disc, coreRadiusFactor, 4);

                Color own = OwnerColor(s.owner);
                glow.color = new Color(own.r, own.g, own.b, glowAlpha);
                ring.color = own;
                core.color = Color.Lerp(own, Color.white, coreWhiten);

                systemDots[s.id] = ring;   // 所有変更時の色更新はリングを正とする（既存の更新ループがそのまま効く）
                systemGlows[s.id] = glow;
                systemCores[s.id] = core;

                Transform nameLabel = MakeLabel(go.transform, s.systemName, new Vector3(0f, systemScale * 1.05f, 0f), 0.9f).transform;
                systemNameLabels.Add(nameLabel);
                systemNameLabelById[s.id] = nameLabel;

                // 防衛惑星は攻城状態（制空権/侵略値）を星系の下にコンパクト表示
                if (s.planet != null)
                {
                    var sl = MakeLabel(go.transform, "", new Vector3(0f, -systemScale * 0.95f, 0f), 0.7f).GetComponent<TextMesh>();
                    siegeLabels[s.id] = sl;
                }
            }

            foreach (var f in reg.fleets) EnsureFleetView(f);
            BuildSceneDecor();
        }

        /// <summary>
        /// その艦隊の表示物（本体/選択リング/番号/状態ラベル/旗艦印）が無ければ作る。
        /// <b>後から増えた艦隊にも効く</b>＝建造や増援、QA で追加した艦隊が盤面に出ない不具合を防ぐ（実機QA指摘）。
        /// </summary>
        private void EnsureFleetView(StrategicFleet f)
        {
            {
                if (f == null || fleetMarks.ContainsKey(f)) return;
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

                // 艦隊番号ラベル（艦隊画像の中＝重ねて表示。番号は不変ゆえここで一度だけ設定）。
                var num = MakeLabel(go.transform, $"第{f.id}艦隊", new Vector3(0f, 0f, 0f), 0.5f).GetComponent<TextMesh>();
                num.color = new Color(1f, 1f, 0.85f); // 画像の上で読めるよう明るく
                num.GetComponent<MeshRenderer>().sortingOrder = 7; // 艦隊スプライト(4)・他ラベル(6)より前面
                fleetNumLabels[f] = num;

                // ETA ラベル（移動中のみ表示）
                var eta = MakeLabel(go.transform, "", new Vector3(0f, 0.9f, 0f), 0.7f).GetComponent<TextMesh>();
                eta.color = selectColor;
                fleetEta[f] = eta;

                // 軍団長乗艦マーカー（★・軍団旗艦のみ表示。Refresh で可視化）。
                var corpsMark = MakeLabel(go.transform, "★", new Vector3(0f, -0.95f, 0f), 0.85f).GetComponent<TextMesh>();
                corpsMark.color = corpsFlagshipColor;
                corpsMark.gameObject.SetActive(false);
                fleetCorpsMarks[f] = corpsMark;
            }
        }

        /// <summary>盤面の共通表示（バナー/操作ヒント/要塞/恒星）。艦隊ごとの表示物は EnsureFleetView が作る。</summary>
        private void BuildSceneDecor()
        {
            banner = MakeLabel(transform, "", new Vector3(0f, 7.3f, 0f), 1.0f).GetComponent<TextMesh>();
            // S5：プレイヤー勢力の税率/国庫/民心/安定度の読み取り表示（バナー直下）
            policyLine = MakeLabel(transform, "", new Vector3(0f, 6.6f, 0f), 0.7f).GetComponent<TextMesh>();
            policyLine.color = new Color(0.85f, 0.9f, 0.7f);
            helpLine = MakeLabel(transform, "左ク:選択(Shift追加) / 左ドラッグ:スクロール / ダブルクリック＋ドラッグ:矩形選択 / 回廊ダブルクリック:潜行 / 星系ダブルクリック:システムビュー / 右ク:進軍 / I:星系情報 / +/-・1・2・3:速度 / Space:停止",
                new Vector3(0f, -7.4f, 0f), 0.7f).GetComponent<TextMesh>();
            helpLine.color = new Color(0.7f, 0.7f, 0.8f);

            // 要所（前線の要衝）だけ3D要塞メッシュへ差し替える（素材が無ければ何もしない＝従来表示）。
            fortressSystems.Clear();
            AttachFortressModels();

            // 要塞にしなかった星系は、名前ごとの恒星3Dモデルへ差し替える（要塞が優先）。
            AttachStarModels();
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
            UpdateFleetClusterOffsets(); // 同一星系の艦隊の重なり回避オフセットを先に計算（FleetWorldPos が参照）
            // 回廊色：交戦中は戦闘色で点滅、前線（両端が敵対所有＝FTL不可）は赤、要衝は金、その他は通常
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < corridorLines.Count && i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                Color col;
                bool fortressActive = c.fortress != null && FortressRules.BlocksPassage(c.fortress);
                if (IsEngagedCorridor(c)) col = Color.Lerp(combatColor, Color.white, pulse * 0.6f);
                else if (fortressActive) col = Color.Lerp(fortressBlockadeColor, Color.white, pulse * 0.4f); // #40 要塞封鎖
                else col = StrategyRules.IsFtlBlocked(map, c) ? frontlineColor
                    : (c.type == CorridorType.要衝 ? chokeColor : corridorColor);
                corridorLines[i].startColor = corridorLines[i].endColor = col;
            }
            UpdateCorridorFortressLabels(); // #40：要塞の所有・守備・封鎖状態を回廊上に出す

            // 除去された艦隊（戦闘で消滅）のマーカーを片付ける
            List<StrategicFleet> gone = null;
            foreach (var kv in fleetMarks)
                if (!reg.fleets.Contains(kv.Key)) (gone ??= new List<StrategicFleet>()).Add(kv.Key);
            if (gone != null)
                foreach (var f in gone)
                {
                    if (fleetMarks[f] != null) Destroy(fleetMarks[f].transform.parent.gameObject);
                    fleetMarks.Remove(f); fleetRings.Remove(f); fleetEta.Remove(f); fleetCorpsMarks.Remove(f); fleetNumLabels.Remove(f); selectedFleets.Remove(f);
                }

            foreach (var kv in systemDots)
            {
                StarSystem s = map.GetSystem(kv.Key);
                if (s == null || kv.Value == null) continue;
                Color own = OwnerColor(s.owner);
                kv.Value.color = own;                                   // リング＝所有の主表示
                if (systemGlows.TryGetValue(kv.Key, out var g) && g != null)
                    g.color = new Color(own.r, own.g, own.b, glowAlpha); // 光暈も所有色へ追従
                if (systemCores.TryGetValue(kv.Key, out var c) && c != null)
                    c.color = Color.Lerp(own, Color.white, coreWhiten);  // 核は白寄りで発光感を出す
            }

            // 攻城状態：制空権健在は「制空○%」（橙）、ドメイン・ダウン中は「侵略○%」（赤）
            foreach (var kv in siegeLabels)
            {
                StarSystem s = map.GetSystem(kv.Key);
                TextMesh sl = kv.Value;
                if (s == null || s.planet == null || sl == null) continue;
                Planet p = s.planet;
                // 攻城中（敵対艦隊が在席）のときだけ状態を表示。占領完了/解囲でラベルを消す（残存バグ修正）。
                if (!IsActiveSiege(s, p))
                {
                    if (sl.text.Length != 0) sl.text = "";
                    continue;
                }
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
            // 後から増えた艦隊（建造・増援・QA）にも表示物を用意する（実機QA：追加艦隊が盤面に出ない）。
            if (reg != null && reg.fleets != null)
                for (int i = 0; i < reg.fleets.Count; i++) EnsureFleetView(reg.fleets[i]);

            RebuildFleetClusters(); // 同陣営・近接の艦隊をまとめ直す（#戦略MAPの艦艇表示）

            foreach (var kv in fleetMarks)
            {
                StrategicFleet f = kv.Key;
                if (f == null || kv.Value == null) continue;
                var anchor = kv.Value.transform.parent;                      // 移動アンカー（go）。本体（kv.Value）は子。

                bool hasSprite = fleetSprites.ContainsKey(f.faction);
                kv.Value.color = hasSprite ? Color.white : FactionColor(f.faction);
                // 移動方向（回廊の向き）へ本体だけ回す。停泊中は最後の向きを保つ。専用画像のみ回転。
                if (hasSprite && TryFleetHeading(f, out var dir))
                    kv.Value.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

                bool selected = selectedFleets.Contains(f);
                if (fleetRings.TryGetValue(f, out var ring)) ring.enabled = selected;

                // #戦略MAPの艦艇表示：同じ場所の同陣営はひとまとめに見せる。
                // まとまりの「代表」だけを描き、残りは畳む＝微小な駒とラベルが折り重なるのを防ぐ。
                FleetCluster cl = ClusterOf(f);
                bool expanded = cl == null || cl.IsSingle || IsClusterExpanded(cl);
                bool isRep = cl == null || RepresentativeOf(cl) == f.id;
                // 畳んでいる間、盤面に出すのは<b>選択中の艦隊だけ</b>。
                // まとまりそのものは画面空間のバッジ（FleetMarkerBadgeLayer）が示すので、
                // ワールド側に代表の駒を置かない＝恒星/要塞の3Dや星系のクリック領域と重ならない。
                bool drawThis = (expanded || selectedFleets.Contains(f)) && ShownOnMap(f);

                anchor.gameObject.SetActive(drawThis);
                if (!drawThis) continue;

                // 畳んでいる間に見える唯一の駒＝選択中の艦隊。星系の脇に置いて見失わせない。
                bool highlightOnly = !expanded;
                anchor.position = highlightOnly && cl != null
                    ? (Vector3)(cl.center + new Vector2(0f, -selectedHighlightOffset))
                    : FleetWorldPos(f);
                anchor.localScale = Vector3.one * fleetScale;

                // #戦略MAP刷新：艦隊の文字は「引いているほど絞る」＝重なりを防ぐ。
                // 交戦中と選択中は縮尺に関わらず出す（見落とすと困る情報だけ残す）。
                float zoom = cam != null ? cam.orthographicSize : 0f;
                bool important = selected || f.engaged;
                // 集約の件数はバッジ（画面空間）が出すので、ワールド側のラベルは個別情報だけを担う。
                bool showDetail = important || (expanded && zoom <= fleetDetailZoom);   // 兵力/ETA
                bool showName = (important || zoom <= fleetNameZoom) && expanded;       // 「第N艦隊」は展開時だけ

                if (fleetEta.TryGetValue(f, out var eta) && eta != null)
                {
                    eta.gameObject.SetActive(showDetail);
                    if (showDetail)
                    {
                        eta.text = FleetStatusLabel(f, cl, expanded, isRep, garrisonedFleetIds.Contains(f.id));
                        eta.transform.localScale = Vector3.one;
                        eta.color = selectColor;
                    }
                }
                if (fleetNumLabels.TryGetValue(f, out var num) && num != null)
                    num.gameObject.SetActive(showName);
                // 軍団旗艦の目印。記号（★）はフォントに無いと豆腐になるため文字で出す（#記号欠落）。
                if (fleetCorpsMarks.TryGetValue(f, out var cm) && cm != null)
                {
                    bool showMark = f.isCorpsFlagship && expanded;
                    cm.gameObject.SetActive(showMark);
                    if (showMark) cm.text = "旗";
                }
            }

            UpdateEchelonBoxes();   // 軍団＝四角／軍団が集結した軍集団＝外側の四角で囲う
            // MAP は表示専用（命令は艦隊メニュー）。移動中の全艦隊の「どこからどこへ」を描く。
            // 選択艦隊の強調もこの中で行うので、旧 DrawSelectedRoutes は呼ばない。
            DrawMovementOverlay();
            UpdateBanner();
        }

        // ===== 艦隊マーカーの集約（#戦略MAPの艦艇表示） =====

        /// <summary>
        /// いまの盤面から艦隊のまとまりを作り直す。数え上げは Core（<see cref="FleetClusterRules"/>）＝
        /// 欠落も二重計上も起きないことをテストで固定してある。ここは入力を集めて結果を引くだけ。
        /// </summary>
        private void RebuildFleetClusters()
        {
            clusterInputs.Clear();
            clusterOfFleet.Clear();
            CollectGarrisonedFleetIds();
            if (reg == null || reg.fleets == null) { fleetClusters.Clear(); return; }

            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null) continue;
                // #E：停泊中の艦隊は MAP に出さない（混雑の解消）。まとまりの集計にも入れない
                // ＝停泊集合バッジも出ない。所属や艦艇数は消さず「軍団編成」メニューで見る。
                if (!ShownOnMap(f)) continue;
                // #40 駐留艦隊：要塞に駐留している艦隊は<b>まとまりの集計に入れない</b>。
                // 入れると「星系に停泊している艦艇数」と「要塞の駐留艦艇数」の両方に同じ艦隊が乗り、
                // 画面上で二重に数えられる（星系の一覧側は既に ExcludeGarrisoned を通してある）。
                if (garrisonedFleetIds.Contains(f.id)) continue;
                TryFleetHeading(f, out Vector2 dir);
                clusterInputs.Add(new FleetMarkerInput(
                    f.id, f.faction, FleetLogicalPos(f), f.strength,
                    f.IsOnCorridor, selectedFleets.Contains(f), f.IsOnCorridor ? dir : Vector2.zero,
                    f.Ships));   // 表示は艦艇数（隻）で行う
            }

            FleetClusterRules.Build(clusterInputs, clusterMergeRadius, fleetClusters);

            for (int c = 0; c < fleetClusters.Count; c++)
                for (int j = 0; j < fleetClusters[c].fleetIds.Count; j++)
                    clusterOfFleet[fleetClusters[c].fleetIds[j]] = fleetClusters[c];

            UpdateFleetBadges();
        }

        /// <summary>
        /// 集約バッジ（画面空間）を更新する。畳んでいるまとまりだけに出す＝展開中は個別の駒が見えるので不要。
        /// 描いた矩形がそのまま当たり判定になる（<see cref="FleetMarkerBadgeLayer.TryHit"/>）。
        /// </summary>
        private void UpdateFleetBadges()
        {
            if (badgeLayer == null)
            {
                var go = new GameObject("FleetMarkerBadgeLayer");
                go.transform.SetParent(transform, false);
                badgeLayer = go.AddComponent<FleetMarkerBadgeLayer>();
            }

            badgeClusters.Clear();
            for (int i = 0; i < fleetClusters.Count; i++)
            {
                FleetCluster c = fleetClusters[i];
                if (c == null || c.IsSingle) continue;      // 単独は従来どおり駒だけ
                if (IsClusterExpanded(c)) continue;         // 展開中は個別表示に任せる
                badgeClusters.Add(c);
            }

            // 星系の見かけ半径（要塞は大きい）をピクセルで渡す＝バッジをモデルと星系名から逃がす量。
            if (cam != null && cam.orthographicSize > 0.0001f)
            {
                float viewportPx = Mathf.Max(1f, cam.rect.height * Screen.height);
                float pxPerWorld = viewportPx / (cam.orthographicSize * 2f);
                float worldRadius = systemScale * 0.5f * (fortressSystems.Count > 0 ? fortressDiameterFactor : 1f);
                badgeLayer.SetStarScreenRadius(worldRadius * pxPerWorld);
            }

            Rect vp = cam != null ? cam.rect : new Rect(0f, 0f, 1f, 1f);
            badgeLayer.UpdateBadges(badgeClusters, cam, OwnerColor, vp, true);
        }

        /// <summary>
        /// 集約に使う<b>論理位置</b>（見た目の位置とは分ける）。停泊中は<b>星系そのものの座標</b>を使う＝
        /// <see cref="FleetWorldPos"/> には軍団を見せるための分散オフセット（最大 fleetClusterSpread）が
        /// 入っており、それを基準にすると同じ星系の艦隊が集約半径を超えてまとまらない（実機レビュー指摘）。
        /// 回廊上は補間位置そのままでよい（隣り合って進む艦隊だけがまとまる）。
        /// </summary>
        private Vector2 FleetLogicalPos(StrategicFleet f)
        {
            if (f == null || map == null) return Vector2.zero;
            if (!f.IsOnCorridor)
            {
                StarSystem cur = map.GetSystem(f.currentSystemId);
                return cur != null ? cur.position : Vector2.zero;   // 分散オフセットを含めない
            }
            return FleetWorldPos(f);
        }

        /// <summary>
        /// その艦隊を戦略MAPに描くか（#E 停泊表示の整理）。
        ///
        /// <b>描くのは航行中の艦隊だけ</b>＝停泊中の駒・ラベル・集約バッジ・軍団枠は出さない
        /// （星系ごとに駒が積み上がって恒星も星名も読めなくなるため）。
        /// データは消していない＝所属軍団・司令官・艦艇数・移動命令は「軍団編成」メニューと
        /// 艦隊メニューで確認・操作できる。出発地／目的地／実航路の線は別途
        /// <see cref="DrawMovementOverlay"/> が描く（航行中だけなので混雑しない）。
        /// </summary>
        private bool ShownOnMap(StrategicFleet f)
        {
            if (f == null) return false;
            if (showStationaryFleetsOnMap) return true;   // 旧表示へ戻す逃げ道（既定 false）
            return f.IsOnCorridor;                        // 航行中（交戦中の回廊での固着を含む）だけ
        }

        /// <summary>
        /// 要塞に駐留している艦隊idを1回だけ集める（#40）。要塞は数個なので走査は軽い。
        /// 艦隊ごとに <see cref="FortressGarrisonRules.IsGarrisoned"/> を呼ぶと回廊×艦隊の走査になるため、
        /// 名簿側から一度で集めてキャッシュする（スケーラビリティ規律：差分・キャッシュ）。
        /// </summary>
        private void CollectGarrisonedFleetIds()
        {
            garrisonedFleetIds.Clear();
            if (map == null || map.corridors == null) return;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress == null) continue;
                IReadOnlyList<int> ids = FortressGarrisonRules.GarrisonFleetIds(c.fortress);
                for (int j = 0; j < ids.Count; j++) garrisonedFleetIds.Add(ids[j]);
            }
        }

        /// <summary>その艦隊が属するまとまり（無ければ null）。</summary>
        private FleetCluster ClusterOf(StrategicFleet f)
            => f != null && clusterOfFleet.TryGetValue(f.id, out var c) ? c : null;

        /// <summary>まとまりの代表＝id が最小の艦隊（決定論・毎フレーム同じ駒が代表になる）。</summary>
        private static int RepresentativeOf(FleetCluster c)
            => (c != null && c.fleetIds.Count > 0) ? c.fleetIds[0] : -1;

        /// <summary>
        /// そのまとまりを個別表示に展開するか。①十分寄っている ②中に選択中の艦隊が居る
        /// ③一覧を開いている、のいずれか。②により<b>選択した艦隊を見失わない</b>。
        /// </summary>
        private bool IsClusterExpanded(FleetCluster c)
        {
            if (c == null) return true;
            // 選択しても<b>展開しない</b>（実機QA：1隊選んだだけで34隊が扇状に広がった）。
            // 選択中の艦隊だけを星系の近くで強調し、まとまりの見た目は保つ。
            // 一覧を閉じたら展開も解く（閉じるボタンで閉じた場合も取りこぼさない）。
            if (!FleetClusterListPanel.IsOpen) openedCluster = null;
            if (openedCluster != null && ReferenceEquals(openedCluster, c)) return true;
            float zoom = cam != null ? cam.orthographicSize : 0f;
            return zoom <= clusterExpandZoom;
        }

        /// <summary>
        /// 艦隊（または代表としてのまとまり）の1行表示。特殊記号は使わない（#記号欠落）。
        /// <paramref name="garrisoned"/>＝要塞に駐留中。駐留艦の隻数は<b>要塞のラベルが出す</b>ので、
        /// ここでは隻数を重ねて出さない（同じ艦隊の隻数が盤面に二度並ばない）。
        /// </summary>
        private static string FleetStatusLabel(StrategicFleet f, FleetCluster c, bool expanded, bool isRep,
                                               bool garrisoned = false)
        {
            if (!expanded && isRep && c != null && !c.IsSingle)
            {
                // まとめて出す：艦隊数と総兵力。航行中は方角も添える（経路は選択時だけ描く）。
                string head = FleetClusterRules.MarkerLabel(c);
                if (c.moving)
                {
                    string dir = FleetClusterRules.HeadingLabel(c.heading);
                    if (!string.IsNullOrEmpty(dir)) return $"{head}\n{dir}へ航行";
                }
                return head;
            }

            if (f == null) return "";
            if (garrisoned) return "駐留";     // 隻数は要塞のラベル側（駐留 N部隊 M隻）で読む
            if (f.engaged) return "交戦中";
            if (f.IsMoving) return $"ETA {f.Eta:F1}";
            if (f.IsOnCorridor) return "保持";
            return $"{f.Ships:N0}隻";   // プレイヤー向けは艦艇数のみ（抽象兵力は出さない）
        }

        // ===== 移動の表示（MAP は表示専用・命令は艦隊メニューへ集約）=====

        /// <summary>経路キー → その経路を進んでいる艦隊たち（同じ経路だけを1本にまとめる）。</summary>
        private readonly Dictionary<long, List<StrategicFleet>> routeGroups = new Dictionary<long, List<StrategicFleet>>();
        private readonly List<long> routeGroupOrder = new List<long>();   // 決定論的な描画順（艦隊の並び順）
        private readonly List<LineRenderer> movementArrows = new List<LineRenderer>();
        private readonly List<TextMesh> movementLabels = new List<TextMesh>();

        /// <summary>
        /// 移動中の全艦隊について「どこからどこへ向かっているか」を盤面に描く。
        ///
        /// <b>同じ経路（出発元・いま向かう隣星系・最終目的地・勢力がすべて同じ）だけ</b>を1本にまとめ、
        /// 違う経路は別々に描く＝異なる経路を1つに誤表示しない。まとめた本数は「×n」で出す
        /// （密集時は既存の集約バッジ／艦隊一覧で個別に辿れる）。
        /// 現在区間（いま渡っている回廊）は実線、その先の残り経路は最終目的地までの細い線で区別する。
        /// 選択中の艦隊が含まれる経路は太く明るくする。
        /// </summary>
        private void DrawMovementOverlay()
        {
            routeGroups.Clear();
            routeGroupOrder.Clear();
            if (reg == null || reg.fleets == null || map == null) { HideMovementOverlay(0, 0); return; }

            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || !f.IsOnCorridor) continue;
                if (!FleetOrderRules.TryDescribeRoute(f, out int fromId, out int hopToId, out int finalId)) continue;

                long key = FleetRouteDisplayRules.RouteKey(fromId, hopToId, finalId, f.faction);
                if (!routeGroups.TryGetValue(key, out var list))
                {
                    list = new List<StrategicFleet>();
                    routeGroups[key] = list;
                    routeGroupOrder.Add(key);
                }
                list.Add(f);
            }

            int li = 0, ai = 0;
            for (int g = 0; g < routeGroupOrder.Count; g++)
            {
                List<StrategicFleet> group = routeGroups[routeGroupOrder[g]];
                StrategicFleet head = group[0];
                bool selected = false;
                for (int i = 0; i < group.Count && !selected; i++) selected = selectedFleets.Contains(group[i]);

                FleetOrderRules.TryDescribeRoute(head, out int fromId, out int hopToId, out int finalId);
                StarSystem from = map.GetSystem(fromId), hop = map.GetSystem(hopToId), fin = map.GetSystem(finalId);
                if (from == null || hop == null) continue;

                // 現在区間＝出発元→いま向かう隣星系。その先は<b>艦隊が実際に保持している経路</b>をそのまま描く。
                // ここで最短経路を計算し直すと、要塞回避や飛び石禁止で切り詰めた実航路とずれた線になる
                // （実機QA：目的地まで線が伸びて見えるのに実際は途中で止まる、という食い違いが出た）。
                var pts = new List<Vector3> { from.position, hop.position };
                IReadOnlyList<int> rest = head.RemainingRoute;
                for (int k = 0; k < rest.Count; k++)
                {
                    StarSystem s = map.GetSystem(rest[k]);
                    if (s != null) pts.Add(s.position);
                }

                LineRenderer lr = GetRouteLine(li++);
                lr.positionCount = pts.Count;
                lr.SetPositions(pts.ToArray());
                lr.startWidth = lr.endWidth = selected ? 0.10f : 0.05f;
                Color baseCol = FactionColor(head.faction);
                lr.startColor = lr.endColor = new Color(baseCol.r, baseCol.g, baseCol.b, selected ? 0.95f : 0.45f);
                lr.enabled = true;

                // 進行方向の矢じり（いまの位置から次の星系へ）。どちら向きに動いているかを一目で分かるようにする。
                Vector2 here = FleetWorldPos(head);
                FleetRouteDisplayRules.ArrowHead(here, hop.position, RouteDisplayParams.Default,
                                                 out Vector2 tip, out Vector2 wingL, out Vector2 wingR);
                LineRenderer arrow = GetMovementArrow(ai++);
                arrow.positionCount = 3;
                arrow.SetPosition(0, wingL);
                arrow.SetPosition(1, tip);
                arrow.SetPosition(2, wingR);
                arrow.startWidth = arrow.endWidth = selected ? 0.10f : 0.06f;
                arrow.startColor = arrow.endColor = new Color(baseCol.r, baseCol.g, baseCol.b, selected ? 1f : 0.6f);
                arrow.enabled = true;

                // ラベル：出発→目的地（経由数）＋状態＋まとめた本数。経由数も実航路から数える。
                int via = rest.Count;
                string route = FleetRouteDisplayRules.RouteText(
                    SystemName(fromId), SystemName(finalId), via);
                string state = FleetOrderRules.StateLabel(head);
                string count = group.Count > 1 ? $" ×{group.Count}" : "";
                TextMesh tm = GetMovementLabel(ai - 1);
                tm.text = $"{route}　{state}{count}";
                tm.color = new Color(baseCol.r, baseCol.g, baseCol.b, selected ? 1f : 0.7f);
                Vector2 lp = FleetRouteDisplayRules.LabelPosition(here, hop.position,
                                                                 RouteDisplayParams.Default);
                tm.transform.position = new Vector3(lp.x, lp.y, 0f);
                tm.gameObject.SetActive(true);
            }

            HideMovementOverlay(li, ai);
        }

        /// <summary>使わなかった線・矢じり・ラベルを畳む。</summary>
        private void HideMovementOverlay(int usedLines, int usedArrows)
        {
            for (int i = usedLines; i < routeLines.Count; i++) routeLines[i].enabled = false;
            for (int i = usedArrows; i < movementArrows.Count; i++) movementArrows[i].enabled = false;
            for (int i = usedArrows; i < movementLabels.Count; i++)
                if (movementLabels[i] != null) movementLabels[i].gameObject.SetActive(false);
        }

        private LineRenderer GetMovementArrow(int i)
        {
            while (movementArrows.Count <= i)
            {
                var lr = NewLine("MoveArrow", 3);
                lr.startWidth = lr.endWidth = 0.06f;
                movementArrows.Add(lr);
            }
            return movementArrows[i];
        }

        private TextMesh GetMovementLabel(int i)
        {
            while (movementLabels.Count <= i)
            {
                var go = MakeLabel(transform, "", Vector3.zero, 0.85f);
                go.name = "MoveLabel";
                movementLabels.Add(go.GetComponent<TextMesh>());
            }
            return movementLabels[i];
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
            // #戦略MAP刷新：戦略は新しい濃紺の航宙図（中央が暗く外縁だけ淡い星雲）を優先し、
            // 無ければ従来の galaxy_backdrop へ落ちる＝素材が入っていない環境でも壊れない。
            Texture2D tex = Resources.Load<Texture2D>("Textures/strategy_navy_backdrop");
            if (tex == null) tex = Resources.Load<Texture2D>("Textures/galaxy_backdrop");
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
            // 停泊中は星系の位置（＋同一星系の重なり回避オフセット）。回廊上（前進・保持）は補間。
            if (!f.IsOnCorridor)
                return cur.position + (fleetClusterOffsets.TryGetValue(f.id, out var off) ? off : Vector2.zero);
            StarSystem dst = map.GetSystem(f.destinationSystemId);
            if (dst == null) return cur.position;
            return Vector2.Lerp(cur.position, dst.position, f.Progress);
        }

        /// <summary>
        /// 同一星系に停泊する複数艦隊を散らして重ならないようにするオフセットを計算する（fleet id→offset）。
        /// <b>軍団ごとにまとめて配置</b>＝同じ <see cref="StrategicFleet.corpsId"/> の艦隊は近くに小さくまとめ（軍団の四角が
        /// タイトに囲える）、軍団どうし・無所属艦隊はサブクラスタとして星系の周りに離して並べる。回廊上の艦隊は対象外。
        /// 軍団内は <see cref="fleetClusterSpread"/>（メンバ中心間距離）、サブクラスタ間は <see cref="fleetGroupSpread"/>。
        /// </summary>
        private void UpdateFleetClusterOffsets()
        {
            fleetClusterOffsets.Clear();
            if (reg == null) return;

            // 星系ごとに停泊艦隊を集める。
            var bySystem = new Dictionary<int, List<StrategicFleet>>();
            foreach (var f in reg.fleets)
            {
                if (f == null || f.IsOnCorridor) continue; // 停泊中のみ（回廊上は経路で散る）
                if (!bySystem.TryGetValue(f.currentSystemId, out var list))
                    bySystem[f.currentSystemId] = list = new List<StrategicFleet>();
                list.Add(f);
            }

            foreach (var kv in bySystem)
            {
                var list = kv.Value;
                if (list.Count <= 1) continue; // 1隻なら散らさない
                list.Sort((a, b) => a.id.CompareTo(b.id)); // 決定論（毎フレ同じ並び）

                // サブクラスタへ分割：同じ軍団(corpsId)は1グループ、無所属は各艦隊が単独グループ。
                var corpsGroups = new Dictionary<int, List<StrategicFleet>>();
                var groups = new List<List<StrategicFleet>>();
                foreach (var f in list)
                {
                    if (f.HasCorps)
                    {
                        if (!corpsGroups.TryGetValue(f.corpsId, out var g))
                        {
                            corpsGroups[f.corpsId] = g = new List<StrategicFleet>();
                            groups.Add(g); // 初出順＝決定論（list は id 昇順）
                        }
                        g.Add(f);
                    }
                    else groups.Add(new List<StrategicFleet> { f }); // 無所属は単独グループ
                }

                int gc = groups.Count;
                if (gc == 1)
                {
                    // グループが1つ＝そのまま星系中心にまとめて配置。
                    LayoutRing(groups[0], Vector2.zero, fleetClusterSpread);
                }
                else
                {
                    // 複数グループ：各グループの中心を星系の周りに離して並べ、中で軍団メンバをまとめる。
                    // ★半径に上限を課す（実機QA：32隊が銀河全域へ扇状に散った）。
                    // グループ数が増えるほど 1/sin(π/n) は際限なく伸びるため、そのままでは隣の星系まで届く。
                    // 集約表示があるので散らす必要はもう小さい＝星系の近傍に収める。
                    float groupRadius = Mathf.Min(maxFleetSpreadRadius,
                        fleetGroupSpread / (2f * Mathf.Sin(Mathf.PI / gc)));
                    for (int gi = 0; gi < gc; gi++)
                    {
                        // グループは横並び優先（開始角 0＝2軍団なら左右）＝軍団名ラベルが縦に重ならない。
                        float ang = Mathf.PI * 2f * gi / gc;
                        Vector2 center = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * groupRadius;
                        LayoutRing(groups[gi], center, fleetClusterSpread);
                    }
                }
            }
        }

        /// <summary>1サブクラスタ（軍団 or 単独艦隊）を center を中心に小さくまとめて配置する。</summary>
        private void LayoutRing(List<StrategicFleet> group, Vector2 center, float spread)
        {
            int m = group.Count;
            if (m == 1) { fleetClusterOffsets[group[0].id] = center; return; }
            // メンバ中心間が spread 以上になる小さな輪（弦長 2r*sin(π/m)=spread）。
            float r = Mathf.Min(maxFleetSpreadRadius, spread / (2f * Mathf.Sin(Mathf.PI / m))); // 上限つき（扇状の暴走を防ぐ）
            for (int i = 0; i < m; i++)
            {
                float ang = Mathf.PI * 2f * i / m + Mathf.PI * 0.5f;
                fleetClusterOffsets[group[i].id] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            }
        }

        /// <summary>
        /// 梯団の入れ子の四角を描く（#戦略マップ艦隊表示）。内枠＝軍団（同じ <see cref="StrategicFleet.corpsId"/>）、
        /// 外枠＝軍集団（同じ <see cref="StrategicFleet.armyGroupId"/> の軍団が同一星系に2個以上集結したとき）。
        /// 枠の内側上部に梯団名（第N軍団／第N軍集団）を文字表示。回廊上の艦隊は対象外。
        /// </summary>
        private void UpdateEchelonBoxes()
        {
            // #E：軍団枠は<b>停泊中の艦隊</b>を囲うものなので、停泊表示を止めた今は MAP に出さない。
            // 「軍団の中に配下艦隊」という見え方は「軍団編成」メニュー（CorpsOrganizationPanel）が引き継ぐ
            // ＝分かりやすい構造を捨てたのではなく、混雑しない場所へ移した。
            if (!showStationaryFleetsOnMap) { HideEchelonBoxes(); return; }

            // ===== 外枠：軍集団（同じ armyGroupId が同一星系に「軍団2個以上」集結したとき） =====
            // (armyGroupId, systemId) ごとに停泊中の艦隊を集約。
            var armyGroups = new Dictionary<(int, int), List<StrategicFleet>>();
            if (reg != null)
                foreach (var f in reg.fleets)
                {
                    if (f == null || !f.HasArmyGroup || !f.HasCorps || f.IsOnCorridor) continue;
                    var key = (f.armyGroupId, f.currentSystemId);
                    if (!armyGroups.TryGetValue(key, out var list)) armyGroups[key] = list = new List<StrategicFleet>();
                    list.Add(f);
                }

            int ai = 0;
            foreach (var kv in armyGroups)
            {
                var list = kv.Value;
                // 軍団が2個以上集まったときだけ外枠を出す（「軍団が集まったら」）。
                var corpsSeen = new HashSet<int>();
                for (int i = 0; i < list.Count; i++) corpsSeen.Add(list[i].corpsId);
                if (corpsSeen.Count < 2) continue;

                string name = null;
                for (int i = 0; i < list.Count && string.IsNullOrEmpty(name); i++) name = list[i].armyGroupName;
                if (string.IsNullOrEmpty(name)) name = $"第{kv.Key.Item1}軍集団";

                // 外枠は軍団枠の外側に回り込むよう余白を大きめに。
                DrawEchelonBox(armyBoxLines, armyBoxLabels, ai, list,
                    sidePad: 0.95f, topPad: 1.7f, botPad: 0.95f,
                    color: armyBoxColor, width: 0.04f, label: name, labelCharSize: 0.6f);
                ai++;
            }
            for (int j = ai; j < armyBoxLines.Count; j++) armyBoxLines[j].enabled = false;
            for (int j = ai; j < armyBoxLabels.Count; j++) armyBoxLabels[j].gameObject.SetActive(false);

            // ===== 内枠：軍団（同じ corpsId が同一星系） =====
            var corpsGroups = new Dictionary<(int, int), List<StrategicFleet>>();
            if (reg != null)
                foreach (var f in reg.fleets)
                {
                    if (f == null || !f.HasCorps || f.IsOnCorridor) continue;
                    var key = (f.corpsId, f.currentSystemId);
                    if (!corpsGroups.TryGetValue(key, out var list)) corpsGroups[key] = list = new List<StrategicFleet>();
                    list.Add(f);
                }

            int bi = 0;
            foreach (var kv in corpsGroups)
            {
                var list = kv.Value;
                if (list.Count == 0) continue;
                string name = null;
                for (int i = 0; i < list.Count && string.IsNullOrEmpty(name); i++) name = list[i].corpsName;
                if (string.IsNullOrEmpty(name)) name = $"第{kv.Key.Item1}軍団";

                DrawEchelonBox(corpsBoxLines, corpsBoxLabels, bi, list,
                    sidePad: corpsBoxPadding, topPad: 1.0f, botPad: 0.55f,
                    color: corpsBoxColor, width: 0.025f, label: name, labelCharSize: 0.5f);
                bi++;
            }
            for (int j = bi; j < corpsBoxLines.Count; j++) corpsBoxLines[j].enabled = false;
            for (int j = bi; j < corpsBoxLabels.Count; j++) corpsBoxLabels[j].gameObject.SetActive(false);
        }

        /// <summary>梯団の枠（軍団・軍集団）をすべて隠す（#E 停泊表示の整理）。</summary>
        private void HideEchelonBoxes()
        {
            for (int j = 0; j < armyBoxLines.Count; j++) if (armyBoxLines[j] != null) armyBoxLines[j].enabled = false;
            for (int j = 0; j < armyBoxLabels.Count; j++) if (armyBoxLabels[j] != null) armyBoxLabels[j].gameObject.SetActive(false);
            for (int j = 0; j < corpsBoxLines.Count; j++) if (corpsBoxLines[j] != null) corpsBoxLines[j].enabled = false;
            for (int j = 0; j < corpsBoxLabels.Count; j++) if (corpsBoxLabels[j] != null) corpsBoxLabels[j].gameObject.SetActive(false);
        }

        /// <summary>艦隊群の外接矩形に四角を描き、枠の内側上部に梯団名ラベルを置く（軍団/軍集団 共通）。</summary>
        private void DrawEchelonBox(List<LineRenderer> linePool, List<TextMesh> labelPool, int idx,
            List<StrategicFleet> fleets, float sidePad, float topPad, float botPad,
            Color color, float width, string label, float labelCharSize)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < fleets.Count; i++)
            {
                Vector2 p = FleetWorldPos(fleets[i]);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            var bl = new Vector3(minX - sidePad, minY - botPad, 0f);
            var br = new Vector3(maxX + sidePad, minY - botPad, 0f);
            var tr = new Vector3(maxX + sidePad, maxY + topPad, 0f);
            var tl = new Vector3(minX - sidePad, maxY + topPad, 0f);

            LineRenderer lr = GetPooledBox(linePool, idx, width);
            lr.positionCount = 5;
            lr.SetPositions(new[] { bl, br, tr, tl, bl });
            lr.startColor = lr.endColor = color;
            lr.enabled = true;

            TextMesh lbl = GetPooledLabel(labelPool, idx, labelCharSize);
            lbl.text = label;
            lbl.color = new Color(color.r, color.g, color.b, 1f); // 枠と同系色・読めるよう不透明
            lbl.transform.localPosition = new Vector3((minX + maxX) * 0.5f, maxY + topPad - 0.32f, 0f); // 枠の内側上部
            lbl.gameObject.SetActive(true);
        }

        private LineRenderer GetPooledBox(List<LineRenderer> pool, int i, float width)
        {
            while (pool.Count <= i)
            {
                var lr = NewLine("EchelonBox", 1);
                lr.loop = false;
                pool.Add(lr);
            }
            pool[i].startWidth = pool[i].endWidth = width;
            return pool[i];
        }

        private TextMesh GetPooledLabel(List<TextMesh> pool, int i, float charSize)
        {
            while (pool.Count <= i)
            {
                // 盤面ルート直下＝ワールド座標で配置。
                var tm = MakeLabel(transform, "", Vector3.zero, charSize).GetComponent<TextMesh>();
                pool.Add(tm);
            }
            return pool[i];
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

        /// <summary>
        /// 要塞にしなかった星系を、名前ごとの恒星3Dモデルへ差し替える（#恒星モデル）。
        /// <b>描画だけ</b>＝所有・回廊・選択・進軍・星名は不変。恒星の色は<b>陣営では変えない</b>
        /// （色は FBX の頂点カラー＝名前ごとの固有色）。所属は従来どおり所有リングで読む。
        /// モデルが無い名前は従来の発光核のまま＝素材が揃っていなくても盤面は成立する。
        /// </summary>
        private void AttachStarModels()
        {
            if (!useStarModels || map == null || map.systems == null) return;

            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || fortressSystems.Contains(s.id)) continue; // 要塞が優先

                string path = StarModelRules.ResourcePathForName(s.systemName);
                GameObject prefab = Resources.Load<GameObject>(path);
                if (prefab == null) continue; // 未納品の名前は従来表示（発光核のまま）

                Transform node = FindSystemNode(s.id);
                if (node == null) continue;

                var model = Instantiate(prefab, node, false);
                model.name = "StarModel";
                model.transform.localPosition = new Vector3(0f, 0f, -starDepthOffset / Mathf.Max(0.01f, systemScale));
                model.transform.localRotation = Quaternion.Euler(starEuler);
                model.transform.localScale = Vector3.one;

                var lights = model.GetComponentsInChildren<Light>(true);
                for (int r = 0; r < lights.Length; r++) lights[r].enabled = false;
                var cams = model.GetComponentsInChildren<Camera>(true);
                for (int r = 0; r < cams.Length; r++) cams[r].enabled = false;

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    renderers[r].sortingOrder = 4;
                    renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderers[r].receiveShadows = false;
                    ApplyStarMaterials(renderers[r]);
                }

                // 要塞と同じく実測で大きさを合わせる（FBX のインポート倍率に依らない）。
                float worldDiameter = FitToWorldDiameter(model.transform, renderers, systemScale * starDiameterFactor);

                // 恒星本体は小さめ。所有リングは従来サイズのまま外側に残る＝リングと恒星が混ざらない。
                // 光暈は「恒星の色」で控えめなコロナにする（所有色は使わない＝リングとの役割を分ける）。
                TintCoronaFromStar(s.id, renderers, worldDiameter);

                // 従来の白い発光核は隠す（恒星メッシュと二重に光らない）。リングと星名は残す。
                if (systemCores.TryGetValue(s.id, out var core) && core != null) core.enabled = false;
                starSystems.Add(s.id);

                if (starSystems.Count == 1) LogStarDiagnostics(s, path, model.transform, renderers, worldDiameter);
            }
        }

        /// <summary>
        /// 光暈スプライトを恒星の色（頂点カラーの平均）で染めて控えめなコロナにする（#恒星モデル）。
        /// 所有色は<b>リングが担う</b>ので、ここで陣営色を使うと役割が混ざって所属が読みにくくなる。
        /// 頂点カラーが読めない（Read/Write 無効）モデルでは従来どおり所有色のままにする。
        /// </summary>
        private void TintCoronaFromStar(int systemId, Renderer[] renderers, float worldDiameter)
        {
            if (!systemGlows.TryGetValue(systemId, out var glow) || glow == null) return;
            if (!TryAverageStarColor(renderers, out Color starColor)) return;

            // 恒星本体よりひとまわり大きいだけの控えめなコロナ（リングの内側に収める）。
            float coronaWorld = Mathf.Max(worldDiameter, 0.01f) * starCoronaScale;
            glow.transform.localScale = Vector3.one * (coronaWorld / Mathf.Max(0.01f, systemScale));
            glow.color = new Color(starColor.r, starColor.g, starColor.b, starCoronaAlpha);
        }

        /// <summary>Photosphere（表面）サブメッシュの頂点カラー平均＝その恒星の代表色。</summary>
        private static bool TryAverageStarColor(Renderer[] renderers, out Color color)
        {
            color = Color.white;
            for (int r = 0; r < renderers.Length; r++)
            {
                var mf = renderers[r].GetComponent<MeshFilter>();
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;

                Color[] colors = mesh.colors;
                if (colors == null || colors.Length == 0) continue;

                // 全頂点の平均で足りる（紅炎は少数なので表面色が支配的）。
                float rr = 0f, gg = 0f, bb = 0f;
                for (int i = 0; i < colors.Length; i++) { rr += colors[i].r; gg += colors[i].g; bb += colors[i].b; }
                float n = colors.Length;
                color = new Color(rr / n, gg / n, bb / n, 1f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// その星系を盤面で描いている3Dモデルの Resources パス（艦隊メニューの行き先プレビュー用・#D）。
        ///
        /// <b>盤面と同じ選び方</b>を通す＝別の恒星や作り物の絵を出さない。要塞へ差し替えた星系は
        /// 要塞のパスを返す（恒星と混同しない）。モデルを使わない設定なら空文字＝プレビュー無し。
        /// </summary>
        public string PreviewResourcePathForSystem(int systemId)
        {
            if (fortressSystems.Contains(systemId)) return fortressResourcePath ?? "";
            if (!useStarModels || map == null) return "";
            StarSystem s = map.GetSystem(systemId);
            if (s == null) return "";
            return StarModelRules.ResourcePathForName(s.systemName);
        }

        /// <summary>
        /// 恒星モデルのマテリアル。作り方は <see cref="StarMaterialFactory"/> に集約してある
        /// ＝艦隊メニューの行き先プレビュー（<see cref="ModelPreviewLibrary"/>）と<b>同じ見た目</b>になる。
        /// </summary>
        private void ApplyStarMaterials(Renderer r) => StarMaterialFactory.Apply(r);

        /// <summary>恒星の描画状態を1行 Console へ（実機QAの切り分け用・最初の1体だけ）。</summary>
        private void LogStarDiagnostics(StarSystem s, string path, Transform model, Renderer[] renderers, float worldDiameter)
        {
            string shaderName = "(なし)";
            if (renderers.Length > 0 && renderers[0].sharedMaterial != null && renderers[0].sharedMaterial.shader != null)
                shaderName = renderers[0].sharedMaterial.shader.name;
            var mf = renderers.Length > 0 ? renderers[0].GetComponent<MeshFilter>() : null;
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            bool hasColors = mesh != null && mesh.isReadable && mesh.colors != null && mesh.colors.Length > 0;

            Debug.Log($"[Star] {s.systemName} → {path} / shader={shaderName}" +
                      $" / 頂点カラー={(hasColors ? "あり" : "なし")} readable={(mesh != null && mesh.isReadable)}" +
                      $" / 直径={worldDiameter:F2}（星のリング={systemScale:F2}）/ slots={(renderers.Length > 0 ? renderers[0].sharedMaterials.Length : 0)}" +
                      $" / 差し替え数={starSystems.Count}");
        }

        /// <summary>
        /// 要所（前線の要衝回廊の端点）を選ぶ（#要塞モデル）。敵対勢力を結ぶ要衝回廊の端点を、
        /// 回廊の本数が多い＝結節点として重要な順に最大 <paramref name="max"/> 個返す。
        /// <b>見た目の差し替え先を決めるだけ</b>でゲーム内の扱いは変えない。
        /// </summary>
        public List<int> PickChokepointSystems(int max)
        {
            var picked = new List<int>();
            if (map == null || map.corridors == null || max <= 0) return picked;

            // 候補＝敵対どうしを結ぶ要衝回廊の端点。次数（つながる回廊の本数）が多いほど要所らしい。
            var degree = new Dictionary<int, int>();
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.type != CorridorType.要衝) continue;
                StarSystem a = map.GetSystem(c.aId), b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;
                if (!FactionRelations.IsHostile(null, a.owner, null, b.owner)) continue; // 前線のみ
                degree[a.id] = (degree.TryGetValue(a.id, out int da) ? da : 0) + map.Neighbors(a.id).Count;
                degree[b.id] = (degree.TryGetValue(b.id, out int db) ? db : 0) + map.Neighbors(b.id).Count;
            }
            if (degree.Count == 0) return picked;

            // 次数降順→id 昇順（決定論：同じ盤面なら毎回同じ星が要塞になる）。
            var ids = new List<int>(degree.Keys);
            ids.Sort((x, y) =>
            {
                int cmp = degree[y].CompareTo(degree[x]);
                return cmp != 0 ? cmp : x.CompareTo(y);
            });
            for (int i = 0; i < ids.Count && picked.Count < max; i++) picked.Add(ids[i]);
            return picked;
        }

        /// <summary>
        /// 要所の星系を3D要塞メッシュへ差し替える（#要塞モデル）。<b>描画だけ</b>＝所有・回廊・選択・進軍は不変。
        /// リング（所有色）と星名は残し、中心の発光核だけをメッシュに置き換える＝陣営と選択が読めなくならない。
        /// FBX が未配置なら何もしない（従来の見た目のまま）＝素材の納品前でも壊れない。
        /// 選択と右クリック進軍は星系座標の当たり判定（<c>systemClickRadius</c>）なので、見た目を変えても効き続ける。
        /// </summary>
        // 回廊要塞のラベル（所有・守備を毎フレーム書き換える）。回廊ID→ラベル。
        private readonly Dictionary<int, TextMesh> corridorFortressLabels = new Dictionary<int, TextMesh>();

        /// <summary>
        /// 実際に回廊を扼している要塞（<see cref="Corridor.fortress"/>）の位置へ Blender 製モデルを置く（#40）。
        /// 回廊の中点に置くので<b>見えている要塞＝通せんぼしている固定拠点</b>になる。1つも無ければ false。
        /// </summary>
        private bool AttachCorridorFortressModels(GameObject prefab)
        {
            if (map == null || map.corridors == null) return false;

            int placed = 0;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress == null) continue;
                Transform na = FindSystemNode(c.aId), nb = FindSystemNode(c.bId);
                if (na == null || nb == null) continue;

                var node = new GameObject($"CorridorFortress_{c.aId}_{c.bId}").transform;
                node.SetParent(transform, false);
                node.position = (na.position + nb.position) * 0.5f;   // 回廊の中点＝扼している場所

                var model = Instantiate(prefab, node, false);
                model.name = "FortressModel";
                model.transform.localPosition = new Vector3(0f, 0f, -fortressDepthOffset);
                model.transform.localRotation = Quaternion.Euler(fortressEuler);

                var lights = model.GetComponentsInChildren<Light>(true);
                for (int r = 0; r < lights.Length; r++) lights[r].enabled = false;
                var cams = model.GetComponentsInChildren<Camera>(true);
                for (int r = 0; r < cams.Length; r++) cams[r].enabled = false;

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    renderers[r].sortingOrder = 5;
                    renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderers[r].receiveShadows = false;
                    ApplyFortressMaterials(renderers[r]);
                }
                AimFortressMainGun(model.transform, renderers);
                FitToWorldDiameter(model.transform, renderers, systemScale * fortressDiameterFactor);

                // 「誰の要塞で、守備がどれだけ残っているか」を回廊の上に出す＝封鎖の理由が画面で分かる。
                var labelGo = MakeLabel(node, "", new Vector3(0f, systemScale * 1.1f, 0f), 1f);
                corridorFortressLabels[CorridorKey(c)] = labelGo.GetComponent<TextMesh>();

                placed++;
                if (placed >= Mathf.Max(1, fortressCount)) break;
            }
            return placed > 0;
        }

        /// <summary>回廊を一意に指す整数キー（両端の星系IDから作る・向き不問）。</summary>
        private static int CorridorKey(Corridor c)
            => Mathf.Min(c.aId, c.bId) * 10007 + Mathf.Max(c.aId, c.bId);

        /// <summary>回廊要塞のラベルを現在の状態へ更新する（所有・守備・封鎖中かどうか）。</summary>
        private void UpdateCorridorFortressLabels()
        {
            if (corridorFortressLabels.Count == 0 || map == null || map.corridors == null) return;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress == null) continue;
                if (!corridorFortressLabels.TryGetValue(CorridorKey(c), out TextMesh tm) || tm == null) continue;

                Fortress f = c.fortress;
                bool blocks = FortressRules.BlocksPassage(f);
                // 施設の守備力と<b>駐留艦隊</b>を併記する（足し合わせない＝別勘定）。
                string body = reg != null
                    ? FortressGarrisonRules.GarrisonSummaryText(f, reg.GetFleet)
                    : $"守備力 {Mathf.RoundToInt(f.garrisonStrength)}";
                tm.text = blocks
                    ? $"{f.fortressName}\n{f.owner}　{body}　封鎖中"
                    : $"{f.fortressName}\n{f.owner}　{body}　通行可";
                tm.color = blocks ? fortressBlockadeColor : new Color(0.75f, 0.8f, 0.88f);
            }
        }

        private void AttachFortressModels()
        {
            if (fortressCount <= 0 || string.IsNullOrEmpty(fortressResourcePath)) return;

            GameObject prefab = Resources.Load<GameObject>(fortressResourcePath);
            if (prefab == null) return; // 未納品＝従来表示（警告も出さない＝通常運用）

            // ★#40：実際に回廊を扼している要塞（Corridor.fortress）があれば、<b>その回廊の上に</b>モデルを置く。
            // 見た目の要塞と封鎖している固定拠点が別物だと「どれが通せんぼしているのか」が画面から分からない。
            // 要塞が1つも据えられていない盤面（従来のセーブ等）だけ、従来どおり要衝の星系へ飾る。
            if (AttachCorridorFortressModels(prefab)) return;

            List<int> targets = PickChokepointSystems(fortressCount);
            if (targets.Count == 0) return;

            // 実ライトは使わない（2D Renderer では 3D ライトが効かない）＝陰影はシェーダー内の擬似光源で作る。

            for (int i = 0; i < targets.Count; i++)
            {
                StarSystem s = map.GetSystem(targets[i]);
                if (s == null) continue;
                Transform node = FindSystemNode(targets[i]);
                if (node == null) continue;

                var model = Instantiate(prefab, node, false);
                model.name = "FortressModel";
                // スプライト（星系ドット/回廊）より手前へ少し出す＝2Dの絵と奥行きで competing しない。
                model.transform.localPosition = new Vector3(0f, 0f, -fortressDepthOffset / Mathf.Max(0.01f, systemScale));
                model.transform.localRotation = Quaternion.Euler(fortressEuler);
                model.transform.localScale = Vector3.one;

                // FBX に紛れ込んだライト/カメラは使わない（盤面の見え方を乱すため）。
                var lights = model.GetComponentsInChildren<Light>(true);
                for (int r = 0; r < lights.Length; r++) lights[r].enabled = false;
                var cams = model.GetComponentsInChildren<Camera>(true);
                for (int r = 0; r < cams.Length; r++) cams[r].enabled = false;

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    renderers[r].sortingOrder = 5; // 2Dスプライトと混在するので描画順を明示
                    renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderers[r].receiveShadows = false;
                    ApplyFortressMaterials(renderers[r]);
                }

                // 主砲がカメラ側へ斜めに向くよう自動で回す（FBX の軸取りに依存しない）。
                AimFortressMainGun(model.transform, renderers);

                // 大きさは**実測**して合わせる（FBX のインポート倍率に依らない）。
                // 普通の星のリング直径（＝systemScale ワールド単位）の fortressDiameterFactor 倍にする。
                float worldDiameter = FitFortressScale(model.transform, renderers);

                // 要塞は普通の星より大きく、既定のままだと所有リングと光暈を完全に覆ってしまう
                // ＝青/赤の所属が読めなくなる（実機QA）。この星系だけリングと光暈を実測径の外側へ広げ、
                // 星名も要塞の上へ逃がす。当たり判定も絵に合わせて広げる（クリック/Iが効かない対策）。
                EnlargeOwnerMarkersForFortress(targets[i], worldDiameter);

                // 中心の発光核だけ隠す（リング＝所有色と星名は残す）。
                if (systemCores.TryGetValue(targets[i], out var core) && core != null) core.enabled = false;
                fortressSystems.Add(targets[i]);

                if (i == 0) LogFortressDiagnostics(s, model.transform, renderers);
            }
        }

        /// <summary>
        /// 主砲（<c>Reactor_Cyan</c> の submesh＝青白い発光核）の位置を実測し、その向きが
        /// カメラ側へ<b>斜めに</b>向くようモデルを回す（#要塞モデル）。FBX の軸の取り方（Blender の
        /// axis_forward/up）に依存せず、モデルを差し替えても砲口が見える。読み取り不可なメッシュや
        /// 発光スロットが見つからない場合は <see cref="fortressEuler"/> の既定角のままにする。
        /// </summary>
        private void AimFortressMainGun(Transform model, Renderer[] renderers)
        {
            if (model == null || renderers == null) return;
            if (!TryFindMainGunLocalDirection(model, renderers, out Vector3 gunLocal)) return;

            // 目標＝カメラ（-Z 方向）へ向けつつ、右上へ振って 3/4 の立体的な見え方にする。
            Vector3 target = fortressAimDirection.sqrMagnitude > 1e-4f
                ? fortressAimDirection.normalized
                : new Vector3(-0.42f, 0.30f, -1f).normalized;

            // いまの姿勢で砲がどちらを向いているかを出し、それを target へ重ねる回転を前掛けする。
            Vector3 gunWorld = model.TransformDirection(gunLocal).normalized;
            model.rotation = Quaternion.FromToRotation(gunWorld, target) * model.rotation;
        }

        /// <summary>主砲方向（モデルのローカル空間）を探す。発光スロットの重心－モデル中心。</summary>
        private bool TryFindMainGunLocalDirection(Transform model, Renderer[] renderers, out Vector3 dir)
        {
            dir = Vector3.zero;
            for (int r = 0; r < renderers.Length; r++)
            {
                var mf = renderers[r].GetComponent<MeshFilter>();
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;

                Material[] mats = renderers[r].sharedMaterials;
                if (mats == null) continue;

                for (int s = 0; s < mats.Length && s < mesh.subMeshCount; s++)
                {
                    string name = mats[s] != null ? mats[s].name : FortressSlotNameByIndex(s);
                    if (name == null || name.IndexOf("Reactor", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    int[] tris = mesh.GetTriangles(s);
                    if (tris == null || tris.Length == 0) continue;
                    Vector3[] verts = mesh.vertices;

                    Vector3 sum = Vector3.zero;
                    int n = 0;
                    for (int t = 0; t < tris.Length; t++)
                    {
                        int vi = tris[t];
                        if (vi < 0 || vi >= verts.Length) continue;
                        sum += verts[vi]; n++;
                    }
                    if (n == 0) continue;

                    // 重心は**メッシュのローカル座標**なので、そのまま親の座標として足すと
                    // FBX の軸変換で子に載った回転/倍率を無視してしまう（＝向きが狂う）。
                    // レンダラーのローカル→ワールド→モデルローカル、と正しく通す。
                    Vector3 centroid = sum / n;
                    Vector3 world = renderers[r].transform.TransformPoint(centroid);
                    Vector3 local = model.InverseTransformPoint(world); // モデル原点（＝球の中心）からの向き
                    if (local.sqrMagnitude < 1e-6f) continue;
                    dir = local.normalized;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 要塞メッシュの大きさを実測して合わせる。FBX のインポート倍率（Blender 半径やスケール設定）に依らず、
        /// <b>普通の星のリング直径の <see cref="fortressDiameterFactor"/> 倍</b>のワールド直径になる。
        /// </summary>
        private float FitFortressScale(Transform model, Renderer[] renderers)
            => FitToWorldDiameter(model, renderers, systemScale * fortressDiameterFactor); // 星のリング直径＝systemScale

        /// <summary>
        /// メッシュの外接直径を実測して、指定のワールド直径になるよう縮尺を合わせる（恒星/要塞 共通）。
        /// FBX 側のスケール（Blender 単位・エクスポート設定・インポート倍率）に一切依存しない。
        /// </summary>
        private static float FitToWorldDiameter(Transform model, Renderer[] renderers, float targetWorld)
        {
            if (model == null || renderers == null || renderers.Length == 0) return 0f;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float current = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (current <= 1e-4f) return 0f;

            float target = Mathf.Max(0.05f, targetWorld);
            model.localScale *= target / current;
            return target;
        }

        /// <summary>
        /// 要塞に差し替えた星系だけ、所有リング・光暈・星名・当たり判定を要塞の大きさに合わせる（#要塞モデル）。
        /// 陣営色（青/赤）はリングで読ませているので、要塞に隠れると所属が分からなくなる＝実測径の外へ出す。
        /// </summary>
        private void EnlargeOwnerMarkersForFortress(int systemId, float worldDiameter)
        {
            if (worldDiameter <= 1e-4f) return;

            // 子の localScale 1 のとき、リングのワールド直径は systemScale（親スケール）。
            // よって「ワールド直径 D」にしたければ localScale = D / systemScale。
            float ringWorld = worldDiameter * fortressRingScale;
            float scale = ringWorld / Mathf.Max(0.01f, systemScale);

            if (systemDots.TryGetValue(systemId, out var ring) && ring != null)
                ring.transform.localScale = Vector3.one * scale;
            if (systemGlows.TryGetValue(systemId, out var glow) && glow != null)
                glow.transform.localScale = Vector3.one * scale * 1.28f; // リングの外側へ淡く広げる

            // 星名は要塞とリングの上へ逃がす（localPosition は親のローカル＝ワールドでは systemScale 倍）。
            if (systemNameLabelById.TryGetValue(systemId, out var label) && label != null)
            {
                float liftWorld = ringWorld * 0.5f + fortressLabelLift;
                Vector3 p = label.localPosition;
                label.localPosition = new Vector3(p.x, liftWorld / Mathf.Max(0.01f, systemScale), p.z);
            }

            // 絵に合わせて当たり判定も広げる（既定 0.65 のままでは要塞の縁を押しても反応しない）。
            float hit = ringWorld * 0.5f;
            if (hit > fortressHitRadius) fortressHitRadius = hit;
        }

        /// <summary>
        /// 星系の当たり判定半径。要塞へ差し替えた星系は見た目が大きいぶん広げる（クリック/右クリック進軍/I）。
        /// 判定は従来どおり星系の座標を中心とした円＝モデルの中心と一致する。
        /// </summary>
        public float ClickRadiusFor(int systemId)
        {
            if (fortressHitRadius > systemClickRadius && fortressSystems.Contains(systemId)) return fortressHitRadius;
            return systemClickRadius;
        }

        /// <summary>
        /// マテリアル名（FBX のスロット名）から専用マテリアルを割り当てる（#要塞モデル）。
        /// URP のプロジェクトで FBX の埋め込みマテリアルをそのまま使うと、シェーダー不一致で
        /// 真っ黒/マゼンタになりうる。ここで URP Lit を作って色・金属感・発光を与え、見本どおりの
        /// 「銀灰の装甲・暗い整備帯・青白い主砲・暖色の窓」を再現する。生成物（FBX）には手を触れない。
        /// </summary>
        private void ApplyFortressMaterials(Renderer r)
        {
            Material[] slots = r.sharedMaterials;
            if (slots == null || slots.Length == 0) return;
            var next = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                string slotName = slots[i] != null ? slots[i].name : "";
                // マテリアルを取り込まない設定でインポートされた場合は名前が引けないので、スロット順で補う。
                if (string.IsNullOrEmpty(slotName)) slotName = FortressSlotNameByIndex(i);
                next[i] = FortressMaterial(slotName) ?? slots[i];
            }
            r.sharedMaterials = next;
        }

        /// <summary>FBX のマテリアルスロット順（納品仕様）。名前が取れないときの予備。</summary>
        private static readonly string[] FortressSlotOrder =
        {
            "Recess_Graphite", "Armor_Silver", "Armor_Light", "Armor_Dark",
            "Gunmetal", "Reactor_Cyan", "Windows_Amber",
        };

        private static string FortressSlotNameByIndex(int i)
            => (i >= 0 && i < FortressSlotOrder.Length) ? FortressSlotOrder[i] : "Armor_Silver";

        /// <summary>スロット名→マテリアル（生成して使い回す）。未知の名前は装甲の銀へ寄せる。</summary>
        private Material FortressMaterial(string slotName)
        {
            if (string.IsNullOrEmpty(slotName)) slotName = "Armor_Silver";
            // FBX インポート時に付く接尾辞（" (Instance)" 等）を落として素の名前で引く。
            int paren = slotName.IndexOf(" (");
            if (paren > 0) slotName = slotName.Substring(0, paren);

            if (fortressMaterials.TryGetValue(slotName, out Material cached) && cached != null) return cached;

            // ★このプロジェクトの URP は **2D Renderer**（Assets/Settings/Renderer2D.asset）で動いている。
            // 2D Renderer は 3D ライトの前方/遅延ライティングパスを持たないため、URP/Lit を貼っても陰影が付かず
            // 平坦な白黒の模様になる（実機QAの症状）。実ライトに頼らず自前で陰影を計算する専用シェーダーを使う。
            // Resources 配下に置いてあるのでビルドからも剥がれない。
            Shader shader = Resources.Load<Shader>("Shaders/FortressSelfLit");
            if (shader == null) shader = Shader.Find("Ginei/FortressSelfLit");
            if (shader == null) shader = Shader.Find("Sprites/Default"); // 最後の保険（陰影なしでも形は出る）
            if (shader == null) return null;

            var m = new Material(shader) { name = "Fortress_" + slotName };
            Color baseColor; float metallic, smoothness; Color emission = Color.black;

            switch (slotName)
            {
                case "Recess_Graphite": // 分割パネルの溝・整備/ドック帯の暗部
                    baseColor = new Color(0.13f, 0.15f, 0.18f); metallic = 0.30f; smoothness = 0.25f; break;
                case "Armor_Light":     // 明るい装甲パネル
                    baseColor = new Color(0.82f, 0.85f, 0.89f); metallic = 0.60f; smoothness = 0.62f; break;
                case "Armor_Dark":      // 暗い装甲パネル（陰影の変化をつける）
                    baseColor = new Color(0.42f, 0.46f, 0.53f); metallic = 0.70f; smoothness = 0.45f; break;
                case "Gunmetal":        // 小型砲塔・アンテナ・主砲の枠
                    baseColor = new Color(0.30f, 0.34f, 0.40f); metallic = 0.85f; smoothness = 0.50f; break;
                case "Reactor_Cyan":    // 主砲の青白い発光核
                    baseColor = new Color(0.78f, 0.93f, 1f); metallic = 0f; smoothness = 0.85f;
                    emission = new Color(0.55f, 0.85f, 1f) * 3.0f; break;
                case "Windows_Amber":   // 暖色の窓（居住/ドック）
                    baseColor = new Color(1f, 0.82f, 0.52f); metallic = 0f; smoothness = 0.60f;
                    emission = new Color(1f, 0.66f, 0.30f) * 2.0f; break;
                default:                // Armor_Silver ほか＝主装甲の銀灰
                    baseColor = new Color(0.72f, 0.75f, 0.80f); metallic = 0.65f; smoothness = 0.55f; break;
            }

            SetColorAny(m, baseColor, "_BaseColor", "_Color");
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);

            // 全スロットで同じキーライト方向にする＝球全体の陰影が一貫し、立体に見える。
            if (m.HasProperty("_LightDir"))
                m.SetVector("_LightDir", new Vector4(fortressKeyLightDir.x, fortressKeyLightDir.y, fortressKeyLightDir.z, 0f));

            if (emission != Color.black)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
                // 発光面は陰影に沈ませない（主砲の青白と窓の暖色が常に見える）。
                if (m.HasProperty("_AmbientColor")) m.SetColor("_AmbientColor", new Color(0.9f, 0.9f, 0.9f));
                if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0.1f);
            }

            fortressMaterials[slotName] = m;
            return m;
        }

        /// <summary>URP/Standard どちらの色プロパティ名でも設定できるようにする。</summary>
        private static void SetColorAny(Material m, Color c, string a, string b)
        {
            if (m.HasProperty(a)) m.SetColor(a, c);
            else if (m.HasProperty(b)) m.SetColor(b, c);
        }

        /// <summary>
        /// 要塞の描画状態を1行で Console に出す（実機QAの切り分け用）。
        /// 「白黒の砂嵐」の原因はパイプライン不一致（Built-in に URP/Lit）か法線かZ競合かの3択なので、
        /// 実際に使われたシェーダー・法線の有無・実寸をここで確定させる。
        /// </summary>
        private void LogFortressDiagnostics(StarSystem s, Transform model, Renderer[] renderers)
        {
            bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            string shaderName = "(なし)";
            if (renderers.Length > 0 && renderers[0].sharedMaterial != null && renderers[0].sharedMaterial.shader != null)
                shaderName = renderers[0].sharedMaterial.shader.name;

            var mf = renderers.Length > 0 ? renderers[0].GetComponent<MeshFilter>() : null;
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            int tris = mesh != null ? mesh.triangles.Length / 3 : -1;
            bool hasNormals = mesh != null && mesh.normals != null && mesh.normals.Length > 0;

            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            Debug.Log($"[Fortress] {s.systemName} / pipeline={(urp ? "URP" : "Built-in")} / shader={shaderName}" +
                      $" / tris={tris} normals={hasNormals} readable={(mesh != null && mesh.isReadable)}" +
                      $" / worldDiameter={Mathf.Max(b.size.x, b.size.y):F2}（星のリング={systemScale:F2}）" +
                      $" / localScale={model.localScale.x:F3} / slots={(renderers.Length > 0 ? renderers[0].sharedMaterials.Length : 0)}" +
                      $" / ring径={(Mathf.Max(b.size.x, b.size.y) * fortressRingScale):F2} 判定半径={ClickRadiusFor(s.id):F2}（通常={systemClickRadius:F2}）");
        }

        /// <summary>星系ノード（System_ で始まる子）を id から引く。</summary>
        private Transform FindSystemNode(int systemId)
        {
            if (systemDots.TryGetValue(systemId, out var ring) && ring != null)
                return ring.transform.parent; // リングは星系ノードの子
            return null;
        }

        /// <summary>星系ノードの子スプライトを作る（光暈/リング/核の共通生成）。半径倍率と描画順だけが違う。</summary>
        private SpriteRenderer MakeChild(Transform parent, string name, Sprite sprite, float radiusFactor, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * radiusFactor;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // 星系名ラベル（TextMesh はワールド空間なので、引くと画面上で小さくなり密集する）。
        // LateUpdate から UpdateLabelLegibility が拡大率と表示可否を毎フレーム調整する。
        private readonly List<Transform> systemNameLabels = new List<Transform>();

        /// <summary>
        /// 星系名を読める大きさに保つ（#星系名が小さく密集する）。引くほどラベルを拡大して<b>画面上の文字サイズを一定</b>にし、
        /// 画面ピクセルで重なりを見て、混んだところだけ後着を隠す（縮尺では隠さない）。
        /// 親（星系ノード）が systemScale で縮んでいるため、ここでは localScale の倍率だけを触る。
        /// </summary>
        private void UpdateLabelLegibility()
        {
            if (cam == null || systemNameLabels.Count == 0) return;

            // ★縮尺（orthographicSize）で一律に隠さない（実機QA：縦長 900x1200 の全体表示で星系名が全消え）。
            // 縦長ではビューポートが狭いぶんフィットに大きな縮尺が要り、世界単位のしきい値だと必ず超えてしまう。
            // 判定は<b>画面ピクセル</b>で行い、①文字の見かけの大きさを一定に保ち ②混んだところだけ間引く。
            float viewportPx = Mathf.Max(1f, cam.rect.height * Screen.height);
            float pxPerWorld = viewportPx / Mathf.Max(0.0001f, cam.orthographicSize * 2f);

            // 見かけを一定にする倍率＝縮尺にもビューポートの実寸にも依らない。
            float refPxPerWorld = LabelReferencePxPerWorld;
            float k = Mathf.Clamp(refPxPerWorld / pxPerWorld, 0.25f, 6f);

            // 画面に出ている星系の位置を集める（間引きの前に「どれだけ混んでいるか」を測る）。
            labelScreenPoints.Clear();
            labelVisibleIndex.Clear();
            float vx0 = cam.rect.xMin * Screen.width, vx1 = cam.rect.xMax * Screen.width;
            float vy0 = cam.rect.yMin * Screen.height, vy1 = cam.rect.yMax * Screen.height;
            for (int i = 0; i < systemNameLabels.Count; i++)
            {
                Transform t = systemNameLabels[i];
                if (t == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(t.position);
                if (sp.z <= 0f || sp.x < vx0 || sp.x > vx1 || sp.y < vy0 || sp.y > vy1)
                {
                    if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
                    continue;
                }
                labelScreenPoints.Add(new Vector2(sp.x, sp.y));
                labelVisibleIndex.Add(i);
            }

            // ★見かけ一定のままだと縦長の全体表示では星の間隔（画面px）より文字が広く、
            // 「重なるから隠す」を素直に適用するとほとんどの名前が消える（実機QA：全消え）。
            // そこで<b>まず混み具合に合わせて縮め</b>、実ピクセルの下限までは縮めてから重なりだけ間引く。
            float shrink = CrowdingShrink();
            k *= shrink;
            Vector3 scale = Vector3.one * k;

            // 画面上で近すぎるラベルは後から来たほうを隠す（重なり回避）。並びは生成順＝決定論。
            labelScreenRects.Clear();
            float w = starLabelScreenWidth * shrink;
            float h = starLabelScreenHeight * shrink;

            for (int n = 0; n < labelVisibleIndex.Count; n++)
            {
                Transform t = systemNameLabels[labelVisibleIndex[n]];
                Vector2 sp = labelScreenPoints[n];

                var r = new Rect(sp.x - w * 0.5f, sp.y - h * 0.5f, w, h);
                bool show = true;
                for (int j = 0; j < labelScreenRects.Count; j++)
                    if (labelScreenRects[j].Overlaps(r)) { show = false; break; }
                if (show) labelScreenRects.Add(r);

                if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
                if (show) t.localScale = scale;
            }
        }

        /// <summary>
        /// 画面上の星系がどれだけ近いかから、ラベルの縮小率を決める（1＝縮めない）。
        /// 隣どうしの距離の中央値がラベル幅より狭いぶんだけ縮め、
        /// <see cref="starLabelMinScreenHeight"/> の実ピクセル下限より小さくはしない。
        /// </summary>
        private float CrowdingShrink()
        {
            int n = labelScreenPoints.Count;
            float floor = starLabelScreenHeight > 0.01f
                ? Mathf.Clamp01(starLabelMinScreenHeight / starLabelScreenHeight) : 1f;
            if (n < 2 || starLabelScreenWidth <= 0.01f) return 1f;

            labelNeighborDist.Clear();
            for (int i = 0; i < n; i++)
            {
                float best = float.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    float d = Vector2.Distance(labelScreenPoints[i], labelScreenPoints[j]);
                    if (d < best) best = d;
                }
                if (best < float.MaxValue) labelNeighborDist.Add(best);
            }
            if (labelNeighborDist.Count == 0) return 1f;

            labelNeighborDist.Sort();
            float median = labelNeighborDist[labelNeighborDist.Count / 2];
            return Mathf.Clamp(median / starLabelScreenWidth, floor, 1f);
        }

        /// <summary>ラベルの見かけを揃える基準（設計時のビューポートでの ピクセル/ワールド）。</summary>
        private float LabelReferencePxPerWorld
        {
            get
            {
                float refViewportPx = StrategyScreenLayout.Default.MapHeight * 1080f; // 設計時のマップ窓の高さ(px)
                return refViewportPx / Mathf.Max(0.0001f, labelReferenceZoom * 2f);
            }
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

        /// <summary>
        /// 細いリング（輪郭だけの円）のスプライトを生成する（#戦略MAP刷新）。
        /// 太いベタ丸をやめ「小さな発光核＋細いリング」で星系を示すための輪。内外の縁を AA してズームでも滑らか。
        /// <paramref name="thicknessFrac"/> は半径に対する線の太さの割合（0.08＝細線）。
        /// </summary>
        private static Sprite MakeRingSprite(int size, float thicknessFrac)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            float half = Mathf.Max(0.75f, r * Mathf.Clamp(thicknessFrac, 0.01f, 0.5f) * 0.5f); // 線の半幅
            float mid = r - half - 1f;          // 線の中心半径（外周に1px の逃げ）
            float edge = Mathf.Max(1f, size / 160f); // AA 幅
            var cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    // |d - mid| が half 以内なら不透明、そこから edge でなめらかに消える。
                    float a = Mathf.Clamp01((half - Mathf.Abs(d - mid)) / edge + 1f);
                    if (Mathf.Abs(d - mid) > half + edge) a = 0f;
                    cols[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(cols);
            tex.Apply(true);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>
        /// 円ディスクのスプライトを生成する（星系ドット/艦隊マル/リング/会戦ピン共用）。
        /// 深ズーム対応：高解像度＋<b>縁をアンチエイリアス</b>（アルファのなめらかな falloff）で、
        /// 強く拡大してもギザギザにならず滑らかな円を保つ（解像度に比例した縁幅で AA）。
        /// pixelsPerUnit=size なので世界サイズは解像度に依らず常に直径1ワールド単位。
        /// </summary>
        private static Sprite MakeDiscSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true); // mipmap＝縮小時のちらつき防止
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            float edge = Mathf.Max(1f, size / 128f); // AA の縁幅（解像度に比例）
            var cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    // r-edge まで不透明、r で透明へなめらかに（縁を AA＝深ズームでも滑らかな円）
                    float a = Mathf.Clamp01((r - d) / edge);
                    cols[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(cols);
            tex.Apply(true); // mipmap を生成
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

    }
}
