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


    }
}
