using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Globalization;

namespace Ritm {
 public static class Program {
  [STAThread] public static int Main(string[] args){
   if(args.Length==2&&args[0]=="--self-test")return SelfTest.Run(args[1]);
   if(args.Length==2&&args[0].StartsWith("--render-")){try{
    Fx.Suppressed=true;var previewApp=new Application();UI.InstallStyles();
    var temp=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])),"preview-data-"+Guid.NewGuid().ToString("N"));
    var data=new Store(temp);data.SetTheme(args[0]=="--render-light"||args[0].EndsWith("-light")?"light":"dark");if(args[0].StartsWith("--render-palette-")){string name=args[0].Substring("--render-palette-".Length);if(name.EndsWith("-light"))name=name.Substring(0,name.Length-6);data.SetPalette(name);}data.CreateProject("Ритм");data.CreateProject("Личное");
    data.Save(new TaskItem{title="Продумать планы на неделю",description="Оставить время для важных дел и отдыха",category="Ритм",status="doing",date=UI.Date(DateTime.Today),priority=2},0);
    data.Save(new TaskItem{title="Записаться на тренировку",category="Личное",status="todo",priority=3},0);
    data.Save(new TaskItem{title="Собрать идеи для проекта",category="Ритм",status="todo",priority=2,subtasks={new Step{id="a",title="Определить цель",done=true},new Step{id="b",title="Записать мысли"}}},0);
    data.Save(new TaskItem{title="Подвести итоги месяца",category="Личное",status="done",completed=true,date=UI.Date(DateTime.Today),priority=4},0);
    if(args[0].StartsWith("--render-team")){var server=Path.Combine(temp+"-server","Отдел продаж");Directory.CreateDirectory(server);data.ConnectTeam(server,"Отдел продаж","Анна");
     data.Save(new TaskItem{title="Подготовить КП для клиента",category="Отдел продаж",status="doing",priority=1,date=UI.Date(DateTime.Today),assignee="Анна"},0);data.Save(new TaskItem{title="Обзвонить новых лидов",category="Отдел продаж",status="todo",priority=2,assignee="Иван Петров"},0);
     data.Save(new TaskItem{title="Обновить прайс-лист",category="Отдел продаж",status="todo",priority=3},0);data.Save(new TaskItem{title="Отчёт за октябрь",category="Отдел продаж",status="done",completed=true,assignee="Мария"},0);}
    var preview=new MainWindow(data);Window target=preview;if(args[0].StartsWith("--render-team"))preview.PreviewTaskLayout("board","Отдел продаж");if(args[0].StartsWith("--render-team-editor"))target=new TaskEditor(null,data.Tasks.Find(t=>t.category=="Отдел продаж"),data.ProjectNames,t=>true,t=>{},n=>data.IsTeamName(n)?data.Members(n):null);
    if(args[0]=="--render-calendar")preview.PreviewView("calendar");if(args[0]=="--render-day")preview.PreviewView("today");
    if(args[0]=="--render-project-list"){preview.PreviewView("calendar");preview.PreviewTaskLayout("list","Ритм");}if(args[0]=="--render-day-board"){preview.PreviewView("today");preview.PreviewTaskLayout("board");}if(args[0]=="--render-settings")target=new SettingsWindow(null,data,new GoogleDriveClient(temp),()=>{},()=>{});
    if(args[0]=="--render-editor")target=new TaskEditor(null,data.Tasks[0],data.ProjectNames,t=>true,t=>{});
    if(args[0]=="--render-attachments"||args[0]=="--render-photo"){var t=data.Tasks.Find(x=>x.subtasks.Count>0);var pic=Path.Combine(temp,"Макет экрана.png");SamplePicture(pic);data.Attach(t,pic);data.Attach(t,"Смета на ноябрь.pdf",new byte[]{1});t=data.Save(t,t.version);
     target=args[0]=="--render-photo"?(Window)new PhotoViewer(null,t.attachments.FindAll(a=>a.IsImage),0,a=>data.AttachmentPath(t,a)):new TaskEditor(null,t,data.ProjectNames,x=>true,x=>{},null,data);
     // Миниатюры грузятся в фоне — даём им прийти до снимка.
     Thread.Sleep(700);System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{}));}
    var visual=(FrameworkElement)target.Content;int width=target==preview||target is PhotoViewer?1280:620,height=target==preview||target is PhotoViewer?840:args[0]=="--render-attachments"?1080:800;
    visual.Measure(new Size(width,height));visual.Arrange(new Rect(0,0,width,height));visual.UpdateLayout();if(args[0]=="--render-toggle"){UI.SetTheme("light");visual.UpdateLayout();}
    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(visual);
    var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var file=File.Create(args[1]))png.Save(file);
    preview.Close();return 0;}catch(Exception e){File.WriteAllText(args[1]+".error.txt",e.ToString());return 1;}
   }
   bool created;using(var mutex=new Mutex(true,"Local\\RitmPlannerDesktop-v1",out created)){
    if(!created){MessageBox.Show("Ритм уже запущен. Откройте его окно на панели задач.","Ритм");return 0;}
    try{
     Thread.CurrentThread.CurrentCulture=new CultureInfo("ru-RU");Thread.CurrentThread.CurrentUICulture=new CultureInfo("ru-RU");
     FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("ru-RU")));
     var app=new Application();UI.InstallStyles();app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show("Действие не завершено.\n"+e.Exception.Message,"Ритм",MessageBoxButton.OK,MessageBoxImage.Information);e.Handled=true;};
     string root=args.Length==2&&args[0]=="--data"?Path.GetFullPath(args[1]):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RitmPlanner","Data");var store=new Store(root);app.Run(new MainWindow(store));return 0;
    }catch(Exception e){MessageBox.Show("Не удалось открыть Ритм.\n"+e.Message+"\nЛокальные данные не удалены.","Ритм",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
   }
  }
  // Пример фото для превью вложений.
  static void SamplePicture(string path){
   var v=new System.Windows.Media.DrawingVisual();using(var dc=v.RenderOpen()){dc.DrawRectangle(new System.Windows.Media.LinearGradientBrush(System.Windows.Media.Color.FromRgb(0x56,0x52,0x84),System.Windows.Media.Color.FromRgb(0xE9,0xA8,0x7C),35),null,new Rect(0,0,1600,1000));dc.DrawEllipse(System.Windows.Media.Brushes.White,null,new Point(1150,330),150,150);}
   var bmp=new System.Windows.Media.Imaging.RenderTargetBitmap(1600,1000,96,96,System.Windows.Media.PixelFormats.Pbgra32);bmp.Render(v);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));using(var file=File.Create(path))png.Save(file);
  }
 }
}





