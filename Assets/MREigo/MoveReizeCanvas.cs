using Hecomi.HoloLensPlayground;
using HoloToolkit.Unity.InputModule;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveReizeCanvas : MonoBehaviour, IManipulationHandler
{
    Canvas canvas;
    BodyLocked bodyLock;

    void Awake()
    {
        this.transform.localScale *= 2;
    }

    // Use this for initialization
    void Start()
    {
        // 全てのジェスチャーイベントをキャッチできるようにする
        InputManager.Instance.AddGlobalListener(gameObject);

        bodyLock = GetComponent<BodyLocked>();
        canvas = GetComponent<Canvas>();
    }

    // Update is called once per frame
    void Update()
    {
    }


    Vector3 last_;
    public void OnManipulationStarted(ManipulationEventData eventData)
    {
        last_ = eventData.CumulativeDelta;
    }
    public void OnManipulationCanceled(ManipulationEventData eventData)
    {
    }
    public void OnManipulationCompleted(ManipulationEventData eventData)
    {
    }
    public void OnManipulationUpdated(ManipulationEventData eventData)
    {
        var delta = eventData.CumulativeDelta- last_;
        last_ = eventData.CumulativeDelta;

        if(bodyLock.overrideDistans==0)
        {
            bodyLock.overrideDistans = bodyLock.maxDistance;
        }
        bodyLock.overrideDistans += Vector3.Dot(Camera.main.transform.forward, delta*10);

        Debug.Log(delta*100);
    }

}
