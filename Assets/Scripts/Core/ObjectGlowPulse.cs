using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ObjectGlowPulse : MonoBehaviour
{
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.4f);
    [SerializeField] private float pulseSpeed = 2.0f;
    [SerializeField] private float maxIntensity = 2.5f;
    [SerializeField] private bool glowOnStart = false;

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private bool _isGlowing = false;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        _renderer.material.EnableKeyword("_EMISSION");
        _isGlowing = glowOnStart;
    }

    private void Update()
    {
        if (!_isGlowing) return;

        float emission = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f * maxIntensity;
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(EmissionColorId, glowColor * emission);
        _renderer.SetPropertyBlock(_propBlock);
    }

    public void SetGlowing(bool state)
    {
        _isGlowing = state;
        if (!state)
        {
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(EmissionColorId, Color.black);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}