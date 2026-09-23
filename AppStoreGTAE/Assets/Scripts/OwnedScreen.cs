using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Pantalla "Mis objetos": reutiliza el mismo prefab de tarjeta que la
// tienda, pero solo muestra las skins que ya compraste (StoreManager las
// marca en Firebase bajo users/{uid}/purchased).
//
// El boton de cada tarjeta es "Equipar / Equipado": escribe
// users/{uid}/equipped, asi que si equipas en la PWA tambien se refleja
// aqui (y el accesorio 3D cambia solo).
//
// Como armarla:
// 1) Duplica el panel de la tienda (o crea un panel nuevo con su propio
//    ScrollView + Content).
// 2) Crea un GameObject vacio, agregale este script.
// 3) Arrastra su Content en "contentParent" y el mismo prefab de tarjeta
//    en "cardPrefab".
// 4) Alterna entre tienda e inventario activando/desactivando paneles.
public class OwnedScreen : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private SkinCardUI cardPrefab;

    [Header("Contador (opcional)")]
    [SerializeField] private TMP_Text countText;

    private void Start()
    {
        if (StoreManager.Instance != null)
            StoreManager.Instance.OnChanged += Rebuild;
        else
            Invoke(nameof(RetrySubscribe), 0.5f);
        Rebuild();
    }

    private void RetrySubscribe()
    {
        if (StoreManager.Instance != null)
        {
            StoreManager.Instance.OnChanged += Rebuild;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (contentParent == null || cardPrefab == null) return;
        if (StoreManager.Instance == null) return;

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        int count = 0;
        foreach (var kv in StoreManager.Instance.Catalog)
        {
            if (!StoreManager.Instance.IsPurchased(kv.Key)) continue;
            var card = Instantiate(cardPrefab, contentParent);
            var layout = card.GetComponent<LayoutElement>();
            if (layout == null) layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 1070;
            card.equipMode = true;
            card.Setup(kv.Value);
            count++;
        }

        if (countText != null)
            countText.text = count == 0
                ? "Aun no tienes objetos. Compra en la tienda."
                : "Tienes " + count + " objeto(s).";
    }

    private void OnDestroy()
    {
        if (StoreManager.Instance != null)
            StoreManager.Instance.OnChanged -= Rebuild;
    }
}
