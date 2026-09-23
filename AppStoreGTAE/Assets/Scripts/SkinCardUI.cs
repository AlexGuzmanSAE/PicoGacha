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

    // OwnedScreen lo pone en true antes de Setup: la tarjeta pasa a ser
    // "Equipar / Equipado" en vez de "Comprar".
    public bool equipMode;

    private SkinData skin;

    public void Setup(SkinData skin)
    {
        this.skin = skin;

        if (nameText != null) nameText.text = skin.name;
        else Debug.LogError("[SkinCardUI] nameText sin conectar en el prefab.");
        if (priceText != null) priceText.text = skin.price + " monedas";

        if (skinImage != null)
        {
            var sprite = Resources.Load<Sprite>("Skins/" + skin.img);
            if (sprite != null)
            {
                skinImage.sprite = sprite;
            }
            else
            {
                Debug.LogWarning("No se encontro el sprite: Skins/" + skin.img + " (revisa img en Firebase y el archivo en Resources/Skins/)");
            }
        }
        else Debug.LogError("[SkinCardUI] skinImage sin conectar en el prefab.");

        if (buyButton == null)
        {
            Debug.LogError("[SkinCardUI] buyButton sin conectar en el prefab. Conectalo o la compra no funcionara.");
            return;
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

        if (equipMode)
        {
            if (store.IsEquipped(skin.ID)) SetButton("Equipado", false);
            else SetButton("Equipar", true);
            return;
        }

        if (store.IsEquipped(skin.ID))
        {
            SetButton("Equipado", false);
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
        if (buyButton != null) buyButton.interactable = interactable;
    }

    private void OnBuyClicked()
    {
        if (equipMode)
        {
            StoreManager.Instance?.Equip(skin.ID);
            return;
        }

        if (buyButton != null) buyButton.interactable = false;

        StoreManager.Instance.TryPurchaseSkin(skin.ID, (success, message) =>
        {
            // Sin logs: la UI lo refleja (botones y monedas se redibujan solos
            // cuando Firebase confirma la compra).
        });
    }
}
