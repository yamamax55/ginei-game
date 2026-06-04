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

        private AudioSource bgmSource;
        private AudioSource seSource;

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

        public void PlayBeam()      => PlaySE(seBeam);
        public void PlayHit()       => PlaySE(seHit);
        public void PlayExplosion() => PlaySE(seExplosion);
        public void PlayUIClick()   => PlaySE(seUiClick);
    }
}
