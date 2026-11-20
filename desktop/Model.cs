using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace Ritm {
 public class Step { public string id {get;set;} public string title {get;set;} public bool done {get;set;} public object Copy(){return MemberwiseClone();} }
 // Вложение: name — как показывать, file — имя файла в папке attachments (личные — в локальном хранилище, общие — в папке проекта).
 public class Attachment { public string id {get;set;} public string name {get;set;} public string file {get;set;} public long size {get;set;} public string addedUtc {get;set;} public string addedBy {get;set;}
  public object Copy(){return MemberwiseClone();}
  static readonly string[] pictures={".jpg",".jpeg",".png",".bmp",".gif",".tif",".tiff",".ico",".webp",".heic"};
  public bool IsImage {get{return pictures.Contains(Extension);}}
  public string Extension {get{int dot=(name??"").LastIndexOf('.');return dot<0?"":name.Substring(dot).ToLowerInvariant();}}
 }
 // Событие истории задачи: kind — что изменилось (created, status, assignee, project, date, priority, title, description, steps, attachments, deleted), from/to — значения до и после.
 public class TaskEvent { public string id {get;set;} public string at {get;set;} public string by {get;set;} public string kind {get;set;} public string from {get;set;} public string to {get;set;} public object Copy(){return MemberwiseClone();} }
 public class TaskItem {
  public string id {get;set;} public string title {get;set;} public string description {get;set;}
  public bool isMeeting {get;set;} public int? meetingHue {get;set;} public int? meetingTransparency {get;set;} public string meetingColor {get;set;} public string meetingTime {get;set;} public List<int> meetingDays {get;set;} public string date {get;set;} public int priority {get;set;} public string category {get;set;}
  public bool completed {get;set;} public List<Step> subtasks {get;set;}
  public int version {get;set;} public long createdAt {get;set;} public string updatedUtc {get;set;}
  public string deviceId {get;set;} public bool deleted {get;set;}
  public string status {get;set;} public string recordType {get;set;}
  public string assignee {get;set;} public string createdBy {get;set;}
  public List<Attachment> attachments {get;set;}
  public string completedUtc {get;set;} public List<TaskEvent> history {get;set;}
  public TaskItem() { id=Guid.NewGuid().ToString();title="";description="";category="Жизнь";priority=3;subtasks=new List<Step>();attachments=new List<Attachment>();history=new List<TaskEvent>(); }
  // Когда задача выполнена (по местному времени). У старых записей без completedUtc — время последнего изменения.
  public DateTime? CompletedLocal(){if(!completed)return null;DateTimeOffset when;var stamp=completedUtc??updatedUtc;return stamp!=null&&DateTimeOffset.TryParse(stamp,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out when)?when.LocalDateTime:(DateTime?)null;}
  // Глубокая копия без JSON: store.Tasks копирует все задачи при каждой отрисовке, а сериализация с историей изменений занимала заметное время.
  public TaskItem Clone() {
   var c=(TaskItem)MemberwiseClone();
   if(meetingDays!=null)c.meetingDays=new List<int>(meetingDays);
   if(subtasks!=null)c.subtasks=subtasks.Select(s=>s==null?null:(Step)s.Copy()).ToList();
   if(attachments!=null)c.attachments=attachments.Select(a=>a==null?null:(Attachment)a.Copy()).ToList();
   if(history!=null)c.history=history.Select(e=>e==null?null:(TaskEvent)e.Copy()).ToList();
   return c;
  }
  public void Validate() {
   Guid g; DateTime d;
   if(!Guid.TryParse(id,out g)||id!=g.ToString())throw new Exception("Некорректный идентификатор задачи.");
   if(string.IsNullOrWhiteSpace(title)||title.Length>200)throw new Exception("Введите название задачи — до 200 символов.");
   if(description==null)description="";
   if(description.Length>10000)throw new Exception("Описание слишком длинное. Максимум — 10 000 символов.");
   if(string.IsNullOrWhiteSpace(category)||category.Length>50)throw new Exception("Введите категорию — до 50 символов.");
   if(date!=null&&(!DateTime.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d)||d.Year<1900))throw new Exception("Проверьте дату задачи.");
   if(isMeeting){if(meetingHue.HasValue&&(meetingHue<0||meetingHue>360)||meetingTransparency.HasValue&&(meetingTransparency<0||meetingTransparency>100))throw new Exception("Проверьте цвет и прозрачность встречи.");DateTime time;if(date==null||!DateTime.TryParseExact(meetingTime,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out time))throw new Exception("Укажите дату и время встречи (чч:мм).");if(meetingDays!=null&&(meetingDays.Count>7||meetingDays.Any(x=>x<0||x>6)||meetingDays.Distinct().Count()!=meetingDays.Count))throw new Exception("Проверьте дни встреч.");} if(priority<1||priority>4)throw new Exception("Некорректный приоритет.");
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
   if(history==null)history=new List<TaskEvent>();history.RemoveAll(e=>e==null||string.IsNullOrEmpty(e.kind)||string.IsNullOrEmpty(e.at));
   foreach(var e in history){if(string.IsNullOrEmpty(e.id))e.id=Guid.NewGuid().ToString();e.by=Clip(e.by,50);e.kind=Clip(e.kind,20);e.from=Clip(e.from,200);e.to=Clip(e.to,200);}
   if(history.Count>TaskHistory.Limit)history=history.Take(1).Concat(history.Skip(history.Count-TaskHistory.Limit+1)).ToList();
   if(!completed)completedUtc=null;
   title=title.Trim();category=category.Trim();
  }
  // Имя участника общего проекта: пробелы схлопываются, пустое значение — «не указано».
  // Имя файла вложения из записи задачи не должно выводить за пределы папки attachments.
  public static bool SafeFile(string file){return !string.IsNullOrWhiteSpace(file)&&file.Length<=120&&file.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())<0&&file.Trim('.').Length>0&&file==file.Trim();}
  static string Clip(string s,int max){return s==null||s.Length<=max?s:s.Substring(0,max);}
  public static string Person(string name,string label="Имя"){name=string.Join(" ",(name??"").Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries));if(name.Length==0)return null;if(name.Length>50)throw new Exception(label+": до 50 символов.");return name;}
 }
 public static class TaskHistory {
  public const int Limit=300;
  static readonly string[] priorities={"Срочный","Высокий","Обычный","Низкий"};
  // Дописывает в after события о том, чем он отличается от before (before == null — новая задача), и отмечает время выполнения.
  public static void Record(TaskItem before,TaskItem after,string by,string at){
   if(after.recordType!="task")return;if(after.history==null)after.history=new List<TaskEvent>();
   Action<string,string,string> add=(kind,from,to)=>after.history.Add(new TaskEvent{id=Guid.NewGuid().ToString(),at=at,by=by,kind=kind,from=from,to=to});
   if(after.status=="done"&&(before==null||before.status!="done"))after.completedUtc=at;
   else if(after.status=="done"&&after.completedUtc==null)after.completedUtc=before.completedUtc??before.updatedUtc;
   if(after.status!="done")after.completedUtc=null;
   if(before==null){add("created",null,after.status);if(after.status!="todo")add("status","todo",after.status);if(after.assignee!=null)add("assignee",null,after.assignee);return;}
   if(after.deleted&&!before.deleted){add("deleted",null,null);return;}
   if(before.status!=after.status)add("status",before.status,after.status);
   if(!Store.SamePerson(before.assignee,after.assignee)&&before.assignee!=after.assignee)add("assignee",before.assignee,after.assignee);
   if(before.category!=after.category)add("project",before.category,after.category);
   if(before.date!=after.date)add("date",before.date,after.date);
   if(before.priority!=after.priority)add("priority",Priority(before.priority),Priority(after.priority));
   if(before.title!=after.title)add("title",before.title,after.title);
   if((before.description??"")!=(after.description??""))add("description",null,null);
   string s0=Steps(before),s1=Steps(after);if(s0!=s1)add("steps",s0,s1);
   int a0=before.attachments==null?0:before.attachments.Count,a1=after.attachments==null?0:after.attachments.Count;if(a0!=a1)add("attachments",a0.ToString(),a1.ToString());
  }
  static string Priority(int p){return p>=1&&p<=4?priorities[p-1]:p.ToString();}
  static string Steps(TaskItem t){var list=t.subtasks??new List<Step>();return list.Count(s=>s.done)+"/"+list.Count;}
  public static string Status(string s){return s=="doing"?"В работе":s=="done"?"Готово":"К выполнению";}
  // История для показа: у старых задач, созданных до появления истории, событие создания восстанавливается из createdAt и createdBy.
  public static List<TaskEvent> View(TaskItem t){
   var list=(t.history??new List<TaskEvent>()).ToList();
   if(!list.Any(e=>e.kind=="created")&&t.createdAt>0)list.Insert(0,new TaskEvent{kind="created",at=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddMilliseconds(t.createdAt).ToString("o",CultureInfo.InvariantCulture),by=t.createdBy});
   return list;
  }
  public static DateTime? Local(TaskEvent e){DateTimeOffset when;return DateTimeOffset.TryParse(e.at,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out when)?when.LocalDateTime:(DateTime?)null;}
  static string Day(string date){DateTime d;return date!=null&&DateTime.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d)?d.ToString("d MMMM yyyy",new CultureInfo("ru-RU")):"без срока";}
  static string Steps(string v){var p=(v??"0/0").Split('/');return p.Length==2?p[0]+" из "+p[1]:v;}
  public static string Describe(TaskEvent e){
   switch(e.kind){
    case "created":return "Задача создана";
    case "deleted":return "Задача удалена";
    case "status":return e.to=="doing"?"Взята в работу":e.to=="done"?"Выполнена":e.from=="done"?"Снова открыта · «"+Status(e.to)+"»":"Статус: «"+Status(e.from)+"» → «"+Status(e.to)+"»";
    case "assignee":return e.to==null?"Исполнитель снят (был "+e.from+")":e.from==null?"Назначена: "+e.to:"Переназначена: "+e.from+" → "+e.to;
    case "project":return "Перенесена в проект «"+e.to+"» из «"+e.from+"»";
    case "date":return e.from==null?"Назначен срок: "+Day(e.to):e.to==null?"Срок снят (был "+Day(e.from)+")":"Срок: "+Day(e.from)+" → "+Day(e.to);
    case "priority":return "Приоритет: "+e.from+" → "+e.to;
    case "title":return "Переименована: «"+e.from+"» → «"+e.to+"»";
    case "description":return "Изменено описание";
    case "steps":return "Подзадачи: выполнено "+Steps(e.to);
    case "attachments":return "Вложения: "+e.from+" → "+e.to;
    default:return "Изменение: "+e.kind;
   }
  }
 }
 public class BuildLink {public string Key {get;set;} public string Name {get;set;}}
 public class ProjectGitSettings {public List<BuildLink> Builds {get;set;} public string Directory {get;set;} public string Branch {get;set;} public bool BuildEnabled {get;set;}}
 public class FolderWatchSettings {public string Path {get;set;} public int Minutes {get;set;} public Dictionary<string,string> Snapshot {get;set;} public List<string> Log {get;set;} }
 public class Preferences {public Dictionary<string,FolderWatchSettings> FolderWatches {get;set;}public Dictionary<string,string> MeetingNotified {get;set;} public int SidebarTextScale {get;set;} public int MainTextScale {get;set;} public Dictionary<string,bool> ProjectServerViews {get;set;} public Dictionary<string,string> ProjectAliases {get;set;} public Dictionary<string,ProjectGitSettings> ProjectGit {get;set;} public string GitDirectory {get;set;} public string SyncDirectory {get;set;} public string DeviceId {get;set;} public string Theme {get;set;} public string Palette {get;set;} public FocusState Focus {get;set;} public string FocusTaskId {get;set;} public Dictionary<string,string> TaskLayouts {get;set;} public bool ReduceMotion {get;set;} public bool ShowTaskWidget {get;set;} public double? WidgetLeft {get;set;} public double? WidgetBottom {get;set;} public List<TeamLink> Teams {get;set;} public string UserName {get;set;} public int TaskSort {get;set;} public bool NotificationsMuted {get;set;} public bool NotificationsInAppOnly {get;set;} public Dictionary<string,bool> Notifications {get;set;} public string NotifiedMain {get;set;} public Dictionary<string,string> NotifiedBranches {get;set;} }
 public class BackupDocument {public string app {get;set;} public int version {get;set;} public List<TaskItem> tasks {get;set;} public List<TaskItem> projects {get;set;} }
 public static class Json {
  // Поля пишутся в алфавитном порядке. Порядок, который даёт JavaScriptSerializer, зависит от кэша рефлексии и меняется между запусками,
  // а по тексту JSON считаются хэши файлов истории — без стабильного порядка одна и та же версия задачи сохранялась бы повторно.
  public static string Write(object value){var s=new JavaScriptSerializer {MaxJsonLength=10000000};return s.Serialize(Canonical(s.DeserializeObject(s.Serialize(value))));}
  static object Canonical(object value){
   var map=value as IDictionary<string,object>;if(map!=null){var sorted=new Dictionary<string,object>();foreach(var key in map.Keys.OrderBy(k=>k,StringComparer.Ordinal))sorted[key]=Canonical(map[key]);return sorted;}
   var list=value as object[];if(list!=null)return list.Select(Canonical).ToArray();
   return value;
  }
  public static T Read<T>(string value){return new JavaScriptSerializer {MaxJsonLength=10000000}.Deserialize<T>(value);}
 }
}
