using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eén leervraag tijdens de wandeling door de darm. Geen punten: het doel is iets bijleren.
/// Bij een fout antwoord krijgt de speler uitleg waarom dat antwoord niet klopt én waarom het juiste wel klopt.
/// Aanmaken: rechtermuisklik in het Project-venster > Create > Oncologie > Darm-vraag.
/// Zo kunnen de vragen van de studenten (geneeskunde/verpleegkunde) zonder code toegevoegd worden.
/// </summary>
[CreateAssetMenu(fileName = "Vraag_", menuName = "Oncologie/Darm-vraag")]
public class ColonQuestion : ScriptableObject
{
    [System.Serializable]
    public class Answer
    {
        [TextArea(1, 3)] public string text = "";
        public bool correct;
        [Tooltip("Waarom dit antwoord (niet) klopt. Vriendelijk en uitleggend, nooit afkeurend.")]
        [TextArea(2, 5)] public string explanation = "";
    }

    [Tooltip("Onderwerp, voor het overzicht (bv. 'Wat is een poliep', 'Screening')")]
    public string topic = "";
    [TextArea(2, 4)] public string question = "";
    [Tooltip("2 tot 4 antwoorden. Minstens één moet juist zijn.")]
    public List<Answer> answers = new List<Answer>();
    [Tooltip("Algemene uitleg, wordt na elk antwoord getoond")]
    [TextArea(3, 6)] public string explanation = "";
    [Tooltip("Optioneel: voorgelezen vraag (voice-over, later)")]
    public AudioClip questionVoice;

    [Header("Bron")]
    [Tooltip("Aangevinkt = tijdelijke tekst, nog te vervangen/controleren door de studenten")]
    public bool isPlaceholder = true;
    [Tooltip("Wie de inhoud aanleverde of controleerde")]
    public string source = "";

    public int CorrectIndex
    {
        get
        {
            for (int i = 0; i < answers.Count; i++) if (answers[i].correct) return i;
            return -1;
        }
    }

    void OnValidate()
    {
        if (answers.Count > 4) Debug.LogWarning(name + ": maximaal 4 antwoorden worden getoond.", this);
        if (answers.Count > 0 && CorrectIndex < 0) Debug.LogWarning(name + ": geen enkel antwoord staat op 'correct'.", this);
    }
}
