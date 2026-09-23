using UnityEngine;

// Vista previa 3D: al equipar una skin, muestra su accesorio sobre el
// personaje. Si equipas desde la PWA (users/{uid}/equipped), el cambio
// llega por listener y el accesorio cambia aqui solo.
//
// Normalmente lo agrega solo el SkinPreviewBootstrap al PicoChan de la
// escena, con la config de Resources. También lo puedes agregar a mano:
// arrastra el PicoChan + el asset de config en el inspector.
public class SkinPreview3D : MonoBehaviour
{
    [Header("Ancla (vacio = se crea sobre el personaje)")]
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Vector3 autoAnchorOffset = new Vector3(0, 1.7f, 0);

    [Header("Config de efectos")]
    [SerializeField] private SkinEffectConfig effectConfig;

    private GameObject currentAccessory;

    // Para configurarlo por codigo (lo usa SkinPreviewBootstrap).
    public void Configure(SkinEffectConfig config)
    {
        effectConfig = config;
        RefreshFromStore();
    }

    private void Start()
    {
        EnsureAnchor();
        if (StoreManager.Instance != null)
        {
            StoreManager.Instance.OnEquippedChanged += HandleEquipped;
            StoreManager.Instance.OnChanged += RefreshFromStore;
        }
        else
        {
            Invoke(nameof(RetrySubscribe), 0.5f);
        }
        RefreshFromStore();
    }

    private void RetrySubscribe()
    {
        if (StoreManager.Instance != null)
        {
            StoreManager.Instance.OnEquippedChanged += HandleEquipped;
            StoreManager.Instance.OnChanged += RefreshFromStore;
            RefreshFromStore();
        }
    }

    private void HandleEquipped(string equippedId)
    {
        RefreshFromStore();
    }

    private void RefreshFromStore()
    {
        if (StoreManager.Instance == null || effectConfig == null) return;
        string id = StoreManager.Instance.EquippedId;
        if (string.IsNullOrEmpty(id)) { ClearAccessory(); return; }
        if (!StoreManager.Instance.Catalog.TryGetValue(id, out SkinData skin))
            return; // catalogo aun cargando
        if (effectConfig.TryGetForSkin(skin, out var entry))
            ShowEntry(entry);
        else
            ClearAccessory();
    }

    // Muestra el accesorio del effectId ("" o desconocido = quitar).
    public void ShowEffect(string effectId)
    {
        ClearAccessory();
        if (string.IsNullOrEmpty(effectId) || effectConfig == null) return;
        if (!effectConfig.TryGet(effectId, out var entry)) return;
        ShowEntry(entry);
    }

    private void ShowEntry(SkinEffectConfig.EffectEntry entry)
    {
        ClearAccessory();
        EnsureAnchor();

        PrimitiveType type = PrimitiveType.Sphere;
        switch (entry.primitive)
        {
            case SkinEffectConfig.PrimitiveKind.Cube: type = PrimitiveType.Cube; break;
            case SkinEffectConfig.PrimitiveKind.Cylinder: type = PrimitiveType.Cylinder; break;
            case SkinEffectConfig.PrimitiveKind.Capsule: type = PrimitiveType.Capsule; break;
        }
        currentAccessory = GameObject.CreatePrimitive(type);
        currentAccessory.name = "Accessory_" + entry.effectId;
        // Sin fisicas: fuera colliders para que no empuje al personaje.
        foreach (var c in currentAccessory.GetComponents<Collider>())
            Destroy(c);
        currentAccessory.transform.SetParent(headAnchor, false);
        currentAccessory.transform.localPosition = entry.localOffset;
        currentAccessory.transform.localScale = entry.localScale;

        var renderer = currentAccessory.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = entry.color;
            renderer.material = mat;
        }
    }

    public void ClearAccessory()
    {
        if (currentAccessory != null)
            Destroy(currentAccessory);
        currentAccessory = null;
    }

    private void EnsureAnchor()
    {
        if (headAnchor != null) return;
        var go = new GameObject("HeadAnchor");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = autoAnchorOffset;
        headAnchor = go.transform;
    }

    private void OnDestroy()
    {
        if (StoreManager.Instance != null)
        {
            StoreManager.Instance.OnEquippedChanged -= HandleEquipped;
            StoreManager.Instance.OnChanged -= RefreshFromStore;
        }
    }
}
