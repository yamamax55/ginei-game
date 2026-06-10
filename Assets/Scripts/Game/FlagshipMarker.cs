using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 旗艦（Squadron を持つ本体）の頭上に識別マーカーを表示するクラス。
    /// 配下艦には付かないため、これが旗艦と配下艦の一目での見分けになる。
    ///
    /// 構成：金色のダイヤ型アイコン（"FlagshipMarker"）＋ その背後の発光ハロー（"FlagshipMarkerGlow"）。
    /// - 金ダイヤ＝「旗艦」の固定マーク（陣営非依存。艦に埋もれず一目で分かる）。
    /// - 発光ハロー＝陣営色（帝国=赤／同盟=青）で塗り、旗艦の所属を示す＆マーカーを目立たせる。
    ///   ハローの色付けは陣営色の正規担当 FactionColor に一任する（陣営色をここでハードコードしない）。
    ///
    /// 設計上の注意：
    /// - 旗艦 root のスケールは絶対に変えない（Squadron の陣形計算が TransformPoint を使うため、
    ///   root を拡大すると配下艦の陣形間隔が狂う）。識別はこの「マーカー子オブジェクト」で行う。
    /// - FactionColor は "FlagshipMarker"(金固定) を着色対象から除外し、"FlagshipMarkerGlow" のみ陣営色で塗る。
    /// - 常に画面に対して水平・艦の真上に表示（ビルボード）。旗艦が回頭しても向きが暴れない。
    /// - Squadron の配下艦自動収集はこのマーカー名を除外する（配下艦扱いされないように）。
    /// </summary>
    [RequireComponent(typeof(Squadron))]
    public class FlagshipMarker : MonoBehaviour
    {
        [Header("マーカー設定")]
        [Tooltip("艦の中心からの高さ（ワールド単位、真上に表示）")]
        public float height = 0.9f;

        [Tooltip("マーカー（金ダイヤ）の大きさ")]
        public float markerScale = 0.55f;

        [Tooltip("艦体・ラベル・ビームより手前に描画するための sorting order")]
        public int sortingOrder = 30;

        [Tooltip("マーカーの色（陣営に依存しない固定色。既定は金）")]
        public Color markerColor = new Color(1f, 0.85f, 0.15f);

        [Header("発光ハロー設定")]
        [Tooltip("ハローの大きさ（ダイヤに対する倍率）")]
        public float glowScale = 2.0f;

        [Tooltip("発光パルスの速さ")]
        public float pulseSpeed = 3f;

        [Tooltip("発光パルスの強さ（大きさの揺れ幅）")]
        public float pulseAmount = 0.15f;

        [Tooltip("FactionColor が未設定の艦で使うハローの既定色")]
        public Color defaultGlowColor = new Color(1f, 0.85f, 0.15f, 0.5f);

        // スプライトはアプリ寿命で1個だけ生成し、全旗艦で共有する（リーク防止）
        private static Sprite diamondSprite;
        private static Sprite glowSprite;

        private Transform markerTransform;
        private Transform glowTransform;

        private void Awake()
        {
            // Instantiate 直後（BattleSetup が ApplyColors を呼ぶ前）にマーカーを用意するため Awake で生成。
            CreateMarker();
        }

        private void LateUpdate()
        {
            if (markerTransform == null) return;
            // 常に艦の真上・画面水平に固定（回頭に追従して暴れないようにビルボード）
            markerTransform.position = transform.position + Vector3.up * height;
            markerTransform.rotation = Quaternion.identity;

            // 発光ハローをゆっくり脈動させて旗艦を目立たせる（ポーズ中も動くよう unscaled）
            if (glowTransform != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
                glowTransform.localScale = Vector3.one * (glowScale * pulse);
            }
        }

        private void CreateMarker()
        {
            // プレハブに焼き込まれた既存 "FlagshipMarker" があれば再利用（二重生成を防ぐ）
            Transform existing = transform.Find("FlagshipMarker");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("FlagshipMarker");
                go.transform.SetParent(transform);
            }

            go.transform.localScale = Vector3.one * markerScale;

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDiamondSprite();
            sr.sortingOrder = sortingOrder;
            sr.color = markerColor; // 陣営色ではなく固定色。FactionColor は対象外。

            markerTransform = go.transform;

            // 発光ハロー（ダイヤの背後・陣営色）。マーカー子として持ち、ビルボードに追従させる。
            glowTransform = CreateGlow(go.transform);
        }

        /// <summary>発光ハローの子オブジェクトを用意して返す（陣営色は FactionColor が塗る）。</summary>
        private Transform CreateGlow(Transform parent)
        {
            Transform existingGlow = parent.Find("FlagshipMarkerGlow");
            GameObject glow;
            if (existingGlow != null)
            {
                glow = existingGlow.gameObject;
            }
            else
            {
                glow = new GameObject("FlagshipMarkerGlow");
                glow.transform.SetParent(parent);
            }

            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = Vector3.one * glowScale;

            SpriteRenderer gsr = glow.GetComponent<SpriteRenderer>();
            if (gsr == null) gsr = glow.AddComponent<SpriteRenderer>();
            gsr.sprite = GetGlowSprite();
            gsr.sortingOrder = sortingOrder - 1; // ダイヤの背後
            // 既定色（FactionColor があれば後で陣営色に上書きされる）
            gsr.color = defaultGlowColor;

            return glow.transform;
        }

        /// <summary>
        /// ダイヤ型（菱形）のスプライトを生成して返す。生成は1回だけで以降は共有。
        /// 中心は白（SpriteRenderer.color で色付け）、外周は黒フチにして明るい背景でも視認できるようにする。
        /// </summary>
        private static Sprite GetDiamondSprite()
        {
            if (diamondSprite != null) return diamondSprite;

            const int size = 48;
            const float center = (size - 1) / 2f;
            const float radius = size / 2f;
            const float border = 4f; // 黒フチの太さ(px)

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color edge = new Color(0f, 0f, 0f, 1f); // 黒フチ

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // マンハッタン距離で菱形（ダイヤ型）を描く
                    float d = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    if (d > radius)
                    {
                        tex.SetPixel(x, y, clear);
                    }
                    else if (d > radius - border)
                    {
                        tex.SetPixel(x, y, edge);            // 外周＝黒フチ
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.white);     // 内側＝白（色は SpriteRenderer.color で付ける）
                    }
                }
            }
            tex.Apply();

            diamondSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return diamondSprite;
        }

        /// <summary>
        /// 円形の発光ハロー用スプライトを生成して返す（中心が濃く外周へ向けて透明になる放射グラデ）。
        /// 色は SpriteRenderer.color（陣営色）で付ける。生成は1回だけで以降は共有。
        /// </summary>
        private static Sprite GetGlowSprite()
        {
            if (glowSprite != null) return glowSprite;

            const int size = 64;
            const float center = (size - 1) / 2f;
            const float radius = size / 2f;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float t = Mathf.Clamp01(dist / radius);
                    // 中心1→外周0 のなめらかな減衰（端をふんわり）。二乗で柔らかい光に。
                    float alpha = (1f - t);
                    alpha *= alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return glowSprite;
        }
    }
}
