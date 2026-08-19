using UnityEngine;
using TMPro;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;


public class StoreManager : MonoBehaviour
{
    [SerializeField] private Transform contentPanel;
    [SerializeField] private GameObject skinCardPrefab;

    private DatabaseReference dbRoot;

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogError("No se pudieron resolver las dependencias de Firebase: " + task.Result);
                return;
            }
            else
            {
                dbRoot = FirebaseDatabase.DefaultInstance.RootReference;
                dbRoot.Child("skins").ValueChanged += OnSkinsChanged;
            }
        });    
    }

    private void OnSkinsChanged(object sender, ValueChangedEventArgs args)
    {
        if(args.DatabaseError != null)
        {
            Debug.LogError("Error al obtener datos de skins: " + args.DatabaseError.Message);
            return;
        }

        foreach(Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }

        if(args.Snapshot == null) return;

        foreach(var child in args.Snapshot.Children)
        {
            if(child.Value is IDictionary<string, object> skinDataDict)
            {
                var skin = SkinData.FromDictionary(child.Key, skinDataDict);
                GameObject skinCard = Instantiate(skinCardPrefab, contentPanel);
                SkinCardUI cardUI = skinCard.GetComponent<SkinCardUI>();
                cardUI.SetUp(skin);
            }
        }

    }

    private void OnDestroy()
    {
        if(dbRoot != null)
        {
            dbRoot.Child("skins").ValueChanged -= OnSkinsChanged;
        }
    }

}
