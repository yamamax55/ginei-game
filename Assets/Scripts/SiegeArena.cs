using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星攻城の戦術アリーナ（#131 PB-1/PB-5）。戦略マップで惑星に到着→突入したときに
    /// Battle シーンへ生成される。中心に惑星、その周囲に「アルテミスの首飾り射程＝接近限界リング」を描き、
    /// 旗艦がリング内（制空圏）へ入れないよう LateUpdate で押し出す（FleetMovement は変更しない＝BlackHole 方式）。
    /// 中心の惑星には制空権（<see cref="OrbitalDefense"/>）を攻撃対象として置き、攻城艦隊が砲撃で削る。
    /// 制空権が 0＝ドメイン・ダウンしたら接近限界（押し出し・リング）を解除し、惑星へ近づけるようにする。
    /// </summary>
    public class SiegeArena : MonoBehaviour
    {
        [Header("惑星攻城アリーナ")]
        [Tooltip("アルテミスの首飾り射程＝この半径内（制空圏）へ艦隊は侵入できない")]
        public float approachRadius = 5f;
        [Tooltip("中心の惑星の見た目スケール")]
        public float planetScale = 3f;
        public Color planetColor = new Color(0.55f, 0.6f, 0.7f);
        public Color ringColor = new Color(1f, 0.35f, 0.2f, 0.85f);
        public string planetLabel = "惑星";
        [Tooltip("制空権（軌道防衛＝S-AV）の最大耐久。攻城艦隊がこれを削り切るとドメイン・ダウン")]
        public float orbitalDefenseMax = 20000f;

        private Material ringMat;
        private Sprite disc;
        private OrbitalDefense orbital;
        private SpriteRenderer planetRenderer;
        private TextMesh defenseLabel;
        private LineRenderer ring;
        private bool domainDown;

        /// <summary>BattleSetup から生成直後に呼ぶ。値を設定してビジュアルを構築する。</summary>
        /// <param name="startRatio">開始時の制空権割合(0..1)。攻城途中で突入すれば既に削れた状態で始まる。</param>
        public void Configure(float approachRadius, float planetScale, Color planetColor, string label,
            Faction planetOwner, float orbitalDefenseMax, float startRatio = 1f)
        {
            this.approachRadius = Mathf.Max(0.5f, approachRadius);
            this.planetScale = Mathf.Max(0.1f, planetScale);
            this.planetColor = planetColor;
            this.planetLabel = label;
            this.orbitalDefenseMax = Mathf.Max(1f, orbitalDefenseMax);
            Build();

            // 制空権（攻撃対象）を中心に配置し、守備側勢力で登録。攻城艦隊が索敵して砲撃する。
            orbital.Initialize(planetOwner, this.orbitalDefenseMax, startRatio);
            orbital.OnDomainDown += HandleDomainDown;
        }

        private void Build()
        {
            disc = MakeDisc(64);

            // 中心の惑星（制空権＝攻撃対象を兼ねる）
            var p = new GameObject("Planet");
            p.transform.SetParent(transform, false);
            p.transform.localScale = Vector3.one * planetScale;
            planetRenderer = p.AddComponent<SpriteRenderer>();
            planetRenderer.sprite = disc; planetRenderer.color = planetColor; planetRenderer.sortingOrder = -20;
            orbital = p.AddComponent<OrbitalDefense>();

            // 惑星名ラベル
            var lblGo = new GameObject("PlanetLabel");
            lblGo.transform.SetParent(transform, false);
            lblGo.transform.localPosition = new Vector3(0f, planetScale * 0.65f, 0f);
            var tm = lblGo.AddComponent<TextMesh>();
            tm.text = planetLabel; tm.font = FontProvider.JapaneseFont; tm.fontSize = 48;
            tm.characterSize = 0.12f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var mr = lblGo.GetComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = 40;

            // 制空権の残量ラベル（惑星の下）
            var defGo = new GameObject("OrbitalDefenseLabel");
            defGo.transform.SetParent(transform, false);
            defGo.transform.localPosition = new Vector3(0f, -planetScale * 0.65f, 0f);
            defenseLabel = defGo.AddComponent<TextMesh>();
            defenseLabel.font = FontProvider.JapaneseFont; defenseLabel.fontSize = 40;
            defenseLabel.characterSize = 0.1f; defenseLabel.anchor = TextAnchor.MiddleCenter;
            defenseLabel.alignment = TextAlignment.Center;
            var dmr = defGo.GetComponent<MeshRenderer>();
            if (defenseLabel.font != null) dmr.sharedMaterial = defenseLabel.font.material;
            dmr.sortingOrder = 40;

            // アルテミスの首飾り射程＝接近限界リング
            ringMat = new Material(Shader.Find("Sprites/Default"));
            var ringGo = new GameObject("ApproachLimitRing");
            ringGo.transform.SetParent(transform, false);
            ring = ringGo.AddComponent<LineRenderer>();
            ring.material = ringMat; ring.useWorldSpace = false; ring.loop = true;
            ring.widthMultiplier = 0.15f; ring.numCapVertices = 2;
            ring.startColor = ring.endColor = ringColor;
            const int seg = 72;
            ring.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = (Mathf.PI * 2f / seg) * i;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * approachRadius);
            }
        }

        private void Update()
        {
            // 制空権の残量を表示（ドメイン健在中のみ）。timeScale 非依存の単純表示。
            if (defenseLabel == null || orbital == null) return;
            if (domainDown) return;
            int pct = Mathf.CeilToInt(orbital.Ratio * 100f);
            defenseLabel.text = $"制空権 {pct}%";
            // 残量に応じて緑→赤へ。リングも同色で連動。
            Color c = Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(0.4f, 1f, 0.5f), orbital.Ratio);
            defenseLabel.color = c;
            if (ring != null) ring.startColor = ring.endColor = new Color(c.r, c.g, c.b, ringColor.a);
        }

        /// <summary>制空権が 0 になった瞬間に呼ばれる：接近限界を解除して惑星へ近づけるようにする。</summary>
        private void HandleDomainDown()
        {
            domainDown = true;
            if (ring != null) ring.enabled = false;                 // 接近限界リングを消す
            if (defenseLabel != null) { defenseLabel.text = "ドメイン・ダウン"; defenseLabel.color = Color.white; }
            if (planetRenderer != null) planetRenderer.color = planetColor * 0.6f; // 制空権喪失＝惑星を暗転
            var hud = FindFirstObjectByType<FleetHUDManager>();
            if (hud != null) hud.ShowMessage("ドメイン・ダウン：制空権を制圧。惑星へ接近可能", 5f);
        }

        private void LateUpdate()
        {
            // ドメイン・ダウン後は接近限界を解除（押し出さない）。
            if (domainDown) return;

            // 接近限界：旗艦が制空圏（首飾り射程）内へ入っていたらリング上へ押し戻す（配下艦は旗艦に追従）。
            Vector2 center = transform.position;
            var flags = FleetRegistry.AllFlagships;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength fs = flags[i];
                if (fs == null) continue;
                Vector3 pos = fs.transform.position;
                Vector2 d = (Vector2)pos - center;
                float dist = d.magnitude;
                if (dist < approachRadius && dist > 0.0001f)
                {
                    Vector2 clamped = center + d / dist * approachRadius;
                    fs.transform.position = new Vector3(clamped.x, clamped.y, pos.z);
                }
            }
        }

        private void OnDestroy()
        {
            if (ringMat != null) Destroy(ringMat);
            if (disc != null && disc.texture != null) Destroy(disc.texture);
        }

        private static Sprite MakeDisc(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            var cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dd = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    cols[y * size + x] = (dd <= r - 1f) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(cols);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
