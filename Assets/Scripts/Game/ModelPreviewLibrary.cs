using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// Resources の3Dモデル（恒星・要塞）を<b>小さな絵</b>として使い回すための工房（#行き先プレビュー）。
    ///
    /// <b>方針</b>
    /// <list type="bullet">
    ///   <item>行ごとにカメラを置かない。<b>モデル1種につき1枚だけ</b> RenderTexture へ焼き、
    ///   同じモデルを使う行はその1枚を共有する（星系は名前でモデルが決まるので種類は少ない）。</item>
    ///   <item>焼くのは<b>要求された瞬間の1フレームだけ</b>（<see cref="Camera.Render"/> を手で1回呼ぶ）。
    ///   カメラは常時無効なので毎フレームの描画負荷は増えない。</item>
    ///   <item>撮影用の模型は盤面から遠く離れた場所（<see cref="RigOrigin"/>）に置き、
    ///   撮影カメラはその1点だけを写す＝戦略/戦術の描画に混ざらない。</item>
    ///   <item>モデルの選び方（星系名→FBX）は <see cref="StarModelRules"/>、見た目のマテリアルは
    ///   <see cref="StarMaterialFactory"/> ＝<b>盤面と同じ窓口</b>を通す。別の恒星や作り物の絵は出さない。</item>
    /// </list>
    ///
    /// 使い終わったら <see cref="Release"/>／破棄で RenderTexture と模型を解放する
    /// （RenderTexture は明示的に <c>Release()</c> しないと GPU に残る）。
    /// </summary>
    public class ModelPreviewLibrary : MonoBehaviour
    {
        /// <summary>撮影場所（盤面から十分に遠い＝カメラの可動範囲外）。</summary>
        public static readonly Vector3 RigOrigin = new Vector3(0f, -100000f, 0f);

        [Tooltip("焼き付ける絵の一辺（ピクセル）。行のアイコンなので小さくてよい")]
        public int textureSize = 96;

        [Tooltip("同時に持つ絵の上限（超えたら以後は焼かない＝無制限に増やさない）")]
        public int maxTextures = 32;

        private static ModelPreviewLibrary instance;

        private Camera rigCamera;
        private Transform rigRoot;
        private readonly Dictionary<string, RenderTexture> textures = new Dictionary<string, RenderTexture>();
        private readonly HashSet<string> failed = new HashSet<string>();   // 読めなかったパス（毎回試さない）

        /// <summary>共有の工房（無ければ作る）。シーンを跨がない＝戦略シーンの寿命に合わせる。</summary>
        public static ModelPreviewLibrary Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindAnyObjectByType<ModelPreviewLibrary>();
                if (instance == null)
                {
                    var go = new GameObject("ModelPreviewLibrary");
                    instance = go.AddComponent<ModelPreviewLibrary>();
                }
                return instance;
            }
        }

        /// <summary>その Resources パスのモデルの絵（焼いていなければ1回だけ焼く）。取れなければ null。</summary>
        public static Texture PreviewFor(string resourcePath)
            => string.IsNullOrEmpty(resourcePath) ? null : Instance.Get(resourcePath);

        /// <summary>星系名に対応する恒星の絵（盤面と同じ <see cref="StarModelRules"/> で選ぶ）。</summary>
        public static Texture StarPreviewForName(string systemName)
            => PreviewFor(StarModelRules.ResourcePathForName(systemName));

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private Texture Get(string resourcePath)
        {
            if (textures.TryGetValue(resourcePath, out RenderTexture cached) && cached != null) return cached;
            if (failed.Contains(resourcePath)) return null;
            if (textures.Count >= Mathf.Max(1, maxTextures)) return null;   // 上限＝際限なく焼かない

            RenderTexture rt = Bake(resourcePath);
            if (rt == null) { failed.Add(resourcePath); return null; }
            textures[resourcePath] = rt;
            return rt;
        }

        /// <summary>模型を1体だけ置いて1フレーム描き、その絵を返す（模型はすぐ捨てる）。</summary>
        private RenderTexture Bake(string resourcePath)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return null;   // 未納品のモデル＝プレビュー無しで運用（行は普通に出る）

            EnsureRig();
            if (rigCamera == null) return null;

            GameObject model = Instantiate(prefab, rigRoot, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(10f, 200f, 0f);   // 盤面の見え方に近い向き
            model.transform.localScale = Vector3.one;

            // 模型に付いてくるライト/カメラは撮影の邪魔になるので止める（盤面と同じ扱い）。
            var lights = model.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = false;
            var cams = model.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++) cams[i].enabled = false;

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            bool isStar = resourcePath.StartsWith(StarModelRules.ResourceFolder, System.StringComparison.Ordinal);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
                if (isStar) StarMaterialFactory.Apply(renderers[i]);   // 盤面と同じマテリアル＝同じ見た目
            }

            // 模型の実寸に合わせて画角を決める（FBX のインポート倍率に依らず必ず画面に収まる）。
            float radius = WorldRadius(renderers, model.transform.position);
            rigCamera.orthographicSize = Mathf.Max(0.01f, radius * 1.15f);

            var rt = new RenderTexture(Mathf.Max(16, textureSize), Mathf.Max(16, textureSize), 16,
                                       RenderTextureFormat.ARGB32)
            {
                name = "Preview_" + resourcePath,
                antiAliasing = 1,
            };
            rt.Create();

            rigCamera.targetTexture = rt;
            rigCamera.Render();          // ここだけ描く（常時レンダリングしない）
            rigCamera.targetTexture = null;

            Destroy(model);
            return rt;
        }

        private void EnsureRig()
        {
            if (rigCamera != null) return;

            var rootGo = new GameObject("PreviewRig");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.position = RigOrigin;
            rigRoot = rootGo.transform;

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(rigRoot, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            camGo.transform.localRotation = Quaternion.identity;

            rigCamera = camGo.AddComponent<Camera>();
            rigCamera.orthographic = true;
            rigCamera.orthographicSize = 1f;
            rigCamera.nearClipPlane = 0.01f;
            rigCamera.farClipPlane = 100f;
            rigCamera.clearFlags = CameraClearFlags.SolidColor;
            rigCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 透明＝行の背景に馴染む
            rigCamera.cullingMask = ~0;
            rigCamera.enabled = false;                                // 手で Render() するときだけ描く
            rigCamera.allowMSAA = false;
            rigCamera.allowHDR = false;
        }

        /// <summary>模型の外接半径（中心からの最遠点）。</summary>
        private static float WorldRadius(Renderer[] renderers, Vector3 center)
        {
            float r = 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Bounds b = renderers[i].bounds;
                r = Mathf.Max(r, Vector3.Distance(center, b.center) + b.extents.magnitude);
            }
            return r > 0f ? r : 1f;
        }

        /// <summary>焼いた絵をすべて解放する（RenderTexture は明示解放しないと GPU に残る）。</summary>
        public void Release()
        {
            foreach (var kv in textures)
            {
                if (kv.Value == null) continue;
                kv.Value.Release();
                Destroy(kv.Value);
            }
            textures.Clear();
            failed.Clear();
        }

        private void OnDestroy()
        {
            Release();
            if (instance == this) instance = null;
        }
    }
}
