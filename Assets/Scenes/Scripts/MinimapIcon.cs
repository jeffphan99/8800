using UnityEngine;

public class MinimapIcon : MonoBehaviour
{
    [Header("Minimap Settings")]
    public GameObject iconPrefab;
    public Color iconColor = Color.white;
    public Vector3 iconOffset = new Vector3(0, 100, 0); // Height above object
    public bool rotateWithObject = true;

    private GameObject iconInstance;
    private Transform minimapCameraTransform;

    void Start()
    {
        // Find the minimap camera
        GameObject minimapCam = GameObject.Find("MinimapCamera");
        if (minimapCam != null)
        {
            minimapCameraTransform = minimapCam.transform;
        }

        CreateIcon();
    }

    void CreateIcon()
    {
        if (iconPrefab != null)
        {
            // Create icon as child of this object
            iconInstance = Instantiate(iconPrefab, transform.position + iconOffset, Quaternion.identity);
            iconInstance.transform.SetParent(transform);
            iconInstance.layer = LayerMask.NameToLayer("Minimap");

            // Set color if it has a renderer
            Renderer renderer = iconInstance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = iconColor;
            }
        }
        else
        {
            CreateDefaultIcon();
        }
    }

    void CreateDefaultIcon()
    {
        iconInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconInstance.name = gameObject.name + "_MinimapIcon";
        iconInstance.transform.SetParent(transform);
        iconInstance.transform.localPosition = iconOffset;
        iconInstance.transform.localScale = Vector3.one * 2f;
        iconInstance.layer = LayerMask.NameToLayer("Minimap");

        // Rotate to face up
        iconInstance.transform.rotation = Quaternion.Euler(90, 0, 0);

        // Set color
        Renderer renderer = iconInstance.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = iconColor;
        }

        // Remove collider
        Collider collider = iconInstance.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    void LateUpdate()
    {
        if (iconInstance == null) return;

        // Keep icon at correct height
        Vector3 pos = transform.position + iconOffset;
        iconInstance.transform.position = pos;

        // Rotate icon to match object
        if (rotateWithObject)
        {
            iconInstance.transform.rotation = Quaternion.Euler(90, transform.eulerAngles.y, 0);
        }
        else
        {
            // Always face up
            iconInstance.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
    }

    void OnDestroy()
    {
        if (iconInstance != null)
        {
            Destroy(iconInstance);
        }
    }
}