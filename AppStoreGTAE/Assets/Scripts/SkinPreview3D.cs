using System;
using System.Collections.Generic;
using UnityEngine;

public class SkinPreview3D : MonoBehaviour
{
    [Serializable]
    public class WardrobeSlot
    {
        public string key = "";
        public GameObject accessory;
    }


    [SerializeField] private string characterName = "PicoChan";
    [SerializeField] private Transform characterRoot;


    [SerializeField] private List<WardrobeSlot> slots = new List<WardrobeSlot>();

    [SerializeField] private string accessoryPrefix = "Accessory_";

    private void Start()
    {
        if (characterRoot == null && !string.IsNullOrEmpty(characterName))
        {
            var character = GameObject.Find(characterName);
            if (character != null) characterRoot = character.transform;
        }
        if (StoreManager.Instance != null)
        {
            StoreManager.Instance.OnEquippedChanged += HandleEquipped;
            StoreManager.Instance.OnChanged += RefreshFromStore;
        }
        else
        {
            Invoke(nameof(RetrySubscribe), 0.5f);
        }
        HideAll();
        RefreshFromStore();
    }

    private void RetrySubscribe()
    {
        if (StoreManager.Instance != null)
        {
            StoreManager.Instance.OnEquippedChanged += HandleEquipped;
            StoreManager.Instance.OnChanged += RefreshFromStore;
            RefreshFromStore();
        }
    }

    private void HandleEquipped(string equippedId)
    {
        RefreshFromStore();
    }

    private void RefreshFromStore()
    {
        if (StoreManager.Instance == null) return;
        string id = StoreManager.Instance.EquippedId;
        if (string.IsNullOrEmpty(id))
        {
            HideAll();
            return;
        }
        string key = id;
        if (StoreManager.Instance.Catalog.TryGetValue(id, out SkinData skin) &&
            !string.IsNullOrEmpty(skin.effectId))
        {
            key = skin.effectId;
        }
        ShowKey(key);
    }

    public void ShowEffect(string effectId)
    {
        if (string.IsNullOrEmpty(effectId)) HideAll();
        else ShowKey(effectId);
    }

    public void ClearAccessory()
    {
        HideAll();
    }

    private bool HasWiredSlots()
    {
        if (slots == null) return false;
        foreach (var s in slots)
        {
            if (s != null && s.accessory != null) return true;
        }
        return false;
    }

    private void ShowKey(string key)
    {
        if (HasWiredSlots())
        {
            foreach (var s in slots)
            {
                if (s == null || s.accessory == null) continue;
                s.accessory.SetActive(s.key == key);
            }
            return;
        }
        if (characterRoot == null) return;
        foreach (Transform child in characterRoot)
        {
            string n = child.name;
            if (!n.StartsWith(accessoryPrefix)) continue;
            string rest = n.Substring(accessoryPrefix.Length);
            child.gameObject.SetActive(rest == key || rest.StartsWith(key + "_"));
        }
    }

    private void HideAll()
    {
        if (HasWiredSlots())
        {
            foreach (var s in slots)
            {
                if (s == null || s.accessory == null) continue;
                s.accessory.SetActive(false);
            }
            return;
        }
        if (characterRoot == null) return;
        foreach (Transform child in characterRoot)
        {
            if (child.name.StartsWith(accessoryPrefix))
                child.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (StoreManager.Instance != null)
        {
            StoreManager.Instance.OnEquippedChanged -= HandleEquipped;
            StoreManager.Instance.OnChanged -= RefreshFromStore;
        }
    }
}
