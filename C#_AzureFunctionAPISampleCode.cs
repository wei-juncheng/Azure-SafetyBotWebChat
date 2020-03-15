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