// ============================================================
// PicoGacha PWA — tienda conectada a Firebase Realtime Database.
//
// Usa LOS MISMOS nodos que Unity (ver StoreManager.cs):
//   skins/{id} = { name, img, price, rarity, effectId }
//   users/{uid}/coins       (número)
//   users/{uid}/purchased/{skinId} = true
//   users/{uid}/equipped    (skinId)
// Por eso comprar aquí se ve en Unity y viceversa: ambos escuchan
// los mismos datos con listeners en tiempo real (onValue).
// ============================================================

// ---- 1) Config de TU proyecto Firebase ----
// Salen de AppStoreGTAE/Assets/google-services.json.
// Si registras una "Web app" en la consola, reemplaza appId por el tuyo
// (Auth y Database funcionan igual sin él).
const firebaseConfig = {
  apiKey: "AIzaSyAq-Pebq8pVgcgzwSBl2eiurEEmPMAviyo",
  authDomain: "testproyect-5ee30.firebaseapp.com",
  databaseURL: "https://testproyect-5ee30-default-rtdb.firebaseio.com",
  projectId: "testproyect-5ee30",
  storageBucket: "testproyect-5ee30.firebasestorage.app",
  messagingSenderId: "343347264391",
};

const STARTING_COINS = 1000; // igual que StoreManager.STARTING_COINS en Unity
const RARITY_COLORS = ["#9aa3b2", "#34d399", "#38bdf8", "#e879f9", "#fbbf24"];

// ---- 2) Arranque ----
firebase.initializeApp(firebaseConfig);
const auth = firebase.auth();
const db = firebase.database();

const $ = (id) => document.getElementById(id);
const statusEl = $("status");
function setStatus(msg) { statusEl.textContent = msg || ""; }

let uid = null;
let catalog = {};      // skinId -> { name, img, price, rarity, effectId }
let purchased = {};    // skinId -> true
let equippedId = "";
let coins = 0;

// ---- 3) Auth (misma cuenta que en Unity: email + contraseña) ----
$("btn-register").onclick = async () => {
  setStatus("Creando cuenta…");
  try {
    await auth.createUserWithEmailAndPassword($("email").value.trim(), $("password").value);
    setStatus("Cuenta creada, sesión iniciada.");
  } catch (e) { setStatus("Error: " + friendlyAuthError(e)); }
};

$("btn-login").onclick = async () => {
  setStatus("Iniciando sesión…");
  try {
    await auth.signInWithEmailAndPassword($("email").value.trim(), $("password").value);
    setStatus("Sesión iniciada.");
  } catch (e) { setStatus("Error: " + friendlyAuthError(e)); }
};

$("btn-logout").onclick = () => auth.signOut();

// Solo test: suma monedas sin pasar por la tienda.
$("btn-coins").onclick = async () => {
  if (!coinsRef) return;
  await coinsRef.transaction((c) => Number(c ?? 0) + 1000);
};

function friendlyAuthError(e) {
  switch (e.code) {
    case "auth/weak-password": return "la contraseña necesita al menos 6 caracteres.";
    case "auth/email-already-in-use": return "ya existe una cuenta con ese email.";
    case "auth/invalid-email": return "ese email no es válido.";
    case "auth/user-not-found": return "no existe una cuenta con ese email.";
    case "auth/wrong-password": return "contraseña incorrecta.";
    default: return e.message;
  }
}

// Cuando cambia la sesión: muestra/oculta paneles y (des)conecta listeners.
auth.onAuthStateChanged((user) => {
  detachUser();
  uid = user ? user.uid : null;
  $("auth-panel").classList.toggle("hidden", !!uid);
  $("app-panel").classList.toggle("hidden", !uid);
  if (uid) {
    $("user-email").textContent = user.email;
    attachUser();
  } else {
    coins = 0; purchased = {}; equippedId = "";
    renderCoins(); renderAll();
  }
});

// ---- 4) Listeners en tiempo real ----
// Catálogo público (las reglas permiten leer /skins sin sesión).
db.ref("skins").on("value", (snap) => {
  catalog = snap.val() || {};
  renderAll();
}, (err) => setStatus("Error leyendo tienda: " + err.message + " (revisa Reglas en la consola)"));

let coinsRef = null, purchasedRef = null, equippedRef = null;

function attachUser() {
  const base = db.ref("users/" + uid);
  coinsRef = base.child("coins");
  purchasedRef = base.child("purchased");
  equippedRef = base.child("equipped");

  // Jugador nuevo: saldo inicial (igual que Unity).
  coinsRef.get().then((snap) => {
    if (!snap.exists()) coinsRef.set(STARTING_COINS);
  });

  coinsRef.on("value", (s) => { coins = s.val() ?? 0; renderCoins(); renderAll(); });
  purchasedRef.on("value", (s) => { purchased = s.val() || {}; renderAll(); });
  equippedRef.on("value", (s) => { equippedId = s.val() || ""; renderAll(); });
}

function detachUser() {
  if (coinsRef) coinsRef.off();
  if (purchasedRef) purchasedRef.off();
  if (equippedRef) equippedRef.off();
  coinsRef = purchasedRef = equippedRef = null;
}

// ---- 5) Compra (igual que Unity: transacción en coins + marca purchased) ----
async function buySkin(id) {
  const skin = catalog[id];
  if (!skin || purchased[id]) return;
  setStatus("Comprando " + skin.name + "…");
  try {
    // Transacción atómica: descuenta solo si alcanza (evita doble gasto
    // si compras a la vez en Unity y aquí).
    const res = await coinsRef.transaction((current) => {
      const c = Number(current ?? 0);
      if (c < skin.price) return; // aborta = undefined
      return c - skin.price;
    });
    if (!res.committed) { setStatus(`Te faltan monedas (${coins}/${skin.price}).`); return; }
    await purchasedRef.child(id).set(true);
    await equippedRef.set(id); // auto-equipa como en Unity
    // Sin mensaje de éxito: la UI ya lo refleja (botón "Equipado", monedas).
  } catch (e) {
    setStatus("Error comprando (¿permisos? revisa Reglas): " + e.message);
  }
}

async function equipSkin(id) {
  if (!purchased[id]) return;
  await equippedRef.set(id);
}

// Solo test: "vende" la skin (la quita de purchased y devuelve las monedas).
async function sellSkin(id) {
  const skin = catalog[id];
  if (!skin || !purchased[id]) return;
  if (!confirm(`Vender "${skin.name}" por ${skin.price} monedas? (solo test)`)) return;
  try {
    await coinsRef.transaction((c) => Number(c ?? 0) + skin.price);
    await purchasedRef.child(id).remove();
    if (equippedId === id) await equippedRef.remove();
    // Sin mensaje: la UI lo refleja (la carta vuelve a "Comprar" / desaparece de Mis objetos).
  } catch (e) {
    setStatus("Error vendiendo: " + e.message);
  }
}

// ---- 6) Render ----
$("tab-store").onclick = () => switchTab(true);
$("tab-owned").onclick = () => switchTab(false);

function switchTab(store) {
  $("tab-store").classList.toggle("active", store);
  $("tab-owned").classList.toggle("active", !store);
  $("store-grid").classList.toggle("hidden", !store);
  $("owned-grid").classList.toggle("hidden", store);
  $("owned-empty").classList.toggle("hidden", store || ownedIds().length > 0);
}

function ownedIds() { return Object.keys(catalog).filter((id) => purchased[id]); }

function renderCoins() { $("coins").textContent = "Monedas: " + (uid ? coins : "--"); }

function cardEl(id, mode) {
  const skin = catalog[id];
  const card = document.createElement("div");
  card.className = "skin-card";
  card.style.borderTopColor = RARITY_COLORS[skin.rarity] || "#f87171";

  const img = document.createElement("img");
  img.alt = skin.name;
  // Las imágenes viven en pwa/img/ con el MISMO nombre que el campo "img".
  img.src = "./img/" + skin.img + ".png";
  img.onerror = () => { img.style.visibility = "hidden"; }; // ej. policia.png aún no existe
  card.appendChild(img);

  const name = document.createElement("h3");
  name.textContent = skin.name;
  card.appendChild(name);

  const price = document.createElement("p");
  price.className = "price";
  price.textContent = skin.price + " monedas";
  card.appendChild(price);

  if (skin.effectId) {
    const fx = document.createElement("p");
    fx.className = "fx";
    fx.textContent = "✨ efecto 3D en Unity: " + skin.effectId;
    card.appendChild(fx);
  }

  const btn = document.createElement("button");
  if (mode === "store") {
    if (!uid) { btn.textContent = "Inicia sesión"; btn.disabled = true; }
    else if (equippedId === id) { btn.textContent = "Equipado"; btn.disabled = true; }
    else if (purchased[id]) { btn.textContent = "Comprada"; btn.disabled = true; }
    else if (coins < skin.price) { btn.textContent = "Sin saldo"; btn.disabled = true; }
    else { btn.textContent = "Comprar"; btn.onclick = () => buySkin(id); }
  } else {
    if (equippedId === id) { btn.textContent = "Equipado"; btn.disabled = true; }
    else { btn.textContent = "Equipar"; btn.onclick = () => equipSkin(id); }
    const sell = document.createElement("button");
    sell.textContent = "Vender (test)";
    sell.className = "ghost small sell";
    sell.onclick = () => sellSkin(id);
    const wrap = document.createElement("div");
    wrap.appendChild(btn);
    wrap.appendChild(sell);
    card.appendChild(wrap);
    return card;
  }
  card.appendChild(btn);
  return card;
}

function renderAll() {
  const store = $("store-grid");
  store.innerHTML = "";
  Object.keys(catalog).forEach((id) => store.appendChild(cardEl(id, "store")));

  const owned = $("owned-grid");
  owned.innerHTML = "";
  const ids = ownedIds();
  ids.forEach((id) => owned.appendChild(cardEl(id, "owned")));
  $("owned-count").textContent = ids.length ? `(${ids.length})` : "";
  $("owned-empty").classList.toggle("hidden", $("tab-owned").classList.contains("hidden") || ids.length > 0);
}

renderCoins();

// ---- 7) PWA: registra el service worker (solo funciona con http/https,
// NO abriendo el archivo con doble clic) ----
if ("serviceWorker" in navigator && location.protocol.startsWith("http")) {
  window.addEventListener("load", () => {
    navigator.serviceWorker.register("./sw.js").catch((e) => console.log("SW no registrado:", e));
  });
}
