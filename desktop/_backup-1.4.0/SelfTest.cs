using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ritm {
 public static class SelfTest {
  static void Assert(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);}
  static void Fails(Action action,string label){bool failed=false;try{action();}catch{failed=true;}Assert(failed,label);}
  public static int Run(string root){
   Directory.CreateDirectory(root);string result=Path.Combine(root,"result.txt");
   try{
    var a=new Store(Path.Combine(root,"a"));var b=new Store(Path.Combine(root,"b"));var shared=Path.Combine(root,"shared");a.SetSync(shared);b.SetSync(shared);
    var task=a.Save(new TaskItem{title="Проверка",description="offline persistence",date="2026-10-28",category="Работа",priority=2,subtasks={new Step{id="test-step",title="Первый шаг"}}},0);
    Assert(new Store(Path.Combine(root,"a")).Tasks.Single().title=="Проверка","restart persistence");b.Sync();var stale=b.Tasks.Single();Assert(stale.subtasks.Count==1,"sync and subtasks");
    var update=task.Clone();update.completed=true;update.subtasks[0].done=true;task=a.Save(update,task.version);Assert(task.version==2&&task.completed,"completion and version");
    Fails(()=>b.Save(stale,stale.version),"stale edit rejection");Assert(b.Tasks.Single().completed,"refresh before stale write");
    var bad=task.Clone();bad.date="2026-02-30";Fails(()=>a.Save(bad,bad.version),"invalid calendar date");
    a.SetSync(null);b.SetSync(null);var ax=a.Tasks.Single();ax.title="Изменено на A";ax=a.Save(ax,ax.version);Thread.Sleep(20);var bx=b.Tasks.Single();bx.title="Изменено на B";bx=b.Save(bx,bx.version);
    a.SetSync(shared);b.SetSync(shared);a.Sync();Assert(a.Tasks.Single().title=="Изменено на B"&&b.Tasks.Single().title=="Изменено на B","offline branch convergence");Assert(Directory.GetFiles(Path.Combine(root,"a","history")).Length>=2,"previous versions retained");
    string backup=Path.Combine(root,"backup.json");a.Export(backup);var imported=b.ReadImport(backup);Assert(imported.Count==1&&imported[0].id!=a.Tasks.Single().id&&imported[0].version==0,"import creates new identities");
    a.Delete(a.Tasks.Single());b.Sync();Assert(a.Tasks.Count==0&&b.Tasks.Count==0,"tombstone propagation");Fails(()=>b.Save(bx,bx.version),"deleted task cannot silently resurrect");
    var restored=a.ReadImport(backup);a.Import(restored);Assert(a.Tasks.Count==1&&a.Tasks.Single().subtasks[0].done,"backup recovery");
    Directory.Move(shared,shared+"-unavailable");a.Sync();Assert(a.SyncError&&a.Tasks.Count==1,"unavailable sync keeps local tasks");var local=a.Tasks.Single();local.title="Работа без сети";a.Save(local,local.version);Assert(a.Tasks.Single().title=="Работа без сети","offline save succeeds");
    a.CreateProject("Пустой проект");Assert(a.ProjectNames.Contains("Пустой проект"),"empty projects retained");Assert(a.Tasks.Count==1,"projects excluded from tasks");
    var board=a.Tasks.Single();board.category="Пустой проект";board.status="doing";board.completed=false;board=a.Save(board,board.version);Assert(new Store(Path.Combine(root,"a")).Tasks.Single().status=="doing","kanban state restart");
    a.RenameProject("Пустой проект","Новый проект");Assert(a.Tasks.Single().category=="Новый проект"&&!a.ProjectNames.Contains("Пустой проект"),"project rename updates assignments");
    a.DeleteProject("Новый проект");Assert(a.Tasks.Single().category=="Без проекта"&&!a.ProjectNames.Contains("Новый проект"),"project removal preserves tasks");
    a.SetTheme("dark");Assert(new Store(Path.Combine(root,"a")).Settings.Theme=="dark","theme persisted");
    Assert(GoogleDriveClient.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")=="E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM","PKCE RFC 7636 vector");
    var google=new GoogleDriveClient(Path.Combine(root,"oauth-test"));Assert(!google.IsConnected&&!google.IsConfigured,"no credentials means disconnected");
    string config=Path.Combine(root,"oauth-example.json");File.WriteAllText(config,Json.Write(new OAuthFile{installed=new OAuthClient{client_id="local-test.apps.googleusercontent.com",client_secret="test-secret-marker"}}));google.ImportConfiguration(config);Assert(google.IsConfigured&&!google.IsConnected,"client import does not fake connection");Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"oauth-test","google","client.bin"))).Contains("test-secret-marker"),"OAuth config protected by Windows DPAPI");
    var before=a.Tasks.Single();var eventName=GoogleDriveClient.EventName(before);before.title="Другая версия";Assert(GoogleDriveClient.EventName(before)!=eventName,"immutable cloud versions have distinct identities");
    var layouts=new Store(Path.Combine(root,"view-layouts"));layouts.CreateProject("Вид проекта");layouts.SetTaskLayout("project:Вид проекта","list");layouts.SetTaskLayout("tab:today","board");layouts.SetTaskLayout("tab:tasks","list");layouts=new Store(Path.Combine(root,"view-layouts"));Assert(layouts.GetTaskLayout("project:Вид проекта")=="list"&&layouts.GetTaskLayout("tab:today")=="board","project and day layouts restore independently");layouts.RenameProject("Вид проекта","Переименован");Assert(layouts.GetTaskLayout("project:Переименован")=="list","renamed project keeps its display preference");Fails(()=>layouts.SetTaskLayout("tab:tasks","invalid"),"unknown layout rejected");FocusTimerTest.Run(root);DriveProtocolTest.Run(root);File.WriteAllText(result,"PASS: per-context task layout persistence; project rename layout; focus start/pause/reset/restart; focus/break transition; palette persistence; Drive API protocol (mock); persistence; restart; validation; sync; conflicts; history; deletion; import/export; offline writes; empty projects; project rename/removal; kanban state; theme; PKCE standard vector; encrypted OAuth configuration; immutable cloud identities. Live Google authorization requires registered OAuth client.\r\n",Encoding.UTF8);return 0;
   }catch(Exception e){File.WriteAllText(result,e.ToString(),Encoding.UTF8);return 1;}
  }
 }
}



