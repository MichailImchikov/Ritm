using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Ritm {
 // Вид уведомления. Конструктор сам вносит вид в реестр, а «Настройки → Уведомления» строят переключатели по реестру,
 // поэтому уведомления без переключателя не бывает. Показать уведомление можно только по виду — см. Notifier.Show.
 public sealed class NotificationKind {
  public readonly string Id,Title,Description;public readonly bool DefaultOn;
  public NotificationKind(string id,string title,string description,bool defaultOn=true){
   if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(title)||string.IsNullOrWhiteSpace(description))throw new ArgumentException("У вида уведомления должны быть код, название и описание для настроек.");
   if(Notifications.registry.Any(k=>k.Id==id))throw new ArgumentException("Вид уведомления «"+id+"» уже есть.");
   Id=id;Title=title;Description=description;DefaultOn=defaultOn;Notifications.registry.Add(this);
  }
 }
 public static class Notifications {
  internal static readonly List<NotificationKind> registry=new List<NotificationKind>();
  // Новые виды объявляйте здесь: код не меняйте после выпуска — по нему хранится выбор пользователя.
  public static readonly NotificationKind Meeting=new NotificationKind("meetings.reminder","Уведомления о встречах","За 10 минут до начала встречи и в момент её начала.");
  public static readonly NotificationKind NewTask=new NotificationKind("tasks.new","Новые задачи в общих проектах","Другой участник добавил задачу в общий проект.");
  public static readonly NotificationKind Assigned=new NotificationKind("tasks.assigned","Задача назначена на вас","Вас сделали исполнителем задачи в общем проекте.");
  public static readonly NotificationKind MainUpdated=new NotificationKind("git.main","Обновление main","На сервере origin появился новый коммит main, которого нет в локальной main.");
  public static readonly NotificationKind FolderChanged=new NotificationKind("folder.changed","Изменения в папке","Новые и изменённые файлы в отслеживаемой папке.");
  public static readonly NotificationKind BuildDone=new NotificationKind("build.done","Сборка Unity завершена","Сборка Unity на стенде закончилась.");
  public static ReadOnlyCollection<NotificationKind> All {get{return registry.AsReadOnly();}}
  public static bool Enabled(Preferences p){return !p.NotificationsMuted;}
  // Выбор хранится только для видов, которые пользователь переключал; для остальных действует значение по умолчанию.
  public static bool IsOn(Preferences p,NotificationKind kind){bool on;return Enabled(p)&&(p.Notifications!=null&&p.Notifications.TryGetValue(kind.Id,out on)?on:kind.DefaultOn);}
  public static bool Choice(Preferences p,NotificationKind kind){bool on;return p.Notifications!=null&&p.Notifications.TryGetValue(kind.Id,out on)?on:kind.DefaultOn;}
  public static void Set(Preferences p,NotificationKind kind,bool on){if(p.Notifications==null)p.Notifications=new Dictionary<string,bool>();p.Notifications[kind.Id]=on;}
 }
 // Доставка: в открытом окне — подсказка внизу справа, иначе — уведомление Windows из значка в области уведомлений.
 public sealed class Notifier:IDisposable {
  readonly Window window;readonly Preferences settings;readonly Action<string,Action> inApp;System.Windows.Forms.NotifyIcon tray;Action pending;
  public Notifier(Window owner,Preferences prefs,Action<string,Action> showInApp){window=owner;settings=prefs;inApp=showInApp;}
  public bool Show(NotificationKind kind,string title,string text,Action open=null){
   if(kind==null||!Notifications.IsOn(settings,kind))return false;
   if(settings.NotificationsInAppOnly||window.IsActive&&window.WindowState!=WindowState.Minimized){inApp(title+" · "+text,open);return true;}
   try{var icon=Tray();pending=open;icon.ShowBalloonTip(8000,title,text,System.Windows.Forms.ToolTipIcon.None);}catch{inApp(title+" · "+text,open);}
   return true;
  }
  System.Windows.Forms.NotifyIcon Tray(){
   if(tray!=null)return tray;
   tray=new System.Windows.Forms.NotifyIcon{Text="Ритм",Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Visible=true};
   tray.BalloonTipClicked+=delegate{Bring();var open=pending;pending=null;if(open!=null)open();};tray.BalloonTipClosed+=delegate{pending=null;};tray.MouseClick+=(s,e)=>{if(e.Button==System.Windows.Forms.MouseButtons.Left)Bring();};
   return tray;
  }
  void Bring(){if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;window.Show();window.Activate();}
  public void Dispose(){if(tray==null)return;tray.Visible=false;tray.Dispose();tray=null;}
 }
}
