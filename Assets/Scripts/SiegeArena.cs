using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星攻城の戦術アリーナ（#131 PB-1/PB-5）。戦略マップで惑星に到着→突入したときに
    /// Battle シーンへ生成される。中心に惑星、その周囲に「アルテミスの首飾り射程＝接近限界リング」を描き、
    /// 旗艦がリング内（制空圏）へ入れないよう LateUpdate で押し出す（FleetMovement は変更しない＝BlackHole 方式）。
    /// 艦隊は首飾り射程の外までしか近づけない、を物理的に担保する。
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

        private Material ringMat;
        private Sprite disc;

        /// <summary>BattleSetup から生成直後に呼ぶ。値を設定してビジュアルを構築する。</summary>
        public void Configure(float approachRadius, float planetScale, Color planetColor, string label)
        {
            this.approachRadius = Mathf.Max(0.5f, approachRadius);
            this.planetScale = Mathf.Max(0.1f, planetScale);
            this.planetColor = planetColor;
            this.planetLabel = label;
            Build();
        }

        private void Build()
        {
            disc = MakeDisc(64);

            // 中心の惑星
            var p = new GameObject("Planet");
            p.transform.SetParent(transform, false);
            p.transform.localScale = Vector3.one * planetScale;
            var sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = disc; sr.color = planetColor; sr.sortingOrder = -20;

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

            // アルテミスの首飾り射程＝接近限界リング
            ringMat = new Material(Shader.Find("Sprites/Default"));
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

        private void LateUpdate()
        {
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
