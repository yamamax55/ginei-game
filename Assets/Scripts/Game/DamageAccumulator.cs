using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 短い時間窓のあいだ、同じ標的へのダメージを合算して1つの DamagePopup にまとめる間引き役。
    /// 多数の配下艦が同時に撃つとポップアップが氾濫するため、一定窓（window）ぶん溜めて1発の合算数値で表示する。
    /// Battle シーンに自動生成され（手置き不要）、シーンを跨がない（会戦ごとにリセット）。
    /// ダメージ計算（TakeDamage 等）には一切触れない＝表示の間引きのみ。
    /// </summary>
    public class DamageAccumulator : MonoBehaviour
    {
        [Header("合算設定")]
        [Tooltip("同一標的へのダメージを合算する時間窓（秒）。経過後にまとめて1発のポップアップを出す")]
        public float window = 0.35f;

        /// <summary>1標的ぶんの溜め込み状態（合算ダメージ・最終位置・側背面OR・締切時刻）。</summary>
        private struct Pending
        {
            public int sumDamage;
            public Vector3 worldPos; // 最新の被弾位置
            public bool isFlank;     // 窓内に1発でも側背面があれば true（OR）
            public float dueTime;    // この時刻を過ぎたら吐き出す（Time.time 基準＝timeScale 追従）
            public Transform target; // null/破棄判定用に保持
        }

        // 標的の InstanceID をキーに溜め込む（Transform を直接キーにすると破棄後の参照が残るため ID 経由）。
        private readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
        // Update 中に辞書を変更しないための吐き出し対象バッファ（再利用してGC節約）。
        private readonly List<int> dueKeys = new List<int>();

        private static DamageAccumulator instance;

        /// <summary>
        /// 標的へのダメージを合算へ積む。同一標的・同一窓内なら加算し、新規なら締切を window 後に設定する。
        /// インスタンスが無い（Battle シーン外）場合は従来どおり即時に1発表示へフォールバックする。
        /// </summary>
        public static void Add(Transform target, int damage, bool isFlank, Vector3 worldPos)
        {
            if (target == null)
            {
                // 標的が無ければ合算できない＝そのまま表示（位置だけは有効）。
                DamagePopup.Show(worldPos, damage, isFlank);
                return;
            }
            if (instance == null)
            {
                // アキュムレータ未生成（Battle 以外など）＝従来動作（即時表示）。
                DamagePopup.Show(worldPos, damage, isFlank);
                return;
            }

            int key = target.GetInstanceID();
            if (instance.pending.TryGetValue(key, out Pending p))
            {
                p.sumDamage += damage;
                p.worldPos = worldPos;          // 最新の被弾位置へ追従
                p.isFlank |= isFlank;           // 窓内に側背面が混ざれば側背面扱い
                p.target = target;
                instance.pending[key] = p;
            }
            else
            {
                instance.pending[key] = new Pending
                {
                    sumDamage = damage,
                    worldPos = worldPos,
                    isFlank = isFlank,
                    dueTime = Time.time + Mathf.Max(0.01f, instance.window),
                    target = target
                };
            }
        }

        /// <summary>溜まっている全ての合算ダメージを即座に吐き出す（会戦終了時の取りこぼし防止）。</summary>
        public static void FlushAll()
        {
            if (instance == null) return;
            foreach (var kv in instance.pending)
            {
                Pending p = kv.Value;
                if (p.sumDamage > 0)
                    DamagePopup.Show(p.worldPos, p.sumDamage, p.isFlank);
            }
            instance.pending.Clear();
        }

        private void Update()
        {
            if (pending.Count == 0) return;

            // 締切を過ぎた標的・破棄済みの標的を集める（列挙中の辞書変更を避けるためキーを退避）。
            dueKeys.Clear();
            float now = Time.time; // timeScale 追従＝ポーズ中は進まず、倍速で速く締め切る
            foreach (var kv in pending)
            {
                Pending p = kv.Value;
                // 破棄済み標的は即吐き出して掃除（位置は最後に記録した値を使う）。
                if (p.target == null || now >= p.dueTime)
                    dueKeys.Add(kv.Key);
            }

            for (int i = 0; i < dueKeys.Count; i++)
            {
                if (!pending.TryGetValue(dueKeys[i], out Pending p)) continue;
                if (p.sumDamage > 0)
                    DamagePopup.Show(p.worldPos, p.sumDamage, p.isFlank);
                pending.Remove(dueKeys[i]);
            }
        }

        private void Awake()
        {
            // 重複ガード（シングルトン）。会戦ごとに辞書をまっさらにする。
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            pending.Clear();
        }

        private void OnDestroy()
        {
            // 会戦終了で破棄されるとき、溜まったぶんを吐いてから静的参照を切る。
            if (instance == this)
            {
                FlushAll();
                instance = null;
            }
        }

        /// <summary>Battle シーン読み込み後に自動生成する（手置き不要）。Battle 以外では生成しない。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // 起動直後に既に Battle シーンが開いている場合へのフォールバック。
            EnsureForActiveScene();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name != "Battle") return;
            EnsureInstance();
        }

        private static void EnsureForActiveScene()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Battle")
                EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;
            var go = new GameObject("DamageAccumulator");
            go.AddComponent<DamageAccumulator>();
        }
    }
}
