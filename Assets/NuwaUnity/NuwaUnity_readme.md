# NuwaUnity 
* ###### `ver 1.6.1`

###Feature list:
 * `動作播放(Motion)`
 * `觸碰(Touch)`
 * `馬達轉動(Motor)`
 * `燈光控制(LED)`
 * `語音播放(TTS)`
 * `聲音辨識(in app localcommand)`
 * `語音轉文字(Speed2Text)`
 * `物件辨識(Recognize)`
 * `人臉追蹤(Face_track)`
 * `人臉辨識(Face_Recognize)`
 * `移動/旋轉(Movement)`
 * `遠端連線(Connection)`


# `Unity / Android版本`

Unity version 2018.4.0 later
android SDK Min version : 6.0

# `DemoScene使用`
匯入NuwaPlugins的UnityPackage後，把NuwaUnity\Scene 資料夾下的所有Scene加入Build setting中，並把Demo_title放在首位即可。 

# `How to use`
在Editor中使用addComponent加入NuwaEventTrigger.cs後即可使用。目前無法在執行期間使用addComponent掛載。



# `動作播放(Motion)`
在Demo_motion_play Scene中可以看到動作播放功能。

1.播放動作 - Nuwa要有對應的動作檔名才能正確撥放動作
```csharp
Nuwa.motionPlay(string motion_name);
```

想要撥放指定路徑的motion的話可以使用

```c#
Nuwa.motionPlay(string motion_name, bool fade = false, string motion_bin_path);
```

這樣想要撥放 "/storage/emulated/0/Download/assets/motion_bin/" 中的motion檔名的話

motion_bin_path 設定成 :  "/storage/emulated/0/Download"  即可

2.停止播放動作

```csharp
Nuwa.motionStop();
```

3.暫停動作
```csharp
Nuwa.motionPause();
```

4.從暫停處重新撥放
```csharp
Nuwa.motionResume();
```
<br />

# `觸碰(Touch)`

在Demo_touch Scene 可以看到對觸控的反應。

以下是TouchBegin,TouchEnd,Tap,LongPress的接收Event方式
```csharp
NuwaEventTrigger trigger = this.GetComponent<NuwaEventTrigger>();
if (trigger != null)
{
    trigger.onTouchBegan.AddListener(OnTouchBegin);
    trigger.onTouchEnd.AddListener(OnTouchEnd);
    trigger.onTap.AddListener(OnTap);
    trigger.onLongPress.AddListener(OnLongPress);
}
```

注意!在接收從Android端傳過來的Event時更新UI會出現Error(error狀況 : 無法在非Main Thread的狀況下更新Monobehavior的內容)，要更新UI或是Transform的東西的話需要放在Update中處理。

<br />

# `馬達轉動(Motor)`
在Demon_motor Scene 中可以看到控制指定馬達的轉動位置, 程式碼在MotorMain.cs中

1.取得指定目標的馬達角度
```csharp
Nuwa.getMotorPresentPossitionInDegree(Nuwa.NuwaMotorType.neck_y)
```

2.設定指定目標的馬達角度
```csharp
Nuwa.setMotorPositionInDegree((int)type, (int)motorRotateDegree, (int)motorSpeed);
```
<br />

# `燈光控制(LED)`
在Demo_LED Scene中可以讓UnityApp控制機器人的LED。呼叫此功能，機器人上的LED都會被應用程式控制。

1.設定APP控制燈
```csharp
Nuwa.disableSystemLED();//關閉系統LED
Nuwa.enableLed(Nuwa.LEDPosition, bool);//開啟App的LED控制權
Nuwa.setLedColor(Nuwa.LEDPosition , Color);//設定部位的LED顏色
```


2.關閉設定自由控制燈
```csharp
Nuwa.enableLed(Nuwa.LEDPosition, false); // 關閉app的LED控制權
Nuwa.enableSystemLED(); //讓系統管理LED
```

3.讓燈有呼吸效果
```csharp
Nuwa.enableLedBreath(Nuwa.LEDPosition, int, int);
```
<br />

# `語音播放(TTS)`
在Demo_tts Scene中可以使用字串來撥放TTS語音, 支援中、英文。

1.播放tts
```csharp
Nuwa.startTTS(string);
```

2.停止播放tts
```csharp
Nuwa.stopTTS(string);
```

3.暫停播放tts
```csharp
Nuwa.pauseTTS(string);
```

4.恢復播放tts
```csharp
Nuwa.resumeTTS(string);
```

5.設定TTs撥放時的的Speed,Pitch, 
```csharp
# value need be int, between 1~9
Nuwa.SetSpeakParameter("speed", value.ToString());
Nuwa.SetSpeakParameter("pitch", value.ToString());
```
<br />

# `聲音辨識(in app localcommand)`
在Demo_LocalCommand Scene中。接收命令，判斷接收到的內容後再做處理。支援中文。

1.設定Grammar需要的名稱跟值
```csharp
strinExceptiong mi_Name;     //grammer的名稱
string[] values;    //要辨識的字串
/*
mi_Name = "robot";
values = new string[2] {"哈囉","你好"};
*/

Nuwa.prepareGrammarToRobot(mi_Name, values);
```

2.界接Event
```csharp
Nuwa.onGrammarState += OnGrammarState;      //Grammar設定完
Nuwa.onLocalCommandComplete += TrueFunction;    //聲音辨識成功
Nuwa.onLocalCommandException += FalseFunction;  //聲音辨識例外
```

3.Grammar完成設定後，開始聲音辨識。
```csharp
 void OnGrammarState(bool isError, string info)
 {
    Debug.Log(string.Format("OnGrammarState isError = {0} , info = {1}", isError, info));
    
    //開始聲音辨識
    Nuwa.startLocalCommand();
 }
```
收到的Json格式如下，resoult為聽到的聲音字串。

```csharp
{
  "result": "測試",
  "x-trace-id": "ef73bd1252544f30a818b0f68a6a72c7",
  "engine": "IFly local command",
  "type": 1,
  "class": "com.nuwarobotics.lib.voice.ifly.engine.IFlyLocalAsrEngine",
  "version": 1,
  "extra": {
    "content": "String"
  },
  "content": "{\n  \"sn\":1,\n  \"ls\":true,\n  \"bg\":0,\n  \"ed\":0,\n  \"ws\":[{\n      \"bg\":0,\n      \"cw\":[{\n          \"w\":\"測試\",\n          \"gm\":0,\n          \"sc\":67,\n          \"id\":100001\n        }],\n      \"slot\":\"<NuwaQAQ>\"\n    }],\n  \"sc\":68\n}"
}
```
如果回來的json檔案為空的話代表沒有辨識到要求輸入的內容。
<br />
<br />

# `語音轉文字(Speed2Text)`
在Demo_Speed2Text Scene中，把收到的語音轉成文字，支援中文。

1.介接Event
```csharp
Nuwa.onSpeech2TextComplete += SpeechCallback;
```

2.呼叫SpeechToText
```csharp
Nuwa.setListenParameter(Nuwa.ListenType.RECOGNIZE, "language", "en_us");
Nuwa.setListenParameter(Nuwa.ListenType.RECOGNIZE, "accent", null);
Nuwa.startSpeech2Text(false); //不需要wake up, 所以設定false
```
回傳的字串為純文字，直接使用即可。

另外，此功能需要**網路連線**。假如機器人目前的系統時間沒跟目前所在地區時區相同的話，很有可能會出現回傳回來字串為空的狀態。這點要特別注意。

<br /><br />

# `物件辨識(Recognize)`
在Demo_Recognize Scene中，讓機器人可以辨識物件。

1. 接收連線成功的event
```csharp
Nuwa.onConnected += isConnectRecognizeSystem;//連線成功後接收
Nuwa.onOutput+=ReconizeCheck;//辨識輸出後的資料
```

2. 發送辨識需求
```csharp
Nuwa.startRecognition(Nuwa.NuwaRecognition.OBJ);
```

3. 接收傳回來的資料後，把陣列中的data[i].dataSets[j].title的資料撈出來處理
```csharp
// ReconizeCheck is called once per frame
private void SetInfoData(Nuwa.FaceRecognizeData[] data)
{
     for(int i = 0; i < data.Length; i++)
     {
         InfoText.text += "\nid:" + data[i].idx + ", name:" + data[i].name + ", conf:" + data[i].conf + "\nrect:" + JsonUtility.ToJson(data[i].rect);
     }
}
```

data[i].dataset[j]的資料大致上會如下，抓title中的資料即可。
```csharp
{"confidence":0.21084809303283692,"id":"122","title":"10006_Cup_杯子"}
{"confidence":0.1969204843044281,"id":"265","title":"10337_SodaBottle_汽水瓶"}
{"confidence":0.13715511560440064,"id":"131","title":"10023_Feeding Bottle_奶瓶"}

```
<br /><br />

# `人臉追蹤(Face_track)`
在Demo_Facetrack中，讓機器人追蹤人臉的位置。

1. 註冊人臉追鐘的Event
```csharp
Nuwa.onTrack += GetTrackData;
```

2. 發送辨識需求。等待初始辨識完畢約2~3秒後，就會到3.
```csharp
Nuwa.startRecognition(Nuwa.NuwaRecognition.FACE);
```

3. 接收傳回來的data資料並做設定
```csharp
void GetTrackData(Nuwa.TrackData[] data)
{
    float _x  = float.Parse(data[0].x); //只攔截第一人
    float _y = float.Parse(data[0].y);
    float _w = float.Parse(data[0].width);
    float _h = float.Parse(data[0].height);
    FaceOriginPos = new Vector2(_x, _y); // set face pos
    FaceOriginSize = new Vector2(_w, _h); // set face size
    FaceCenterPos = FaceOriginPos + (FaceOriginSize / 2f); // set face center
}
```

回來的數據大致如下
```csharp
{"height":175,"width":175,"x":250,"y":93}
```

# `人臉辨識(Face_Recognize)`
讓機器人可以辨識眼前的使用者是誰, 程式在Demo_FaceRecognize中。可以先使用新增家人來先行加入要辨識的使用者。

1. 註冊人臉辨識的Event
```csharp
Nuwa.onFaceRecognize += ReconizeCheck;
```

2. 發送辨識需求
```csharp
Nuwa.startRecognition(Nuwa.NuwaRecognition.FACE_RECOGNITION);
```

3. 接收傳回來的data資料並抓取，就可以確認目前在Camera面前的是哪個用戶。-1為沒有被新增家人的使用者。
```csharp
  private void SetInfoData(Nuwa.FaceRecognizeData[] data)
    {
        if(data != null)
        {
            for(int i = 0; i < data.Length; i++)
            {
                InfoText.text += "\nid:" + data[i].idx + ", name:" + data[i].name + ", conf:" + data[i].conf + ", rect:" + JsonUtility.ToJson(data[i].rect);
            }
        }
    }
```


# `移動/旋轉(Movement)`
讓機器人可以前後移動以及旋轉。方式有直接設定值做移動/旋轉，或是線性加速的方式做移動/旋轉。
另外，此功能需要解除移動鎖定才能呼叫使用。另外、幫機器人充電時，機器人會自動設定移動鎖定。


<br>下面為解除輪子的移動鎖定/移動鎖定的程式。
```csharp
Nuwa.LockWheel(); //鎖定輪子
Nuwa.UnLockWheel(); //解除鎖定輪子
```

<br>移動，值介於-0.2~0.2間。需要停止移動的時候設定0即可
```csharp
Nuwa.SetMove(float);
```


線性加速移動(前進)
```csharp
Nuwa.MoveForwardInAccelerationEx();
```
線性加速移動(後退)
```csharp
Nuwa.MoveBackInAccelerationEx();
```
停止線性加速移動。此function只能停止線性加速的移動，無法停止由SetMove()呼叫的移動。
```csharp
Nuwa.StopInAcclerationEx();
```


<br>旋轉 , 值介於-20~20, 需要停止旋轉的時候設定0即可
```csharp
Nuwa.SetTurn(int);
```

向左線性加速旋轉
```csharp
Nuwa.StopTurnEx();
```
向右線性加速旋轉
```csharp
Nuwa.TurnRightEx();
```
停止線性加速旋轉。此Function只能停止線性加速旋轉的部分，無法停止由setTurn()呼叫的移動。
```csharp
Nuwa.TurnLeftEx();
```

還有，當處於線性加速移動/旋轉時強制鎖定輪子、並且在解除鎖定後再使用相反的線性加入移動，機器人會先維持之前的線釁加速移動方式，才會漸漸變成要做的相反移動方式。

# `遠端連線(Connection)`
使用其他裝置連線機器人，對連線的機器人下指令，如播放動作、語音播放、聲音辨識...等。此功能需要裝置跟機器人在同一個Wifi。

建立ConectionManager。
```csharp
NuwaConnection.CreateConnectionManager();
```

搜尋機器人。
```csharp
NuwaConnection.StartScan();
```
停止搜尋機器人。
```csharp
NuwaConnection.StopScan();
```

接收搜尋到的機器人資料
```csharp
NuwaConnection.OnReceiveScanResultEvent += OnReceiveScanResult(string json);
//scan result json format
{"address":"192.168.103.15","connectorPort":"9999","name":"kebbi_508","type":"Wifi"}
```

與機器人連線 / 中斷連線
```csharp
//connect
NuwaConnection.StartConnect(string name, string address);
//disconnect
NuwaConnection.Disconnect();
```

接收機器人連線資訊
```csharp
NuwaConnection.OnReceiveConnectionResultEvent += OnReceiveConnectionResult(NuwaConnection.EConnectResult eConnectResult)
//Connect Result Enum
public enum EConnectResult
{
    Disconnected = 0,
    Connected = 1,
    Error = 999,
}
```