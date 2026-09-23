using UnityEngine;

// Conecta los cosmeticos sin tocar el prefab de PicoChan:
// busca al personaje en la escena, le agrega SkinPreview3D y le pasa
// la config de Resources. Solo necesitas un GameObject vacio en la
// escena con este script (ya viene en SampleScene como "Cosmetics").
public class SkinPreviewBootstrap : MonoBehaviour
{
    [Header("Nombre del personaje en la escena")]
    [SerializeField] private string characterName = "PicoChan";

    [Header("Config (vacio = Resources/SkinEffectConfigDefault)")]
    [SerializeField] private SkinEffectConfig effectConfig;
    [SerializeField] private string resourcesPath = "SkinEffectConfigDefault";

    private void Start()
    {
        if (effectConfig == null)
            effectConfig = Resources.Load<SkinEffectConfig>(resourcesPath);
        if (effectConfig == null)
        {
            Debug.LogError("[Cosmetics] No se encontro la config '" + resourcesPath + "' en Resources.");
            return;
        }

        var character = GameObject.Find(characterName);
        if (character == null)
        {
            Debug.LogError("[Cosmetics] No hay GameObject '" + characterName + "' en la escena.");
            return;
        }

        var preview = character.GetComponent<SkinPreview3D>();
        if (preview == null) preview = character.AddComponent<SkinPreview3D>();
        preview.Configure(effectConfig);
    }
}
