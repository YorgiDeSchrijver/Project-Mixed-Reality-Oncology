using UnityEngine;

namespace Oncologie.Core
{
    /// <summary>
    /// Toont duidelijke visuele indicators en 3D zwevende labels boven de twee keuzelocaties
    /// (Oud papier vs Aan Vader Luc geven).
    /// Zorgt ervoor dat de zones ENKEL zichtbaar zijn tijdens de keuken-keuzefase (KitchenIntro)
    /// en volledig onzichtbaar worden zodra de keuze gemaakt is, tijdens de darmwandeling en op de eindes.
    /// </summary>
    public class ScreeningZoneVisualizer : MonoBehaviour
    {
        [Header("Zone Type")]
        public bool isTrashZone = false;
        public string labelTitle = "Optie: Oud Papier";
        public string labelSub = "Negeren / Uitstellen";

        [Header("Kleuren")]
        [SerializeField] private Color zoneColor = new Color(1f, 0.5f, 0.2f, 0.8f);

        [Header("Animatie")]
        [SerializeField] private float bounceHeight = 0.06f;
        [SerializeField] private float bounceSpeed = 2.5f;

        [Header("Visuele Weergave")]
        [Tooltip("Toon het 2D HUD label boven de zone tijdens de intro keuzefase.")]
        [SerializeField] private bool showFloatingHUD = true;

        private Vector3 _basePos;
        private Camera _mainCam;
        private Renderer[] _childRenderers;
        private Collider _collider;

        private void Awake()
        {
            _childRenderers = GetComponentsInChildren<Renderer>(true);
            _collider = GetComponent<Collider>();
        }

        private void Start()
        {
            _basePos = transform.position;
            _mainCam = Camera.main;

            if (isTrashZone)
            {
                labelTitle = "ZONE A: OUD PAPIER";
                labelSub = "[ Negeren / Uitstellen ]";
                zoneColor = new Color(1f, 0.35f, 0.3f, 0.9f);
            }
            else
            {
                labelTitle = "ZONE B: AAN VADER LUC";
                labelSub = "[ Aandringen op de test ]";
                zoneColor = new Color(0.3f, 0.95f, 0.5f, 0.9f);
            }
        }

        private void Update()
        {
            if (_mainCam == null) _mainCam = Camera.main;

            // Bepaal of de zones zichtbaar moeten zijn:
            // ENKEL in de KitchenIntro fase EN zolang er nog geen keuze is gemaakt!
            bool shouldBeActive = true;
            if (ExperienceFlowManager.Instance != null)
            {
                shouldBeActive = (ExperienceFlowManager.Instance.CurrentState == ExperienceState.KitchenIntro 
                                  && ExperienceFlowManager.Instance.PlayerChoice == ScreeningChoice.None);
            }

            // Schakel alle 3D visuals en colliders direct uit op de eindes of in de darm
            if (_childRenderers != null && _childRenderers.Length > 0)
            {
                for (int i = 0; i < _childRenderers.Length; i++)
                {
                    if (_childRenderers[i] != null && _childRenderers[i].enabled != shouldBeActive)
                    {
                        _childRenderers[i].enabled = shouldBeActive;
                    }
                }
            }

            if (_collider != null && _collider.enabled != shouldBeActive)
            {
                _collider.enabled = shouldBeActive;
            }

            if (!shouldBeActive) return;

            // Zachte zwevende animatie
            float newY = _basePos.y + Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            transform.position = new Vector3(_basePos.x, newY, _basePos.z);
        }

        private void OnGUI()
        {
            if (!showFloatingHUD || _mainCam == null) return;

            // Onzichtbaar op de eindes en in de darm
            if (ExperienceFlowManager.Instance != null)
            {
                if (ExperienceFlowManager.Instance.CurrentState != ExperienceState.KitchenIntro
                    || ExperienceFlowManager.Instance.PlayerChoice != ScreeningChoice.None)
                {
                    return;
                }
            }

            Vector3 screenPos = _mainCam.WorldToScreenPoint(transform.position + Vector3.up * 0.35f);
            if (screenPos.z > 0.4f && screenPos.z < 10f)
            {
                float x = screenPos.x - 100f;
                float y = Screen.height - screenPos.y;

                GUI.backgroundColor = zoneColor;
                GUI.Box(new Rect(x, y, 200f, 45f), $"<b>{labelTitle}</b>\n<size=11>{labelSub}</size>");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isTrashZone ? new Color(1f, 0.3f, 0.3f, 0.5f) : new Color(0.3f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 0.4f, 0.6f));
        }
    }
}
