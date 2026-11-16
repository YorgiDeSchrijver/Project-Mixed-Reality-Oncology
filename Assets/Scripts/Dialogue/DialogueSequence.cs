using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oncologie.Dialogue
{
    [Serializable]
    public class DialogueSequence
    {
        [Tooltip("Unieke sleutel om deze dialoogreeks aan te roepen via script.")]
        public string sequenceKey = "NewSequence";

        [Tooltip("Beschrijving van de scène of het moment.")]
        public string description = "";

        [Tooltip("Lijst van opeenvolgende dialoogregels met tekst en audioplekken.")]
        public List<DialogueLine> lines = new List<DialogueLine>();

        public DialogueSequence() { }

        public DialogueSequence(string key, string desc)
        {
            sequenceKey = key;
            description = desc;
            lines = new List<DialogueLine>();
        }
    }
}
