using System.Collections;
using UnityEngine;

namespace Oncologie.Core
{
    /// <summary>
    /// Beheert alle geluiden, stemmen (voice-overs) en achtergrondmuziek.
    /// Bevat handige Inspector-slots voor AudioClips, en genereert automatisch
    /// procedurele fallback-audio (hartslag, krimp-whoosh, akkoorden) als er nog geen bestanden zijn gekoppeld!
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource heartbeatSource;

        [Header("--- VOICE OVERS: VADER LUC ---")]
        [Tooltip("Luc: 'Kijk nu, alweer zo'n envelop voor die stoelgangtest... Ik doe dat volgend jaar wel.'")]
        public AudioClip voiceLucIntro;

        [Tooltip("Luc: 'Zo, bij het oud papier. Geen zorgen maken om niks!'")]
        public AudioClip voiceLucTrash;

        [Tooltip("Luc: 'Allee vooruit, omdat jij zo aandringt. Morgenochtend doe ik hem meteen.'")]
        public AudioClip voiceLucKeys;

        [Tooltip("Luc na 25s aarzeling: 'Leg maar bij het oud papier jongen, of geef hem aan mij...'")]
        public AudioClip voiceLucReminder;

        [Header("--- VOICE OVERS: ARTS / GIDS IN DARM ---")]
        [Tooltip("Arts: 'We bevinden ons in de dikke darm. Dit kleine knobbeltje is een poliep...'")]
        public AudioClip voiceDoctorPolypExplanation;

        [Tooltip("Arts: 'Geen pijn, geen zichtbaar bloed. Maar de test spoort microscopische sporen op.'")]
        public AudioClip voiceDoctorMicroscopicBlood;

        [Tooltip("Arts (Goede route): 'Omdat je vader de test deed, werd de poliep tijdig ontdekt en verwijderd.'")]
        public AudioClip voiceDoctorPolypRemoved;

        [Tooltip("Arts (Slechte route): 'Zonder screening is de poliep in 3 jaar uitgegroeid tot een agressieve tumor.'")]
        public AudioClip voiceDoctorCancerFormed;

        [Header("--- EFFECTEN & AMBIENCE ---")]
        public AudioClip sfxHeartbeatSlow;   // ~65 BPM
        public AudioClip sfxHeartbeatFast;   // ~95 BPM
        public AudioClip sfxShrinkWhoosh;    // Transitie naar darm
        public AudioClip sfxSnareSnip;       // Medische lis snijdt poliep
        public AudioClip sfxGoodChime;       // Positief belletje/akkoord
        public AudioClip musicHappyKitchen;  // Warme vrolijke sfeer
        public AudioClip musicSadKitchen;    // Kille sombere sfeer
        public AudioClip sfxMotherCrying;    // Gesnik moeder bij slecht einde

        private Coroutine _heartbeatRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioSources();
        }

        private void EnsureAudioSources()
        {
            if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            if (ambientSource == null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.loop = true;
            }
            if (heartbeatSource == null)
            {
                heartbeatSource = gameObject.AddComponent<AudioSource>();
                heartbeatSource.loop = true;
            }
        }

        // ==========================================
        // VOICE-OVER AFSPELEN
        // ==========================================

        public void PlayVoice(AudioClip clip, string speakerName = "")
        {
            if (clip != null)
            {
                voiceSource.Stop();
                voiceSource.clip = clip;
                voiceSource.Play();
            }
            else
            {
                // Subtiele spreek-cue (zachte blip) zodat de gebruiker hoort dat er dialoog is
                PlayProceduralBeep(speakerName == "Arts" ? 440f : 260f, 0.15f);
            }
        }

        // ==========================================
        // HARTSLAG (TRANSITIE & DARM)
        // ==========================================

        public void StartHeartbeat(bool fast)
        {
            StopHeartbeat();
            AudioClip clip = fast ? sfxHeartbeatFast : sfxHeartbeatSlow;

            if (clip != null)
            {
                heartbeatSource.clip = clip;
                heartbeatSource.volume = 0.8f;
                heartbeatSource.Play();
            }
            else
            {
                // Genereer procedurele hartslag puls
                _heartbeatRoutine = StartCoroutine(ProceduralHeartbeatRoutine(fast ? 0.65f : 1.0f));
            }
        }

        public void StopHeartbeat()
        {
            if (_heartbeatRoutine != null)
            {
                StopCoroutine(_heartbeatRoutine);
                _heartbeatRoutine = null;
            }
            heartbeatSource.Stop();
        }

        private IEnumerator ProceduralHeartbeatRoutine(float interval)
        {
            while (true)
            {
                // Lub-dub
                PlayProceduralThud(80f, 0.09f, 0.7f);
                yield return new WaitForSeconds(0.18f);
                PlayProceduralThud(65f, 0.11f, 0.5f);
                yield return new WaitForSeconds(interval - 0.18f);
            }
        }

        // ==========================================
        // TRANSITIE & MEDISCHE SFX
        // ==========================================

        public void PlayWhoosh()
        {
            if (sfxShrinkWhoosh != null)
            {
                sfxSource.PlayOneShot(sfxShrinkWhoosh);
            }
            else
            {
                PlayProceduralSweep(120f, 400f, 0.8f);
            }
        }

        public void PlaySnareSnip()
        {
            if (sfxSnareSnip != null)
            {
                sfxSource.PlayOneShot(sfxSnareSnip);
            }
            else
            {
                PlayProceduralChime();
            }
        }

        // ==========================================
        // AMBIENCE & MUZIEK
        // ==========================================

        public void PlayAmbience(bool isGoodEnding)
        {
            ambientSource.Stop();
            AudioClip clip = isGoodEnding ? musicHappyKitchen : musicSadKitchen;

            if (clip != null)
            {
                ambientSource.clip = clip;
                ambientSource.volume = 0.5f;
                ambientSource.Play();
            }

            if (!isGoodEnding && sfxMotherCrying != null)
            {
                sfxSource.clip = sfxMotherCrying;
                sfxSource.volume = 0.4f;
                sfxSource.Play();
            }
        }

        public void StopAmbience()
        {
            ambientSource.Stop();
            sfxSource.Stop();
        }

        // ==========================================
        // PROCEDURELE AUDIO SYNTHESE (FALLBACKS)
        // ==========================================

        private void PlayProceduralThud(float freq, float duration, float volume)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 25f);
                data[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("Thud", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            sfxSource.PlayOneShot(clip);
        }

        private void PlayProceduralBeep(float freq, float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                data[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * env * 0.15f;
            }

            AudioClip clip = AudioClip.Create("Beep", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            voiceSource.PlayOneShot(clip);
        }

        private void PlayProceduralSweep(float startFreq, float endFreq, float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float normT = t / duration;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, normT);
                float env = Mathf.Sin(Mathf.PI * normT);
                data[i] = Mathf.Sin(2 * Mathf.PI * currentFreq * t) * env * 0.35f;
            }

            AudioClip clip = AudioClip.Create("Sweep", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            sfxSource.PlayOneShot(clip);
        }

        private void PlayProceduralChime()
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            // C major chord (523Hz, 659Hz, 784Hz)
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 3.5f);
                float wave = (Mathf.Sin(2 * Mathf.PI * 523.25f * t) +
                              Mathf.Sin(2 * Mathf.PI * 659.25f * t) +
                              Mathf.Sin(2 * Mathf.PI * 783.99f * t)) / 3f;
                data[i] = wave * env * 0.3f;
            }

            AudioClip clip = AudioClip.Create("Chime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            sfxSource.PlayOneShot(clip);
        }
    }
}
