using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SkinCardUI : MonoBehaviour
{
    [SerializeField] private Image skinImage;
    [SerializeField] private Image rarity;
    [SerializeField] private TMP_Text skinName;
    [SerializeField] private TMP_Text priceText;

    public void SetUp(SkinData skinData)
    {
        skinName.text = skinData.name;
        priceText.text = skinData.price.ToString();

        var sprite = Resources.Load<Sprite>("Skins/" + skinData.img);

        if(sprite != null)
        {
            skinImage.sprite = sprite;
        }
        else
        {
            Debug.LogWarning("Sprite not found for skin: " + skinData.img);
        }

        int rarity = skinData.rarity;
        Color rarityColor = Color.white;
        
        switch(rarity)
        {
            case 0:
                rarityColor = Color.gray; // Common
                break;
            case 1:
                rarityColor = Color.green; // Uncommon
                break;
            case 2:
                rarityColor = Color.blue; // Rare
                break;
            case 3:
                rarityColor = Color.magenta; // Epic
                break;
            case 4:
                rarityColor = Color.yellow; // Legendary
                break;
            default:
                rarityColor = Color.red;
                break;
        }



    }
}
