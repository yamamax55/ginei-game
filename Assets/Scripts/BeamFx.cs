using System.Collections;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ビーム（艦の主砲）の見た目を一元化する静的ヘルパー。
    /// 「ただの単色直線」から、白熱コア→陣営色のグラデ・中央が太いテーパー幅・
    /// 放電フェード（アルファと幅がしぼむ）に底上げする＝エネルギービームの質感。
    /// 旗艦(FleetWeapon)と配下艦(EscortShip)の両方がここを使う（描画ロジックを重複させない）。
    /// マテリアルは呼び出し側が実行時生成し OnDestroy で破棄する（既存方針を踏襲）。
    /// </summary>
    public static class BeamFx
    {
        /// <summary>コア色を白へ寄せる割合（0=陣営色そのまま, 1=真っ白）。</summary>
        public const float CoreWhiteness = 0.7f;
        /// <summary>フェード終了時の幅倍率（放電がしぼむ表現）。</summary>
        public const float EndWidthScale = 0.35f;

        /// <summary>
        /// ビーム用マテリアルを生成する。Sprites/Default は URP/2D で安全（既存実装踏襲）。
        /// 色はグラデ(colorGradient)とフェード(material.color のアルファ)で制御するので白で初期化する。
        /// </summary>
        public static Material CreateMaterial()
        {
            return new Material(Shader.Find("Sprites/Default")) { color = Color.white };
        }

        /// <summary>LineRenderer をビーム見た目に整える（生成時に1回だけ呼ぶ）。</summary>
        public static void ConfigureLine(LineRenderer lr, float baseWidth)
        {
            lr.positionCount = 2;          // 焼き込みで0になっていると描画されないため明示
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 4;         // 丸いビーム端
            lr.numCornerVertices = 0;
            lr.sortingOrder = 20;          // 背景(-100)や艦より手前に描画
            lr.widthMultiplier = baseWidth;
            // 中央がふくらみ両端が細い＝ビームの芯（widthMultiplier がこのカーブをスケールする）
            lr.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0.4f));
            lr.enabled = false;
        }

        /// <summary>
        /// 白熱コア→陣営色→白熱コアのグラデを適用する。
        /// 色が変わった時だけ呼ぶこと（毎フレーム/毎ショット呼ぶと Gradient 生成で GC が増える）。
        /// アルファは1固定にし、フェードは material.color のアルファ側で乗算して表現する。
        /// </summary>
        public static void ApplyGradient(LineRenderer lr, Color beam)
        {
            Color core = Color.Lerp(beam, Color.white, CoreWhiteness);
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(core, 0f),
                    new GradientColorKey(beam, 0.5f),
                    new GradientColorKey(core, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            lr.colorGradient = grad;
        }

        /// <summary>
        /// ビームを発射→放電フェードで消すコルーチン。呼び出し側が StartCoroutine する。
        /// アルファ(material.color)と幅(widthMultiplier)を 1→0 にしぼめて「撃った直後にしぼむ」質感を出す。
        /// Time.deltaTime 基準＝timeScale 追従（ポーズで停止・倍速で加速）。
        /// </summary>
        public static IEnumerator Play(LineRenderer lr, Material mat, float baseWidth, float duration,
            Vector3 origin, Vector3 end)
        {
            if (lr == null) yield break;

            lr.SetPosition(0, origin);
            lr.SetPosition(1, end);
            lr.enabled = true;

            float dur = Mathf.Max(0.01f, duration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(1f - t / dur); // 1→0
                if (mat != null) mat.color = new Color(1f, 1f, 1f, k);
                lr.widthMultiplier = baseWidth * Mathf.Lerp(EndWidthScale, 1f, k);
                yield return null;
            }

            lr.enabled = false;
            if (mat != null) mat.color = Color.white; // 次回ショットのためにリセット
        }
    }
}
