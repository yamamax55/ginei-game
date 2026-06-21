using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// <see cref="Juice"/> のコルーチン宿主（自動生成・シーンを跨いで常駐・非表示）。
    /// 手置き不要。`Juice` の各APIが必要時に1つだけ生成する（重複させない）。
    /// </summary>
    public sealed class JuiceRunner : MonoBehaviour
    {
        private static JuiceRunner instance;

        public static JuiceRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("JuiceRunner") { hideFlags = HideFlags.HideInHierarchy };
                    instance = go.AddComponent<JuiceRunner>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }
    }
}
