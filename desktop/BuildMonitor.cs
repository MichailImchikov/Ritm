using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Ritm {
 public sealed class BuildStatus {
  public string Message="Войдите в консоль стенда, чтобы видеть сборку Unity.";
  public string Step="",Elapsed="",Remaining="";
  public double Percent; public bool HasProgress,Waiting,NeedsLogin,Active;
 }
 public sealed class BuildMonitor {
  const string Server="http://192.168.1.194:8780";
  readonly CookieContainer cookies=new CookieContainer();
  readonly string passwordFile;bool rejected;
  public BuildMonitor(string root){passwordFile=Path.Combine(root,"build-console-password.dat");}
  string Request(string path,string payload=null){
   var req=(HttpWebRequest)WebRequest.Create(Server+path);req.Proxy=null;req.CookieContainer=cookies;req.Timeout=10000;req.ReadWriteTimeout=10000;
   if(payload!=null){req.Method="POST";req.ContentType="application/json";var bytes=Encoding.UTF8.GetBytes(payload);req.ContentLength=bytes.Length;using(var stream=req.GetRequestStream())stream.Write(bytes,0,bytes.Length);}
   using(var response=req.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return reader.ReadToEnd();
  }
  void Authenticate(string password){Request("/api/login",Json.Write(new Dictionary<string,string>{{"password",password}}));}
  public void Login(string password){Authenticate(password);var encrypted=ProtectedData.Protect(Encoding.UTF8.GetBytes(password),null,DataProtectionScope.CurrentUser);File.WriteAllBytes(passwordFile,encrypted);rejected=false;}
  string SavedPassword(){try{if(!File.Exists(passwordFile))return null;return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(passwordFile),null,DataProtectionScope.CurrentUser));}catch(Exception){return null;}}
  static bool Unauthorized(WebException e){var response=e.Response as HttpWebResponse;return response!=null&&response.StatusCode==HttpStatusCode.Unauthorized;}
  public BuildStatus Read(){try{
   try{return Parse(Request("/api/state"));}catch(WebException e){if(!Unauthorized(e))throw;}
   string password=rejected?null:SavedPassword();if(string.IsNullOrEmpty(password))return new BuildStatus{NeedsLogin=true};
   try{Authenticate(password);}catch(WebException e){if(Unauthorized(e)){rejected=true;return new BuildStatus{NeedsLogin=true,Message="Сохранённый пароль не подходит. Войдите снова."};}throw;}
   return Parse(Request("/api/state"));
   }catch(WebException e){if(Unauthorized(e))return new BuildStatus{NeedsLogin=true};return new BuildStatus{Message="Нет связи с сервером сборок."};}catch(Exception){return new BuildStatus{Message="Не удалось прочитать состояние сборки Unity."};}}
  static Dictionary<string,object> Obj(object o){return o as Dictionary<string,object> ?? new Dictionary<string,object>();}
  static object Get(Dictionary<string,object> o,string key){object v;return o.TryGetValue(key,out v)?v:null;}
  static string Text(Dictionary<string,object> o,string key){var v=Get(o,key);return v==null?"":Convert.ToString(v,CultureInfo.InvariantCulture);}
  static bool Bool(Dictionary<string,object> o,string key){return Get(o,key) is bool&&(bool)Get(o,key);}
  static IEnumerable<object> Items(object o){var a=o as System.Collections.IEnumerable;if(a==null||o is string||o is Dictionary<string,object>)return new object[0];return a.Cast<object>();}
  static double? Number(object v){double n;if(v!=null&&double.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out n))return n;return null;}
  static string Duration(double seconds){int s=(int)Math.Max(0,seconds);return s>=3600?(s/3600)+" ч "+(s%3600/60)+" мин":s>=60?(s/60)+" мин "+(s%60)+" с":s+" с";}
  public static BuildStatus Parse(string json){
   var root=Json.Read<Dictionary<string,object>>(json);var jobs=Items(Get(root,"jobs")).Select(Obj).Where(j=>Text(j,"project")=="unity"&&Text(j,"kind")!="check").ToList();
   var job=jobs.FirstOrDefault(j=>Text(j,"status")=="running")??jobs.FirstOrDefault(j=>Text(j,"status")=="waiting");
   if(job==null)return new BuildStatus{Message="Сейчас сборка Unity не выполняется."};
   var progress=Obj(Get(job,"progress"));var build=Obj(Get(progress,"build"));var stage=Items(Get(progress,"stages")).Select(Obj).FirstOrDefault(x=>Text(x,"key")=="build");var detail=stage==null?new Dictionary<string,object>():Obj(Get(stage,"detail"));
   bool waiting=Text(job,"status")=="waiting"||Bool(progress,"waiting");bool active=Text(job,"status")=="running";double? pct=Number(Get(build,"percent"))??Number(Get(detail,"percent"))??Number(Get(stage??new Dictionary<string,object>(),"percent"));
   string step=waiting?"Ожидание подтверждения рассылки":Text(build,"label");if(step=="")step=Text(detail,"label");if(step==""&&stage!=null)step=Text(stage,"label");if(step=="")step=Text(progress,"stage");if(step=="")step="Сборка Unity";
   var elapsed=Number(Get(progress,"elapsed"));string eta=Text(build,"eta_text");if(eta=="")eta=Text(detail,"eta_text");
   return new BuildStatus{Message="Сборка Unity #"+Text(job,"id"),Step=step,Elapsed=elapsed.HasValue?"Прошло: "+Duration(elapsed.Value):"Прошло: нет данных",Remaining=waiting?"Осталось: ожидание подтверждения":eta!=""?"Осталось: "+eta:"Осталось: нет данных",Percent=waiting?100:Math.Max(0,Math.Min(100,pct??0)),HasProgress=waiting||pct.HasValue,Waiting=waiting,Active=active||waiting};
  }
 }
}
