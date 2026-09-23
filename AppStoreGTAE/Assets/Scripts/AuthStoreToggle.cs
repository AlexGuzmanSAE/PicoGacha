using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Un solo boton para saltar entre Log In y Store.
// Vive en el Canvas (siempre visible): si ves el login dice "Store",
// si ves la tienda dice "Log In".
// Ademas escucha a AuthManager: al iniciar sesion manda a la tienda,
// al cerrar sesion manda al login.
public class AuthStoreToggle : MonoBehaviour
{
    public static AuthStoreToggle Instance { get; private set; }

    [Header("Paneles")]
    [SerializeField] private GameObject authPanel;
    [SerializeField] private GameObject storePanel;

    [Header("Boton")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleLabel;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveAllListeners();
            toggleButton.onClick.AddListener(Toggle);
        }
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnLoginStateChanged += HandleAuth;
            HandleAuth(AuthManager.Instance.CurrentUser);
        }
        else
        {
            ShowAuth();
        }
    }

    private void HandleAuth(Firebase.Auth.FirebaseUser user)
    {
        if (user != null) ShowStore();
        else ShowAuth();
    }

    public void Toggle()
    {
        bool authVisible = authPanel != null && authPanel.activeSelf;
        if (authVisible) ShowStore();
        else ShowAuth();
    }

    public void ShowAuth()
    {
        if (authPanel != null) authPanel.SetActive(true);
        if (storePanel != null) storePanel.SetActive(false);
        SetLabel("Store");
    }

    public void ShowStore()
    {
        if (authPanel != null) authPanel.SetActive(false);
        if (storePanel != null) storePanel.SetActive(true);
        SetLabel("Log In");
    }

    private void SetLabel(string text)
    {
        if (toggleLabel != null) toggleLabel.text = text;
    }

    private void OnDestroy()
    {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnLoginStateChanged -= HandleAuth;
        if (Instance == this) Instance = null;
    }
}
