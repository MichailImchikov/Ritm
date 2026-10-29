using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ritm {
 // Общий проект — папка в локальной сети: ritm-project.json (название), members\ (участники), tasks\ (по файлу на задачу).
 public class TeamLink {public string Path {get;set;} public string Name {get;set;} public string User {get;set;} }
 public class TeamManifest {public string app {get;set;} public int version {get;set;} public string name {get;set;} public string createdUtc {get;set;} public string createdBy {get;set;} }
 public class TeamMember {public string name {get;set;} public string lastSeenUtc {get;set;} }
 public sealed class TeamProbe {public string Path,Name;public bool Exists;public List<string> Members=new List<string>();}
 public sealed partial class Store {
  const string ManifestFile="ritm-project.json";
  sealed class Seen {public DateTime Stamp;public TaskItem Item;}
  readonly Dictionary<string,Seen> seen=new Dictionary<string,Seen>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,List<string>> roster=new Dictionary<string,List<string>>();
  readonly Dictionary<string,string> teamErrors=new Dictionary<string,string>();
  readonly Dictionary<string,DateTime> retryAt=new Dictionary<string,DateTime>();
  public List<TeamLink> Teams {get{if(Settings.Teams==null)Settings.Teams=new List<TeamLink>();return Settings.Teams;}}
  public TeamLink Team(string name){return name==null?null:Teams.FirstOrDefault(t=>t.Name==name);}
  public bool IsTeamName(string name){return Team(name)!=null;}
  public IEnumerable<string> TeamNames {get{return Teams.Select(t=>t.Name).OrderBy(n=>n).ToList();}}
  public string Me(string project){var t=Team(project);return t==null?null:t.User;}
  public static bool SamePerson(string a,string b){return a!=null&&b!=null&&string.Equals(a,b,StringComparison.CurrentCultureIgnoreCase);}
  // Личные задачи всегда «мои»; задачи общего проекта — если исполнитель совпадает с моим именем в этом проекте.
  public bool IsMine(TaskItem t){var team=Team(t.category);return team==null||SamePerson(t.assignee,team.User);}
  public string TeamError(string name){string e;return teamErrors.TryGetValue(name,out e)?e:null;}
  public string TeamStatus {get{var down=Teams.Where(t=>TeamError(t.Name)!=null).Select(t=>"«"+t.Name+"»").ToList();return down.Count==0?null:"Нет доступа к "+string.Join(", ",down)+" — изменения отправятся, когда папка станет доступна";}}
  public List<string> Members(string project){
   var names=new List<string>();List<string> listed;
   foreach(var team in project==null?Teams:Teams.Where(t=>t.Name==project)){names.Add(team.User);if(roster.TryGetValue(team.Name,out listed))names.AddRange(listed);}
   names.AddRange(items.Values.Where(t=>!t.deleted&&IsTeamName(t.category)&&(project==null||t.category==project)).SelectMany(t=>new[]{t.assignee,t.createdBy}));
   var result=new List<string>();foreach(var n in names)if(n!=null&&!result.Any(r=>SamePerson(r,n)))result.Add(n);
   return result.OrderBy(n=>n,StringComparer.CurrentCultureIgnoreCase).ToList();
  }
  public static TeamProbe ProbeTeam(string path){
   if(string.IsNullOrWhiteSpace(path))throw new Exception("Укажите папку на сервере.");
   path=Path.GetFullPath(path.Trim());if(!Directory.Exists(path))throw new Exception("Папка недоступна. Проверьте путь и доступ к сети.");
   var probe=new TeamProbe{Path=path};var manifest=ReadManifest(path);
   if(manifest!=null){probe.Exists=true;probe.Name=manifest.name;}else probe.Name=Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
   probe.Members=ReadMembers(path);return probe;
  }
  static TeamManifest ReadManifest(string path){
   var file=Path.Combine(path,ManifestFile);if(!File.Exists(file))return null;
   var m=Json.Read<TeamManifest>(File.ReadAllText(file,Encoding.UTF8));
   if(m==null||string.IsNullOrWhiteSpace(m.name)||m.name.Length>50)throw new Exception("Файл "+ManifestFile+" в общей папке повреждён.");
   m.name=m.name.Trim();return m;
  }
  static List<string> ReadMembers(string path){
   var list=new List<string>();var dir=Path.Combine(path,"members");if(!Directory.Exists(dir))return list;
   foreach(var file in Directory.GetFiles(dir,"*.json")){try{var m=Json.Read<TeamMember>(File.ReadAllText(file,Encoding.UTF8));var n=m==null?null:TaskItem.Person(m.name);if(n!=null)list.Add(n);}catch{}}
   return list;
  }
  static void WriteMember(TeamLink link){
   string key;using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(link.User.ToLower(CultureInfo.CurrentCulture)))).Replace("-","").Substring(0,16);
   Atomic(Path.Combine(link.Path,"members",key+".json"),Json.Write(new TeamMember{name=link.User,lastSeenUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)}));
  }
  public TeamLink ConnectTeam(string path,string projectName,string user){
   user=TaskItem.Person(user,"Ваше имя");if(user==null)throw new Exception("Введите своё имя — так вас увидят остальные участники.");
   var probe=ProbeTeam(path);path=probe.Path;
   string fullRoot=Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if((path.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar).StartsWith(fullRoot,StringComparison.OrdinalIgnoreCase))throw new Exception("Выберите папку на сервере, вне локального хранилища Ритма.");
   if(Teams.Any(t=>string.Equals(Path.GetFullPath(t.Path),path,StringComparison.OrdinalIgnoreCase)))throw new Exception("Эта папка уже подключена.");
   string name=probe.Exists?probe.Name:(projectName??"").Trim();
   if(name.Length==0||name.Length>50||name=="Без проекта")throw new Exception("Название проекта: от 1 до 50 символов.");
   if(ProjectNames.Any(n=>n.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new Exception("У вас уже есть проект «"+name+"». Переименуйте его, чтобы подключить общий проект с таким названием.");
   Directory.CreateDirectory(Path.Combine(path,"tasks"));Directory.CreateDirectory(Path.Combine(path,"members"));
   var test=Path.Combine(path,".ritm-write-"+Guid.NewGuid().ToString("N"));File.WriteAllText(test,"test");File.Delete(test);
   if(!probe.Exists)Atomic(Path.Combine(path,ManifestFile),Json.Write(new TeamManifest{app="ritm-team",version=1,name=name,createdUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture),createdBy=user}));
   var link=new TeamLink{Path=path,Name=name,User=user};WriteMember(link);
   Teams.Add(link);Settings.UserName=user;SavePreferences();Sync();
   if(!items.Values.Any(t=>!t.deleted&&t.recordType=="project"&&t.category==name))Save(new TaskItem{title=name,category=name,recordType="project"},0);
   return link;
  }
  public void SetTeamUser(string project,string user){
   var link=Team(project);if(link==null)throw new Exception("Общий проект не найден.");
   user=TaskItem.Person(user,"Ваше имя");if(user==null)throw new Exception("Введите своё имя.");
   link.User=user;Settings.UserName=user;SavePreferences();try{WriteMember(link);}catch{}
  }
  // Отключение убирает проект только с этого компьютера: задачи в общей папке остаются.
  public void DisconnectTeam(string project){
   var link=Team(project);if(link==null)return;
   foreach(var t in items.Values.Where(t=>t.category==project).ToList()){var file=Path.Combine(Local,t.id+".json");if(File.Exists(file))File.Delete(file);items.Remove(t.id);}
   foreach(var key in seen.Keys.Where(k=>k.StartsWith(link.Path,StringComparison.OrdinalIgnoreCase)).ToList())seen.Remove(key);
   roster.Remove(project);teamErrors.Remove(project);retryAt.Remove(project);
   Teams.Remove(link);if(Settings.TaskLayouts!=null)Settings.TaskLayouts.Remove("project:"+project);SavePreferences();
  }
  bool SyncTeams(){
   bool changed=false;
   foreach(var link in Teams.ToList()){
    DateTime wait;if(retryAt.TryGetValue(link.Name,out wait)&&DateTime.UtcNow<wait)continue;
    try{if(SyncTeam(link))changed=true;teamErrors.Remove(link.Name);retryAt.Remove(link.Name);}
    // Недоступный сервер может отвечать секундами — повторяем не чаще раза в 30 секунд, чтобы окно не подвисало.
    catch(Exception e){teamErrors[link.Name]=e.Message;retryAt[link.Name]=DateTime.UtcNow.AddSeconds(30);}
   }
   return changed;
  }
  bool SyncTeam(TeamLink link){
   if(!Directory.Exists(link.Path))throw new IOException("Папка недоступна.");
   string dir=Path.Combine(link.Path,"tasks");Directory.CreateDirectory(dir);bool changed=false;
   var present=new HashSet<string>();
   foreach(var file in Directory.GetFiles(dir,"*.json")){
    present.Add(Path.GetFileNameWithoutExtension(file));
    // Файлы перечитываются только после изменения — в сети это заметно быстрее.
    DateTime stamp=File.GetLastWriteTimeUtc(file);Seen known;
    if(!seen.TryGetValue(file,out known)||known.Stamp!=stamp){try{known=new Seen{Stamp=stamp,Item=ReadRecord(file)};seen[file]=known;}catch{continue;}}
    if(known.Item.category==link.Name&&Absorb(known.Item.Clone()))changed=true;
   }
   foreach(var t in items.Values.Where(t=>t.category==link.Name).ToList()){
    var path=Path.Combine(dir,t.id+".json");Seen remote;
    if(present.Contains(t.id)&&(!seen.TryGetValue(path,out remote)||Compare(t,remote.Item)<=0))continue;
    Atomic(path,Json.Write(t));seen[path]=new Seen{Stamp=File.GetLastWriteTimeUtc(path),Item=t.Clone()};
   }
   roster[link.Name]=ReadMembers(link.Path);
   return changed;
  }
 }
}
