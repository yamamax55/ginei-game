using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// BGM と効果音を一元管理するシングルトン。DontDestroyOnLoad で永続化。
    /// Inspector で AudioClip を割り当てる。未割り当て clip は無音でエラーにならない。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Object.FindFirstObjectByType<AudioManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("AudioManager");
                        instance = go.AddComponent<AudioManager>();
                        if (Application.isPlaying)
                            DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("BGM")]
        public AudioClip bgmTitle;
        public AudioClip bgmBattle;

        [Header("効果音")]
        public AudioClip seBeam;
        public AudioClip seHit;
        public AudioClip seExplosion;
        public AudioClip seUiClick;

        [Header("音量")]
        [Range(0f, 1f)] public float bgmVolume = 0.6f;
        [Range(0f, 1f)] public float seVolume = 1.0f;

        [Header("効果音の発音制御")]
        [Tooltip("SEの同時発音ボイス数（重ねて鳴らせる数）。多数の配下艦が同時発砲しても途切れにくくする")]
        public int seVoiceCount = 6;
        [Tooltip("ビームSEを鳴らす最小間隔（秒・実時間）。全艦が同時発砲しても音が氾濫しないよう間引く")]
        public float beamMinInterval = 0.035f;
        [Tooltip("ビームSEのピッチ揺らぎ（±）。同じ音の反復で機械的に聞こえるのを避ける")]
        [Range(0f, 0.5f)] public float beamPitchJitter = 0.12f;

        [Tooltip("Inspectorでビーム音が未割当のとき Resources からロードするクリップ名")]
        public string beamClipResource = "shot_1";

        private AudioSource bgmSource;
        private AudioSource seSource;
        private AudioSource[] seVoices; // 重ね発音用ボイスプール（ピッチをばらけさせる）
        private int seVoiceIndex;
        private float nextBeamTime;     // 次にビームSEを鳴らせる実時間

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

            seSource = gameObject.AddComponent<AudioSource>();
            seSource.loop = false;
            seSource.playOnAwake = false;

            // SE のボイスプール（ピッチを1発ごとに変えるため複数ソースを用意）
            int n = Mathf.Max(1, seVoiceCount);
            seVoices = new AudioSource[n];
            for (int i = 0; i < n; i++)
            {
                AudioSource v = gameObject.AddComponent<AudioSource>();
                v.loop = false;
                v.playOnAwake = false;
                seVoices[i] = v;
            }

            // Inspector未割当のビーム音は Resources からフォールバック読み込み（コード生成のAudioManagerでも鳴る）
            if (seBeam == null && !string.IsNullOrEmpty(beamClipResource))
                seBeam = Resources.Load<AudioClip>(beamClipResource);

            ApplyMasterVolume();
        }

        private void Start()
        {
            ApplyMasterVolume();
        }

        public void ApplyMasterVolume()
        {
            float master = GameSettings.Instance.masterVolume;
            if (bgmSource != null) bgmSource.volume = bgmVolume * master;
            if (seSource != null) seSource.volume = seVolume * master;
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;
            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume * GameSettings.Instance.masterVolume;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            bgmSource.Stop();
        }

        public void PlaySE(AudioClip clip)
        {
            if (clip == null || seSource == null) return;
            seSource.PlayOneShot(clip, seVolume * GameSettings.Instance.masterVolume);
        }

        /// <summary>ボイスプールでピッチを変えて鳴らす（重ね発音・音色のばらつき用）。</summary>
        private void PlaySEPitched(AudioClip clip, float pitch)
        {
            if (clip == null) return;
            if (seVoices == null || seVoices.Length == 0) { PlaySE(clip); return; }
            AudioSource v = seVoices[seVoiceIndex];
            seVoiceIndex = (seVoiceIndex + 1) % seVoices.Length;
            v.pitch = pitch;
            v.PlayOneShot(clip, seVolume * GameSettings.Instance.masterVolume);
        }

        /// <summary>
        /// ビーム発射音。多数の艦が同時発砲しても音が氾濫しないよう実時間で間引き、
        /// 1発ごとにピッチを微妙に変えて機械的な反復感を避ける。
        /// </summary>
        public void PlayBeam()
        {
            if (seBeam == null) return;
            float now = Time.unscaledTime; // 倍速/ポーズに依らず一定密度に保つ
            if (now < nextBeamTime) return;
            nextBeamTime = now + Mathf.Max(0f, beamMinInterval);

            float pitch = 1f + Random.Range(-beamPitchJitter, beamPitchJitter);
            PlaySEPitched(seBeam, pitch);
        }

        public void PlayHit()       => PlaySE(seHit);
        public void PlayExplosion() => PlaySE(seExplosion);
        public void PlayUIClick()   => PlaySE(seUiClick);
    }
}
