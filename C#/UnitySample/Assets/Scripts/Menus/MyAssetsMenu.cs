﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MyAssetsMenu : MonoBehaviour
{
    public GameObject   galleryContent;
    public Text         message;
    public AssetSlot    assetSlot;

    private List<AssetSlot> _assetSlots;

    void Awake()
    {
        _assetSlots = new List<AssetSlot>();
    }

    void OnEnable()
    {
        ClearErrorContent();

        //Debug.Log("my: " + PhantasmaDemo.Instance.MyCars.Count + " | slots: " + _assetSlots.Count);
        //if (_assetSlots.Count != PhantasmaDemo.Instance.MyCars.Keys.Count)
        //{
            UpdateMyAssets();
        //}
    }

    public void UpdateMyAssets()
    {
        foreach (var slot in _assetSlots)
        {
            DestroyImmediate(slot.gameObject);
        }

        _assetSlots.Clear();

        //Debug.Log("my assets: " + PhantasmaDemo.Instance.MyCars.Keys.Count);
        var tokenIds = new List<string>(PhantasmaDemo.Instance.MyCars.Keys);

        if (tokenIds.Count == 0)
        {
            ShowMessage("There are no assets on your account.");
            return;
        }

        for (var i = 0; i < tokenIds.Count; i++)
        {
            var asset = PhantasmaDemo.Instance.MyCars[tokenIds[i]];

            var newSlot                     = Instantiate(assetSlot, galleryContent.transform, false);
            newSlot.transform.localPosition += Vector3.down * AssetSlot.SLOT_HEIGHT * i;
            newSlot.SetSlot(asset, EASSET_TYPE.MY_ASSET);
            newSlot.gameObject.SetActive(true);

            _assetSlots.Add(newSlot);
        }
    }

    public void ShowMessage(string msg)
    {
        message.text = msg;
        message.gameObject.SetActive(true);
    }

    private void ClearErrorContent()
    {
        message.text = string.Empty;
        message.gameObject.SetActive(false);
    }

    public void BackClicked()
    {
        CanvasManager.Instance.CloseMyAssets();
    }
}


