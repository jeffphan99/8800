using UnityEngine;

[ExecuteInEditMode]
public class GlobalMaterialDarkener : MonoBehaviour
{
    [Header("Global Lighting Control")]
    [Range(0f, 2f)]
    public float globalBrightness = 1f; // Default dark

    [Range(0f, 1f)]
    public float emissionMultiplier = 0f; // Reduce glowing elements

    [Header("Fog Settings")]
    public bool enableFog = true;
    public Color fogColor = Color.black; 
    public FogMode fogMode = FogMode.ExponentialSquared;
    [Range(0f, 0.5f)]
    public float fogDensity = 0.05f; 


    [Header("Apply To")]
    public bool applyToAll = true;
    public Renderer[] specificRenderers;

    private MaterialPropertyBlock propertyBlock;

    void Start()
    {
        propertyBlock = new MaterialPropertyBlock();
        ApplyGlobalSettings();
    }

    void OnValidate()
    {
        // Update in editor when values change
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
        ApplyGlobalSettings();
    }

    public void ApplyGlobalSettings()
    {
        Renderer[] renderers;

        if (applyToAll)
        {
            renderers = FindObjectsOfType<Renderer>();
        }
        else
        {
            renderers = specificRenderers;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            // Skip UI and minimap icons
            if (renderer.gameObject.layer == LayerMask.NameToLayer("UI") ||
                renderer.gameObject.layer == LayerMask.NameToLayer("Minimap"))
            {
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);


            // Reduce emission if material has it
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                Color emissionColor = renderer.material.GetColor("_EmissionColor");
                propertyBlock.SetColor("_EmissionColor", emissionColor * emissionMultiplier);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }

        Debug.Log($"Applied global darkening to {renderers.Length} renderers");
    }

    [ContextMenu("Reset All Materials")]
    public void ResetAllMaterials()
    {
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.SetPropertyBlock(null);
            }
        }

        Debug.Log("Reset all material property blocks");
    }
}