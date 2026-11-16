using System;
using UnityEngine;

namespace Oncologie.Dialogue
{
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Naam van het sprekende personage (bijv. Vader Luc, Verteller, Jij).")]
        public string speakerName = "Vader Luc";

        [Tooltip("Kleur van de naam in de ondertiteling.")]
        public Color speakerColor = new Color(0.35f, 0.78f, 1.0f);

        [TextArea(2, 5)]
        [Tooltip("De tekst die in de ondertiteling verschijnt.")]
        public string subtitleText = "";

        [Tooltip("Sleep hier een audiobestand (.mp3 / .wav) in. Indien leeg, gebruikt het systeem de 'displayDuration'.")]
        public AudioClip voiceClip;

        [Tooltip("Weergavetijd in seconden wanneer er geen audiobestand is gekoppeld.")]
        public float displayDuration = 4.0f;

        [Tooltip("Rustpauze in seconden vóór de volgende dialoogregel start.")]
        public float pauseAfter = 0.4f;

        public DialogueLine() { }

        public DialogueLine(string speaker, Color color, string text, float duration = 4.0f, AudioClip clip = null)
        {
            speakerName = speaker;
            speakerColor = color;
            subtitleText = text;
            displayDuration = duration;
            voiceClip = clip;
        }
    }
}
