using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星攻城の戦術アリーナ（#131 PB-1/PB-3/PB-5・Battle シーン）。戦略マップで惑星に突入したとき
    /// BattleSetup が生成する。中心に惑星＋「アルテミスの首飾り射程＝接近限界リング」を描き、
    /// 旗艦がリング内（制空圏）へ入れないよう押し出す（FleetMovement 非改変＝BlackHole 方式）。
    /// 包囲した攻城艦隊から S-AV が発進し（リングを越えて惑星へ）、制空権を制圧→侵略値を蓄積する
    /// （PlanetSiegeRules を回す）。進捗はゲージ表示。Backspace 復帰時に戦略へ書き戻す。
    /// </summary>
    public class SiegeArena : MonoBehaviour
    {
        [Header("アリーナ")]
        public float approachRadius = 5f;     // アルテミスの首飾り射程＝接近限界
        public float planetScale = 3f;
        public Color planetColor = new Color(0.55f, 0.6f, 0.7f);
        public Color ringColor = new Color(1f, 0.35f, 0.2f, 0.85f);
        public string planetLabel = "惑星";

        [Header("攻城（戦術側の進行）")]
        public Faction besiegerFaction = Faction.同盟;
        public Faction planetOwner = Faction.帝国;
        [Tooltip("突入時の制空権残り割合(0..1)。戦略の惑星から引き継ぐ")]
        public float initialDefenseRatio = 1f;
        [Tooltip("突入時の侵略値割合(0..1)。戦略の惑星から引き継ぐ")]
        public float initialInvasionRatio = 0f;
        public float siegeMaxDefense = 100f;
        public float siegeInvasionThreshold = 100f;
        [Tooltip("攻城旗艦1隊・1秒あたりの S-AV 戦力（制圧/侵攻の速さ）")]
        public float siegeSpeedPerFleet = 2.5f;

        [Header("S-AV 演出")]
        public int savCraftCount = 18;
        public float savCraftSpeed = 7f;
        public Color savColor = new Color(0.7f, 0.9f, 1f);

        private Planet planet;
        private Material ringMat, lineMat;
        private Sprite disc, whiteLeft;
        private Transform defenseFill, invadeFill;
        private float barWidth = 4f, barHeight = 0.3f;
        private TextMesh statusLabel;
        private bool captured;

        // S-AV クラフト（発進→惑星→再発進のループ）
        private Transform[] craft;
        private Vector2[] craftFrom;
        private float[] craftT;

        /// <summary>戦術側の現在の制空権残り割合(0..1)。</summary>
        public float DefenseRatio => planet != null && siegeMaxDefense > 0f ? planet.orbitalDefense / siegeMaxDefense : 0f;
        /// <summary>戦術側の現在の侵略値割合(0..1)。</summary>
        public float InvasionRatio => planet != null && siegeInvasionThreshold > 0f ? planet.invasionProgress / siegeInvasionThreshold : 0f;
        public bool Captured => captured;

        /// <summary>BattleSetup が値を設定した後に呼ぶ。ビジュアルと攻城状態を構築する。</summary>
        public void Build()
        {
            disc = MakeDisc(64);
            whiteLeft = MakeWhite();
            lineMat = new Material(Shader.Find("Sprites/Default"));
            ringMat = new Material(Shader.Find("Sprites/Default"));

            // 攻城状態（戦略の惑星から制空権・侵略値を引き継ぐ）
            planet = new Planet(0, planetOwner, siegeMaxDefense, siegeInvasionThreshold);
            planet.orbitalDefense = Mathf.Clamp01(initialDefenseRatio) * siegeMaxDefense;
            planet.invasionProgress = Mathf.Clamp01(initialInvasionRatio) * siegeInvasionThreshold;

            BuildPlanet();
            BuildRing();
            BuildGauges();
            BuildCraft();
        }

        private void BuildPlanet()
        {
            var p = new GameObject("Planet");
            p.transform.SetParent(transform, false);
            p.transform.localScale = Vector3.one * planetScale;
            var sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = disc; sr.color = planetColor; sr.sortingOrder = -20;

            var lblGo = new GameObject("PlanetLabel");
            lblGo.transform.SetParent(transform, false);
            lblGo.transform.localPosition = new Vector3(0f, planetScale * 0.7f, 0f);
            var tm = lblGo.AddComponent<TextMesh>();
            tm.text = planetLabel; tm.font = FontProvider.JapaneseFont; tm.fontSize = 48;
            tm.characterSize = 0.12f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var mr = lblGo.GetComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = 40;
        }

        private void BuildRing()
        {
            var ringGo = new GameObject("ApproachLimitRing");
            ringGo.transform.SetParent(transform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
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

        private void BuildGauges()
        {
            float baseY = -planetScale * 0.95f;
            // 制空権バー（橙・減る）
            defenseFill = MakeBar("DefenseBar", new Vector3(-barWidth * 0.5f, baseY, 0f), new Color(0.9f, 0.55f, 0.25f));
            // 占領（侵略）バー（赤・増える）
            invadeFill = MakeBar("InvadeBar", new Vector3(-barWidth * 0.5f, baseY - barHeight * 1.6f, 0f), new Color(0.95f, 0.3f, 0.3f));

            var lblGo = new GameObject("SiegeStatus");
            lblGo.transform.SetParent(transform, false);
            lblGo.transform.localPosition = new Vector3(0f, baseY - barHeight * 3.4f, 0f);
            statusLabel = lblGo.AddComponent<TextMesh>();
            statusLabel.font = FontProvider.JapaneseFont; statusLabel.fontSize = 40;
            statusLabel.characterSize = 0.1f; statusLabel.anchor = TextAnchor.MiddleCenter;
            statusLabel.alignment = TextAlignment.Center; statusLabel.color = Color.white;
            var mr = lblGo.GetComponent<MeshRenderer>();
            if (statusLabel.font != null) mr.sharedMaterial = statusLabel.font.material;
            mr.sortingOrder = 40;
        }

        // 左端ピボットの白スプライトで、X スケール＝割合のバーを作る。背景＋塗りを重ねて塗りの Transform を返す。
        private Transform MakeBar(string name, Vector3 leftPos, Color fillColor)
        {
            var bg = new GameObject(name + "_bg");
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = leftPos;
            bg.transform.localScale = new Vector3(barWidth, barHeight, 1f);
            var bgsr = bg.AddComponent<SpriteRenderer>();
            bgsr.sprite = whiteLeft; bgsr.color = new Color(0f, 0f, 0f, 0.55f); bgsr.sortingOrder = 30;

            var fl = new GameObject(name + "_fill");
            fl.transform.SetParent(transform, false);
            fl.transform.localPosition = leftPos;
            fl.transform.localScale = new Vector3(barWidth, barHeight, 1f);
            var flsr = fl.AddComponent<SpriteRenderer>();
            flsr.sprite = whiteLeft; flsr.color = fillColor; flsr.sortingOrder = 31;
            return fl.transform;
        }

        private void BuildCraft()
        {
            int n = Mathf.Max(0, savCraftCount);
            craft = new Transform[n];
            craftFrom = new Vector2[n];
            craftT = new float[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("S-AV");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.25f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = disc; sr.color = savColor; sr.sortingOrder = 10;
                go.SetActive(false);
                craft[i] = go.transform;
                craftT[i] = Random.value; // 位相をばらす
            }
        }

        private void Update()
        {
            if (planet == null) return;

            int alive = CountBesiegers();

            // 攻城進行：在席の攻城旗艦数 × 速さ を S-AV戦力として制圧→侵略（timeScale 追従）
            if (alive > 0 && !captured && Time.deltaTime > 0f)
            {
                float sav = alive * siegeSpeedPerFleet;
                var r = PlanetSiegeRules.Tick(planet, besiegerFaction, sav, Time.deltaTime, new SiegeParams(1f, 1f, 0f));
                if (r.captured) captured = true;
            }

            UpdateGauges();
            UpdateCraft(alive);
        }

        private int CountBesiegers()
        {
            int n = 0;
            var flags = FleetRegistry.AllFlagships;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength fs = flags[i];
                if (fs != null && fs.IsAlive && fs.faction == besiegerFaction) n++;
            }
            return n;
        }

        private void UpdateGauges()
        {
            if (defenseFill != null)
                defenseFill.localScale = new Vector3(barWidth * Mathf.Clamp01(DefenseRatio), barHeight, 1f);
            if (invadeFill != null)
                invadeFill.localScale = new Vector3(barWidth * Mathf.Clamp01(InvasionRatio), barHeight, 1f);

            if (statusLabel != null)
            {
                if (captured)
                    statusLabel.text = "占領完了！　Backspaceで戦略マップへ";
                else if (!planet.DomainDown)
                    statusLabel.text = $"制空権を制圧中 {Mathf.CeilToInt(DefenseRatio * 100f)}%　S-AV突入中";
                else
                    statusLabel.text = $"侵攻中（占領 {Mathf.FloorToInt(InvasionRatio * 100f)}%）";
            }
        }

        // S-AV を攻城旗艦から発進させ、惑星へ突入させて再発進（攻城中のみ表示）。
        private void UpdateCraft(int alive)
        {
            if (craft == null) return;
            bool active = alive > 0 && !captured;
            Vector2 center = transform.position;

            for (int i = 0; i < craft.Length; i++)
            {
                Transform c = craft[i];
                if (c == null) continue;
                if (!active) { if (c.gameObject.activeSelf) c.gameObject.SetActive(false); continue; }
                if (!c.gameObject.activeSelf)
                {
                    c.gameObject.SetActive(true);
                    craftFrom[i] = RandomBesiegerPos(center);
                    craftT[i] = 0f;
                }

                Vector2 target = center; // 惑星へ突入
                float dist = Mathf.Max(0.5f, Vector2.Distance(craftFrom[i], target));
                craftT[i] += savCraftSpeed * Time.deltaTime / dist;
                if (craftT[i] >= 1f)
                {
                    craftFrom[i] = RandomBesiegerPos(center);
                    craftT[i] = 0f;
                }
                Vector2 pos = Vector2.Lerp(craftFrom[i], target, craftT[i]);
                c.position = new Vector3(pos.x, pos.y, 0f);
            }
        }

        private Vector2 RandomBesiegerPos(Vector2 fallback)
        {
            var flags = FleetRegistry.AllFlagships;
            int count = 0;
            for (int i = 0; i < flags.Count; i++)
                if (flags[i] != null && flags[i].IsAlive && flags[i].faction == besiegerFaction) count++;
            if (count == 0) return (Vector2)transform.position + new Vector2(approachRadius + 2f, 0f);
            int pick = Random.Range(0, count);
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength fs = flags[i];
                if (fs == null || !fs.IsAlive || fs.faction != besiegerFaction) continue;
                if (pick-- == 0) return fs.transform.position;
            }
            return fallback;
        }

        private void LateUpdate()
        {
            // 接近限界：制空圏（首飾り射程）内へ入った艦（旗艦＋配下艦）をリング上へ押し戻す。
            // 旗艦だけ止めると周囲に展開する配下艦が内側へはみ出すため、全個艦を対象にする。
            // S-AVクラフトはレジストリ非登録なので影響を受けない（突入演出としてリング内を飛ぶ）。
            Vector2 center = transform.position;
            var targets = FleetRegistry.AllTargets;
            for (int i = 0; i < targets.Count; i++)
            {
                IShipTarget t = targets[i];
                if (t == null) continue;
                Transform tr = t.Transform;
                if (tr == null) continue;
                Vector3 pos = tr.position;
                Vector2 d = (Vector2)pos - center;
                float dist = d.magnitude;
                if (dist < approachRadius && dist > 0.0001f)
                {
                    Vector2 clamped = center + d / dist * approachRadius;
                    tr.position = new Vector3(clamped.x, clamped.y, pos.z);
                }
            }
        }

        private void OnDestroy()
        {
            if (ringMat != null) Destroy(ringMat);
            if (lineMat != null) Destroy(lineMat);
            if (disc != null && disc.texture != null) Destroy(disc.texture);
            if (whiteLeft != null && whiteLeft.texture != null) Destroy(whiteLeft.texture);
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

        // 左端(0,0.5)ピボットの 1x1 白スプライト。X スケールで左から伸びるバーになる。
        private static Sprite MakeWhite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var cols = new Color32[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0f, 0.5f), 2f);
        }
    }
}
