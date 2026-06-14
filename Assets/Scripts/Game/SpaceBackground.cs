using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙空間の背景（星々）とパララックス効果を管理するクラス。
    /// </summary>
    public class SpaceBackground : MonoBehaviour
    {
        [Header("星の設定")]
        [Tooltip("星の密度（1ユニットあたりの星の数）")]
        public float starDensity = 0.3f;
        
        [Tooltip("星の最小サイズ")]
        public float minStarSize = 0.05f;
        
        [Tooltip("星の最大サイズ")]
        public float maxStarSize = 0.15f;

        [Tooltip("星の色のグラデーション")]
        public Gradient starColorGradient;

        [Header("パララックス設定")]
        [Tooltip("カメラ移動に対する追従倍率（小さいほど遠くに見える）")]
        public float parallaxFactor = 0.05f;

        private ParticleSystem starSystem;
        private Transform cameraTransform;
        private Vector3 lastCameraPosition;
        private Material starMaterial; // 実行時生成。OnDestroyで破棄

        private void Start()
        {
            // メインカメラが無ければ安全に何もしない（パララックスはカメラ前提）
            if (Camera.main == null) return;

            cameraTransform = Camera.main.transform;
            lastCameraPosition = cameraTransform.position;

            // カメラの背景色を深い宇宙色に設定
            Camera.main.backgroundColor = new Color(0.01f, 0.01f, 0.03f, 1f);
            Camera.main.clearFlags = CameraClearFlags.SolidColor;

            SetupParticleSystem();
            GenerateStars();
        }

        private void OnDestroy()
        {
            // 実行時生成したマテリアルを破棄（リーク防止）
            if (starMaterial != null) Destroy(starMaterial);
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;

            // カメラの移動量に応じて背景をずらす（パララックス）
            Vector3 cameraDelta = cameraTransform.position - lastCameraPosition;
            cameraDelta.z = 0; // 2DなのでZは動かさない
            transform.position += cameraDelta * parallaxFactor;
            
            lastCameraPosition = cameraTransform.position;
        }

        /// <summary>
        /// ParticleSystemの基本設定を行います。
        /// </summary>
        private void SetupParticleSystem()
        {
            starSystem = GetComponent<ParticleSystem>();
            if (starSystem == null) starSystem = gameObject.AddComponent<ParticleSystem>();

            var main = starSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0;
            main.startLifetime = 1000;
            main.maxParticles = 10000;
            // Localにすることで、LateUpdateのtransform移動が星に反映されパララックスが効く
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var noise = starSystem.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.4f;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = -100;

            // URP 2D互換マテリアルを作成。ビルドには URP 2D シェーダーが含まれない
            // ことがある（Always Included でない＝Shader.Find が null を返す）ため、
            // URP 安全な Sprites/Default へフォールバックして例外（背景星空の破綻）を防ぐ。
            Shader starShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (starShader == null) starShader = Shader.Find("Sprites/Default");
            if (starShader == null) starShader = Shader.Find("Unlit/Transparent");
            if (starShader == null)
            {
                Debug.LogWarning("[SpaceBackground] 星空用シェーダーが見つからないため背景生成を中止します。");
                return;
            }
            starMaterial = new Material(starShader);

            // 丸い星のテクスチャを生成して割り当てる
            starMaterial.mainTexture = CreateStarTexture();
            renderer.sharedMaterial = starMaterial;

            if (starColorGradient == null || starColorGradient.alphaKeys.Length == 0)
            {
                starColorGradient = new Gradient();
                starColorGradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0.7f, 0.8f, 1.0f), 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
                );
            }
        }

        /// <summary>
        /// ぼかした丸い点のテクスチャを生成します（四角い点を防ぐ）。
        /// </summary>
        private Texture2D CreateStarTexture()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size/2f, size/2f));
                    float alpha = Mathf.Clamp01(1.0f - (dist / (size/2.5f)));
                    alpha = Mathf.Pow(alpha, 2); // 中心を強く
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 広範囲に星をばら撒きます。
        /// </summary>
        public void GenerateStars()
        {
            float areaSize = 300f; 
            int starCount = Mathf.RoundToInt(areaSize * areaSize * starDensity);
            starCount = Mathf.Min(starCount, 10000);
            
            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[starCount];

            for (int i = 0; i < starCount; i++)
            {
                // カメラより十分後ろ(Z=15)に配置
                Vector3 pos = new Vector3(
                    Random.Range(-areaSize / 2f, areaSize / 2f),
                    Random.Range(-areaSize / 2f, areaSize / 2f),
                    15f 
                );

                particles[i].position = pos;
                particles[i].startSize = Random.Range(minStarSize, maxStarSize);
                particles[i].startColor = starColorGradient.Evaluate(Random.value);
                particles[i].remainingLifetime = 1000;
                particles[i].startLifetime = 1000;
            }

            starSystem.SetParticles(particles, starCount);
        }
    }
}

