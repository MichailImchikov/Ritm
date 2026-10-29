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
  public readonly Dictionary<string,Tuple<string,string>> Files=new Dictionary<string,Tuple<string,string>>();
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){
   if(request.Headers.Authorization==null||request.Headers.Authorization.Scheme!="Bearer")throw new Exception("Missing authorization header");
   if(request.Method==HttpMethod.Get&&request.RequestUri.AbsolutePath=="/drive/v3/files")return Reply(Json.Write(new DriveListing{files=Files.Select(p=>new DriveFile{id=p.Key,name=p.Value.Item1}).ToList()}));
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
  }
  static void Check(bool valid,string message){if(!valid)throw new Exception("Drive protocol test: "+message);}
 }
}
