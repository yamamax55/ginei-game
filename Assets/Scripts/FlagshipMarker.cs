using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 旗艦（Squadron を持つ本体）の頭上に識別マーカー（金色のダイヤ型アイコン）を表示するクラス。
    /// 配下艦には付かないため、これが旗艦と配下艦の一目での見分けになる。
    ///
    /// 設計上の注意：
    /// - 旗艦 root のスケールは絶対に変えない（Squadron の陣形計算が TransformPoint を使うため、
    ///   root を拡大すると配下艦の陣形間隔が狂う）。識別はこの「マーカー子オブジェクト」で行う。
    /// - マーカーは陣営色では塗らない（陣営色だと艦に埋もれて目立たないため）。金色＋黒フチの固定色で、
    ///   どちらの陣営でもはっきり目立つ。FactionColor はこの名前("FlagshipMarker")を着色対象から除外する。
    /// - 常に画面に対して水平・艦の真上に表示（ビルボード）。旗艦が回頭しても向きが暴れない。
    /// - Squadron の配下艦自動収集はこの名前を除外する（配下艦扱いされないように）。
    /// </summary>
    [RequireComponent(typeof(Squadron))]
    public class FlagshipMarker : MonoBehaviour
    {
        [Header("マーカー設定")]
        [Tooltip("艦の中心からの高さ（ワールド単位、真上に表示）")]
        public float height = 0.9f;

        [Tooltip("マーカーの大きさ")]
        public float markerScale = 0.5f;

        [Tooltip("艦体・ラベルより手前に描画するための sorting order")]
        public int sortingOrder = 25;

        [Tooltip("マーカーの色（陣営に依存しない固定色。既定は金）")]
        public Color markerColor = new Color(1f, 0.85f, 0.15f);

        // ダイヤ型スプライト（金＋黒フチ）はアプリ寿命で1個だけ生成し、全旗艦で共有する（リーク防止）
        private static Sprite diamondSprite;

        private Transform markerTransform;

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
    }
}
