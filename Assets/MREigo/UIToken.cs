using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIToken : MonoBehaviour
{
    public string token;


    static public UIToken FindToken(GameObject root, string token)
    {
        var uiToken = root.GetComponent<UIToken>();
        if (uiToken != null)
        {
            if (uiToken.token == token) return uiToken;
        }
        foreach (Transform tr in root.transform)
        {
            uiToken = FindToken(tr.gameObject, token);
            if (uiToken != null) return uiToken;
        }
        return null;
    }

    static public T FindToken<T>(GameObject root, string token)where T:Component
    {
        var uiToken = root.GetComponent<UIToken>();
        if (uiToken != null)
        {
            if (uiToken.token == token)
            {
                var comp = uiToken.GetComponent<T>();
                if(comp!=null)return comp;
            }
        }
        foreach (Transform tr in root.transform)
        {
            var comp = FindToken<T>(tr.gameObject, token);
            if (comp != null) return comp;
        }
        return null;
    }

    static public List<UIToken> FindTokens(GameObject root, string token)
    {
        var tokens = new List<UIToken>();
        FindTokens(root, token, ref tokens);
        return tokens;
    }

    static public void FindTokens(GameObject root, string token, ref List<UIToken> tokens)
    {
        var uiToken = root.GetComponent<UIToken>();
        if (uiToken != null)
        {
            if (uiToken.token == token) tokens.Add(uiToken);
        }
        foreach (Transform tr in root.transform)
        {
            FindTokens(tr.gameObject, token, ref tokens);
        }
    }

    static public void ApplyToken(GameObject root, string token, Action<UIToken> callback)
    {
        var uiToken = root.GetComponent<UIToken>();
        if (uiToken != null)
        {
            if (uiToken.token == token) callback(uiToken);
        }
        foreach (Transform tr in root.transform)
        {
            ApplyToken(tr.gameObject, token, callback);
        }
    }

    static public void ApplyToken<T>(GameObject root, string token, Action<T> callback) where T:Component
    {
        var uiToken = root.GetComponent<UIToken>();
        if (uiToken != null)
        {
            if (uiToken.token == token)
            {
                var comp = root.GetComponent<T>();
                if(comp!=null)callback(comp);
            }
        }
        foreach (Transform tr in root.transform)
        {
            ApplyToken<T>(tr.gameObject, token, callback);
        }
    }

}
