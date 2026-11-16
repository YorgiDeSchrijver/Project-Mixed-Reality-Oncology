using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Oncologie.Core
{
    /// <summary>
    /// Automatische setup en verbinder voor de Oncologie Experience.
    /// Zorgt ervoor dat alle GameObjects, controllers, zones, audio en UI direct werken
    /// zodra iemand op Play drukt, ZONDER dat je handmatig tientallen velden in de Inspector hoeft te slepen!
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ExperienceBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            SetupAudio();
            SetupExperience();
            SetupZones();
            SetupOutroBillboard();
        }

        private void SetupAudio()
        {
            if (AudioManager.Instance == null)
            {
                var audioGO = new GameObject("[Audio_Manager]");
                audioGO.transform.SetParent(transform);
                audioGO.AddComponent<AudioManager>();
            }
        }

        private void SetupExperience()
        {
            var manager = FindAnyObjectByType<ExperienceFlowManager>();
            if (manager == null)
            {
                var mgrGO = new GameObject("[Experience_Flow_Manager]");
                mgrGO.transform.SetParent(transform);
                manager = mgrGO.AddComponent<ExperienceFlowManager>();
            }

            // Koppel Kitchen Environment
            GameObject kitchen = GameObject.Find("kitchen");
            GameObject scavenger = GameObject.Find("Scavenger");
            GameObject envelope = GameObject.Find("envelope");

            // Maak start-anchor aan indien nodig
            Transform kitchenAnchor = GetOrCreateAnchor("Kitchen_PlayerAnchor", new Vector3(-0.41f, 0.0f, -0.6f), Quaternion.identity);

            // Zorg dat de envelop de interactie-script heeft
            if (envelope != null)
            {
                var kit = envelope.GetComponent<ScreeningKitInteraction>();
                if (kit == null) kit = envelope.AddComponent<ScreeningKitInteraction>();

                var col = envelope.GetComponent<Collider>();
                if (col != null) col.isTrigger = false;

                var rb = envelope.GetComponent<Rigidbody>();
                if (rb == null) rb = envelope.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.isKinematic = false;
            }
        }

        private void SetupZones()
        {
            // Verwijder verouderde zwevende GiveZone / TrashZone kubussen indien aanwezig
            var oldGive = GameObject.Find("GiveZone");
            if (oldGive != null) Destroy(oldGive);

            var oldTrash = GameObject.Find("TrashZone");
            if (oldTrash != null) Destroy(oldTrash);

            // Zorg dat Scavenger (Vader Luc) een hand-in trigger heeft voor Keuze B (Aandringen)
            GameObject scavenger = GameObject.Find("Scavenger");
            if (scavenger != null)
            {
                var trigger = scavenger.transform.Find("Envelope_Hand_Trigger");
                if (trigger == null)
                {
                    var ht = new GameObject("Envelope_Hand_Trigger");
                    ht.transform.SetParent(scavenger.transform, false);
                    ht.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                    var scCol = ht.AddComponent<SphereCollider>();
                    scCol.isTrigger = true;
                    scCol.radius = 0.85f;
                }
            }

            // Zorg dat TrashCan een trigger collider heeft voor Keuze A (Negeren)
            GameObject trashCan = GameObject.Find("TrashCan");
            if (trashCan != null)
            {
                var col = trashCan.GetComponent<BoxCollider>();
                if (col == null) col = trashCan.AddComponent<BoxCollider>();
                col.isTrigger = true;
            }
        }

        private void SetupOutroBillboard()
        {
            // Zoek of maak het 3D Outro paneel in de keuken
            GameObject billboard = GameObject.Find("Outro_Billboard");
            if (billboard == null)
            {
                billboard = GameObject.CreatePrimitive(PrimitiveType.Quad);
                billboard.name = "Outro_Billboard";
                billboard.transform.position = new Vector3(0f, 1.6f, 1.4f);
                billboard.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                billboard.transform.localScale = new Vector3(1.8f, 1.1f, 1f);

                var col = billboard.GetComponent<Collider>();
                if (col != null) Destroy(col);

                billboard.SetActive(false);
            }
        }

        private Transform GetOrCreateAnchor(string name, Vector3 pos, Quaternion rot)
        {
            GameObject anchor = GameObject.Find(name);
            if (anchor == null)
            {
                anchor = new GameObject(name);
                anchor.transform.position = pos;
                anchor.transform.rotation = rot;
            }
            return anchor.transform;
        }
    }
}
