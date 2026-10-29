using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Ritm {
 public sealed class OAuthClient {public string client_id {get;set;} public string client_secret {get;set;} }
 public sealed class OAuthFile {public OAuthClient installed {get;set;} }
 public sealed class OAuthToken {public string access_token {get;set;}public string refresh_token {get;set;}public int expires_in {get;set;}public string scope {get;set;}public string expiresUtc {get;set;} }
 public sealed class DriveFile {public string id {get;set;}public string name {get;set;} }
 public sealed class DriveListing {public string nextPageToken {get;set;}public List<DriveFile> files {get;set;} }
 public sealed class GoogleDriveClient {
  const string Scope="https://www.googleapis.com/auth/drive.appdata";
  readonly string root,configFile,tokenFile;readonly HttpClient http=new HttpClient{Timeout=TimeSpan.FromSeconds(25)};
  readonly Func<Task<string>> tokenProvider; internal GoogleDriveClient(string dataRoot,HttpClient transport,Func<Task<string>> token):this(dataRoot){http=transport;tokenProvider=token;} readonly HashSet<string> names=new HashSet<string>();
  System.Threading.CancellationTokenSource loginCancellation; public void CancelLogin(){if(loginCancellation!=null)loginCancellation.Cancel();} public int Epoch {get;private set;}
  public bool IsConfigured {get{return File.Exists(configFile);}}
  public bool IsConnected {get{return File.Exists(tokenFile);}}
  public GoogleDriveClient(string dataRoot){root=Path.Combine(dataRoot,"google");configFile=Path.Combine(root,"client.bin");tokenFile=Path.Combine(root,"token.bin");}
  static string Escape(string s){return Uri.EscapeDataString(s??"");}
  static string Base64(byte[] b){return Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');}
  public static string Challenge(string verifier){using(var sha=SHA256.Create())return Base64(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));}
  static string Random(){var bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);return Base64(bytes);}
  static void SecretWrite(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path));byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Write(value)),null,DataProtectionScope.CurrentUser);string temp=path+".tmp";File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
  static T SecretRead<T>(string path){return Json.Read<T>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser)));}
  public void ImportConfiguration(string path){
   if(IsConnected)throw new Exception("Сначала отключите текущий аккаунт Google.");
   if(new FileInfo(path).Length>100000)throw new Exception("Некорректный размер JSON-файла OAuth.");
   var data=Json.Read<OAuthFile>(File.ReadAllText(path,Encoding.UTF8));
   if(data==null||data.installed==null||string.IsNullOrWhiteSpace(data.installed.client_id)||!data.installed.client_id.EndsWith(".apps.googleusercontent.com",StringComparison.Ordinal)||data.installed.client_id.Length>300)throw new Exception("Выберите JSON OAuth-клиента типа Desktop app, скачанный в Google Cloud.");
   SecretWrite(configFile,data.installed);Epoch++;
  }
  public async Task ConnectAsync(){
   if(!IsConfigured)throw new Exception("Сначала импортируйте JSON OAuth-клиента Google.");
   var client=SecretRead<OAuthClient>(configFile);string verifier=Random(),state=Random();var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
   try{
    int port=((IPEndPoint)listener.LocalEndpoint).Port;string redirect="http://127.0.0.1:"+port+"/oauth/";
    string url="https://accounts.google.com/o/oauth2/v2/auth?client_id="+Escape(client.client_id)+"&redirect_uri="+Escape(redirect)+"&response_type=code&scope="+Escape(Scope)+"&state="+Escape(state)+"&code_challenge="+Escape(Challenge(verifier))+"&code_challenge_method=S256&access_type=offline&prompt=consent";
    Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    loginCancellation=new System.Threading.CancellationTokenSource();var timeout=Task.Delay(TimeSpan.FromMinutes(3),loginCancellation.Token);string code=null;
    while(code==null){
     var accept=listener.AcceptTcpClientAsync();if(await Task.WhenAny(accept,timeout)!=accept)throw new Exception("Вход отменён по времени. Нажмите «Войти через Google» ещё раз.");
     using(var socket=await accept){socket.ReceiveTimeout=5000;socket.SendTimeout=5000;using(var stream=socket.GetStream()){
      var buffer=new byte[8192];int used=0;while(used<buffer.Length){var read=stream.ReadAsync(buffer,used,buffer.Length-used);if(await Task.WhenAny(read,Task.Delay(5000))!=read)throw new Exception("Не удалось получить ответ входа Google.");int n=await read;if(n==0)break;used+=n;if(Encoding.ASCII.GetString(buffer,0,used).Contains("\r\n\r\n"))break;}
      string request=Encoding.ASCII.GetString(buffer,0,used),first=request.Split(new[]{"\r\n"},StringSplitOptions.None)[0];var parts=first.Split(' ');Uri callback=null;bool valid=parts.Length>=2&&parts[0]=="GET"&&Uri.TryCreate("http://127.0.0.1:"+port+parts[1],UriKind.Absolute,out callback)&&callback.AbsolutePath=="/oauth/";
      var query=valid?System.Web.HttpUtility.ParseQueryString(callback.Query):null;
      if(!valid||query["state"]!=state){await BrowserReply(stream,"Не удалось проверить запрос. Вернитесь в приложение.",400);continue;}
      if(!string.IsNullOrEmpty(query["error"])){await BrowserReply(stream,"Доступ не предоставлен. Окно можно закрыть.",200);throw new Exception("Вы не предоставили доступ Google Диску.");}
      code=query["code"];if(string.IsNullOrEmpty(code)){await BrowserReply(stream,"Ответ Google не содержит кода входа.",400);code=null;continue;}
      await BrowserReply(stream,"Вход подтверждён. Можно закрыть вкладку и вернуться в Ритм.",200);
     }}
    }
    var form=new Dictionary<string,string>{{"client_id",client.client_id},{"code",code},{"code_verifier",verifier},{"redirect_uri",redirect},{"grant_type","authorization_code"}};if(!string.IsNullOrEmpty(client.client_secret))form["client_secret"]=client.client_secret;
    var token=await TokenRequest(form);if(string.IsNullOrEmpty(token.refresh_token))throw new Exception("Google не выдал длительный доступ. Отключите Ритм в настройках Google и повторите вход.");
    SecretWrite(tokenFile,token);names.Clear();Epoch++;
   }finally{listener.Stop();if(loginCancellation!=null){loginCancellation.Dispose();loginCancellation=null;}}
  }
  static async Task BrowserReply(NetworkStream stream,string message,int status){string html="<!doctype html><html lang='ru'><meta charset='utf-8'><title>Ритм</title><body style='font:18px system-ui;background:#19211d;color:#e2e8e3;padding:10vw'><h1>Ритм</h1><p>"+WebUtility.HtmlEncode(message)+"</p></body></html>";byte[] body=Encoding.UTF8.GetBytes(html),head=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+(status==200?" OK":" Bad Request")+"\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");await stream.WriteAsync(head,0,head.Length);await stream.WriteAsync(body,0,body.Length);}
  async Task<OAuthToken> TokenRequest(Dictionary<string,string> form){using(var response=await http.PostAsync("https://oauth2.googleapis.com/token",new FormUrlEncodedContent(form))){if(!response.IsSuccessStatusCode)throw new Exception("Google не подтвердил доступ. Проверьте настройки OAuth и повторите вход.");var token=Json.Read<OAuthToken>(await response.Content.ReadAsStringAsync());if(token==null||string.IsNullOrEmpty(token.access_token))throw new Exception("Некорректный ответ входа Google.");token.expiresUtc=DateTime.UtcNow.AddSeconds(Math.Max(60,token.expires_in-60)).ToString("o");return token;}}
  async Task<string> AccessToken(){if(tokenProvider!=null)return await tokenProvider();if(!IsConnected)throw new Exception("Подключите Google Диск в настройках.");var token=SecretRead<OAuthToken>(tokenFile);if(DateTime.Parse(token.expiresUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind)>DateTime.UtcNow)return token.access_token;var client=SecretRead<OAuthClient>(configFile);var form=new Dictionary<string,string>{{"client_id",client.client_id},{"refresh_token",token.refresh_token},{"grant_type","refresh_token"}};if(!string.IsNullOrEmpty(client.client_secret))form["client_secret"]=client.client_secret;var renewed=await TokenRequest(form);renewed.refresh_token=token.refresh_token;SecretWrite(tokenFile,renewed);return renewed.access_token;}
  async Task<HttpResponseMessage> Send(HttpMethod method,string url,HttpContent body=null){var request=new HttpRequestMessage(method,url);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await AccessToken());request.Content=body;HttpResponseMessage response;try{response=await http.SendAsync(request);}finally{request.Dispose();}if(!response.IsSuccessStatusCode){int code=(int)response.StatusCode;response.Dispose();if(code==401)throw new Exception("Доступ Google истёк. Повторите вход в настройках.");if(code==403)throw new Exception("Google отклонил запрос. Проверьте включение Drive API и разрешения приложения.");if(code==429)throw new Exception("Google просит подождать. Синхронизация повторится автоматически.");throw new Exception("Google Диск временно недоступен ("+code+").");}return response;}
  public async Task<List<TaskItem>> PullAsync(){
   var result=new List<TaskItem>();var seen=new HashSet<string>();string page=null;int epoch=Epoch;Directory.CreateDirectory(Path.Combine(root,"cache"));
   do{
    string url="https://www.googleapis.com/drive/v3/files?spaces=appDataFolder&pageSize=1000&fields=nextPageToken,files(id,name)&q="+Escape("trashed = false and name contains 'ritm-v2-'")+(page==null?"":"&pageToken="+Escape(page));
    DriveListing listing;using(var response=await Send(HttpMethod.Get,url))listing=Json.Read<DriveListing>(await response.Content.ReadAsStringAsync());if(listing==null||listing.files==null)throw new Exception("Не удалось прочитать список файлов Ритма.");
    foreach(var file in listing.files){if(epoch!=Epoch)throw new Exception("Аккаунт изменился. Повторите синхронизацию.");if(!file.name.StartsWith("ritm-v2-",StringComparison.Ordinal)||!file.name.EndsWith(".json",StringComparison.Ordinal))continue;if(file.id.Length>200||file.id.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))throw new Exception("Некорректный идентификатор файла Google.");seen.Add(file.name);string cache=Path.Combine(root,"cache",file.id+".json");string json;
     if(File.Exists(cache))json=File.ReadAllText(cache,Encoding.UTF8);else{using(var response=await Send(HttpMethod.Get,"https://www.googleapis.com/drive/v3/files/"+Escape(file.id)+"?alt=media")){if(response.Content.Headers.ContentLength>2000000)throw new Exception("Слишком большой файл синхронизации.");json=await response.Content.ReadAsStringAsync();if(json.Length>2000000)throw new Exception("Слишком большой файл синхронизации.");}}
     var item=Json.Read<TaskItem>(json);if(item==null)throw new Exception("Не удалось прочитать облачную задачу.");item.Validate();if(EventName(item,json)!=file.name)throw new Exception("Облачный файл Ритма не прошёл проверку целостности.");if(!File.Exists(cache))File.WriteAllText(cache,json,Encoding.UTF8);result.Add(item);
    }page=listing.nextPageToken;
   }while(!string.IsNullOrEmpty(page));
   names.Clear();foreach(string name in seen)names.Add(name);return result;
  }
  public static string EventName(TaskItem item){return EventName(item,Json.Write(item));} static string EventName(TaskItem item,string json){using(var sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","").ToLowerInvariant();return "ritm-v2-"+item.id+"-"+hash+".json";}}
  public async Task PushAsync(List<TaskItem> items){int epoch=Epoch;foreach(var item in items){if(epoch!=Epoch)throw new Exception("Аккаунт изменился. Повторите синхронизацию.");string name=EventName(item);if(names.Contains(name))continue;string boundary="ritm_"+Guid.NewGuid().ToString("N");var body=new MultipartContent("related",boundary);body.Add(new StringContent(Json.Write(new {name=name,parents=new[]{"appDataFolder"},mimeType="application/json"}),Encoding.UTF8,"application/json"));body.Add(new StringContent(Json.Write(item),Encoding.UTF8,"application/json"));using(var response=await Send(HttpMethod.Post,"https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id",body)){var saved=Json.Read<DriveFile>(await response.Content.ReadAsStringAsync());if(saved==null||string.IsNullOrEmpty(saved.id))throw new Exception("Google не подтвердил сохранение.");names.Add(name);}}}
  public void Disconnect(){Epoch++;if(File.Exists(tokenFile))File.Delete(tokenFile);names.Clear();}
 }
}


