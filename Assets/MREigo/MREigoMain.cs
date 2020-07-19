using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Net;
using System.IO;

using UnityEngine.Networking;
using System.Xml.Serialization;
using System.Text;

using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;
using MREigo;

#if WINDOWS_UWP
using System.Threading;
using System.Threading.Tasks;
using Windows;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media;
using Windows.Media.SpeechRecognition;
#endif

public class MREigoMain : MonoBehaviour, IDictationHandler
{
    public UnityEngine.UI.InputField inputKeyboard;
    public UnityEngine.UI.Text outputKeyboard;
    public UnityEngine.UI.Button btnHonyaku;
    public UnityEngine.UI.Button btnVoiceTest;

    UnityEngine.UI.Text uiText_inputVoice;
    UnityEngine.UI.Text uiText_transVoice;
    UnityEngine.UI.Text uiText_inputVoiceRealtime;
    UnityEngine.UI.Text uiText_transVoiceRealtime;

    public GameObject CanvasRoot;

    #region Start/Update

    TextToSpeech tts_;

    // Use this for initialization
    void Start()
    {
        var canvasRoot = GameObject.Find("CanvasMain");

        uiText_inputVoice = UIToken.FindToken<UnityEngine.UI.Text>(CanvasRoot, "inputVoiceText");
        uiText_transVoice = UIToken.FindToken<UnityEngine.UI.Text>(CanvasRoot, "transVoiceText");
        uiText_inputVoiceRealtime = UIToken.FindToken<UnityEngine.UI.Text>(CanvasRoot, "inputVoiceRtText");
        uiText_transVoiceRealtime = UIToken.FindToken<UnityEngine.UI.Text>(CanvasRoot, "transVoiceRtText");

        foreach (var dev in Microphone.devices)
        {
            int test;
            int test2;
            Microphone.GetDeviceCaps(dev, out test, out test2);
            System.Diagnostics.Debug.WriteLine(dev + " " + test + " " + test2);
        }

        btnHonyaku.onClick.AddListener(onclick_actHonyaku);
        btnVoiceTest.onClick.AddListener(onclick_actVoiceInpuChange);
        tts_ = GetComponent<TextToSpeech>();

#if WINDOWS_UWP
        {
            uwpSpeechReco_.DictationHypothesis += DictationRecognizer_DictationHypothesis;
            uwpSpeechReco_.DictationResult += DictationRecognizer_DictationResult;
            uwpSpeechReco_.DictationComplete += DictationRecognizer_DictationComplete;
            uwpSpeechReco_.DictationError += DictationRecognizer_DictationError;
        }
#else
        {
            MyDictationInputManager.DictationRecognizer.DictationHypothesis += DictationRecognizer_DictationHypothesis;
            MyDictationInputManager.DictationRecognizer.DictationResult += DictationRecognizer_DictationResult;
            MyDictationInputManager.DictationRecognizer.DictationComplete += DictationRecognizer_DictationComplete;
            MyDictationInputManager.DictationRecognizer.DictationError += DictationRecognizer_DictationError;
        }
#endif
        StartUWP();

        tryDictationStart_();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateUWP();

        dictationUpdate_();

        voiceTextQueueUpdate_();

        uiTextUpdate_();
    }

    #endregion


    #region UI

    private void onclick_actVoiceInpuChange()
    {
        if (isRecording)
        {
            tryDictationStop_();
        }
        else
        {
            tryDictationStart_();
        }
    }

    private void onclick_actHonyaku()
    {
        StartCoroutine(TranslatorTest("ja", "en", inputKeyboard.text, (isOk, text) =>
        {
            if (isOk)
            {
                outputKeyboard.text = text;
                tts_.StartSpeaking(text.Replace(".", " "));
            }
            else
            {
                outputKeyboard.text = "翻訳失敗:" + text;
            }
        }));
    }

    void dictationToggleUI_(bool bOn)
    {
        if (bOn)
        {
            btnVoiceTest.GetComponent<RectTransform>().localScale = Vector3.one * 1.2f;
            var text = btnVoiceTest.GetComponentInChildren<UnityEngine.UI.Text>();
            if (text != null) text.text = "現在 音声入力ON";
        }
        else
        {
            btnVoiceTest.GetComponent<RectTransform>().localScale = Vector3.one;
            var text = btnVoiceTest.GetComponentInChildren<UnityEngine.UI.Text>();
            if (text != null) text.text = "現在 音声入力OFF";
        }
    }

    #endregion

    #region 音声認識API呼び出し

    [SerializeField]
    [Range(0.1f, 5f)]
    [Tooltip("The time length in seconds before dictation recognizer session ends due to lack of audio input in case there was no audio heard in the current session.")]
    private float initialSilenceTimeout = 5f;

    [SerializeField]
    [Range(5f, 60f)]
    [Tooltip("The time length in seconds before dictation recognizer session ends due to lack of audio input.")]
    private float autoSilenceTimeout = 20f;

    [SerializeField]
    [Range(1, 60)]
    [Tooltip("Length in seconds for the manager to listen.")]
    private int recordingTime = 10;


    bool isRecording;
    bool isTransition;

  

    void tryDictationStart_()
    {
        System.Diagnostics.Debug.WriteLine("tryDictationStart_");

        if (isTransition) return;
        isRecording = true;

        isTransition = true;
        StartCoroutine(startDictationProc_(() => { isTransition = false; }));
    }
    void tryDictationStop_()
    {
        System.Diagnostics.Debug.WriteLine("tryDictationStop_");

        if (isTransition) return;
        isRecording = false;

        isTransition = true;
        StartCoroutine(stopDictationProc_(() => { isTransition = false; }));
    }

    IEnumerator dictationRestart_(Action endCb)
    {
        System.Diagnostics.Debug.WriteLine("dictationRestart_");

        while (isTransition) yield return null;
        isTransition = true;
        yield return stopDictationProc_(() => { });
        yield return startDictationProc_(() => { });
        isTransition = false;
        if (endCb != null) endCb();
    }

    IEnumerator startDictationProc_(Action endCb)
    {
#if WINDOWS_UWP
        yield return uwpSpeechReco_.StartReco();
#else
        if (!MyDictationInputManager.IsListening)
        {
            yield return MyDictationInputManager.StartRecording(
                gameObject,
                initialSilenceTimeout,
                autoSilenceTimeout,
                recordingTime);
        }
#endif
        endCb();
    }
    IEnumerator stopDictationProc_(Action endCb)
    {
#if WINDOWS_UWP
        yield return uwpSpeechReco_.StopReco();
#else
        if (MyDictationInputManager.IsListening)
        {
            yield return MyDictationInputManager.StopRecording();
        }
#endif
        endCb();
    }

    enum UpdateState_
    {
        None,
        Restarting,
    }
    UpdateState_ updateState_ = UpdateState_.None;

    void dictationUpdate_()
    {
        switch (updateState_)
        {
        case UpdateState_.None:
            var isStoped = false;
#if WINDOWS_UWP
            isStoped = uwpSpeechReco_.IsStopped;
#else
            isStoped = !MyDictationInputManager.IsListening && !MyDictationInputManager.IsTransitioning;
#endif

            if (!isTransition && isRecording && isStoped)
            {
                System.Diagnostics.Debug.WriteLine("Check: IsListening Stop And Restart");
                updateState_ = UpdateState_.Restarting;
                StartCoroutine(dictationRestart_(() => { updateState_ = UpdateState_.None; }));
            }

            dictationToggleUI_(!isStoped);

            break;
        case UpdateState_.Restarting:
            break;
        }
    }

    #endregion



    class VoiceTextData
    {
        public string voiceDictText = "";
        public string voiceTransText = "";
        public enum State
        {
            None,
            Input,
            RequestTransrate,
            Translating,
            Complate
        }
        public State state = State.None;
    }
    Queue<VoiceTextData> voiceTextQueue_ = new Queue<VoiceTextData>();
    List<VoiceTextData> resutVoiceTextList_ = new List<VoiceTextData>();

    #region voiceTextQueueUpdate_
    void voiceTextQueueUpdate_()
    {
        if (voiceTextQueue_.Count > 0)
        {
            var data = voiceTextQueue_.Peek();
            switch (data.state)
            {
            case VoiceTextData.State.RequestTransrate:
                {
                    System.Diagnostics.Debug.WriteLine("翻訳開始:" + data.voiceDictText);

                    data.state = VoiceTextData.State.Translating;
                    StartCoroutine(transVoiceData_(data, (v) =>
                    {
                        data.state = VoiceTextData.State.Complate;
                    }));
                }
                break;
            case VoiceTextData.State.Complate:
                {
                    System.Diagnostics.Debug.WriteLine("翻訳終了:" + data.voiceTransText);
                    {
                        var transVoiceText = new StringBuilder();
                        {
                            var idx = 0;
                            if (resutVoiceTextList_.Count > 10)
                            {
                                idx = resutVoiceTextList_.Count - 10;
                            }
                            for (idx = 0; idx < resutVoiceTextList_.Count; idx++)
                            {
                                var voiceData = resutVoiceTextList_[idx];
                                transVoiceText.Append(voiceData.voiceTransText + " | ");
                            }
                        }
                        //@@transVoiceText.AppendLine("---------");
                        //@@transVoiceText.AppendLine("翻訳結果:" + data.voiceTransText);
                        transVoiceText.AppendLine(data.voiceTransText);

                        if (transVoiceText.Length > 2 && transVoiceText[transVoiceText.Length - 1] == '\n')
                        {
                            transVoiceText.Remove(transVoiceText.Length - 2, 2);
                        }

                        //uiText_transVoice.text = transVoiceText.ToString();
                    }
                    resutVoiceTextList_.Add(data);
                    voiceTextQueue_.Dequeue();
                }
                break;
            }
        }
        else
        {
            var transVoiceText = new StringBuilder();
            {
                foreach (var voiceData in resutVoiceTextList_)
                {
                    transVoiceText.Append(voiceData.voiceTransText + " | ");
                }
            }
            //@@transVoiceText.AppendLine("---------");
            //@@transVoiceText.AppendLine("リアルタイム翻訳:" + realTimeDictTransText);
            if (transVoiceText.Length > 2 && transVoiceText[transVoiceText.Length - 1] == '\n')
            {
                transVoiceText.Remove(transVoiceText.Length - 2, 2);
            }

            //uiText_transVoice.text = transVoiceText.ToString();
            if (realTimeTransText.Length > 0)
            {
                //uiText_transVoiceRealtime.text = realTimeTransText;
            }
            else
            {
                if (resutVoiceTextList_.Count > 0)
                {
                    //uiText_transVoiceRealtime.text = resutVoiceTextList_[resutVoiceTextList_.Count-1].voiceTransText;
                }
            }
        }

        {
            var inputVoiceText = new StringBuilder();
            {
                var idx = 0;
                if (resutVoiceTextList_.Count > 10)
                {
                    idx = resutVoiceTextList_.Count - 10;
                }
                for (idx = 0; idx < resutVoiceTextList_.Count; idx++)
                {
                    var voiceData = resutVoiceTextList_[idx];
                    inputVoiceText.Append(voiceData.voiceDictText + " | ");
                }
                inputVoiceText.AppendLine("------");
            }
            if (inputVoiceText.Length > 2 && inputVoiceText[inputVoiceText.Length - 1] == '\n')
            {
                inputVoiceText.Remove(inputVoiceText.Length - 2, 2);
            }
            //uiText_inputVoice.text = inputVoiceText.ToString();
        }
    }

    IEnumerator transVoiceData_(VoiceTextData voideData, Action<VoiceTextData> endCb)
    {
        bool bIsHololens = false;
#if WINDOWS_UWP
        bIsHololens = (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Holographic");
#endif
        var fromLang = "ja";
        var toLang = "en";
        if (bIsHololens)
        {
            fromLang = "en";
            toLang = "ja";
        }
        yield return TranslatorTest(fromLang, toLang, voideData.voiceDictText, (isOk, text) =>
        {
            if (isOk)
            {
                voideData.voiceTransText = text;
            }
            else
            {
                voideData.voiceTransText = "翻訳失敗:" + text;
            }
            endCb(voideData);
        });
    }
    #endregion

    #region uiTextUpdate_
    StringBuilder inputVoiceText = new StringBuilder();
    StringBuilder transVoiceText = new StringBuilder();
    //StringBuilder inputVoiceRtText = new StringBuilder();
    StringBuilder inputAndTransVoiceRtText = new StringBuilder();

    void uiTextUpdate_()
    {
        inputVoiceText.Length = 0;
        transVoiceText.Length = 0;
        //inputVoiceRtText.Length=0;
        inputAndTransVoiceRtText.Length = 0;

        //処理済みのデータ
        {
            var startIdx = resutVoiceTextList_.Count > 10 ? resutVoiceTextList_.Count - 10 : 0;
            for (int idx = startIdx; idx < resutVoiceTextList_.Count; idx++)
            {
                var voiceData = resutVoiceTextList_[idx];
                inputVoiceText.AppendLine("");
                inputVoiceText.AppendLine(voiceData.voiceDictText + " =>");
                inputVoiceText.AppendLine(voiceData.voiceTransText + " | ");
            }
        }
        //処理中・処理待ちのデータ
        {
            var data = voiceTextQueue_.ToArray();
            foreach (var voiceData in voiceTextQueue_)
            {
                inputVoiceText.Append(voiceData.voiceDictText + " => ");
                inputVoiceText.AppendLine(voiceData.voiceTransText + " | ");
            }
        }
        {
            VoiceTextData lastDat = null;
            if (resutVoiceTextList_.Count > 0)
            {
                lastDat = resutVoiceTextList_[resutVoiceTextList_.Count - 1];
            }

            //リアルタイム受付翻訳中のデータ
            {
                //inputAndTransVoiceRtText.AppendLine(realTimeDictText);
                //inputAndTransVoiceRtText.AppendLine(realTimeTransText);
            }

            //音声入力中のデータ
            {
                //if (lastDat != null) inputAndTransVoiceRtText.Append(lastDat.voiceDictText + "\n");

                inputAndTransVoiceRtText.Append(nowDictationText_);
                inputAndTransVoiceRtText.Append("\n(" + realTimeTransText + ")");

                //if (lastDat != null) inputAndTransVoiceRtText.Append("\n(" + lastDat.voiceTransText + ")");

                //inputVoiceRtText.Append(nowDictationText_);
            }
        }

        //表示処理
        {
            // 末端の改行除去します
            if (inputVoiceText.Length > 2 && inputVoiceText[inputVoiceText.Length - 1] == '\n')
            {
                inputVoiceText.Remove(inputVoiceText.Length - 2, 2);
            }
            if (transVoiceText.Length > 2 && transVoiceText[transVoiceText.Length - 1] == '\n')
            {
                transVoiceText.Remove(transVoiceText.Length - 2, 2);
            }

            uiText_inputVoiceRealtime.alignment = TextAnchor.MiddleRight;
            uiText_transVoiceRealtime.alignment = TextAnchor.MiddleRight;

            // UIにテキストを反映します
            uiText_inputVoice.text = inputVoiceText.ToString();
            uiText_transVoice.text = transVoiceText.ToString();
            //uiText_inputVoiceRealtime.text = inputVoiceRtText.ToString();
            uiText_transVoiceRealtime.text = inputAndTransVoiceRtText.ToString();
        }
    }
    #endregion




    #region メインとなる処理
    string processedDictationText_ = "";

    string nowDictationText_ = "";

    float realTimeTransTimer_ = 0;
    string realTimeDictText = "";
    string realTimeTransText = "";

    string dictErrText_ = "";

    Dictionary<string, string> wordTransTbl_ = new Dictionary<string, string>();


    void procStreamingDictation_(string dictatingText, bool bFinish, int startSepCharaCount, float realtimeTransInteraval)
    {
        string nextTranslateText = dictatingText;
        {
            if (processedDictationText_.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine("------入力中音声 処理済み部分 分割--------");


                //TODO:完全一致じゃない方がいいかも？終端が変わるだけで無効になるし…

                int diffIdx = 0;
                for (int idx = 0; idx < nextTranslateText.Length; idx++)
                {
                    if (idx >= processedDictationText_.Length ||
                        nextTranslateText[idx] != processedDictationText_[idx])
                    {
                        diffIdx = idx;
                        break;
                    }
                }
                if (diffIdx != 0)
                {
                    var newText = nextTranslateText.Substring(diffIdx);

                    System.Diagnostics.Debug.WriteLine(newText + "=分割(" + diffIdx + ")<=" + nextTranslateText);

                    nextTranslateText = newText;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("一致せず 旧" + processedDictationText_);
                    System.Diagnostics.Debug.WriteLine("一致せず 新" + nextTranslateText);
                }
                System.Diagnostics.Debug.WriteLine("----------------------------");
            }
        }
        nowDictationText_ = nextTranslateText;

        {
            var words = nowDictationText_.Split(new char[] { ' ' });
            var text = "";
            foreach (var word in words)
            {
                if (wordTransTbl_.ContainsKey(word))
                {
                    text += word + "=" + wordTransTbl_[word] + " ";
                }
                else
                {
                    text += word + " ";
                }
            }
            nowDictationText_ = text + "\n" + nowDictationText_;
        }

        var inputVoiceText = new StringBuilder();
        {
            var idx = 0;
            if (resutVoiceTextList_.Count > 10)
            {
                idx = resutVoiceTextList_.Count - 10;
            }
            for (idx = 0; idx < resutVoiceTextList_.Count; idx++)
            {
                var voiceData = resutVoiceTextList_[idx];
                inputVoiceText.Append(voiceData.voiceDictText + " | ");
            }
            inputVoiceText.AppendLine("------");
        }

        //認識中の文を表示します
        if (bFinish)
        {
            System.Diagnostics.Debug.WriteLine("音声入力完了:" + nextTranslateText);
            //@@inputVoiceText.AppendLine( "音声入力完了:" + nextTranslateText);

            //uiText_inputVoiceRealtime.text = "聞き取り完了:\n" + nextTranslateText;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("音声入力中:" + nextTranslateText);
            //@@inputVoiceText.AppendLine("音声入力中:" + nextTranslateText);

            //uiText_inputVoiceRealtime.text = "聞き取り中:\n" + nextTranslateText;
        }

        // 終了か認識中の文字が一定量を超えたら翻訳開始してみます
        if (bFinish || nextTranslateText.Length > startSepCharaCount)
        {
            //現在の認識している分を登録します(追加の認識で文に変化があったら再翻訳される)
            processedDictationText_ = dictatingText;
            voiceTextQueue_.Enqueue(new VoiceTextData
            {
                voiceDictText = nextTranslateText,
                state = VoiceTextData.State.RequestTransrate,
            });
            //@@inputVoiceText.AppendLine("..翻訳開始");
            nowDictationText_ = "";
            realTimeDictText = "";
            realTimeTransText = "";
        }
        else if (realTimeTransTimer_ != float.MaxValue)
        {
            if (realTimeTransTimer_ == 0)
            {
                realTimeTransTimer_ = Time.time;
            }

            // とちゅうで翻訳（リアルタイム風)
            var checkTime = Time.time - realTimeTransTimer_;
            if (checkTime > realtimeTransInteraval)
            {
                //@@inputVoiceText.AppendLine("..リアルタイム翻訳");

                realTimeTransTimer_ = float.MaxValue;
                realTimeDictText = nextTranslateText;
                StartCoroutine(transVoiceData_(new VoiceTextData
                {
                    voiceDictText = realTimeDictText,
                    state = VoiceTextData.State.RequestTransrate,
                },
                (data) =>
                {
                    if (data.voiceTransText.Length > 0)
                    {
                        realTimeTransText = data.voiceTransText;
                    }
                    realTimeTransTimer_ = 0;
                }));
            }

            //単語テスト
            {
                var words = nextTranslateText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (!wordTransTbl_.ContainsKey(word))
                    {
                        wordTransTbl_[word] = "";
                        StartCoroutine(transVoiceData_(new VoiceTextData
                        {
                            voiceDictText = word,
                            state = VoiceTextData.State.RequestTransrate,
                        },
                        (data) =>
                        {
                            if (data.voiceTransText.Length > 0)
                            {
                                wordTransTbl_[word] = data.voiceTransText;
                            }
                        }));
                    }
                }
            }
        }
        if (inputVoiceText.Length > 2 && inputVoiceText[inputVoiceText.Length - 1] == '\n')
        {
            inputVoiceText.Remove(inputVoiceText.Length - 2, 2);
        }
        //inputVoice.text = inputVoiceText.ToString();


        if (bFinish)
        {
            processedDictationText_ = "";
        }
    }
    #endregion

    #region 音声認識のコールバック




    private void DictationRecognizer_DictationHypothesis(string text)
    {
        dictErrText_ = "";
        procStreamingDictation_(text, false, 140, 1.0f);
        //procStreamingDictation_(text, false, 10, 1.0f);
    }
    private void DictationRecognizer_DictationResult(string text, UnityEngine.Windows.Speech.ConfidenceLevel confidence)
    {
        dictErrText_ = "";
        procStreamingDictation_(text, true, 0, 0);
    }
    private void DictationRecognizer_DictationError(string error, int hresult)
    {
    }
    private void DictationRecognizer_DictationComplete(UnityEngine.Windows.Speech.DictationCompletionCause cause)
    {
        dictErrText_ = "";
        System.Diagnostics.Debug.WriteLine("Complete:" + cause.ToString());
        System.Diagnostics.Debug.WriteLine("check:" + processedDictationText_);
        processedDictationText_ = "";
        StartCoroutine(dictationRestart_(() => { }));
    }

    public void OnDictationHypothesis(DictationEventData eventData)
    {
        //procStreamingDictation_(eventData.DictationResult, false, 10);
    }
    public void OnDictationResult(DictationEventData eventData)
    {
        //procStreamingDictation_(eventData.DictationResult, true, 10);
    }
    public void OnDictationComplete(DictationEventData eventData)
    {
        //Debug.Log("Complete:" + eventData.DictationResult);
        //processedDictationText_ = "";
        //inputVoice.text = "Complete:" + eventData.DictationResult;
        //StartCoroutine(dictationRestart_());
    }
    public void OnDictationError(DictationEventData eventData)
    {
        dictErrText_ = "Err:" + eventData.DictationResult;
        processedDictationText_ = "";

        Debug.LogError(eventData.DictationResult);

        StartCoroutine(dictationRestart_(() => { }));
    }

    #endregion




    #region 翻訳処理（Azure）

    IEnumerator TranslatorTest(string fromLanguage, string toLanguage, string inputText, Action<bool, string> resultCb)
    {
        //利用するモデル  ：空白 (=統計的機械翻訳) or generalnn(=ニューラルネットワーク)を指定
        string ModelType = "generalnn";

        // 翻訳API実行
        string translateUrl = string.Format("http://api.microsofttranslator.com/v2/Http.svc/Translate?text={0}&from={1}&to={2}&category={3}",
            Uri.EscapeDataString(inputText),
            fromLanguage,
            toLanguage, ModelType
            );

        string ocpApimSubscriptionKey = "4c0c721962894477ab14fa9d32fa08f2";

        using (var request = UnityWebRequest.Get(translateUrl))
        {
            request.SetRequestHeader("Ocp-Apim-Subscription-Key", ocpApimSubscriptionKey);

            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                resultCb(false, request.error);
                yield break;
            }

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(request.downloadHandler.text)))
            {
                var xmlSerializer = new XmlSerializer(typeof(string), "http://schemas.microsoft.com/2003/10/Serialization/");
                var result = (string)xmlSerializer.Deserialize(ms);
                resultCb(true, result);
            }
        }
    }
    #endregion


    #region UWP

#if WINDOWS_UWP

    class UwpSpeechReco
    {
        SpeechRecognizer recognizer_;
        StringBuilder dictatedTextBuilder_ = new StringBuilder();
        //private CoreDispatcher dispatcher;

        public event UnityEngine.Windows.Speech.DictationRecognizer.DictationHypothesisDelegate DictationHypothesis;
        public event UnityEngine.Windows.Speech.DictationRecognizer.DictationResultDelegate DictationResult;
        public event UnityEngine.Windows.Speech.DictationRecognizer.DictationCompletedDelegate DictationComplete;
        public event UnityEngine.Windows.Speech.DictationRecognizer.DictationErrorHandler DictationError;

        public IEnumerator StartReco()
        {
            bool bEnd = false;
            Task.Run(async () =>
            {
                if (recognizer_ == null)
                {
                    await Init();
                }
                if (recognizer_.State == SpeechRecognizerState.Idle)
                {
                    recognizer_.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(0.0f);
                    recognizer_.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(4.0f);
                    recognizer_.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2f);
                    await recognizer_.ContinuousRecognitionSession.StartAsync();
                }
                bEnd = true;
            });
            while (!bEnd)
            {
                yield return null;
            }
        }
        public IEnumerator StopReco()
        {
            bool bEnd = false;
            Task.Run(async () =>
            {
                try
                {
                    if (recognizer_ != null)
                    {
                        if (recognizer_.State != SpeechRecognizerState.Idle)
                        {
                            //await recognizer_.ContinuousRecognitionSession.StopAsync();
                        }
                    }
                }
                catch(Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Err" + e.ToString());
                }
                try
                {
                    if (recognizer_ != null)
                    {
                        recognizer_.Dispose();
                        recognizer_ = null;
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Err" + e.ToString());
                }
                bEnd = true;
            });
            while (!bEnd)
            {
                yield return null;
            }
        }

        public bool IsStopped
        {
            get { return recognizer_ == null || recognizer_.State == SpeechRecognizerState.Idle; }
        }

        class RecoEvent_
        {
            public enum EventType
            {
                DictationHypothesis,
                DictationResult,
                DictationComplete,
                DictationError,
            }
            public EventType eventType;
            public string text = "";
            public UnityEngine.Windows.Speech.ConfidenceLevel confidenceLevel;
            public UnityEngine.Windows.Speech.DictationCompletionCause dictationCompletionCause;
        }
        Queue<RecoEvent_> eventQue_ = new Queue<RecoEvent_>();

        public void UpdateMainThread()
        {
            while (eventQue_.Count > 0)
            {
                lock (eventQue_)
                {
                    var v = eventQue_.Dequeue();
                    switch (v.eventType)
                    {
                    case RecoEvent_.EventType.DictationHypothesis:
                        if (DictationHypothesis != null)
                        {
                            DictationHypothesis(v.text);
                        }
                        break;
                    case RecoEvent_.EventType.DictationResult:
                        if (DictationResult != null)
                        {
                            DictationResult(v.text, v.confidenceLevel);
                        }
                        break;
                    case RecoEvent_.EventType.DictationComplete:
                        if (DictationComplete != null)
                        {
                            DictationComplete(v.dictationCompletionCause);
                        }
                        break;
                    case RecoEvent_.EventType.DictationError:
                        if (DictationError != null)
                        {
                            DictationError("", 0);
                        }
                        break;
                    }
                }
            }

            timmer -= Time.deltaTime;
            if (timmer <= 0)
            {
                {
                    ulong limit = Windows.System.MemoryManager.AppMemoryUsageLimit;
                    ulong usage = Windows.System.MemoryManager.AppMemoryUsage;
                    System.Diagnostics.Debug.WriteLine("memory {0}/{1} {2:f2}%", usage, limit, (usage / (float)limit) * 100.0f);
                }

                if (recognizer_ != null)
                {
                    System.Diagnostics.Debug.WriteLine(recognizer_.State);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ini...");
                }
                timmer = 3;
            }
        }
        float timmer = 0;

        public async Task Init()
        {
            //TEST
            {
                bool isMicAvailable = true;
                try
                {
                    //var audioDevices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.AudioCapture);
                    //var audioId = audioDevices.ElementAt(0);

                    var mediaCapture = new Windows.Media.Capture.MediaCapture();
                    var settings = new Windows.Media.Capture.MediaCaptureInitializationSettings();
                    settings.StreamingCaptureMode =
                        Windows.Media.Capture.StreamingCaptureMode.Audio;
                    settings.MediaCategory = Windows.Media.Capture.MediaCategory.Communications;

                    //var _capture = new Windows.Media.Capture.MediaCapture();
                    //var _stream = new InMemoryRandomAccessStream();
                    //await _capture.InitializeAsync(settings);
                    //await _capture.StartRecordToStreamAsync(MediaEncodingProfile.CreateWav(AudioEncodingQuality.Medium), _stream);


                    await mediaCapture.InitializeAsync(settings);
                }
                catch (Exception)
                {
                    isMicAvailable = false;
                }
                if (!isMicAvailable)
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-microphone"));
                }
                else
                {
                }
            }
            // セットアップ
            {
                var language = new Windows.Globalization.Language("en-US");
                recognizer_ = new SpeechRecognizer(language);

                //this.dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

                recognizer_.ContinuousRecognitionSession.ResultGenerated +=
                    ContinuousRecognitionSession_ResultGenerated;

                recognizer_.ContinuousRecognitionSession.Completed +=
                    ContinuousRecognitionSession_Completed;

                recognizer_.HypothesisGenerated +=
                    SpeechRecognizer_HypothesisGenerated;

                SpeechRecognitionCompilationResult result = await recognizer_.CompileConstraintsAsync();
                System.Diagnostics.Debug.WriteLine(" compile res:" + result.Status.ToString());
            }
        }

        async void ContinuousRecognitionSession_ResultGenerated(
            SpeechContinuousRecognitionSession sender,
            SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            if (recognizer_ == null || recognizer_.ContinuousRecognitionSession != sender) return;

            dictatedTextBuilder_.Append(args.Result.Text + "|");

            System.Diagnostics.Debug.WriteLine("ResultGenerated:" + args.Result.Confidence+" "+args.Result.Text );

            lock (eventQue_)
            {
                var confidenceLevel = UnityEngine.Windows.Speech.ConfidenceLevel.Rejected;
                switch (args.Result.Confidence)
                {
                case SpeechRecognitionConfidence.High: confidenceLevel = UnityEngine.Windows.Speech.ConfidenceLevel.High; break;
                case SpeechRecognitionConfidence.Low: confidenceLevel = UnityEngine.Windows.Speech.ConfidenceLevel.Low; break;
                case SpeechRecognitionConfidence.Medium: confidenceLevel = UnityEngine.Windows.Speech.ConfidenceLevel.Medium; break;
                case SpeechRecognitionConfidence.Rejected: confidenceLevel = UnityEngine.Windows.Speech.ConfidenceLevel.Rejected; break;
                }
                eventQue_.Enqueue(new RecoEvent_
                {
                    eventType = RecoEvent_.EventType.DictationResult,
                    text = args.Result.Text,
                    confidenceLevel = confidenceLevel,
                });
            }
        }

        async void ContinuousRecognitionSession_Completed(
          SpeechContinuousRecognitionSession sender,
          SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (recognizer_ == null || recognizer_.ContinuousRecognitionSession != sender) return;

            System.Diagnostics.Debug.WriteLine("Completed :" + args.Status + " " + dictatedTextBuilder_.ToString() + " ");


            var dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.Complete;

            switch (args.Status)
            {
            case SpeechRecognitionResultStatus.Success:
                dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.Complete;
                break;
            case SpeechRecognitionResultStatus.TopicLanguageNotSupported:
                break;
            case SpeechRecognitionResultStatus.GrammarLanguageMismatch:
                break;
            case SpeechRecognitionResultStatus.GrammarCompilationFailure:
                break;
            case SpeechRecognitionResultStatus.AudioQualityFailure:
                dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.AudioQualityFailure;
                break;
            case SpeechRecognitionResultStatus.UserCanceled:
                dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.Canceled;
                break;
            case SpeechRecognitionResultStatus.Unknown:
                dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.UnknownError;
                break;
            case SpeechRecognitionResultStatus.TimeoutExceeded:
                dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.TimeoutExceeded;
                break;
            case SpeechRecognitionResultStatus.PauseLimitExceeded:
                dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.PauseLimitExceeded;
                break;
            case SpeechRecognitionResultStatus.NetworkFailure:
                dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.NetworkFailure;
                break;
            case SpeechRecognitionResultStatus.MicrophoneUnavailable:
                dictationCompletionCause = UnityEngine.Windows.Speech.DictationCompletionCause.MicrophoneUnavailable;
                break;
            }
            {
                eventQue_.Enqueue(new RecoEvent_
                {
                    eventType = RecoEvent_.EventType.DictationComplete,
                    dictationCompletionCause = dictationCompletionCause,
                });
            }
            dictatedTextBuilder_.Clear();
        }
        private async void SpeechRecognizer_HypothesisGenerated(
            SpeechRecognizer sender,
            SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            if (recognizer_ == null || recognizer_ != sender) return;

            string hypothesis = args.Hypothesis.Text;
            string textboxContent = /*dictatedTextBuilder_.ToString() +*/ " " + hypothesis + " ...";

            System.Diagnostics.Debug.WriteLine("... :" + textboxContent + " ");

            eventQue_.Enqueue(new RecoEvent_
            {
                eventType = RecoEvent_.EventType.DictationHypothesis,
                text = args.Hypothesis.Text,
            });
        }
    }
    UwpSpeechReco uwpSpeechReco_ = new UwpSpeechReco();
#endif

    void StartUWP()
    {
#if WINDOWS_UWP
        {
            var language = SpeechRecognizer.SystemSpeechLanguage;
            System.Diagnostics.Debug.WriteLine("now lang:" + language.DisplayName);

            foreach (var v in SpeechRecognizer.SupportedTopicLanguages)
            {
                System.Diagnostics.Debug.WriteLine("lang:" + v.DisplayName);

            }
        }
        //Task.Run(async () =>
        //{
        //await uwpSpeechReco_.Init();
        //});
#endif
    }

    void UpdateUWP()
    {
#if WINDOWS_UWP
        uwpSpeechReco_.UpdateMainThread();
#endif
    }
    #endregion
}


