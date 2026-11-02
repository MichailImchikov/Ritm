using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Ritm {
 sealed class MockDrive:HttpMessageHandler {
  public readonly Dictionary<string,Tuple<string,string>> Files=new Dictionary<string,Tuple<string,string>>();public readonly Dictionary<string,string> Created=new Dictionary<string,string>();public int Patches;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){
   if(request.Headers.Authorization==null||request.Headers.Authorization.Scheme!="Bearer")throw new Exception("Missing authorization header");
   if(request.Method==HttpMethod.Get&&request.RequestUri.AbsolutePath=="/drive/v3/files")return Reply(Json.Write(new DriveListing{files=Files.Select(p=>new DriveFile{id=p.Key,name=p.Value.Item1,createdTime=Created.ContainsKey(p.Key)?Created[p.Key]:null}).ToList()}));
   if(request.Method.Method=="PATCH"){string id=request.RequestUri.AbsolutePath.Split('/').Last();Files[id]=Tuple.Create(Files[id].Item1,await request.Content.ReadAsStringAsync());Patches++;return Reply("{}");}
   if(request.Method==HttpMethod.Delete){Files.Remove(request.RequestUri.AbsolutePath.Split('/').Last());return Reply("{}");}
   if(request.Method==HttpMethod.Get){string id=request.RequestUri.AbsolutePath.Split('/').Last();return Reply(Files[id].Item2);}
   if(request.Method==HttpMethod.Post){var multipart=request.Content as MultipartContent;if(multipart==null||multipart.Headers.ContentType.MediaType!="multipart/related")throw new Exception("Expected multipart/related upload");var parts=multipart.ToList();var metadata=Json.Read<Dictionary<string,object>>(await parts[0].ReadAsStringAsync());if(!Json.Write(metadata["parents"]).Contains("appDataFolder"))throw new Exception("Unexpected Drive storage area");string name=(string)metadata["name"],body=await parts[1].ReadAsStringAsync(),id="file_"+Guid.NewGuid().ToString("N");Files[id]=Tuple.Create(name,body);return Reply(Json.Write(new DriveFile{id=id,name=name}));}throw new Exception("Unexpected Drive request");
  }
  static HttpResponseMessage Reply(string json){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,System.Text.Encoding.UTF8,"application/json")};}
 }
 public static class DriveProtocolTest {
  public static void Run(string root){
   var transport=new MockDrive();var a=new Store(Path.Combine(root,"drive-a"));var b=new Store(Path.Combine(root,"drive-b"));var ca=new GoogleDriveClient(a.Root,new HttpClient(transport,false),()=>Task.FromResult("mock-token"));var cb=new GoogleDriveClient(b.Root,new HttpClient(transport,false),()=>Task.FromResult("mock-token"));
   a.CreateProject("Облачный проект");a.Save(new TaskItem{title="Облачная задача",category="Облачный проект",status="doing"},0);ca.PullAsync().GetAwaiter().GetResult();ca.PushAsync(a.AllRecords).GetAwaiter().GetResult();int count=transport.Files.Count;ca.PushAsync(a.AllRecords).GetAwaiter().GetResult();Check(transport.Files.Count==count,"repeat push must not duplicate versions");
   b.MergeRecords(cb.PullAsync().GetAwaiter().GetResult());Check(b.ProjectNames.Contains("Облачный проект")&&b.Tasks.Single().status=="doing","Drive pull restores projects and board state");var onB=b.Tasks.Single();onB.title="Версия B";b.Save(onB,onB.version);cb.PushAsync(b.AllRecords).GetAwaiter().GetResult();a.MergeRecords(ca.PullAsync().GetAwaiter().GetResult());Check(a.Tasks.Single().title=="Версия B","updates converge through Drive");
   a.Delete(a.Tasks.Single());ca.PushAsync(a.AllRecords).GetAwaiter().GetResult();b.MergeRecords(cb.PullAsync().GetAwaiter().GetResult());Check(b.Tasks.Count==0,"cloud tombstones preserve deletion");var first=transport.Files.First();transport.Files[first.Key]=Tuple.Create(first.Value.Item1,first.Value.Item2.Replace("Облачный проект","Повреждённый проект"));var fresh=new GoogleDriveClient(Path.Combine(root,"drive-integrity"),new HttpClient(transport,false),()=>Task.FromResult("mock-token"));bool rejected=false;try{fresh.PullAsync().GetAwaiter().GetResult();}catch{rejected=true;}Check(rejected,"tampered cloud payload rejected");
   Bridge(root);
  }
  // Мост телефона: задача с телефона через Диск попадает в папку проекта, задача из папки — на Диск, а записи отключённого проекта не становятся личными.
  static void Bridge(string root){
   string folder=Path.Combine(root,"bridge-server");Directory.CreateDirectory(folder);var transport=new MockDrive();var pc=new Store(Path.Combine(root,"bridge-pc"));var drive=new GoogleDriveClient(pc.Root,new HttpClient(transport,false),()=>Task.FromResult("mock-token"));
   pc.ConnectTeam(folder,"Команда","Анна");var onPc=pc.Save(new TaskItem{title="С компьютера",category="Команда"},0);drive.PushAsync(pc.TeamRecords,GoogleDriveClient.TeamPrefix).GetAwaiter().GetResult();
   Check(drive.PullAsync().GetAwaiter().GetResult().Count==0,"team records stay out of the personal stream");Check(transport.Files.Values.All(f=>f.Item1.StartsWith(GoogleDriveClient.TeamPrefix)),"team records use their own prefix");
   var phone=new TaskItem{title="С телефона",category="Команда",assignee="Анна",version=1,deviceId="phone",updatedUtc=DateTime.UtcNow.ToString("o")};phone.Validate();drive.PushAsync(new List<TaskItem>{phone},GoogleDriveClient.TeamPrefix).GetAwaiter().GetResult();
   Check(pc.MergeTeamRecords(drive.PullAsync(GoogleDriveClient.TeamPrefix).GetAwaiter().GetResult()),"phone record reaches the computer");pc.Sync();Check(File.Exists(Path.Combine(folder,"tasks",phone.id+".json")),"phone record reaches the server folder");
   string json=pc.PhoneTeamsJson;var listed=Json.Read<PhoneTeams>(json).teams.Single();Check(listed.name=="Команда"&&listed.user=="Анна"&&listed.path.EndsWith("bridge-server"),"phone sees the project list");drive.PublishTeamsAsync(json).GetAwaiter().GetResult();drive.PublishTeamsAsync(json).GetAwaiter().GetResult();Check(transport.Files.Values.Count(f=>f.Item1==GoogleDriveClient.TeamsFile)==1&&transport.Patches==0,"unchanged project list is not rewritten");
   var fresh=new GoogleDriveClient(pc.Root,new HttpClient(transport,false),()=>Task.FromResult("mock-token"));fresh.PublishTeamsAsync(json+" ").GetAwaiter().GetResult();Check(transport.Files.Values.Count(f=>f.Item1==GoogleDriveClient.TeamsFile)==1&&transport.Patches==1,"project list is updated in place");
   drive.PublishNoticesAsync(new List<PhoneNotice>{new PhoneNotice{id="new",kind=Notifications.BuildDone.Id,title="t",text="x",createdUtc="2026-01-01T00:00:00Z"}}).GetAwaiter().GetResult();var stale=transport.Files.First(f=>f.Value.Item1==GoogleDriveClient.NoticePrefix+"new.json");transport.Created[stale.Key]=DateTime.UtcNow.AddDays(-3).ToString("o");drive.PublishNoticesAsync(new List<PhoneNotice>()).GetAwaiter().GetResult();Check(!transport.Files.ContainsKey(stale.Key),"old phone notices are removed");
   pc.DisconnectTeam("Команда");Check(!pc.MergeTeamRecords(drive.PullAsync(GoogleDriveClient.TeamPrefix).GetAwaiter().GetResult())&&!pc.Tasks.Any(),"disconnected project does not come back as personal");
  }
  static void Check(bool valid,string message){if(!valid)throw new Exception("Drive protocol test: "+message);}
 }
}
