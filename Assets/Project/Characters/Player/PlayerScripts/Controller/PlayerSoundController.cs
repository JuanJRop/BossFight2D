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

        public void PlayFire(float volume) => Play(fire, volume);
        public void PlayDodge(float volume) => Play(dodge, volume);
        public void PlayReload(float volume) => Play(reload, volume);
        public void PlayWin(float volume) => Play(win, volume);
        public void PlayLose(float volume) => Play(lose, volume);

        public void PlayDamage(AudioClip clip, float volume)
        {
            Play(clip != null ? clip : damage, volume);
        }

        public void PlayWalk(AudioClip clip, float volume)
        {
            Play(clip != null ? clip : walk, volume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
