using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ginei
{
    /// <summary>
    /// 星系の非戦闘ビュー（恒星系の閲覧・内政メイン・Battle シーン）。戦略マップで戦闘/攻城が無い星系を
    /// ダブルクリックしたとき `BattleSetup.SetupSystemView` が生成する。
    ///
    /// 内政三層（#767・ハイブリッド）の**惑星層の入口**：恒星を中心に第一惑星・第二惑星…を象徴配置し
    /// （実スケールは採らない）、惑星をクリックすると**その惑星の内政（Province）**を表示する。
    /// 星系の集約サマリ（`GovernanceRules.AggregateSystem`）も併記する＝星系は惑星の集約ビュー。
    /// 戦闘判定は行わない（BattleManager はシステムビュー中、Backspace で戦略へ戻すだけ）。
    /// </summary>
    public class SystemView : MonoBehaviour
    {
        [Header("恒星")]
        [Tooltip("中心の恒星の見た目スケール")]
        public float starScale = 2.2f;
        [Tooltip("恒星の色（既定=暖色の太陽）")]
        public Color starColor = new Color(1f, 0.85f, 0.4f);

        [Header("軌道と惑星（恒星中心に第一惑星・第二惑星…を象徴配置・#767）")]
        [Tooltip("惑星を置く軌道半径（恒星から外側へ。要素数=惑星数）。実スケールは採らず象徴的な間隔にする")]
        public float[] orbitRadii = new float[] { 3.5f, 5.5f, 7.5f };
        [Tooltip("軌道リングの色")]
        public Color orbitColor = new Color(0.6f, 0.7f, 0.9f, 0.35f);
        [Tooltip("惑星の見た目スケール（恒星より小さく）")]
        public float planetScale = 0.7f;
        [Tooltip("惑星クリックの判定半径（ワールド）")]
        public float planetClickRadius = 1.3f;
        [Tooltip("惑星の色パレット（軌道順に巡回）")]
        public Color[] planetColors = new Color[]
        {
            new Color(0.55f, 0.75f, 1f),   // 青
            new Color(0.85f, 0.6f, 0.45f), // 赤茶
            new Color(0.6f, 0.85f, 0.65f), // 緑
            new Color(0.85f, 0.8f, 0.55f), // 黄土
        };

        // BattleSetup から設定（内政の決定的生成・所有勢力の思想バイアスに使う）
        public int systemId;
        public Faction ownerFaction = Faction.帝国;
        public string systemName = "星系";

        // 惑星エントリ（象徴配置の位置・内政データ・表示）
        private class PlanetEntry
        {
            public int index;
            public Vector3 pos;
            public Province province; // 惑星単位の内政（単一の真実・#767）
        }

        private readonly List<PlanetEntry> planets = new List<PlanetEntry>();
        private SpriteRenderer starRenderer;
        private Sprite discSprite;   // 恒星・惑星で共有するディスク（OnDestroy で破棄）
        private Material orbitMat;

        private TextMesh aggregateLabel;   // 星系の集約サマリ（常時）
        private TextMesh planetInfoLabel;  // 選択惑星の内政（選択時のみ）
        private GameObject selectionMarker; // 選択中の惑星を囲うリング
        private int selectedIndex = -1;
        private Camera cam;

        public void Build()
        {
            transform.position = Vector3.zero;
            cam = Camera.main;

            // 恒星・惑星で共有するディスクスプライト
            discSprite = MakeDisc(128);

            // 中心の恒星
            var starGo = new GameObject("Star");
            starGo.transform.SetParent(transform, false);
            starRenderer = starGo.AddComponent<SpriteRenderer>();
            starRenderer.sprite = discSprite;
            starRenderer.color = starColor;
            starRenderer.sortingOrder = -40;
            starGo.transform.localScale = Vector3.one * starScale;

            // 軌道リング＋惑星（恒星を中心に第一惑星・第二惑星…を象徴配置・#767）
            orbitMat = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            if (orbitRadii != null)
            {
                for (int i = 0; i < orbitRadii.Length; i++)
                {
                    BuildOrbitRing(orbitRadii[i], i);
                    BuildPlanet(orbitRadii[i], i);
                }
            }

            // 選択リング（最初は非表示）
            selectionMarker = new GameObject("PlanetSelection");
            selectionMarker.transform.SetParent(transform, false);
            var sel = selectionMarker.AddComponent<LineRenderer>();
            sel.material = orbitMat; sel.useWorldSpace = false; sel.loop = true;
            sel.widthMultiplier = 0.08f; sel.numCapVertices = 2;
            sel.startColor = sel.endColor = new Color(1f, 0.95f, 0.4f);
            sel.sortingOrder = -41;
            const int sseg = 40;
            sel.positionCount = sseg;
            float selR = planetScale * 0.9f + 0.5f;
            for (int i = 0; i < sseg; i++)
            {
                float a = (Mathf.PI * 2f / sseg) * i;
                sel.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * selR);
            }
            selectionMarker.SetActive(false);

            // 星系名ラベル
            var labelGo = new GameObject("SystemViewLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, starScale + 1.4f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = systemName;
            tm.font = FontProvider.JapaneseFont;
            tm.fontSize = 48; tm.characterSize = 0.12f;
            tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            if (tm.font != null) labelGo.GetComponent<MeshRenderer>().material = tm.font.material;

            // 集約サマリ（星系＝惑星の集約ビュー・#767）。星系名の下に常時表示。
            aggregateLabel = MakeLabel("SystemAggregate", new Vector3(0f, starScale + 0.4f, 0f),
                36, 0.09f, TextAnchor.LowerCenter, new Color(0.8f, 0.9f, 1f));
            UpdateAggregate();

            // 選択惑星の内政（右側・選択時のみ）
            planetInfoLabel = MakeLabel("PlanetInfo", new Vector3(9.5f, 3.5f, 0f),
                34, 0.085f, TextAnchor.UpperLeft, new Color(0.9f, 0.95f, 1f));
            planetInfoLabel.text = "";

            // 操作ヒント
            var hint = MakeLabel("SystemViewHint", new Vector3(0f, -(starScale + 2.0f), 0f),
                34, 0.08f, TextAnchor.UpperCenter, new Color(0.8f, 0.85f, 1f));
            hint.text = "システムビュー（恒星系・内政）　惑星クリックで内政を表示　Backspaceで戦略マップへ";
        }

        private void Update()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            // マウス直下の最寄り惑星を選択（物理に依存せず距離判定）
            Vector3 sp = Mouse.current.position.ReadValue();
            sp.z = -cam.transform.position.z;
            Vector3 w = cam.ScreenToWorldPoint(sp);

            int hit = -1;
            float best = planetClickRadius;
            for (int i = 0; i < planets.Count; i++)
            {
                float d = Vector2.Distance(w, planets[i].pos);
                if (d < best) { best = d; hit = i; }
            }
            if (hit >= 0) SelectPlanet(hit);
        }

        /// <summary>惑星を選択して内政を表示する（クリック／外部からの選択の窓口）。</summary>
        public void SelectPlanet(int index)
        {
            if (index < 0 || index >= planets.Count) return;
            selectedIndex = index;
            PlanetEntry e = planets[index];

            selectionMarker.transform.localPosition = e.pos;
            selectionMarker.SetActive(true);

            planetInfoLabel.text = FormatPlanetInfo(e);
        }

        private string FormatPlanetInfo(PlanetEntry e)
        {
            Province p = e.province;
            int output = Mathf.RoundToInt(GovernanceRules.OutputFactor(p) * 100f);
            string unrest = GovernanceRules.IsUnrest(p) ? "　▲反乱リスク" : "";
            return $"第{Ordinal(e.index + 1)}惑星\n" +
                   $"住民思想: {p.nativeIdeology}\n" +
                   $"人口: {Mathf.RoundToInt(p.population)}\n" +
                   $"安定度: {Mathf.RoundToInt(p.stability)}%{unrest}\n" +
                   $"統合度: {Mathf.RoundToInt(p.integration * 100f)}%\n" +
                   $"類型: {p.systemType}（産出効率 {output}%）\n" +
                   $"産出/秒: {FormatPlanetResources(p)}" +
                   FormatPlanetDemographics(p) +
                   FormatPlanetOccupation(p) +
                   FormatPlanetStrategic(p);
        }

        // POP の職業構成（#110 職業）：就労シェア＋就業率＋徴募源（軍属）。
        private static string FormatPlanetOccupation(Province p)
        {
            Workforce w = p.workforce ?? OccupationRules.Default(p.systemType);
            float empRate = OccupationRules.EmploymentRate(p);
            int emp = Mathf.RoundToInt(empRate * 100f);
            int rec = Mathf.RoundToInt(OccupationRules.RecruitablePool(p));
            // 労働生産性（POPLAB-5・#2026）＝適所度#110×技能（標準0.5）×就業率→産出#93。
            float align = OccupationRules.AlignmentFactor(p);
            float prod = LaborProductivityRules.ProductivityFactor(align, 0.5f, empRate);
            return $"\n職業: 農{Pct(w, Occupation.農民)}/工{Pct(w, Occupation.工員)}/鉱{Pct(w, Occupation.鉱員)}" +
                   $"/官{Pct(w, Occupation.官吏)}/兵{Pct(w, Occupation.軍属)}/無職{Pct(w, Occupation.無職)}" +
                   $"\n就業率 {emp}%・徴募源 {rec}人（軍属）" +
                   FormatJsoc(w) +
                   $"\n労働生産性 {prod:F2}（適所度{Mathf.RoundToInt(align * 100f)}%）";
        }

        private static int Pct(Workforce w, Occupation o) => Mathf.RoundToInt(w.Share(o) * 100f);

        // JSOC 大分類（日本標準職業分類を参考・#110）：少数6種を標準分類へ写像して非ゼロ群を記号付きで表示。
        private static string FormatJsoc(Workforce w)
        {
            OccupationProfile prof = OccupationClassificationRules.Classify(w);
            var sb = new System.Text.StringBuilder("\nJSOC大分類: ");
            bool any = false;
            for (int i = 0; i < OccupationProfile.Count; i++)
            {
                var c = (OccupationCategory)i;
                int pct = Mathf.RoundToInt(prof.Share(c) * 100f);
                if (pct <= 0) continue;
                if (any) sb.Append('/');
                sb.Append(OccupationClassificationRules.JsocCode(c)).Append(c.ToString()).Append(pct);
                any = true;
            }
            if (!any) return "";
            // 中分類（JSOC 73群）＝最多POP職業の代表中分類を1件添える（参照辞書からの照会）。
            Occupation dom = DominantOccupation(w);
            int mid = JsocMiddleClassification.RepresentativeMiddle(dom);
            if (mid > 0)
                sb.Append("\n中分類: ").Append(JsocMiddleClassification.FormatCode(mid))
                  .Append(JsocMiddleClassification.Name(mid));
            // 小分類（JSOC 参考・curate）＝最多POP職業の代表小分類を添える。
            string minor = JsocMinorClassification.RepresentativeMinor(dom);
            if (!string.IsNullOrEmpty(minor))
                sb.Append("\n小分類: ").Append(minor).Append(JsocMinorClassification.Name(minor));
            return sb.ToString();
        }

        // 最多シェアの POP 職業（無職を除く）。中分類の代表照会に使う。
        private static Occupation DominantOccupation(Workforce w)
        {
            Occupation best = Occupation.農民;
            float bestShare = -1f;
            for (int i = 0; i < Workforce.Count; i++)
            {
                if (i == (int)Occupation.無職) continue;
                if (w.shares[i] > bestShare) { bestShare = w.shares[i]; best = (Occupation)i; }
            }
            return best;
        }

        // 人口動態（出生死亡・LIFE-3 #153）：年齢構成＋局面＋見込み成長率（安定度で出生/死亡が増減）。
        private static string FormatPlanetDemographics(Province p)
        {
            if (p.demographics == null) return "";
            Population pop = p.demographics;
            PopulationPhase phase = DemographicsRules.Phase(pop, DemographicsRules.DemographicsParams.Default);
            float growth = PopulationDynamicsRules.ProjectedAnnualGrowth(p, DemographicsRules.VitalRates.Default);
            string sign = growth >= 0f ? "+" : "";
            int attract = Mathf.RoundToInt(PopulationMigrationRules.Attractiveness(p) * 100f);
            int femalePct = Mathf.RoundToInt(pop.femaleShare * 100f);
            return $"\n人口動態: 年少 {Mathf.RoundToInt(pop.youth)} / 生産 {Mathf.RoundToInt(pop.working)} / 高齢 {Mathf.RoundToInt(pop.elderly)}" +
                   $"（{phase}・成長 {sign}{growth * 100f:0.#}%/年）" +
                   $"\n男女比: 男{100 - femalePct} : 女{femalePct}" +
                   $"\n定住魅力: {attract}%（高いほど移民が集まり、低いと流出）";
        }

        // 希少資源の鉱床（#178）。鉱床のある惑星だけ「希少資源: 名（豊富さ%・産出/秒）」を出す。
        private static string FormatPlanetStrategic(Province p)
        {
            if (!p.hasStrategicResource) return "";
            StrategicResourceInfo info = StrategicResourceRules.Info(p.strategicResource);
            float rate = StrategicResourceRules.ProvinceRate(p);
            return $"\n<希少資源> {info.displayName}（豊富さ {Mathf.RoundToInt(p.strategicAbundance * 100f)}%・産出/秒 {rate:0.##}）";
        }

        // 惑星の実効産出（類型×安定度比例）を「物資/弾薬/燃料」で表す（#93 を惑星層へ・0は省く）。
        private static string FormatPlanetResources(Province p)
        {
            float sup = ResourceProductionRules.ProvinceRate(p, ResourceType.物資);
            float amm = ResourceProductionRules.ProvinceRate(p, ResourceType.弾薬);
            float fue = ResourceProductionRules.ProvinceRate(p, ResourceType.燃料);
            var parts = new List<string>(3);
            if (sup > 0f) parts.Add($"物資 {sup:0.#}");
            if (amm > 0f) parts.Add($"弾薬 {amm:0.#}");
            if (fue > 0f) parts.Add($"燃料 {fue:0.#}");
            return parts.Count > 0 ? string.Join(" / ", parts) : "なし";
        }

        private void UpdateAggregate()
        {
            var provinces = new List<Province>(planets.Count);
            foreach (var e in planets) provinces.Add(e.province);
            SystemGovernance g = GovernanceRules.AggregateSystem(provinces);
            string unrest = g.anyUnrest ? "　▲反乱の火種あり" : "";

            // 星系の資源産出＝各惑星の実効産出を合算（#767 集約・惑星が産出の真実）
            float sup = 0f, amm = 0f, fue = 0f;
            foreach (var e in planets)
            {
                sup += ResourceProductionRules.ProvinceRate(e.province, ResourceType.物資);
                amm += ResourceProductionRules.ProvinceRate(e.province, ResourceType.弾薬);
                fue += ResourceProductionRules.ProvinceRate(e.province, ResourceType.燃料);
            }

            // 希少資源（#178・偏在）：星系内の鉱床の産出を種類別に合算
            var stratParts = new List<string>();
            foreach (var t in StrategicResourceRules.All)
            {
                float r = 0f;
                foreach (var e in planets)
                    if (e.province.hasStrategicResource && e.province.strategicResource == t)
                        r += StrategicResourceRules.ProvinceRate(e.province);
                if (r > 0f) stratParts.Add($"{StrategicResourceRules.Info(t).displayName} {r:0.##}");
            }
            string stratLine = stratParts.Count > 0
                ? "\n希少資源/秒　" + string.Join(" / ", stratParts)
                : "\n希少資源：なし（偏在＝この星系には鉱床なし）";

            aggregateLabel.text = $"星系全体（{g.planetCount}惑星の集約）" +
                $"　安定度 {Mathf.RoundToInt(g.weightedStability)}%" +
                $"　人口 {Mathf.RoundToInt(g.totalPopulation)}" +
                $"　支配思想 {g.dominantIdeology}{unrest}\n" +
                $"資源産出/秒　物資 {sup:0.#} / 弾薬 {amm:0.#} / 燃料 {fue:0.#}" +
                stratLine;
        }

        private void BuildOrbitRing(float radius, int index)
        {
            var go = new GameObject($"Orbit{index}");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = orbitMat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.widthMultiplier = 0.05f;
            lr.numCapVertices = 2;
            lr.startColor = lr.endColor = orbitColor;
            lr.sortingOrder = -45;
            const int seg = 72;
            lr.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = (Mathf.PI * 2f / seg) * i;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
        }

        // 第index惑星を軌道リング上に象徴配置し、その惑星単位の内政(Province)を生成する（#767）。
        private void BuildPlanet(float radius, int index)
        {
            // golden-angle 風に散らして惑星・ラベルの重なりを避ける（決定的）
            float angle = (90f - index * 137.5f) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

            var go = new GameObject($"Planet{index + 1}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * planetScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = discSprite; // 恒星と共有（OnDestroy で一括破棄）
            sr.color = (planetColors != null && planetColors.Length > 0)
                ? planetColors[index % planetColors.Length] : Color.white;
            sr.sortingOrder = -42;

            // 「第N惑星」ラベル（惑星のスケールに引きずられないよう root の子にする）
            var labelGo = new GameObject($"Planet{index + 1}Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = pos + new Vector3(0f, planetScale * 0.9f + 0.3f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = $"第{Ordinal(index + 1)}惑星";
            tm.font = FontProvider.JapaneseFont;
            tm.fontSize = 36; tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.85f, 0.9f, 1f);
            if (tm.font != null) labelGo.GetComponent<MeshRenderer>().material = tm.font.material;

            planets.Add(new PlanetEntry { index = index, pos = pos, province = GeneratePlanetProvince(index) });
        }

        // 惑星単位の内政(Province)を決定的に生成する（住民思想は所有勢力寄り＋一部異思想＝不満の種）。
        private Province GeneratePlanetProvince(int index)
        {
            int h = Mathf.Abs((systemId * 73856093) ^ ((index + 1) * 19349663));
            string ownerIdeo = ownerFaction == Faction.同盟 ? "民主" : "専制";
            string otherIdeo = ownerFaction == Faction.同盟 ? "専制" : "民主";
            string nat = (h % 3 == 0) ? otherIdeo : ownerIdeo; // 約1/3は異思想

            float pop = 80f + (h % 140);                 // 80..220
            var p = new Province(systemId, nat, pop);
            p.stability = 22f + (h % 68);                 // 22..90（一部は反乱域に近い）
            p.integration = 0.5f + ((h >> 3) % 6) / 10f;  // 0.5..1.0
            p.systemType = (SystemType)((h >> 5) % 4);    // 工業/農業/鉱業/居住＝惑星が産出する資源（#93 を惑星層へ）
            // 希少資源の鉱床（#178・偏在＝約1/3の惑星のみ・地理＝決定的）。大半は鉱床なし＝争奪の的が限られる。
            if (h % 3 == 1)
            {
                p.hasStrategicResource = true;
                p.strategicResource = (StrategicResourceType)((h >> 11) % 4);
                p.strategicAbundance = 0.4f + ((h >> 7) % 7) / 10f; // 0.4..1.0
            }
            // 年齢コホート（出生死亡の器・LIFE-3 #153）＝若い/老いた構成を決定的に振る（成長/衰退が惑星で違う）。
            float youthShare = 0.18f + ((h >> 9) % 18) / 100f;   // 0.18..0.35
            float elderShare = 0.08f + ((h >> 13) % 16) / 100f;  // 0.08..0.23
            float workShare = Mathf.Max(0.3f, 1f - youthShare - elderShare);
            p.demographics = new Population(pop * youthShare, pop * workShare, pop * elderShare);
            p.demographics.femaleShare = 0.48f + ((h >> 17) % 5) / 100f; // 男女比 0.48..0.52（ほぼ均衡・決定的）
            // 職業構成（生産年齢の就労先・#110 職業）＝惑星の類型でバイアス（工業惑星は工員が多い等）。
            p.workforce = OccupationRules.Default(p.systemType);
            return p;
        }

        /// <summary>ワールド空間ラベル(TextMesh)を生成する小ヘルパー。</summary>
        private TextMesh MakeLabel(string name, Vector3 localPos, int fontSize, float charSize,
            TextAnchor anchor, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = FontProvider.JapaneseFont;
            tm.fontSize = fontSize; tm.characterSize = charSize;
            tm.anchor = anchor; tm.alignment = TextAlignment.Left;
            tm.color = color;
            if (tm.font != null) go.GetComponent<MeshRenderer>().material = tm.font.material;
            return tm;
        }

        /// <summary>1〜9 を漢数字へ（第一惑星・第二惑星…）。範囲外は算用数字。</summary>
        private static string Ordinal(int n)
        {
            string[] k = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            return (n >= 1 && n <= 9) ? k[n] : n.ToString();
        }

        private void OnDestroy()
        {
            if (orbitMat != null) Destroy(orbitMat);
            // 恒星・惑星で共有しているディスクのテクスチャを破棄
            if (discSprite != null && discSprite.texture != null) Destroy(discSprite.texture);
        }

        /// <summary>中心が明るく外周へ減衰するラジアルグラデのディスク（恒星・惑星表現）。</summary>
        private static Sprite MakeDisc(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a; // 中心ほど明るく
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
