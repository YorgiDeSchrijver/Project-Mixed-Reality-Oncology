using UnityEngine;

namespace Oncologie.Core
{
    /// <summary>
    /// Toont duidelijke visuele indicators en 3D zwevende labels boven de twee keuzelocaties
    /// (Oud papier vs Aan Vader Luc geven), zodat niet-technische testers en ouderen direct begrijpen wat ze moeten doen!
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
        [SerializeField] private float bounceHeight = 0.08f;
        [SerializeField] private float bounceSpeed = 2.5f;

        private Vector3 _basePos;
        private Camera _mainCam;

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

            float newY = _basePos.y + Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            transform.position = new Vector3(_basePos.x, newY, _basePos.z);
        }

        [Header("Visuele Weergave")]
        [Tooltip("Schakel 2D HUD labels uit om het scherm rustig en filmisch te houden zonder clutter.")]
        [SerializeField] private bool showFloatingHUD = false;

        private void OnGUI()
        {
            if (!showFloatingHUD || _mainCam == null) return;

            Vector3 screenPos = _mainCam.WorldToScreenPoint(transform.position + Vector3.up * 0.4f);
            if (screenPos.z > 0.5f && screenPos.z < 12f)
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
