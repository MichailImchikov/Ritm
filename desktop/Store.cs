using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ritm {
 public sealed partial class Store {
  public readonly string Root;
  public Preferences Settings;
  public string Status="Сохранено на компьютере";
  public bool SyncError;
  readonly Dictionary<string,TaskItem> items=new Dictionary<string,TaskItem>();
  string Local {get{return Path.Combine(Root,"tasks");}}
  public Store(string root){
   Root=root;Directory.CreateDirectory(Local);Directory.CreateDirectory(Path.Combine(Root,"history"));
   var config=Path.Combine(Root,"settings.json");
   Settings=File.Exists(config)?Json.Read<Preferences>(File.ReadAllText(config)):new Preferences();
   if(string.IsNullOrEmpty(Settings.DeviceId))Settings.DeviceId=Guid.NewGuid().ToString();
   SavePreferences();LoadLocal();Sync();
  }
  public List<TaskItem> Tasks {get{return items.Values.Where(t=>!t.deleted&&t.recordType!="project").Select(t=>t.Clone()).ToList();}}
  public List<TaskItem> AllRecords {get{return items.Values.Select(t=>t.Clone()).ToList();}}
  public IEnumerable<string> ProjectNames {get{return items.Values.Where(t=>!t.deleted).Select(t=>t.recordType=="project"?t.title:t.category).Where(n=>n!="Без проекта").Distinct().OrderBy(n=>n).ToList();}}
  public void SetTheme(string theme){Settings.Theme=theme;SavePreferences();}
  public void SetPalette(string palette){if(!new[]{"violet","ocean","sage","amber","rose"}.Contains(palette))throw new Exception("Неизвестная палитра.");Settings.Palette=palette;SavePreferences();}
  public void SaveLocalPreferences(){SavePreferences();}
  public string GetTaskLayout(string context,string fallback="board"){string layout;return Settings.TaskLayouts!=null&&Settings.TaskLayouts.TryGetValue(context,out layout)&&(layout=="board"||layout=="list")?layout:fallback;}
  public void SetTaskLayout(string context,string layout){if(layout!="board"&&layout!="list")throw new Exception("Неизвестный вид задач.");if(Settings.TaskLayouts==null)Settings.TaskLayouts=new Dictionary<string,string>();Settings.TaskLayouts[context]=layout;SavePreferences();}
  public void CreateProject(string name){name=(name??"").Trim();if(name=="Без проекта"||name.Length==0||name.Length>50)throw new Exception("Название проекта: от 1 до 50 символов.");if(ProjectNames.Any(n=>n.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new Exception("Проект с таким названием уже есть.");Save(new TaskItem{title=name,category=name,recordType="project"},0);}
  public void RenameProject(string oldName,string newName){newName=(newName??"").Trim();if(oldName==newName)return;if(IsTeamName(oldName))throw new Exception("Общий проект нельзя переименовать: его название видят все участники.");if(newName.Length==0||newName.Length>50||newName=="Без проекта"||ProjectNames.Any(n=>n.Equals(newName,StringComparison.OrdinalIgnoreCase)))throw new Exception("Выберите другое название проекта (до 50 символов).");foreach(var t in AllRecords.Where(t=>!t.deleted&&t.category==oldName)){t.category=newName;if(t.recordType=="project")t.title=newName;Save(t,t.version);}if(Settings.ProjectGit!=null&&Settings.ProjectGit.ContainsKey(oldName)){Settings.ProjectGit[newName]=Settings.ProjectGit[oldName];Settings.ProjectGit.Remove(oldName);SavePreferences();}if(Settings.TaskLayouts!=null&&Settings.TaskLayouts.ContainsKey("project:"+oldName)){Settings.TaskLayouts["project:"+newName]=Settings.TaskLayouts["project:"+oldName];Settings.TaskLayouts.Remove("project:"+oldName);SavePreferences();}}
  public void DeleteProject(string name){if(IsTeamName(name))throw new Exception("Общий проект можно только отключить на этом компьютере.");foreach(var t in AllRecords.Where(t=>!t.deleted&&t.category==name)){if(t.recordType=="project")Delete(t);else{t.category="Без проекта";Save(t,t.version);}}if(Settings.ProjectGit!=null&&Settings.ProjectGit.Remove(name))SavePreferences();}
  public List<TaskItem> PersonalRecords {get{return items.Values.Where(t=>!IsTeamName(t.category)).Select(t=>t.Clone()).ToList();}}
  public bool MergeRecords(IEnumerable<TaskItem> incoming){bool changed=false;foreach(var t in incoming){t.Validate();if(IsTeamName(t.category))continue;DateTimeOffset stamp;if(t.version<1||string.IsNullOrEmpty(t.deviceId)||!DateTimeOffset.TryParse(t.updatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out stamp))throw new Exception("Некорректная облачная запись.");TaskItem old;if(!items.TryGetValue(t.id,out old)||Compare(t,old)>0){if(old!=null)Archive(old,"history");Atomic(Path.Combine(Local,t.id+".json"),Json.Write(t));items[t.id]=t.Clone();changed=true;}else if(Json.Write(old)!=Json.Write(t))Archive(t,"history");}return changed;}
  void SavePreferences(){Atomic(Path.Combine(Root,"settings.json"),Json.Write(Settings));}
  public void SetSync(string path){
   if(!string.IsNullOrWhiteSpace(path)){
    path=Path.GetFullPath(path);
    string fullRoot=Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    if((path.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar).StartsWith(fullRoot,StringComparison.OrdinalIgnoreCase))throw new Exception("Выберите отдельную общую папку, вне локального хранилища Ритма.");
    Directory.CreateDirectory(path);
    var test=Path.Combine(path,".ritm-write-"+Guid.NewGuid().ToString("N"));File.WriteAllText(test,"test");File.Delete(test);
   }else path=null;
   Settings.SyncDirectory=path;SavePreferences();Sync();
  }
  public TaskItem Save(TaskItem value,int expectedVersion){
   LoadLocal();Sync();value.Validate();TaskItem current;
   if(items.TryGetValue(value.id,out current)){
    if(current.version!=expectedVersion||current.deleted||current.updatedUtc!=value.updatedUtc||current.deviceId!=value.deviceId)throw new Exception("Задача изменилась на другом устройстве. Ваш текст остался в форме. Скопируйте его и откройте свежую версию задачи.");
    if(current.category!=value.category&&IsTeamName(current.category))throw new Exception("Задачу общего проекта нельзя перенести в другой проект — её видят остальные участники. Создайте копию.");
   }else if(expectedVersion!=0)throw new Exception("Исходная задача не найдена.");
   var saved=value.Clone();if(current==null&&saved.createdBy==null&&saved.recordType=="task")saved.createdBy=Me(saved.category);saved.version=expectedVersion+1;saved.updatedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);saved.deviceId=Settings.DeviceId;
   if(saved.createdAt==0)saved.createdAt=(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalMilliseconds;
   var path=Path.Combine(Local,saved.id+".json");if(current!=null)Archive(current,"history");Atomic(path,Json.Write(saved));items[saved.id]=saved;Place(saved);Sync();return saved.Clone();
  }
  public void Delete(TaskItem value){var tombstone=value.Clone();tombstone.deleted=true;Save(tombstone,value.version);}
  void LoadLocal(){
   foreach(var file in Directory.GetFiles(Local,"*.json")){
    var t=ReadRecord(file);TaskItem old;if(!items.TryGetValue(t.id,out old)||Compare(t,old)>0)items[t.id]=t;
   }
  }
  TaskItem ReadRecord(string file){
   if(new FileInfo(file).Length>2000000)throw new Exception("Слишком большой файл задачи: "+Path.GetFileName(file));
   var t=Json.Read<TaskItem>(File.ReadAllText(file,Encoding.UTF8));if(t==null)throw new Exception("Пустой файл задачи.");t.Validate();
   DateTimeOffset stamp;
   if(t.id!=Path.GetFileNameWithoutExtension(file)||t.version<1||!DateTimeOffset.TryParse(t.updatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out stamp)||string.IsNullOrEmpty(t.deviceId))throw new Exception("Повреждён файл задачи: "+Path.GetFileName(file));
   return t;
  }
  static int Compare(TaskItem a,TaskItem b){int c=DateTimeOffset.Parse(a.updatedUtc,CultureInfo.InvariantCulture).CompareTo(DateTimeOffset.Parse(b.updatedUtc,CultureInfo.InvariantCulture));return c!=0?c:string.CompareOrdinal(a.deviceId,b.deviceId);}
  void Archive(TaskItem t,string kind){
   string raw=Json.Write(t),hash;
   using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-","").Substring(0,16);
   string dir=Path.Combine(Root,kind);Directory.CreateDirectory(dir);string path=Path.Combine(dir,t.id+"-"+hash+".json");if(!File.Exists(path))Atomic(path,raw);
  }
  public bool Sync(){bool teams=SyncTeams();return SyncShared()|teams;}
  bool SyncShared(){
   SyncError=false;
   if(string.IsNullOrEmpty(Settings.SyncDirectory)){Status="Сохранено на компьютере";return false;}
   bool changed=false;
   try{
    if(!Directory.Exists(Settings.SyncDirectory))throw new IOException("Общая папка недоступна.");
    string shared=Path.Combine(Settings.SyncDirectory,"RitmData","tasks");Directory.CreateDirectory(shared);
    foreach(var file in Directory.GetFiles(shared,"*.json")){
     TaskItem incoming;
     try{incoming=ReadRecord(file);}catch(Exception){SyncError=true;continue;}
     if(!IsTeamName(incoming.category)&&Absorb(incoming))changed=true;
    }
    foreach(var t in items.Values){if(IsTeamName(t.category))continue;var path=Path.Combine(shared,t.id+".json");if(!File.Exists(path)){Atomic(path,Json.Write(t));continue;}TaskItem remote;try{remote=ReadRecord(path);}catch{SyncError=true;continue;}if(Compare(t,remote)>0)Atomic(path,Json.Write(t));}
    Status=SyncError?"Есть нечитаемые файлы в общей папке. Локальные задачи сохранены.":"Общая папка обновлена · "+DateTime.Now.ToString("HH:mm");
   }catch(Exception e){SyncError=true;Status="Локально сохранено. Синхронизация: "+e.Message;}
   return changed;
  }
  bool Absorb(TaskItem incoming){
   TaskItem local;
   if(!items.TryGetValue(incoming.id,out local)){Atomic(Path.Combine(Local,incoming.id+".json"),Json.Write(incoming));items[incoming.id]=incoming;return true;}
   if(Json.Write(incoming)==Json.Write(local))return false;
   // Keep both versions before deterministic resolution, including deletion conflicts.
   Archive(local,"history");Archive(incoming,"history");
   if(Compare(incoming,local)<=0)return false;
   Atomic(Path.Combine(Local,incoming.id+".json"),Json.Write(incoming));items[incoming.id]=incoming;return true;
  }
  public void Export(string path){Atomic(path,Json.Write(new BackupDocument {app="ritm-desktop",version=2,tasks=Tasks,projects=AllRecords.Where(t=>!t.deleted&&t.recordType=="project").ToList()}));}
  public List<TaskItem> ReadImport(string path){
   if(new FileInfo(path).Length>10000000)throw new Exception("Размер резервной копии не должен превышать 10 МБ.");
   var backup=Json.Read<BackupDocument>(File.ReadAllText(path,Encoding.UTF8));
   if(backup==null||backup.tasks==null||backup.tasks.Count>10000)throw new Exception("Некорректная резервная копия.");
   var list=backup.tasks.Concat(backup.projects??new List<TaskItem>()).ToList();if(list.Count>10000)throw new Exception("В копии слишком много записей.");
   foreach(var task in list){task.id=Guid.NewGuid().ToString();task.version=0;task.createdAt=0;task.deleted=false;task.Validate();}return list;
  }
  public void Import(List<TaskItem> list){foreach(var t in list)Save(t,0);}
  static void Atomic(string path,string content){
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllText(temp,content,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
}

