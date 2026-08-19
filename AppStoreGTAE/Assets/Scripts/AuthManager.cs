using System;
using UnityEngine;
using UnityEngine.UI;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using TMPro;

// Registro y login con Firebase Authentication (email/contraseña).
// Todo en un solo lugar: cuando el jugador aprieta un boton, hablamos
// directo con Firebase, sin pasos intermedios.
//
// Antes de probar: en la consola de Firebase, ve a
// Authentication -> Sign-in method -> habilita "Correo electronico/contraseña".
//
// Como poner esto en la escena:
// 1) Crea un GameObject y agregale este script.
// 2) Arrastra los InputFields, botones y el texto de mensajes en el inspector.
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    [Header("Inputs")]
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("Botones")]
    [SerializeField] private Button registerButton;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button logoutButton;

    [Header("Feedback")]
    [SerializeField] private Text messageText;

    // opcional: se activan/desactivan solos segun si hay sesion o no
    [Header("Paneles (opcional)")]
    [SerializeField] private GameObject panelAuth;
    [SerializeField] private GameObject panelLogueado;

    private FirebaseAuth auth;

    public FirebaseUser CurrentUser { get; private set; }

    // otros scripts se pueden suscribir a esto para saber
    // cuando el jugador inicio o cerro sesion
    public event Action<FirebaseUser> OnLoginStateChanged;

    private void Awake()
    {
        // evita que se creen dos AuthManager si recargas la escena
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        registerButton.onClick.AddListener(OnRegisterClicked);
        loginButton.onClick.AddListener(OnLoginClicked);
        logoutButton.onClick.AddListener(OnLogoutClicked);
    }

    private void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                SetMessage("Error de Firebase: " + task.Result);
                return;
            }

            auth = FirebaseAuth.DefaultInstance;
            auth.StateChanged += OnAuthStateChanged;
            OnAuthStateChanged(this, EventArgs.Empty); // por si ya habia una sesion guardada
        });
    }

    // se llama solo cada vez que cambia la sesion (login, registro o logout)
    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        CurrentUser = auth.CurrentUser;
        bool loggedIn = CurrentUser != null;

        if (panelAuth != null) panelAuth.SetActive(!loggedIn);
        if (panelLogueado != null) panelLogueado.SetActive(loggedIn);

        OnLoginStateChanged?.Invoke(CurrentUser);
    }

    // ---- lo que pasa al apretar cada boton ----

    private void OnRegisterClicked()
    {
        SetMessage("Creando cuenta...");

        auth.CreateUserWithEmailAndPasswordAsync(emailInput.text, passwordInput.text)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    SetMessage("Error al registrarse: " + FriendlyError(task.Exception));
                    return;
                }

                SetMessage("Cuenta creada, ya iniciaste sesion.");
            });
    }

    private void OnLoginClicked()
    {
        SetMessage("Iniciando sesion...");

        auth.SignInWithEmailAndPasswordAsync(emailInput.text, passwordInput.text)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    SetMessage("Error al iniciar sesion: " + FriendlyError(task.Exception));
                    return;
                }

                SetMessage("Sesion iniciada.");
            });
    }

    private void OnLogoutClicked()
    {
        auth.SignOut();
        SetMessage("Sesion cerrada.");
    }

    private void SetMessage(string message)
    {
        if (messageText != null) messageText.text = message;
        Debug.Log("[AuthManager] " + message);
    }

    // traduce los errores mas comunes de Firebase a un mensaje simple.
    // si no reconoce el error, muestra el mensaje original de Firebase.
    private string FriendlyError(Exception exception)
    {
        var firebaseEx = exception?.GetBaseException() as FirebaseException;
        if (firebaseEx == null) return exception?.Message ?? "error desconocido";

        switch ((AuthError)firebaseEx.ErrorCode)
        {
            case AuthError.WeakPassword: return "la contraseña necesita al menos 6 caracteres.";
            case AuthError.EmailAlreadyInUse: return "ya existe una cuenta con ese email.";
            case AuthError.InvalidEmail: return "ese email no es valido.";
            case AuthError.WrongPassword: return "contraseña incorrecta.";
            case AuthError.UserNotFound: return "no existe una cuenta con ese email.";
            default: return firebaseEx.Message;
        }
    }

    private void OnDestroy()
    {
        if (auth != null) auth.StateChanged -= OnAuthStateChanged;
    }
}
