using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 星系の戦術ビュー（非戦闘）。戦略マップで星系をダブルクリックしたとき、戦闘中でなくても
    /// 戦術マップ(Battle シーン)へ入って恒星系を閲覧するための最小ビュー。`BattleSetup.SetupSystemView` が生成する。
    ///
    /// 現状は「恒星を中心」に星系名と軌道リング（プレースホルダ）を描くだけ。
    /// ★後の宿題（方針確定＝#767）：恒星を中心に第一惑星・第二惑星…を**象徴配置**（実スケールは採らない）し、
    /// 惑星をクリックして**惑星単位の内政**を操作できるようにする（システムビュー＝内政メイン）。実装は本クラスへ追加する。
    /// 軌道リングは惑星を置く位置のヒント（現状は惑星オブジェクトは未配置）。
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
        [Tooltip("惑星の色パレット（軌道順に巡回）")]
        public Color[] planetColors = new Color[]
        {
            new Color(0.55f, 0.75f, 1f),   // 青
            new Color(0.85f, 0.6f, 0.45f), // 赤茶
            new Color(0.6f, 0.85f, 0.65f), // 緑
            new Color(0.85f, 0.8f, 0.55f), // 黄土
        };

        public string systemName = "星系";

        private SpriteRenderer starRenderer;
        private Sprite discSprite;   // 恒星・惑星で共有するディスク（OnDestroy で破棄）
        private Material orbitMat;

        /// <summary>恒星・星系名・軌道リングを生成する。</summary>
        public void Build()
        {
            transform.position = Vector3.zero;

            // 恒星・惑星で共有するディスクスプライト
            discSprite = MakeDisc(128);

            // 中心の恒星（ラジアルグラデのディスク）
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

            // 星系名ラベル
            var labelGo = new GameObject("SystemViewLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, starScale + 1.2f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = systemName;
            tm.font = FontProvider.JapaneseFont;
            tm.fontSize = 48; tm.characterSize = 0.12f;
            tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            if (tm.font != null) labelGo.GetComponent<MeshRenderer>().material = tm.font.material;

            // 操作ヒント（恒星系の閲覧＝非戦闘。惑星配置は未実装）
            var hintGo = new GameObject("SystemViewHint");
            hintGo.transform.SetParent(transform, false);
            hintGo.transform.localPosition = new Vector3(0f, -(starScale + 2.0f), 0f);
            var hint = hintGo.AddComponent<TextMesh>();
            hint.text = "システムビュー（恒星系）　Backspaceで戦略マップへ戻る\n惑星単位の内政は実装予定";
            hint.font = FontProvider.JapaneseFont;
            hint.fontSize = 36; hint.characterSize = 0.08f;
            hint.anchor = TextAnchor.UpperCenter; hint.alignment = TextAlignment.Center;
            hint.color = new Color(0.8f, 0.85f, 1f);
            if (hint.font != null) hintGo.GetComponent<MeshRenderer>().material = hint.font.material;
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

        // 第index惑星を軌道リング上に象徴配置する（実スケールではない・#767）。
        // ★後の宿題（#767）：惑星をクリックして惑星単位の内政を開く操作をここに追加する。
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

        /// <summary>中心が明るく外周へ減衰するラジアルグラデのディスク（恒星表現）。</summary>
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
