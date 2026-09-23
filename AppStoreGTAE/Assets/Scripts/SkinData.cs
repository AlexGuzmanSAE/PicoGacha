using System;
using System.Collections.Generic;
using UnityEngine;

public class SkinData
{
    public string ID;
    public string img;
    public string name;
    public int price;
    public int rarity;
    // Opcional: id del efecto 3D (ej "cap_blue"). Si el nodo no lo trae,
    // queda "" = sin efecto, no rompe las skins existentes.
    public string effectId;

    public static SkinData FromDictionary(string key, IDictionary<string, object> data)
    {
        var skin = new SkinData();
        skin.ID = key;
        skin.name = data.ContainsKey("name") ? data["name"].ToString() : "";
        skin.img = data.ContainsKey("img") ? data["img"].ToString() : "";


        if(data.ContainsKey("price"))
        {
            int.TryParse(data["price"].ToString(), out skin.price);
        }
        
        if(data.ContainsKey("rarity"))
        {
            int.TryParse(data["rarity"].ToString(), out skin.rarity);
        }

        skin.effectId = data.ContainsKey("effectId") ? data["effectId"].ToString() : "";

        return skin;
    }
}
