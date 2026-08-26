using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Controller
{
    public class PlayerSoundController : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip fire;
        [SerializeField] private AudioClip dodge;
        [SerializeField] private AudioClip walk;
        [SerializeField] private AudioClip reload;
        [SerializeField] private AudioClip damage;
        [SerializeField] private AudioClip win;
        [SerializeField] private AudioClip lose;

        public void PlayFire(float volume) => Play(fire, volume, 0.96f, 1.04f);
        public void PlayDodge(float volume) => Play(dodge, volume, 1.08f, 1.18f);
        public void PlayReload(float volume) => Play(reload, volume, 0.98f, 1.02f);
        public void PlayWin(float volume) => Play(win, volume, 1f, 1f);
        public void PlayLose(float volume) => Play(lose, volume, 0.9f, 0.96f);

        public void PlayDamage(AudioClip clip, float volume)
        {
            Play(clip != null ? clip : damage, volume, 0.9f, 1.02f);
        }

        public void PlayWalk(AudioClip clip, float volume)
        {
            Play(clip != null ? clip : walk, volume, 0.96f, 1.04f);
        }

        private void Play(AudioClip clip, float volume, float minimumPitch, float maximumPitch)
        {
            if (audioSource == null || clip == null) return;
            audioSource.pitch = Random.Range(minimumPitch, maximumPitch);
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
