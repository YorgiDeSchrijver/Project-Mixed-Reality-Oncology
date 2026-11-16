using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>Draait een label (rond de verticale as) naar de speler, zodat tekst altijd leesbaar is.</summary>
    public class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = WorldUI.MainCamera();
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
