using System;
using System.Collections.Generic;
using UnityEngine;

// Config de efectos 3D por skin. Sin modelar nada: primitivos de Unity.
// Cada fila une una skin (por effectId de Firebase o por skinId directo)
// con un accesorio sobre la cabeza.
// Funciona desde ya con tus skins actuales (skin_001/skin_002) sin tocar
// Firebase: si la skin no trae effectId, se usa el skinId.
[CreateAssetMenu(fileName = "SkinEffectConfig", menuName = "PicoGacha/SkinEffectConfig")]
public class SkinEffectConfig : ScriptableObject
{
    public enum PrimitiveKind { Sphere, Cube, Cylinder, Capsule }

    [Serializable]
    public class EffectEntry
    {
        public string effectId = "cap_blue";
        public string skinId = "";
        public PrimitiveKind primitive = PrimitiveKind.Sphere;
        public Color color = Color.blue;
        public Vector3 localOffset = new Vector3(0, 0.35f, 0);
        public Vector3 localScale = new Vector3(0.35f, 0.2f, 0.35f);
    }

    [SerializeField] private List<EffectEntry> effects = new List<EffectEntry>
    {
        new EffectEntry { effectId = "cap_blue", skinId = "skin_003", primitive = PrimitiveKind.Sphere, color = Color.blue, localOffset = new Vector3(0, 0.35f, 0), localScale = new Vector3(0.35f, 0.2f, 0.35f) },
        new EffectEntry { effectId = "helmet_yellow", skinId = "skin_002", primitive = PrimitiveKind.Cylinder, color = Color.yellow, localOffset = new Vector3(0, 0.35f, 0), localScale = new Vector3(0.35f, 0.25f, 0.35f) },
        new EffectEntry { effectId = "hat_purple", skinId = "skin_001", primitive = PrimitiveKind.Capsule, color = new Color(0.6f, 0.2f, 0.8f), localOffset = new Vector3(0, 0.45f, 0), localScale = new Vector3(0.3f, 0.4f, 0.3f) },
    };

    public bool TryGet(string effectId, out EffectEntry entry)
    {
        foreach (var e in effects)
        {
            if (e != null && !string.IsNullOrEmpty(e.effectId) && e.effectId == effectId)
            {
                entry = e;
                return true;
            }
        }
        entry = null;
        return false;
    }

    // Resuelve el efecto de una skin: primero por effectId (Firebase),
    // si no trae, por skinId (funciona sin cambiar la base de datos).
    public bool TryGetForSkin(SkinData skin, out EffectEntry entry)
    {
        entry = null;
        if (skin == null) return false;
        if (!string.IsNullOrEmpty(skin.effectId) && TryGet(skin.effectId, out entry))
            return true;
        foreach (var e in effects)
        {
            if (e != null && !string.IsNullOrEmpty(e.skinId) && e.skinId == skin.ID)
            {
                entry = e;
                return true;
            }
        }
        return false;
    }
}
