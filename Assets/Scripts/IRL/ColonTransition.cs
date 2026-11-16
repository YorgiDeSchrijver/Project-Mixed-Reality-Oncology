using System;
using System.Collections;
using System.Collections.Generic;
using Oncologie.Dialogue;
using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>
    /// Duidelijke, rustige overgang van de keuken naar de darm (± 35 s):
    ///  1. de keuken dimt en de tijd "staat stil";
    ///  2. Luc verandert in een doorschijnende scan;
    ///  3. zijn dikke darm tekent zich, met uitleg;
    ///  4. een rode markering toont de poliep;
    ///  5. de scan groeit om de speler heen (het hologram beweegt, niet de camera → VR-comfortabel);
    ///  6. zwart + titelkaart, daarna wordt de darmscène geladen.
    /// Elke stap heeft een ondertitel van de verteller, zodat het verhaal te volgen is.
    /// </summary>
    public class ColonTransition : MonoBehaviour
    {
        public LucCharacter luc;
        [Tooltip("Punt aan Luc's voeten waar het hologram verschijnt.")]
        public Transform lucFeet;

        [Header("Teksten (verteller)")]
        [TextArea] public string line1 = "Even stilstaan. Luc voelt zich kerngezond… maar wat gebeurt er vanbinnen?";
        [TextArea] public string line2 = "Dit is Luc, zoals een scan hem zou tonen.";
        [TextArea] public string line3 = "Het oranje kanaal is zijn dikke darm: ongeveer anderhalve meter lang.";
        [TextArea] public string line4 = "En hier zit een poliep: een klein bultje in de darmwand. Luc voelt er helemaal niets van. Zo begint darmkanker meestal.";
        [TextArea] public string line5 = "We gaan een kijkje nemen. Je wordt duizend keer kleiner…";
        public string titleText = "In de dikke darm van Luc";
        [TextArea] public string titleSub = "Kijk rond. Beweeg met je joystick (desktop: W/S).";

        public bool IsRunning { get; private set; }

        private static readonly Color NarratorColor = new Color(1f, 0.82f, 0.35f);

        private readonly List<(Light light, float intensity)> _lights = new List<(Light, float)>();
        private Color _ambient, _ambEq, _ambGround;
        private float _ambientIntensity, _reflection, _skyExposure = -1f;
        private Material _sky;

        public void Play(Action onFinished)
        {
            if (IsRunning) return;
            StartCoroutine(Run(onFinished));
        }

        private IEnumerator Run(Action onFinished)
        {
            IsRunning = true;
            PlayerRigSelector.SetMovementLocked(true);
            CaptureLighting();
            var cam = WorldUI.MainCamera();

            // 1. Keuken dimt, tijd staat stil
            StartCoroutine(Dim(0.22f, 2.5f));
            yield return Say(line1, 5f);

            // 2. Luc wordt een scan
            var holo = AnatomyHologram.Create(lucFeet != null ? lucFeet : luc.transform, cam.transform.position);
            if (AudioManagerExists()) Oncologie.Core.AudioManager.Instance.StartHeartbeat(false);
            float t = 0;
            Coroutine sayRoutine = StartCoroutine(Say(line2, 4.5f));
            while (t < 2.2f)
            {
                t += Time.deltaTime;
                float k = t / 2.2f;
                holo.SetBodyAlpha(k);
                if (k > 0.5f && luc != null) luc.SetVisible(false);
                yield return null;
            }
            yield return sayRoutine;

            // 3. De dikke darm tekent zich
            sayRoutine = StartCoroutine(Say(line3, 5.5f));
            t = 0;
            while (t < 3f)
            {
                t += Time.deltaTime;
                holo.SetColonDraw(t / 3f);
                holo.SetLabels(Mathf.Clamp01((t - 2f)), 0);
                yield return null;
            }
            holo.SetColonDraw(1); holo.SetLabels(1, 0);
            yield return sayRoutine;

            // 4. De poliep
            holo.SetMarker(true);
            holo.SetLabels(1, 1);
            if (AudioManagerExists()) Oncologie.Core.AudioManager.Instance.PlaySnareSnip();
            yield return Say(line4, 7f);

            // 5. Inzoomen: het hologram groeit rond de speler
            sayRoutine = StartCoroutine(Say(line5, 4f));
            StartCoroutine(Dim(0f, 3f));
            if (AudioManagerExists()) Oncologie.Core.AudioManager.Instance.PlayWhoosh();
            yield return Zoom(holo, cam, 6.5f);
            yield return sayRoutine;

            // 6. Zwart + titel, dan laden
            yield return ScreenFader.Instance.Fade(1f, 1.2f);
            Destroy(holo.gameObject);
            yield return ScreenFader.Instance.ShowTitle(titleText, titleSub, 3.2f);
            IsRunning = false;
            onFinished?.Invoke();
        }

        private IEnumerator Zoom(AnatomyHologram holo, Camera cam, float duration)
        {
            Transform root = holo.transform;
            Quaternion r0 = root.rotation;
            Vector3 aimStart = root.TransformPoint(AnatomyHologram.AimLocal);
            const float endScale = 70f;
            float t = 0;
            bool hidden = false;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = k * k * (3f - 2f * k);
                float s = Mathf.Exp(Mathf.Log(endScale) * Mathf.Pow(k, 1.6f));
                if (!hidden && k > 0.2f) { hidden = true; HideWorld(holo.transform, cam.transform); }

                // Draai zodat je op het einde ín de lengte van de darm kijkt.
                Vector3 tangentWorld = r0 * AnatomyHologram.AimTangentLocal;
                Quaternion align = Quaternion.FromToRotation(tangentWorld, cam.transform.forward) * r0;
                root.rotation = Quaternion.Slerp(r0, align, e);
                root.localScale = Vector3.one * s;

                Vector3 target = cam.transform.position + cam.transform.forward * 0.05f;
                Vector3 aimWorld = Vector3.Lerp(aimStart, target, e);
                root.position = aimWorld - root.rotation * (AnatomyHologram.AimLocal * s);

                holo.SetLabels(1f - Mathf.Clamp01(k * 3f), 1f - Mathf.Clamp01(k * 3f));
                holo.SetBodyAlpha(1f - Mathf.Clamp01(k * 2f));
                yield return null;
            }
        }

        /// <summary>De (inmiddels donkere) keuken verbergen zodat ze niet door het groeiende hologram steekt.</summary>
        private static void HideWorld(Transform hologram, Transform camera)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r.transform.IsChildOf(hologram) || r.transform.IsChildOf(camera)) continue;
                r.enabled = false;
            }
        }

        private IEnumerator Say(string text, float seconds)
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.PlaySingleLine(new DialogueLine("Verteller", NarratorColor, text, seconds - 0.4f) { pauseAfter = 0.2f });
            yield return new WaitForSeconds(seconds);
        }

        private static bool AudioManagerExists() => Oncologie.Core.AudioManager.Instance != null;

        // ---------------- Licht ----------------

        private void CaptureLighting()
        {
            _lights.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                _lights.Add((l, l.intensity));
            _ambient = RenderSettings.ambientSkyColor;
            _ambEq = RenderSettings.ambientEquatorColor;
            _ambGround = RenderSettings.ambientGroundColor;
            _ambientIntensity = RenderSettings.ambientIntensity;
            _reflection = RenderSettings.reflectionIntensity;
            _sky = RenderSettings.skybox;
            if (_sky != null && _sky.HasProperty("_Exposure")) _skyExposure = _sky.GetFloat("_Exposure");
        }

        private IEnumerator Dim(float to, float duration)
        {
            float from = _curDim, t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                ApplyDim(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            ApplyDim(to);
        }

        private float _curDim = 1f;

        private void ApplyDim(float k)
        {
            _curDim = k;
            CandleSet.Dim = k;
            foreach (var (light, intensity) in _lights) if (light != null) light.intensity = intensity * k;
            RenderSettings.ambientSkyColor = _ambient * k;
            RenderSettings.ambientEquatorColor = _ambEq * k;
            RenderSettings.ambientGroundColor = _ambGround * k;
            RenderSettings.ambientIntensity = _ambientIntensity * k;
            RenderSettings.reflectionIntensity = _reflection * k;
            if (_sky != null && _skyExposure >= 0) _sky.SetFloat("_Exposure", _skyExposure * k);
        }

        private void OnDestroy()
        {
            // Skybox-materiaal is een asset: exposure terugzetten zodat het niet in het project blijft hangen.
            if (_sky != null && _skyExposure >= 0) _sky.SetFloat("_Exposure", _skyExposure);
        }
    }
}
