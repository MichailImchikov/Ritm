using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace Ritm {
 public class Step { public string id {get;set;} public string title {get;set;} public bool done {get;set;} }
 // Вложение: name — как показывать, file — имя файла в папке attachments (личные — в локальном хранилище, общие — в папке проекта).
 public class Attachment { public string id {get;set;} public string name {get;set;} public string file {get;set;} public long size {get;set;} public string addedUtc {get;set;} public string addedBy {get;set;}
  static readonly string[] pictures={".jpg",".jpeg",".png",".bmp",".gif",".tif",".tiff",".ico",".webp",".heic"};
  public bool IsImage {get{return pictures.Contains(Extension);}}
  public string Extension {get{int dot=(name??"").LastIndexOf('.');return dot<0?"":name.Substring(dot).ToLowerInvariant();}}
 }
 public class TaskItem {
  public string id {get;set;} public string title {get;set;} public string description {get;set;}
  public string date {get;set;} public int priority {get;set;} public string category {get;set;}
  public bool completed {get;set;} public List<Step> subtasks {get;set;}
  public int version {get;set;} public long createdAt {get;set;} public string updatedUtc {get;set;}
  public string deviceId {get;set;} public bool deleted {get;set;}
  public string status {get;set;} public string recordType {get;set;}
  public string assignee {get;set;} public string createdBy {get;set;}
  public List<Attachment> attachments {get;set;}
  public TaskItem() { id=Guid.NewGuid().ToString();title="";description="";category="Жизнь";priority=3;subtasks=new List<Step>();attachments=new List<Attachment>(); }
  public TaskItem Clone() {return Json.Read<TaskItem>(Json.Write(this));}
  public void Validate() {
   Guid g; DateTime d;
   if(!Guid.TryParse(id,out g)||id!=g.ToString())throw new Exception("Некорректный идентификатор задачи.");
   if(string.IsNullOrWhiteSpace(title)||title.Length>200)throw new Exception("Введите название задачи — до 200 символов.");
   if(description==null)description="";
   if(description.Length>10000)throw new Exception("Описание слишком длинное. Максимум — 10 000 символов.");
   if(string.IsNullOrWhiteSpace(category)||category.Length>50)throw new Exception("Введите категорию — до 50 символов.");
   if(date!=null&&(!DateTime.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d)||d.Year<1900))throw new Exception("Проверьте дату задачи.");
   if(priority<1||priority>4)throw new Exception("Некорректный приоритет.");
   if(string.IsNullOrEmpty(recordType))recordType="task";
   if(recordType!="task"&&recordType!="project")throw new Exception("Некорректный тип записи.");
   if(completed)status="done";else if(string.IsNullOrEmpty(status)||status=="done")status="todo";
   if(status!="todo"&&status!="doing"&&status!="done")throw new Exception("Некорректный статус.");
   if(subtasks==null)subtasks=new List<Step>();
   if(subtasks.Count>50||subtasks.Any(s=>s==null||string.IsNullOrWhiteSpace(s.title)||s.title.Length>200))throw new Exception("В задаче может быть до 50 подзадач с названиями до 200 символов.");
   foreach(var s in subtasks)if(string.IsNullOrEmpty(s.id))s.id=Guid.NewGuid().ToString();
   if(attachments==null)attachments=new List<Attachment>();
   if(attachments.Count>50)throw new Exception("К задаче можно прикрепить до 50 файлов.");
   foreach(var a in attachments)if(a==null||!Guid.TryParse(a.id,out g)||string.IsNullOrWhiteSpace(a.name)||a.name.Length>200||!SafeFile(a.file))throw new Exception("Повреждён список вложений задачи.");
   assignee=Person(assignee,"Имя исполнителя");createdBy=Person(createdBy,"Имя автора");
   title=title.Trim();category=category.Trim();
  }
  // Имя участника общего проекта: пробелы схлопываются, пустое значение — «не указано».
  // Имя файла вложения из записи задачи не должно выводить за пределы папки attachments.
  public static bool SafeFile(string file){return !string.IsNullOrWhiteSpace(file)&&file.Length<=120&&file.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())<0&&file.Trim('.').Length>0&&file==file.Trim();}
  public static string Person(string name,string label="Имя"){name=string.Join(" ",(name??"").Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries));if(name.Length==0)return null;if(name.Length>50)throw new Exception(label+": до 50 символов.");return name;}
 }
 public class BuildLink {public string Key {get;set;} public string Name {get;set;}}
 public class ProjectGitSettings {public List<BuildLink> Builds {get;set;} public string Directory {get;set;} public string Branch {get;set;} public bool BuildEnabled {get;set;}}
 public class Preferences {public int SidebarTextScale {get;set;} public int MainTextScale {get;set;} public Dictionary<string,bool> ProjectServerViews {get;set;} public Dictionary<string,string> ProjectAliases {get;set;} public Dictionary<string,ProjectGitSettings> ProjectGit {get;set;} public string GitDirectory {get;set;} public string SyncDirectory {get;set;} public string DeviceId {get;set;} public string Theme {get;set;} public string Palette {get;set;} public FocusState Focus {get;set;} public string FocusTaskId {get;set;} public Dictionary<string,string> TaskLayouts {get;set;} public bool ReduceMotion {get;set;} public List<TeamLink> Teams {get;set;} public string UserName {get;set;} public int TaskSort {get;set;} public bool NotificationsMuted {get;set;} public bool NotificationsInAppOnly {get;set;} public Dictionary<string,bool> Notifications {get;set;} public string NotifiedMain {get;set;} public Dictionary<string,string> NotifiedBranches {get;set;} }
 public class BackupDocument {public string app {get;set;} public int version {get;set;} public List<TaskItem> tasks {get;set;} public List<TaskItem> projects {get;set;} }
 public static class Json {
  public static string Write(object value){return new JavaScriptSerializer {MaxJsonLength=10000000}.Serialize(value);}
  public static T Read<T>(string value){return new JavaScriptSerializer {MaxJsonLength=10000000}.Deserialize<T>(value);}
 }
}
