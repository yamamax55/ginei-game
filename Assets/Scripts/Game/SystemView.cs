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
                   $"産出: {output}%";
        }

        private void UpdateAggregate()
        {
            var provinces = new List<Province>(planets.Count);
            foreach (var e in planets) provinces.Add(e.province);
            SystemGovernance g = GovernanceRules.AggregateSystem(provinces);
            string unrest = g.anyUnrest ? "　▲反乱の火種あり" : "";
            aggregateLabel.text = $"星系全体（{g.planetCount}惑星の集約）" +
                $"　安定度 {Mathf.RoundToInt(g.weightedStability)}%" +
                $"　人口 {Mathf.RoundToInt(g.totalPopulation)}" +
                $"　支配思想 {g.dominantIdeology}{unrest}";
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
