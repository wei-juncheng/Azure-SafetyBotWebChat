# 確保Azure Bot Service在網頁聊天中的安全性

- Azure Bot Service提供直接在HTML網頁上使用JavaScript與Bot交談的Web Chat通道，讓對話機器人可以被廣泛應用在許多企業官網甚至是企業內部網站，例如：IT支援、合約查詢、部門文件查詢等等的用途，但是提到企業內部系統就需要特別注意資安問題，避免公司內部資料被外部人員惡意存取或偷竊。
- 本文將會示範如何使用Bot Service中的Web Chat模式在HTML網頁中建立一個聊天室，並且使用Token交換機制 搭配 IP限制功能，兩層防護避免企業資料被外人存取。
## 架構說明：
- 由於Bot Service的Web Chat功能是直接在前端使用JavaScript與Bot認證、建立連線、渲染聊天室介面，因此需要將認證所需要的資訊寫在前端，為了避免您的Bot Secret Key暴露於風險之中，本文將會有以下三層防護：
    1. 使用Azure Bot Service及Azure Function建置一個Bot Token交換機制。
        - 限制使用者與Bot溝通時只能使用暫時性的Token
    2. 啟用Azure Function 的IP限制規則。
        - 只允許來自特定IP的使用者可以取得認證Token
    3. 啟用Bot 後端作為運算資源的Web App中的IP限制規則。
        - 只允許來自特定IP的使用者可以與Bot溝通
- 如此一來，就算Bot的Token在過期之前被外部人員竊取，也無法從外部IP與Bot溝通。
![](https://i.imgur.com/bI2fJYa.png)
## 事前準備：
1. 一個Microsoft Azure可用的帳號
2. 已經建立完成的Azure Bot Service (或Azure Bot Channels Registration)
3. 允許Bot Service中的【Web Chat】功能
>若尚未在Azure上建立一個可用的Bot，請參閱 [Microsoft Doc - Create a bot with Azure Bot Service](https://docs.microsoft.com/zh-tw/azure/bot-service/abs-quickstart?view=azure-bot-service-4.0)

## 操作流程：
1. 建立Azure Function作為提供Bot Token的API (本文使用C#為例)
2. 設定Azure Function 的IP來源規則
3. 設定Azure Web App 的IP來源規則
4. 在本地端建立HTML頁面測試
---

### 建立Azure Function作為提供Bot Token的API (以C#為例)

1. 登入Azure Portal，並建立Function App。
    - 至Microsoft Azure Portal新增資源，於搜尋列中輸入Function App

![於搜尋列中輸入Function App](https://i.imgur.com/0eKfUwz.png)

2. 點選Create按鈕，準備建立資源
![](https://i.imgur.com/kkE2GLf.png)

3. 輸入建立資源需要的設定。[Basics]
    - Subscription：選取你可用的訂用帳戶
    - Resource Group：點選【Create new】或選取已經存在的資源群組
    - Function App name：為你的App取一個望文生義的名稱
    - Runtime stack：選擇【.NET Core】

    輸入完成後點選下方【Next: Hosting >】
![](https://i.imgur.com/43vt6wf.png)
> 如需使用其他語言建立Azure Function，請參考[Microsoft Doc - Create a function in Azure that responds to HTTP requests](https://docs.microsoft.com/zh-tw/azure/azure-functions/functions-create-first-azure-function-azure-cli?pivots=programming-language-python&tabs=powershell%2Ccurl)
4. 輸入建立資源需要的設定。[Hosting]
    - Operating System：選擇【Windows】
    - Plan type：選擇【App service plan】
    - SKu and size：可點選【Change size】再依照專案需求選擇其他運算能力配置

    選擇完畢請直接點選【Review + create】進行最後確認
    ![](https://i.imgur.com/UK26LG1.png)

5. 確認以上配置皆正確無誤之後，點選【Create】開始部屬Azure Function
![](https://i.imgur.com/78Wihxw.png)

6. 等到畫面顯示Your deployment is complete，表示資源建立完成。點選【Go to resource】前往Azure Function資源頁面
![](https://i.imgur.com/ZlPluxD.png)

7. 點選【+ New function】
![](https://i.imgur.com/96okLx1.png)

8. 點選【In-portal】> 【Continue】允許在Azure Portal網頁上進行編輯
![](https://i.imgur.com/cBOR86s.png)

9. 點選【Webhook+ API】> 【Create】
![](https://i.imgur.com/ekHDH6T.png)

10. 進入程式碼編輯頁面之後，將以下程式碼複製貼上至code編輯區塊。
    - 其中```request.Headers.Add()```的"BotConnector"後方請空一格再填入您從Azure Bot Service取得的Secret Key(取代範例code中的*符號)，並按下"Save and run"
>取得Bot Service Secret Key的方法介紹：
>https://docs.microsoft.com/en-us/azure/bot-service/bot-service-channel-connect-webchat?view=azure-bot-service-4.0#web-chat-and-direct-line-considerations
```csharp=
#r "Newtonsoft.Json"
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");
    WebRequest request = WebRequest.Create("https://webchat.botframework.com/api/tokens");
    request.Method = "GET";
    request.Headers.Add("Authorization","BotConnector **********************************************");
    
    WebResponse response = request.GetResponse();
    var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
    
    return (ActionResult)new OkObjectResult($"{{\"token\":{responseString}}}");
    
}
```

11. 順利執行完畢之後，點選畫面中的【</>Get function URL】，並且直接複製提示視窗中的連結，貼上至瀏覽器的新分頁中，畫面會顯示一組Token。
    - 如此一來，使用者只要使用GET request即可取得一組暫時性的Token。
>請將 function URL複製下來，我們稍後會需要使用這個Endpoint URL
![](https://i.imgur.com/oiq2flI.png)

12. 為了接下來的本地端測試，因此需要更改Azure Function的CORS設定，才能在本地端直接透過HTML檔案開啟Bot頁面。
    - 請回到目前建置的Azure Function資源首頁，點選【Platform features】> 【CORS】並且將列表中的項目全部刪除，並且新增一項「*」(一個星號)
    - 將列表中的項目全部刪除，並且新增一項「*」(一個星號)
![](https://i.imgur.com/N2EX0HU.png)
>Azure Function預設會進行跨域請求的限制，由於本文僅做為示範用途，因此使用HTML直接在瀏覽器開啟，若沒有做此設定，Azure Function無法接受來源為Null的請求，使用者就會拿不到Token

#### 到目前為止，我們已經成功建立一個API讓使用者可以透過GET Method在網頁前端使用JavaScript取得Bot Token，但是這會讓全世界所有人都可以拿到您的Bot Token，因此我們接下來需要限制Azure Function只允許特定IP請求


---

### 設定Azure Function 的IP來源規則

1. 在剛剛Azure Function的資源首頁點選【Platform features】 > 【Networking】 >【Configure Access Restrictions】即可設定IP限制規則
![](https://i.imgur.com/jWkoN8q.png)
![](https://i.imgur.com/IRAT0i5.png)





---

### 設定Azure Web App 的IP來源規則

#### 由於Azure Bot Service背後的運算資源是建立在Azure Web App，因此Web Chat使用者傳送的每一則訊息都會透過DirectLine通道轉送到Azure Web App與Bot API溝通。如果要限制外部人員使用Web Chat中與Bot溝通，最直接的方法就是限制Azure Web App的IP規則。
1. 請先回到您建立Bot Service的資源群組，開啟您的Bot Service後端所連結的Azure Web App(App Service)，其名稱預設是與您的Bot Service名稱相似。

2. 進入Web App的資源頁面之後， 點選左邊選單中的【Networking】，再點選【Configure Access Restrictions】即可設定IP限制規則
![](https://i.imgur.com/wC76nic.png)

---

#### 到目前為止，您的Bot已經透過時效性Token機制以及IP來源限制兩種方法增加了許多安全性，不在您設定的IP範圍內是無法與Bot溝通的。接下來我們將會在本地端建立一份HTML檔案，並且直接在瀏覽器開啟之後即可與Bot溝通。
### 在本地端建立HTML頁面測試
1. 建立新檔案命名為index.html，並複製以下程式碼貼到index.html中：

```javascript=
<!DOCTYPE html>
<html lang="en-US">
  <head>
    <title>Web Chat: Full-featured bundle</title>
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <script src="https://cdn.botframework.com/botframework-webchat/latest/webchat.js"></script>
    <style>
      html,
      body {
        height: 100%;
        background-color: gray;
      }
      body {
        margin: 0;
      }
      #webchat {
        height: 100%;
        width: 100%;
      }
    </style>
  </head>
  <body>
    <div id="webchat" role="main"></div>
    <script>
      (async function() {
        
        //此端點為自行建立的Azure Function，使用者可以透過呼叫這個API取得一組Bot Token
        //請將先前用Azure Function建置好Bot Token Exchange API的Endpoint URL複製貼上以取代*符號
        const res = await fetch('***********************************************', {method: 'GET'});
        const { token } = await res.json();
        
        //註冊觸發事件，讓Bot主動傳送歡迎訊息
        const store = window.WebChat.createStore({}, ({dispatch}) => next => action => {
            if (action.type === 'DIRECT_LINE/CONNECT_FULFILLED') {
                dispatch({
                    type: 'WEB_CHAT/SEND_EVENT',
                    payload: {
                        name: 'webchat/join',
                        value: {
                            language: window.navigator.language
                        }
                    }
                });
            }
            return next(action);
        });
        
        window.WebChat.renderWebChat(
          {
            directLine: window.WebChat.createDirectLine({ token }),
            store
          },
          document.getElementById('webchat')
        );
        document.querySelector('#webchat > *').focus();
      })().catch(err => console.error(err));
    </script>
  </body>
</html>
```
2. 第29行fetch()內請填入文章上半段從Azure Function複製下來的function URL (用URL取代「*」符號)
3. 儲存檔案之後，直接開啟這份HTML檔案即可顯示網路聊天室，開始與Bot溝通


---

## 結語：
- 到目前為止，我們透過這份HTML檔案模擬使用者透過網頁前端JavaScript建立聊天室開始與Bot溝通，代表企業可以在公司的官網或內部網站置入這段HTML程式碼，只要正確的include Azure Bot Service所提供的JavaScript腳本，即可自動建立一個聊天室介面。
- 基於安全性考量，Bot Token有時效性，同一個Bot Token僅能使用60分鐘，若沒有更新就會過期，與Bot的溝通也會被中斷。幸運的是，Azure Bot Service所提供的JavaScript腳本固定每15分鐘會自動向Direct Line伺服器更新Token，避免使用者與Bot的溝通被打斷。
## 後續：
- 本文示範透過Azure的設定還有特殊的架構來減低您企業內部Bot的資安風險。不過目前建立的網頁介面是最基本的UI，若企業有需要針對UI作客製化的樣式修改，也可以透過前端JavaScript的調整輕鬆地完成，未來我將會為大家介紹前端聊天室UI介面如何調整，敬請期待。