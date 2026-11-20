using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ritm {
 // Синхронизация с папками в сети: общей папкой личных задач и папками общих проектов.
 // Проход делится на три шага, чтобы окно не ждало сеть:
 //  1. Scan (фоновый поток) — список файлов с датами изменения; читаются только новые и изменённые файлы.
 //  2. Apply (поток окна) — слияние прочитанного с задачами в памяти, без обращений к сети.
 //  3. Push (фоновый поток) — запись на сервер задач, которые здесь новее, и отправка вложений.
 // Фоновые шаги работают только со своими объектами-снимками и не трогают состояние Store.
 public sealed partial class Store {
  sealed class SyncTarget {public string Root,Tasks;public TeamLink Team;}
  sealed class RemoteFile {public string Path;public DateTime Stamp;public TaskItem Item;public bool Broken;}
  sealed class MemberFile {public DateTime Stamp;public string Name;}
  sealed class TargetScan {public SyncTarget Target;public string Error;public readonly List<RemoteFile> Files=new List<RemoteFile>();public Dictionary<string,MemberFile> Members;}
  sealed class PendingWrite {public SyncTarget Target;public string Path,Json,Error;public TaskItem Item;public DateTime Stamp;}
  sealed class PendingCopy {public string Local,Remote;}
  sealed class SyncPlan {public readonly List<SyncTarget> Targets=new List<SyncTarget>();public Dictionary<string,DateTime> Known;public readonly Dictionary<string,Dictionary<string,MemberFile>> Members=new Dictionary<string,Dictionary<string,MemberFile>>(StringComparer.OrdinalIgnoreCase);}
  sealed class PushPlan {public readonly List<PendingWrite> Writes=new List<PendingWrite>();public readonly List<PendingCopy> Copies=new List<PendingCopy>();}
  // Файлы участников по папке проекта: перечитываются только после изменения.
  readonly Dictionary<string,Dictionary<string,MemberFile>> memberFiles=new Dictionary<string,Dictionary<string,MemberFile>>(StringComparer.OrdinalIgnoreCase);
  bool syncing;

  // Синхронный проход — для явных действий (подключение проекта, выбор папки) и проверок.
  public bool Sync(){
   var plan=PlanSync();bool changed=Apply(plan,Scan(plan));
   var push=PlanPush();if(push.Writes.Count+push.Copies.Count>0){Push(push);Confirm(push);}
   return changed;
  }
  // Фоновый проход для окна. Возвращает true, если пришли изменения и окно нужно перерисовать.
  public async Task<bool> SyncAsync(){
   if(syncing)return false;syncing=true;
   try{
    var plan=PlanSync();bool changed=false;
    if(plan.Targets.Count>0){var scans=await Task.Run(()=>Scan(plan));changed=Apply(plan,scans);}else Apply(plan,new List<TargetScan>());
    var push=PlanPush();if(push.Writes.Count+push.Copies.Count>0){await Task.Run(()=>Push(push));Confirm(push);}
    return changed;
   }finally{syncing=false;}
  }

  SyncPlan PlanSync(){
   var plan=new SyncPlan{Known=seen.ToDictionary(kv=>kv.Key,kv=>kv.Value.Stamp,StringComparer.OrdinalIgnoreCase)};
   foreach(var link in Teams.ToList()){
    // Недоступный сервер может отвечать секундами — после ошибки повторяем не чаще раза в 30 секунд.
    DateTime wait;if(retryAt.TryGetValue(link.Name,out wait)&&DateTime.UtcNow<wait)continue;
    plan.Targets.Add(new SyncTarget{Root=link.Path,Tasks=Path.Combine(link.Path,"tasks"),Team=link});
    Dictionary<string,MemberFile> known;plan.Members[link.Path]=memberFiles.TryGetValue(link.Path,out known)?new Dictionary<string,MemberFile>(known,StringComparer.OrdinalIgnoreCase):null;
   }
   if(!string.IsNullOrEmpty(Settings.SyncDirectory))plan.Targets.Add(new SyncTarget{Root=Settings.SyncDirectory,Tasks=Path.Combine(Settings.SyncDirectory,"RitmData","tasks")});
   return plan;
  }

  static List<TargetScan> Scan(SyncPlan plan){return plan.Targets.Select(t=>ScanTarget(t,plan)).ToList();}
  static TargetScan ScanTarget(SyncTarget target,SyncPlan plan){
   var scan=new TargetScan{Target=target};
   try{
    if(!Directory.Exists(target.Root))throw new IOException(target.Team==null?"Общая папка недоступна.":"Папка недоступна.");
    Directory.CreateDirectory(target.Tasks);
    // Дата изменения приходит вместе со списком файлов — отдельный запрос к серверу на каждый файл не нужен.
    foreach(var info in new DirectoryInfo(target.Tasks).EnumerateFiles("*.json")){
     var file=new RemoteFile{Path=info.FullName,Stamp=info.LastWriteTimeUtc};DateTime known;
     if(!plan.Known.TryGetValue(file.Path,out known)||known!=file.Stamp){try{file.Item=ReadRecord(file.Path);}catch{file.Broken=true;}}
     scan.Files.Add(file);
    }
    if(target.Team!=null){Dictionary<string,MemberFile> known;plan.Members.TryGetValue(target.Root,out known);scan.Members=ScanMembers(target.Root,known);}
   }catch(Exception e){scan.Error=e.Message;}
   return scan;
  }
  static Dictionary<string,MemberFile> ScanMembers(string root,Dictionary<string,MemberFile> known){
   var result=new Dictionary<string,MemberFile>(StringComparer.OrdinalIgnoreCase);var dir=Path.Combine(root,"members");if(!Directory.Exists(dir))return result;
   foreach(var info in new DirectoryInfo(dir).EnumerateFiles("*.json")){
    MemberFile old;if(known!=null&&known.TryGetValue(info.FullName,out old)&&old.Stamp==info.LastWriteTimeUtc){result[info.FullName]=old;continue;}
    string name=null;try{var m=Json.Read<TeamMember>(File.ReadAllText(info.FullName,Encoding.UTF8));name=m==null?null:TaskItem.Person(m.name);}catch{}
    result[info.FullName]=new MemberFile{Stamp=info.LastWriteTimeUtc,Name=name};
   }
   return result;
  }

  // Проект могли отключить или папку сменить, пока шёл фоновый проход, — такие результаты не применяются.
  bool Current(SyncTarget t){return t.Team!=null?Teams.Contains(t.Team):Settings.SyncDirectory==t.Root;}
  readonly Dictionary<string,TargetScan> lastScans=new Dictionary<string,TargetScan>(StringComparer.OrdinalIgnoreCase);

  bool Apply(SyncPlan plan,List<TargetScan> scans){
   bool changed=false;lastScans.Clear();
   if(string.IsNullOrEmpty(Settings.SyncDirectory)){SyncError=false;Status="Сохранено на компьютере";}
   foreach(var scan in scans){
    var target=scan.Target;if(!Current(target))continue;
    if(scan.Error!=null){Fail(target,scan.Error);continue;}
    if(target.Team!=null){teamErrors.Remove(target.Team.Name);retryAt.Remove(target.Team.Name);}
    bool broken=false;
    foreach(var file in scan.Files){
     if(file.Broken){broken=true;continue;}
     Seen known;
     if(file.Item!=null){known=new Seen{Stamp=file.Stamp,Item=file.Item};seen[file.Path]=known;}
     // Неизменившийся файл уже учтён в прошлый раз.
     else if(!seen.TryGetValue(file.Path,out known)||items.ContainsKey(known.Item.id))continue;
     var incoming=known.Item;
     if(target.Team!=null){
      if(incoming.category!=target.Team.Name)continue;
      TaskItem before;items.TryGetValue(incoming.id,out before);
      if(Absorb(incoming.Clone())){changed=true;Track(target.Team,before,items[incoming.id]);}
     }else if(!IsTeamName(incoming.category)&&Absorb(incoming.Clone()))changed=true;
    }
    if(target.Team!=null){memberFiles[target.Root]=scan.Members;roster[target.Team.Name]=scan.Members.Values.Where(m=>m.Name!=null).Select(m=>m.Name).ToList();}
    else{SyncError=broken;Status=broken?"Есть нечитаемые файлы в общей папке. Локальные задачи сохранены.":"Общая папка обновлена · "+DateTime.Now.ToString("HH:mm");}
    lastScans[target.Tasks]=scan;
   }
   return changed;
  }
  void Fail(SyncTarget target,string error){
   if(target.Team!=null){teamErrors[target.Team.Name]=error;retryAt[target.Team.Name]=DateTime.UtcNow.AddSeconds(30);}
   else{SyncError=true;Status="Локально сохранено. Синхронизация: "+error;}
  }

  // Что отправить на сервер: только по папкам, которые сейчас ответили.
  PushPlan PlanPush(){
   var push=new PushPlan();
   foreach(var scan in lastScans.Values){
    var target=scan.Target;if(!Current(target))continue;
    var present=new HashSet<string>(scan.Files.Select(f=>f.Path),StringComparer.OrdinalIgnoreCase);
    var broken=new HashSet<string>(scan.Files.Where(f=>f.Broken).Select(f=>f.Path),StringComparer.OrdinalIgnoreCase);
    foreach(var t in items.Values.Where(t=>target.Team!=null?t.category==target.Team.Name:!IsTeamName(t.category))){
     var path=Path.Combine(target.Tasks,t.id+".json");Seen remote;
     // Нечитаемый файл в общей папке не перезаписывается; в общем проекте его заменяет локальная версия.
     if(target.Team==null&&broken.Contains(path))continue;
     if(present.Contains(path)&&seen.TryGetValue(path,out remote)&&Compare(t,remote.Item)<=0)continue;
     push.Writes.Add(new PendingWrite{Target=target,Path=path,Json=Json.Write(t),Item=t.Clone()});
    }
    if(target.Team==null)continue;
    // Вложения задач общего проекта уходят на сервер; локальная копия удаляется после отправки.
    foreach(var t in items.Values.Where(t=>t.category==target.Team.Name&&!t.deleted&&t.attachments!=null))
     foreach(var a in t.attachments){if(!TaskItem.SafeFile(a.file))continue;var local=Path.Combine(LocalFiles,a.file);if(File.Exists(local))push.Copies.Add(new PendingCopy{Local=local,Remote=Path.Combine(target.Root,FilesFolder,a.file)});}
   }
   return push;
  }
  static void Push(PushPlan push){
   var down=new HashSet<SyncTarget>();
   foreach(var w in push.Writes){
    // После первой ошибки остальные файлы этой папки не пробуем: каждый ждал бы таймаута сети.
    if(down.Contains(w.Target)){w.Error="Папка недоступна.";continue;}
    try{Atomic(w.Path,w.Json);w.Stamp=File.GetLastWriteTimeUtc(w.Path);}catch(Exception e){w.Error=e.Message;down.Add(w.Target);}
   }
   foreach(var c in push.Copies){
    try{
     Directory.CreateDirectory(Path.GetDirectoryName(c.Remote));
     if(!File.Exists(c.Remote)){var temp=c.Remote+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.Copy(c.Local,temp);File.Move(temp,c.Remote);}finally{if(File.Exists(temp))File.Delete(temp);}}
     File.Delete(c.Local);
    }catch{}
   }
  }
  void Confirm(PushPlan push){
   foreach(var w in push.Writes){
    if(w.Error==null)seen[w.Path]=new Seen{Stamp=w.Stamp,Item=w.Item};
    else if(Current(w.Target))Fail(w.Target,w.Error);
   }
  }
 }
}
