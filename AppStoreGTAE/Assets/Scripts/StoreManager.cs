using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using Firebase.Auth;
using TMPro;

// Lee la lista de skins desde Firebase Realtime Database, muestra una
// tarjeta por cada una, y maneja la compra: monedas fake y la lista de
// skins que ya compro el jugador.
//
// Necesita a AuthManager en la escena (para saber quien es el jugador).
// Cuando cambia algo (las skins, las monedas o lo comprado) se vuelve a
// dibujar toda la tienda, asi que las tarjetas siempre muestran el estado
// correcto sin tener que sincronizar nada a mano.
//
// Como armar la escena:
// 1) Crea un GameObject vacio y agregale este script.
// 2) Arrastra el "Content" de tu ScrollView (con un Grid Layout Group) en
//    "contentParent".
// 3) Arrastra el prefab de tarjeta (el que tiene SkinCardUI) en "cardPrefab".
// 4) Opcional: arrastra un Text en "coinsText" para mostrar el saldo.
//
// El JSON de ejemplo esta en el README.
public class StoreManager : MonoBehaviour
{
    public static StoreManager Instance { get; private set; }

    private const long STARTING_COINS = 1000;

    [Header("Tienda")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private SkinCardUI cardPrefab;

    [Header("Monedas (opcional)")]
    [SerializeField] private TMP_Text coinsText;

    [Header("Avisos (opcional: muestra errores como el de permisos)")]
    [SerializeField] private TMP_Text statusText;

    private DatabaseReference dbRoot;
    private string uid; // null si no hay sesion iniciada

    // Se guardan las instancias para el detach: quitar el listener con un
    // Child() nuevo no remueve nada y los listeners se duplican.
    private DatabaseReference coinsRef;
    private DatabaseReference purchasedRef;
    private DatabaseReference equippedRef;

    private readonly Dictionary<string, SkinData> currentSkins = new Dictionary<string, SkinData>();
    private readonly HashSet<string> purchasedSkinIds = new HashSet<string>();

    public long CurrentCoins { get; private set; }
    public bool HasUser => uid != null;
    public string EquippedId { get; private set; } = "";
    public IReadOnlyDictionary<string, SkinData> Catalog => currentSkins;

    // OwnedScreen y SkinPreview3D se suscriben para refrescarse solos.
    public event Action OnChanged;
    public event Action<string> OnEquippedChanged;

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
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogError("Error de Firebase: " + task.Result);
                return;
            }

            dbRoot = FirebaseDatabase.DefaultInstance.RootReference;
            dbRoot.Child("skins").ValueChanged += OnSkinsChanged;

            WaitForAuthManager();
        });
    }

    // AuthManager puede tardar un frame en inicializarse, asi que
    // reintentamos hasta que exista antes de suscribirnos
    private void WaitForAuthManager()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnLoginStateChanged += OnLoginStateChanged;
            OnLoginStateChanged(AuthManager.Instance.CurrentUser);
        }
        else
        {
            Invoke(nameof(WaitForAuthManager), 0.1f);
        }
    }

    // ---------------------------------------------------------------
    //  sesion del jugador: monedas y skins compradas
    // ---------------------------------------------------------------

    private void OnLoginStateChanged(FirebaseUser user)
    {
        DetachPlayerListeners();

        uid = user != null ? user.UserId : null;
        purchasedSkinIds.Clear();
        EquippedId = "";
        CurrentCoins = 0;
        UpdateCoinsText();

        if (uid == null)
        {
            RedrawCards();
            return;
        }

        coinsRef = dbRoot.Child("users").Child(uid).Child("coins");
        purchasedRef = dbRoot.Child("users").Child(uid).Child("purchased");
        equippedRef = dbRoot.Child("users").Child(uid).Child("equipped");

        // si el jugador es nuevo, le damos las monedas iniciales
        coinsRef.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && !task.IsFaulted && !task.Result.Exists)
            {
                coinsRef.SetValueAsync(STARTING_COINS);
            }
        });

        coinsRef.ValueChanged += OnCoinsChanged;
        purchasedRef.ValueChanged += OnPurchasedChanged;
        equippedRef.ValueChanged += OnEquippedValue;
    }

    private void DetachPlayerListeners()
    {
        if (coinsRef != null) coinsRef.ValueChanged -= OnCoinsChanged;
        if (purchasedRef != null) purchasedRef.ValueChanged -= OnPurchasedChanged;
        if (equippedRef != null) equippedRef.ValueChanged -= OnEquippedValue;
        coinsRef = null;
        purchasedRef = null;
        equippedRef = null;
    }

    private void OnCoinsChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            ReportDbError("monedas", args.DatabaseError);
            return;
        }

        long coins = 0;
        if (args.Snapshot != null && args.Snapshot.Exists)
        {
            long.TryParse(args.Snapshot.Value.ToString(), out coins);
        }

        CurrentCoins = coins;
        UpdateCoinsText();
        RedrawCards();
    }

    private void OnPurchasedChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            ReportDbError("compras", args.DatabaseError);
            return;
        }

        purchasedSkinIds.Clear();
        if (args.Snapshot != null)
        {
            foreach (var child in args.Snapshot.Children)
            {
                purchasedSkinIds.Add(child.Key);
            }
        }

        RedrawCards();
    }

    private void OnEquippedValue(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            ReportDbError("equipped", args.DatabaseError);
            return;
        }

        EquippedId = (args.Snapshot != null && args.Snapshot.Exists)
            ? args.Snapshot.Value.ToString()
            : "";
        // Si lo equipaste en la PWA, el accesorio 3D cambia aqui solo.
        OnEquippedChanged?.Invoke(EquippedId);
        RedrawCards();
    }

    private void UpdateCoinsText()
    {
        if (coinsText != null) coinsText.text = "Monedas: " + CurrentCoins;
    }

    // Centraliza errores de lectura: log + aviso visible si hay statusText.
    // El "permission denied" casi siempre = reglas de RTDB en la consola.
    private void ReportDbError(string what, DatabaseError error)
    {
        Debug.LogError("Error leyendo " + what + ": " + error.Message);
        if (error.Message != null && error.Message.ToLower().Contains("permission"))
        {
            SetStatus("Sin permiso en Firebase (" + what + "). Revisa Realtime Database > Reglas en la consola.");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    // ---------------------------------------------------------------
    //  catalogo de skins
    // ---------------------------------------------------------------

    private void OnSkinsChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("Error leyendo skins: " + args.DatabaseError.Message);
            return;
        }

        currentSkins.Clear();
        if (args.Snapshot != null)
        {
            foreach (var child in args.Snapshot.Children)
            {
                if (child.Value is IDictionary<string, object> dict)
                {
                    var skin = SkinData.FromDictionary(child.Key, dict);
                    currentSkins[skin.ID] = skin;
                }
            }
        }

        RedrawCards();
    }

    private void RedrawCards()
    {
        if (contentParent == null || cardPrefab == null) return;

        EnsureStoreLayout();

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var skin in currentSkins.Values)
        {
            var card = Instantiate(cardPrefab, contentParent);
            // Altura fija ante el VerticalLayoutGroup (por si el prefab
            // no trae LayoutElement).
            var layout = card.GetComponent<LayoutElement>();
            if (layout == null) layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 1070;
            card.Setup(skin);
        }

        OnChanged?.Invoke();
    }

    // Deja el Content como el template oficial de ScrollView:
    // estirado arriba, pivote arriba, grupo vertical + fitter vertical.
    // Asi el scroll funciona aunque la escena venga mal configurada.
    private void EnsureStoreLayout()
    {
        var rt = contentParent as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = Vector2.zero;
        }

        var group = contentParent.GetComponent<VerticalLayoutGroup>();
        if (group == null) group = contentParent.gameObject.AddComponent<VerticalLayoutGroup>();
        group.spacing = 20;
        group.childAlignment = TextAnchor.UpperCenter;
        group.childControlWidth = false;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;

        var fitter = contentParent.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = contentParent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // ---------------------------------------------------------------
    //  compra de skins
    // ---------------------------------------------------------------

    public bool IsPurchased(string skinId) => purchasedSkinIds.Contains(skinId);
    public bool IsEquipped(string skinId) => !string.IsNullOrEmpty(skinId) && EquippedId == skinId;

    // Marca una skin comprada como equipada. El accesorio 3D reacciona
    // al cambio via OnEquippedChanged (tanto si equipas aqui como en la PWA).
    public void Equip(string skinId)
    {
        if (uid == null || dbRoot == null) return;
        if (!IsPurchased(skinId))
        {
            Debug.LogWarning("[Store] No puedes equipar lo que no tienes: " + skinId);
            return;
        }
        dbRoot.Child("users").Child(uid).Child("equipped").SetValueAsync(skinId)
            .ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) Debug.LogError("[Store] Equip error: " + t.Exception?.Message);
            });
    }

    // intenta comprar una skin: valida saldo, descuenta monedas de forma
    // atomica (transaccion) y agrega la skin a "purchased".
    //
    // ojo: esta validacion corre en el cliente, suficiente para una clase,
    // pero un jugador con malas intenciones podria manipularla. Para
    // produccion de verdad, esto se mueve a una Cloud Function.
    public void TryPurchaseSkin(string skinId, Action<bool, string> onComplete)
    {
        if (uid == null)
        {
            onComplete?.Invoke(false, "Tienes que iniciar sesion para comprar.");
            return;
        }

        if (!currentSkins.TryGetValue(skinId, out var skin))
        {
            onComplete?.Invoke(false, "Esa skin ya no existe.");
            return;
        }

        if (purchasedSkinIds.Contains(skinId))
        {
            onComplete?.Invoke(false, "Ya tienes esta skin.");
            return;
        }

        var coinsRef = dbRoot.Child("users").Child(uid).Child("coins");

        // transaccion atomica: evita comprar dos veces si el jugador
        // hace doble clic
        coinsRef.RunTransaction(mutableData =>
        {
            long coins = 0;
            if (mutableData.Value != null)
            {
                long.TryParse(mutableData.Value.ToString(), out coins);
            }

            if (coins < skin.price)
            {
                return TransactionResult.Abort();
            }

            mutableData.Value = coins - skin.price;
            return TransactionResult.Success(mutableData);
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                onComplete?.Invoke(false, "No te alcanzan las monedas.");
                return;
            }

            dbRoot.Child("users").Child(uid).Child("purchased").Child(skinId).SetValueAsync(true)
                .ContinueWithOnMainThread(_ =>
                {
                    // Auto-equipa al comprar para que el efecto 3D se vea al instante.
                    Equip(skinId);
                    onComplete?.Invoke(true, "Compraste " + skin.name + ".");
                });
        });
    }

    private void OnDestroy()
    {
        if (dbRoot != null)
        {
            dbRoot.Child("skins").ValueChanged -= OnSkinsChanged;
        }
        DetachPlayerListeners();

        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnLoginStateChanged -= OnLoginStateChanged;
        }
    }
}
