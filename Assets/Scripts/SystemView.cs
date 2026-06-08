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

        [Header("軌道リング（惑星配置のプレースホルダ・後の宿題）")]
        [Tooltip("描く軌道リングの半径（恒星から外側へ。将来ここに第一惑星・第二惑星…を置く）")]
        public float[] orbitRadii = new float[] { 3.5f, 5.5f, 7.5f };
        public Color orbitColor = new Color(0.6f, 0.7f, 0.9f, 0.35f);

        public string systemName = "星系";

        private SpriteRenderer starRenderer;
        private Material orbitMat;

        /// <summary>恒星・星系名・軌道リングを生成する。</summary>
        public void Build()
        {
            transform.position = Vector3.zero;

            // 中心の恒星（ラジアルグラデのディスク）
            var starGo = new GameObject("Star");
            starGo.transform.SetParent(transform, false);
            starRenderer = starGo.AddComponent<SpriteRenderer>();
            starRenderer.sprite = MakeDisc(128);
            starRenderer.color = starColor;
            starRenderer.sortingOrder = -40;
            starGo.transform.localScale = Vector3.one * starScale;

            // 軌道リング（惑星を置く位置のヒント＝後の宿題のプレースホルダ）
            orbitMat = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            if (orbitRadii != null)
            {
                for (int i = 0; i < orbitRadii.Length; i++)
                    BuildOrbitRing(orbitRadii[i], i);
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
            hint.text = "システムビュー（恒星系）　Backspaceで戦略マップへ戻る\n惑星配置（第一惑星・第二惑星…）は実装予定";
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

        private void OnDestroy()
        {
            if (orbitMat != null) Destroy(orbitMat);
            if (starRenderer != null && starRenderer.sprite != null && starRenderer.sprite.texture != null)
                Destroy(starRenderer.sprite.texture);
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
