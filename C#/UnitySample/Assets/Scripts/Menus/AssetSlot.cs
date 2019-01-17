﻿using Phantasma.Numerics;
using UnityEngine;
using UnityEngine.UI;

public enum EASSET_TYPE
{
    MY_ASSET,
    BUY_MARKET_ASSET,
    SELL_MARKET_ASSET
}

public class AssetSlot : MonoBehaviour
{
    public const float SLOT_HEIGHT = 250f;

    public Text         nameText, priceText;
    public Button       buyButton, sellButton, removeButton;
    public Image        assetImage;
    public BigInteger   auctionID;

    private Car _asset;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void SetSlot(Car car, EASSET_TYPE type)
    {
        _asset = car;

        auctionID = car.AuctionID;

        nameText.text       = car.MutableData.name;
        
        priceText.text      = PhantasmaDemo.Instance.market.Auctions[car.CarID].marketAuction.Price.ToString();
        assetImage.sprite   = car.Icon;

        switch (type)
        {
            case EASSET_TYPE.MY_ASSET:
                sellButton.gameObject.SetActive(true);

                buyButton.gameObject.SetActive(false);
                removeButton.gameObject.SetActive(false);
                break;
            case EASSET_TYPE.BUY_MARKET_ASSET:
                buyButton.gameObject.SetActive(true);

                sellButton.gameObject.SetActive(false);
                removeButton.gameObject.SetActive(false);
                break;
            case EASSET_TYPE.SELL_MARKET_ASSET:
                removeButton.gameObject.SetActive(true);

                buyButton.gameObject.SetActive(false);
                sellButton.gameObject.SetActive(false);
                break;
        }

    }

    /// <summary>
    /// Buy this asset from the market
    /// </summary>
    public void BuyAsset()
    {
        CanvasManager.Instance.ShowBuyPopup(_asset);
    }

    /// <summary>
    /// Put this asset for sale on the market
    /// </summary>
    public void SellAsset()
    {
        CanvasManager.Instance.ShowSellPopup(_asset);
    }

    /// <summary>
    /// Remove an asset that is for sale on the market
    /// </summary>
    public void RemoveAsset()
    {
        PhantasmaDemo.Instance.market.RemoveAsset(_asset);
    }
}
