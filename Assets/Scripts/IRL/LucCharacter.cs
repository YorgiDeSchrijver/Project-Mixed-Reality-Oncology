using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>
    /// Vader Luc: staat aan het hoofd van de tafel en kijkt de speler aan (head-IK), zodat hij reageert op jou
    /// in plaats van een stilstaande pop te zijn. Vereist een AnimatorController met IK Pass aan.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class LucCharacter : MonoBehaviour
    {
        public bool lookAtPlayer = true;
        [Range(0, 1)] public float lookWeight = 0.9f;
        [Tooltip("Normalized time van de (enige) pose in de animatieclip.")]
        public float poseTime = 0.15f;

        private Animator _anim;
        private Renderer[] _renderers;
        private float _w;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Start()
        {
            if (_anim.runtimeAnimatorController != null)
            {
                _anim.Play(_anim.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, poseTime);
                _anim.speed = 0f;
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            var cam = WorldUI.MainCamera();
            _w = Mathf.MoveTowards(_w, lookAtPlayer && cam != null ? lookWeight : 0f, Time.deltaTime * 1.5f);
            _anim.SetLookAtWeight(_w, 0.2f, 0.9f, 0f, 0.6f);
            if (cam != null) _anim.SetLookAtPosition(cam.transform.position);
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in _renderers) if (r != null) r.enabled = visible;
        }
    }
}
