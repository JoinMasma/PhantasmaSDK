﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Numerics;
using Phantasma.SDK;
using Phantasma.VM.Utils;

/*
 * Phantasma Spook
 * https://github.com/phantasma-io/PhantasmaSpook/tree/master/Docs
 */

public class PhantasmaDemo : MonoBehaviour
{
    private const string _SERVER_ADDRESS = "http://localhost:7077/rpc";

    public KeyPair Key { get; private set; }

    private enum EWALLET_STATE
    {
        INIT,
        SYNC,
        UPDATE,
        READY
    }

    /*
    public enum EventKind
    {
        ChainCreate,
        TokenCreate,
        TokenSend,
        TokenReceive,
        TokenMint,
        TokenBurn,
        TokenEscrow,
        AddressRegister,
        FriendAdd,
        FriendRemove,
        GasEscrow,
        GasPayment,
    }
     */

    public Market       market;
    public List<Sprite> carImages;

    private EWALLET_STATE   _state = EWALLET_STATE.INIT;
    private decimal         _balance;

    public API          PhantasmaApi    { get; private set; }
    public bool         IsTokenCreated  { get; private set; }
    public bool         IsTokenOwner    { get; private set; }
    public List<Car>    MyCars          { get; set; }
    
    private static PhantasmaDemo _instance;
    public static PhantasmaDemo Instance
    {
        get { _instance = _instance == null ? FindObjectOfType(typeof(PhantasmaDemo)) as PhantasmaDemo : _instance; return _instance; }
    }

    private void Awake()
    {
        MyCars = new List<Car>();
    }

    private void Start ()
    {
        //GetAccount("P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr"); //TEST

        PhantasmaApi = new API("http://localhost:7077/rpc");
    }

    private IEnumerator SyncBalance()
    {
        yield return new WaitForSeconds(2);
        //var balances = api.GetAssetBalancesOf(this.keys);
        //balance = balances.ContainsKey(assetSymbol) ? balances[assetSymbol] : 0;
        _state = EWALLET_STATE.UPDATE;
    }

    private void Update () {

        switch (_state)
        {
            case EWALLET_STATE.INIT:
                {
                    _state = EWALLET_STATE.SYNC;
                    StartCoroutine(SyncBalance());
                    break;
                }

            case EWALLET_STATE.UPDATE:
                {
                    _state = EWALLET_STATE.READY;
                    CanvasManager.Instance.accountMenu.SetBalance(_balance.ToString(CultureInfo.InvariantCulture));
                    break;
                }
        }		
	}

    public void Login(string privateKey)
    {
        Key = KeyPair.FromWIF(privateKey);

        GetAccount(Key.Address.ToString());
    }

    private void LoggedIn(string address)
    {
        Debug.Log("logged in: " + address);

        //var address = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25";
        //var addressBytes = Encoding.ASCII.GetBytes(address);
        
        CanvasManager.Instance.SetAddress(address);
        CanvasManager.Instance.CloseLogin();
    }

    public void LogOut()
    {
        //Debug.Log("logged out");

        // TODO something else here ?

        CanvasManager.Instance.ClearAddress();
        CanvasManager.Instance.OpenLogin();
    }

    #region Blockchain calls

    public void GetAccount(string address)
    {
        //TODO handle do lado do unity disto: Cannot connect to destination hostUnityEngine.Debug:Log(Object)

        // Private key: L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25
        // Public key:  P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr

        Debug.Log("Get account: " + address);

        CanvasManager.Instance.ShowFetchingDataPopup("Fetching account data from the blockchain...");

        StartCoroutine(PhantasmaApi.GetAccount(address, result =>
            {
                CanvasManager.Instance.accountMenu.SetBalance("Name: " + result.name);

                foreach (var balanceSheetResult in result.balances)
                {
                    var amount = decimal.Parse(balanceSheetResult.amount) / (decimal) Mathf.Pow(10f, 8);
                    CanvasManager.Instance.accountMenu.AddBalanceEntry("Chain: " + balanceSheetResult.chain + " - " + amount + " " + balanceSheetResult.symbol);

                    //Debug.Log("balance: " + balanceSheetResult.Chain + " | " + balanceSheetResult.Amount);
                }

                CanvasManager.Instance.HideFetchingDataPopup();

                LoggedIn(address);

            },
            (errorType, errorMessage) => { CanvasManager.Instance.loginMenu.SetLoginError(errorType + " - " + errorMessage); }
        ));
    }

    //public void ListTransactions()
    //{
    //    StartCoroutine(ListTransactionsCoroutine());
    //}

    //private IEnumerator ListTransactionsCoroutine()
    //{
    //    var myData = "{ \"jsonrpc\":\"2.0\",\"method\":\"getAccount\",\"params\":[\"P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr\"],\"id\":1}";
    //    var www = UnityWebRequest.Post(_SERVER_ADDRESS, myData);

    //    yield return www.SendWebRequest();

    //    if (www.isNetworkError || www.isHttpError)
    //    {
    //        Debug.Log(www.error);
    //    }
    //    else
    //    {
    //        Debug.Log(www.downloadHandler.text);
    //    }
    //}

    #endregion

    public void CreateToken()
    {
        CheckTokenCreation(() =>
        {
            CanvasManager.Instance.ShowFetchingDataPopup("Creating a new token on the blockchain...");

            var script = ScriptUtils.BeginScript()
                .AllowGas(Key.Address, 1, 9999)
                .CallContract("nexus", "CreateToken", Key.Address, "CAR", "Car Demo Token", 10000, 0, TokenFlags.Transferable | TokenFlags.Finite)
                .SpendGas(Key.Address)
                .EndScript();

            StartCoroutine(PhantasmaApi.SignAndSendTransaction(script, "main",
                (result) =>
                {
                    Debug.Log("sign result: " + result);

                    StartCoroutine(TestGetT(result));

                    CanvasManager.Instance.HideFetchingDataPopup();

                },
                (errorType, errorMessage) =>
                {
                    // TODO
                    CanvasManager.Instance.HideFetchingDataPopup();
                    CanvasManager.Instance.loginMenu.SetLoginError(errorType + " - " + errorMessage);
                }
            ));
        });
    }

    public IEnumerator TestGetT(string result)
    {
        Debug.Log("1");
        yield return new WaitForSecondsRealtime(5f);

        Debug.Log("2");
        yield return new WaitForSecondsRealtime(5f);

        Debug.Log("3");
        yield return new WaitForSecondsRealtime(5f);


        yield return PhantasmaApi.GetTransaction(result, (tx) =>
        {
            foreach (var evt in tx.events)
            {
                Debug.Log("has event: " + evt.kind + " - " + evt.data);

                if (Enum.TryParse(evt.kind, out EventKind eKind))
                {
                    if (eKind == EventKind.TokenCreate)
                    {
                        var bytes = Base16.Decode(evt.data);
                        var data = Serialization.Unserialize<TokenEventData>(bytes);

                        //PhantasmaApi.LogTransaction(Key.Address, 0, TransactionType.Created_Token, "CAR");

                        break;
                    }
                    else
                    {
                        // TODO aconteceu algum erro...
                    }
                }
                else
                {
                    // TODO aconteceu algum erro..
                }
            }



        });
    }

    public void CheckTokenCreation(Action callback = null)
    {
        IsTokenCreated = false;

        CanvasManager.Instance.ShowFetchingDataPopup("Fetching tokens from the blockchain...");

        var script = ScriptUtils.BeginScript()
            .AllowGas(Key.Address, 1, 9999)
            .CallContract("nexus", "GetToken", Key.Address, "CAR")
            .SpendGas(Key.Address)
            .EndScript();
        
        StartCoroutine(PhantasmaApi.GetTokens(
            (result) =>
            {
                Debug.Log("sign result tokens: " + result.Length);

                foreach (var token in result)
                {
                    Debug.Log("token: " + token.symbol);

                    if (token.symbol.Equals("CAR")) // TODO "CAR")) Por o novo token dps da criação dos tokens não ter bugs
                    {
                        Debug.Log("CREATED TRUE");
                        IsTokenCreated = true;
                        break;
                    }
                }

                CanvasManager.Instance.HideFetchingDataPopup();

                if (callback != null)
                {
                    callback();
                }

            },
            (errorType, errorMessage) =>
            {
                // TODO
                CanvasManager.Instance.HideFetchingDataPopup();
                CanvasManager.Instance.loginMenu.SetLoginError(errorType + " - " + errorMessage);
            }
        ));
    }

    public bool OwnsToken(Action callback = null)
    {
        IsTokenOwner = false;

        CanvasManager.Instance.ShowFetchingDataPopup("Fetching tokens from the blockchain...");

        StartCoroutine(PhantasmaApi.GetTokens(
            (result) =>
            {
                Debug.Log("sign result: " + result);

                foreach (var token in result)
                {
                    Debug.Log("token add: " + token.ownerAddress + " | MY: " + Key.Address);

                    if (token.ownerAddress.Equals(Key.Address.ToString()))
                    {
                        Debug.Log("SAME ADD: " + token.symbol);
                        IsTokenOwner = true;
                        break;
                    }
                }

                CanvasManager.Instance.HideFetchingDataPopup();

                if (callback != null)
                {
                    callback();
                }
            },
            (errorType, errorMessage) =>
            {
                // TODO
                CanvasManager.Instance.HideFetchingDataPopup();
                CanvasManager.Instance.loginMenu.SetLoginError(errorType + " - " + errorMessage);
            }
        ));

        return IsTokenOwner;
    }
}
