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

    private DatabaseReference dbRoot;
    private string uid; // null si no hay sesion iniciada

    private readonly Dictionary<string, SkinData> currentSkins = new Dictionary<string, SkinData>();
    private readonly HashSet<string> purchasedSkinIds = new HashSet<string>();

    public long CurrentCoins { get; private set; }
    public bool HasUser => uid != null;

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
        CurrentCoins = 0;
        UpdateCoinsText();

        if (uid == null)
        {
            RedrawCards();
            return;
        }

        var coinsRef = dbRoot.Child("users").Child(uid).Child("coins");
        var purchasedRef = dbRoot.Child("users").Child(uid).Child("purchased");

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
    }

    private void DetachPlayerListeners()
    {
        if (dbRoot == null || uid == null) return;
        dbRoot.Child("users").Child(uid).Child("coins").ValueChanged -= OnCoinsChanged;
        dbRoot.Child("users").Child(uid).Child("purchased").ValueChanged -= OnPurchasedChanged;
    }

    private void OnCoinsChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("Error leyendo monedas: " + args.DatabaseError.Message);
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
            Debug.LogError("Error leyendo compras: " + args.DatabaseError.Message);
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

    private void UpdateCoinsText()
    {
        if (coinsText != null) coinsText.text = "Monedas: " + CurrentCoins;
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

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var skin in currentSkins.Values)
        {
            var card = Instantiate(cardPrefab, contentParent);
            card.Setup(skin);
        }
    }

    // ---------------------------------------------------------------
    //  compra de skins
    // ---------------------------------------------------------------

    public bool IsPurchased(string skinId) => purchasedSkinIds.Contains(skinId);

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
