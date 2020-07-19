using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;


public class HoloInputField : InputField
{

    public override void OnPointerClick(PointerEventData eventData)
    {
        var ff = FocusManager.Instance;
        base.OnPointerClick(eventData);
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        InputManager.Instance.OverrideFocusedObject = this.gameObject;
        base.OnPointerEnter(eventData);
    }
    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        
    }
    public override void OnPointerExit(PointerEventData eventData)
    {
        InputManager.Instance.OverrideFocusedObject = null;
        base.OnPointerExit(eventData);
    }
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
    }

}
