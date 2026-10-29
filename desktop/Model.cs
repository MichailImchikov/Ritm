using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace Ritm {
 public class Step { public string id {get;set;} public string title {get;set;} public bool done {get;set;} }
 public class TaskItem {
  public string id {get;set;} public string title {get;set;} public string description {get;set;}
  public string date {get;set;} public int priority {get;set;} public string category {get;set;}
  public bool completed {get;set;} public List<Step> subtasks {get;set;}
  public int version {get;set;} public long createdAt {get;set;} public string updatedUtc {get;set;}
  public string deviceId {get;set;} public bool deleted {get;set;}
  public string status {get;set;} public string recordType {get;set;}
  public TaskItem() { id=Guid.NewGuid().ToString();title="";description="";category="Жизнь";priority=3;subtasks=new List<Step>(); }
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
   title=title.Trim();category=category.Trim();
  }
 }
 public class Preferences {public string SyncDirectory {get;set;} public string DeviceId {get;set;} public string Theme {get;set;} public string Palette {get;set;} public FocusState Focus {get;set;} public string FocusTaskId {get;set;} public Dictionary<string,string> TaskLayouts {get;set;} public bool ReduceMotion {get;set;} }
 public class BackupDocument {public string app {get;set;} public int version {get;set;} public List<TaskItem> tasks {get;set;} public List<TaskItem> projects {get;set;} }
 public static class Json {
  public static string Write(object value){return new JavaScriptSerializer {MaxJsonLength=10000000}.Serialize(value);}
  public static T Read<T>(string value){return new JavaScriptSerializer {MaxJsonLength=10000000}.Deserialize<T>(value);}
 }
}
