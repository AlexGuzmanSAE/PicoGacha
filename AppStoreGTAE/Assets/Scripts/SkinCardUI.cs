using UnityEngine;
using UnityEngine.UI;
using TMPro;


// Una tarjeta individual: imagen, nombre, precio y boton de compra.
//
// La imagen NO se descarga de internet: ya esta adentro de Unity, en
// Assets/Resources/Skins/. El nombre del archivo debe ser igual al campo
// "spriteName" que viene de Firebase (sin la extension .png).
//
// Ponlo en un prefab con: Image, Text (nombre), Text (precio), Button con
// su propio Text (para la etiqueta "Comprar" / "Comprada" / "Sin saldo").
public class SkinCardUI : MonoBehaviour
{
    [SerializeField] private Image skinImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonLabel;

    private SkinData skin;

    public void Setup(SkinData skin)
    {
        this.skin = skin;

        nameText.text = skin.name;
        priceText.text = skin.price + " monedas";

        var sprite = Resources.Load<Sprite>("Skins/" + skin.name);
        if (sprite != null)
        {
            skinImage.sprite = sprite;
        }
        else
        {
            Debug.LogWarning("No se encontro el sprite: " + skin.name);
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);

        RefreshButtonState();
    }

    // decide que dice el boton y si se puede apretar, segun si hay sesion,
    // si ya la compro, o si le alcanzan las monedas
    private void RefreshButtonState()
    {
        var store = StoreManager.Instance;

        if (store == null || !store.HasUser)
        {
            SetButton("Inicia sesion", false);
            return;
        }

        if (store.IsPurchased(skin.ID))
        {
            SetButton("Comprada", false);
            return;
        }

        bool canAfford = store.CurrentCoins >= skin.price;
        SetButton(canAfford ? "Comprar" : "Sin saldo", canAfford);
    }

    private void SetButton(string label, bool interactable)
    {
        if (buyButtonLabel != null) buyButtonLabel.text = label;
        buyButton.interactable = interactable;
    }

    private void OnBuyClicked()
    {
        buyButton.interactable = false;

        StoreManager.Instance.TryPurchaseSkin(skin.ID, (success, message) =>
        {
            Debug.Log("[SkinCardUI] " + message);
            // no hace falta actualizar el boton a mano: comprar cambia las
            // monedas y "purchased" en Firebase, y eso hace que
            // SkinStoreManager vuelva a dibujar toda la tienda
        });
    }
}
