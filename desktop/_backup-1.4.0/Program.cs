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
    var previewApp=new Application();UI.InstallStyles();
    var temp=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])),"preview-data-"+Guid.NewGuid().ToString("N"));
    var data=new Store(temp);data.SetTheme(args[0]=="--render-light"||args[0].EndsWith("-light")?"light":"dark");if(args[0].StartsWith("--render-palette-")){string name=args[0].Substring("--render-palette-".Length);if(name.EndsWith("-light"))name=name.Substring(0,name.Length-6);data.SetPalette(name);}data.CreateProject("Ритм");data.CreateProject("Личное");
    data.Save(new TaskItem{title="Продумать планы на неделю",description="Оставить время для важных дел и отдыха",category="Ритм",status="doing",date=UI.Date(DateTime.Today),priority=2},0);
    data.Save(new TaskItem{title="Записаться на тренировку",category="Личное",status="todo",priority=3},0);
    data.Save(new TaskItem{title="Собрать идеи для проекта",category="Ритм",status="todo",priority=2,subtasks={new Step{id="a",title="Определить цель",done=true},new Step{id="b",title="Записать мысли"}}},0);
    data.Save(new TaskItem{title="Подвести итоги месяца",category="Личное",status="done",completed=true,date=UI.Date(DateTime.Today),priority=4},0);
    var preview=new MainWindow(data);Window target=preview;
    if(args[0]=="--render-calendar")preview.PreviewView("calendar");if(args[0]=="--render-day")preview.PreviewView("today");
    if(args[0]=="--render-project-list"){preview.PreviewView("calendar");preview.PreviewTaskLayout("list","Ритм");}if(args[0]=="--render-day-board"){preview.PreviewView("today");preview.PreviewTaskLayout("board");}if(args[0]=="--render-settings")target=new SettingsWindow(null,data,new GoogleDriveClient(temp),()=>{},()=>{});
    if(args[0]=="--render-editor")target=new TaskEditor(null,data.Tasks[0],data.ProjectNames,t=>true,t=>{});
    var visual=(FrameworkElement)target.Content;int width=target==preview?1280:620,height=target==preview?840:800;
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
     string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RitmPlanner","Data");var store=new Store(root);app.Run(new MainWindow(store));return 0;
    }catch(Exception e){MessageBox.Show("Не удалось открыть Ритм.\n"+e.Message+"\nЛокальные данные не удалены.","Ритм",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
   }
  }
 }
}





