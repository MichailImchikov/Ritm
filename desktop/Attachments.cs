using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ritm {
 // Вложения задач. Личные — в %LOCALAPPDATA%\RitmPlanner\Data\attachments, общих проектов — в папке attachments рядом с tasks\ на сервере.
 public sealed partial class Store {
  public const long MaxAttachmentBytes=50L*1024*1024;
  const string FilesFolder="attachments";
  string LocalFiles {get{return Path.Combine(Root,FilesFolder);}}
  public string FilesFolderOf(string category){var team=Team(category);return Path.Combine(team!=null?team.Path:Root,FilesFolder);}
  // Новый файл сначала копируется в локальное хранилище — так прикрепление работает и без сети. В папку общего проекта он уходит при сохранении задачи или при следующей синхронизации.
  public Attachment Attach(TaskItem task,string source){
   var info=new FileInfo(source);if(!info.Exists)throw new Exception("Файл не найден: "+source);
   if(info.Length>MaxAttachmentBytes)throw new Exception("«"+info.Name+"» больше 50 МБ. Такой файл лучше положить в общую папку и дать путь в описании.");
   return Attach(task,info.Name,info.Length,target=>File.Copy(source,target));
  }
  public Attachment Attach(TaskItem task,string name,byte[] data){
   if(data.LongLength>MaxAttachmentBytes)throw new Exception("Файл больше 50 МБ.");
   return Attach(task,name,data.LongLength,target=>File.WriteAllBytes(target,data));
  }
  Attachment Attach(TaskItem task,string name,long size,Action<string> write){
   if(task.attachments==null)task.attachments=new List<Attachment>();
   if(task.attachments.Count>=50)throw new Exception("К задаче можно прикрепить до 50 файлов.");
   name=Path.GetFileName(name).Trim();if(name.Length==0)name="файл";if(name.Length>200)name=name.Substring(name.Length-200);
   var a=new Attachment{id=Guid.NewGuid().ToString(),name=name,size=size,addedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture),addedBy=Me(task.category)};a.file=StoredName(a);
   Directory.CreateDirectory(LocalFiles);var target=Path.Combine(LocalFiles,a.file);var temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{write(temp);File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}
   task.attachments.Add(a);return a;
  }
  // В папке файл узнаваем по исходному имени, а хвост из идентификатора не даёт одинаковым именам столкнуться.
  static string StoredName(Attachment a){
   var invalid=Path.GetInvalidFileNameChars();string ext=new string(a.Extension.Where(c=>c=='.'||char.IsLetterOrDigit(c)).Take(10).ToArray());
   string stem=new string(Path.GetFileNameWithoutExtension(a.name).Select(c=>invalid.Contains(c)?'_':c).Take(60).ToArray()).Trim().Trim('.');
   return (stem.Length==0?"файл":stem)+"_"+a.id.Substring(0,8)+(ext.Length>1?ext:"");
  }
  // Где лежит файл: сначала локальная копия (личная или ещё не отправленная), затем папка общего проекта.
  public string AttachmentPath(TaskItem task,Attachment a){
   if(a==null||!TaskItem.SafeFile(a.file))return null;
   var local=Path.Combine(LocalFiles,a.file);if(File.Exists(local))return local;
   var team=Team(task.category);if(team==null)return null;
   try{var remote=Path.Combine(team.Path,FilesFolder,a.file);return File.Exists(remote)?remote:null;}catch{return null;}
  }
  // Файл, прикреплённый в форме, которую закрыли без сохранения, не нужен ни одной задаче.
  public void Discard(Attachment a){try{if(a!=null&&TaskItem.SafeFile(a.file))File.Delete(Path.Combine(LocalFiles,a.file));}catch{}}
  // Отправляет локальные копии вложений задачи общего проекта на сервер. Если папка недоступна, файл остаётся локально до следующей синхронизации.
  void Place(TaskItem t){
   var team=Team(t.category);if(team==null||t.deleted||t.attachments==null)return;
   foreach(var a in t.attachments){
    if(!TaskItem.SafeFile(a.file))continue;var local=Path.Combine(LocalFiles,a.file);if(!File.Exists(local))continue;
    try{
     var dir=Path.Combine(team.Path,FilesFolder);Directory.CreateDirectory(dir);var remote=Path.Combine(dir,a.file);
     if(!File.Exists(remote)){var temp=remote+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.Copy(local,temp);File.Move(temp,remote);}finally{if(File.Exists(temp))File.Delete(temp);}}
     File.Delete(local);
    }catch{}
   }
  }
 }
 public static class Photo {
  // Загружает фото без блокировки файла и с поворотом по EXIF (снимки с телефона). maxWidth уменьшает только большие изображения.
  public static BitmapSource Load(string path,int maxWidth=0){
   using(var stream=new MemoryStream(File.ReadAllBytes(path))){
    var frame=BitmapFrame.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);int angle=0;
    try{var meta=frame.Metadata as BitmapMetadata;if(meta!=null&&meta.ContainsQuery("System.Photo.Orientation")){var o=Convert.ToInt32(meta.GetQuery("System.Photo.Orientation"));angle=o==6?90:o==3?180:o==8?270:0;}}catch{}
    int width=frame.PixelWidth;stream.Position=0;
    var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.StreamSource=stream;if(maxWidth>0&&width>maxWidth)bmp.DecodePixelWidth=maxWidth;bmp.EndInit();bmp.Freeze();
    if(angle==0)return bmp;var turned=new TransformedBitmap(bmp,new RotateTransform(angle));turned.Freeze();return turned;
   }
  }
  public static void LoadAsync(string path,int maxWidth,Action<BitmapSource> done){
   var ui=System.Windows.Threading.Dispatcher.CurrentDispatcher;
   Task.Run(()=>{BitmapSource result=null;try{if(path!=null)result=Load(path,maxWidth);}catch{}ui.BeginInvoke(new Action(()=>done(result)));});
  }
  public static string Size(long bytes){return bytes<1024?bytes+" Б":bytes<1024*1024?(bytes/1024.0).ToString("0",CultureInfo.CurrentCulture)+" КБ":(bytes/1048576.0).ToString("0.#",CultureInfo.CurrentCulture)+" МБ";}
  public static void Open(Window owner,string path){try{if(path==null)throw new Exception("Файл недоступен: нет связи с папкой проекта или файл удалён.");Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception e){MessageBox.Show(owner,e.Message,"Ритм",MessageBoxButton.OK,MessageBoxImage.Information);}}
  public static void Reveal(Window owner,string path){try{if(path==null)throw new Exception("Файл недоступен: нет связи с папкой проекта или файл удалён.");Process.Start("explorer.exe","/select,\""+path+"\"");}catch(Exception e){MessageBox.Show(owner,e.Message,"Ритм",MessageBoxButton.OK,MessageBoxImage.Information);}}
  public static void SaveCopy(Window owner,Attachment a,string path){
   try{if(path==null)throw new Exception("Файл недоступен: нет связи с папкой проекта или файл удалён.");var dialog=new Microsoft.Win32.SaveFileDialog{FileName=a.name,Title="Сохранить вложение"};if(dialog.ShowDialog(owner)==true)File.Copy(path,dialog.FileName,true);}
   catch(Exception e){MessageBox.Show(owner,e.Message,"Не удалось сохранить",MessageBoxButton.OK,MessageBoxImage.Information);}
  }
 }
 // Просмотр фото задачи внутри приложения: ← → листают, колесо и двойной щелчок — масштаб, перетаскивание — сдвиг увеличенного снимка.
 public sealed class PhotoViewer:Window {
  readonly List<Attachment> photos;readonly Func<Attachment,string> locate;int index;
  readonly Image image=new Image{Stretch=Stretch.Uniform,SnapsToDevicePixels=true};readonly ScaleTransform scale=new ScaleTransform();readonly TranslateTransform shift=new TranslateTransform();
  readonly Grid stage=new Grid{ClipToBounds=true,Background=Brushes.Transparent};readonly TextBlock caption,counter,notice;readonly Button prev,next;
  Point grab;bool panning;
  public PhotoViewer(Window owner,List<Attachment> list,int start,Func<Attachment,string> where){
   photos=list;locate=where;index=start;if(owner!=null){Owner=owner;Icon=owner.Icon;}
   Width=1100;Height=780;MinWidth=520;MinHeight=380;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Fx.Solid("#141416");
   var root=new DockPanel{Background=Fx.Solid("#141416")};Content=root;
   var bar=new DockPanel{Background=Fx.Solid("#1D1D21"),Margin=new Thickness(0)};var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,10,10)};
   actions.Children.Add(Tool("","Уменьшить (−)",()=>Zoom(1/1.25,null)));actions.Children.Add(Tool("","По размеру окна (0)",Fit));actions.Children.Add(Tool("","Увеличить (+)",()=>Zoom(1.25,null)));
   actions.Children.Add(Tool("","Открыть в программе Windows",()=>Photo.Open(this,Current)));actions.Children.Add(Tool("","Показать в папке",()=>Photo.Reveal(this,Current)));actions.Children.Add(Tool("","Сохранить как…",()=>Photo.SaveCopy(this,photos[index],Current)));
   DockPanel.SetDock(actions,Dock.Right);bar.Children.Add(actions);
   var info=new StackPanel{Margin=new Thickness(18,10,10,10),VerticalAlignment=VerticalAlignment.Center};caption=UI.Text("",13,Fx.Solid("#F2F2F4"),true);caption.TextWrapping=TextWrapping.NoWrap;caption.TextTrimming=TextTrimming.CharacterEllipsis;info.Children.Add(caption);counter=UI.Text("",10,Fx.Solid("#9A9AA3"));counter.Margin=new Thickness(0,3,0,0);info.Children.Add(counter);bar.Children.Add(info);
   DockPanel.SetDock(bar,Dock.Bottom);root.Children.Add(bar);
   // Сдвиг и масштаб применяются к рамке во весь размер области, а не к самому снимку: так координаты курсора и рамки совпадают.
   var frame=new Grid();frame.Children.Add(image);var group=new TransformGroup();group.Children.Add(scale);group.Children.Add(shift);frame.RenderTransform=group;stage.Children.Add(frame);
   notice=UI.Text("",13,Fx.Solid("#9A9AA3"));notice.HorizontalAlignment=HorizontalAlignment.Center;notice.TextAlignment=TextAlignment.Center;stage.Children.Add(notice);
   prev=Arrow("",HorizontalAlignment.Left,()=>Go(-1));next=Arrow("",HorizontalAlignment.Right,()=>Go(1));stage.Children.Add(prev);stage.Children.Add(next);root.Children.Add(stage);
   stage.MouseWheel+=(s,e)=>{Zoom(e.Delta>0?1.2:1/1.2,e.GetPosition(stage));e.Handled=true;};
   stage.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2){if(scale.ScaleX>1.01)Fit();else Zoom(2.5,e.GetPosition(stage));e.Handled=true;return;}if(scale.ScaleX<=1.01||e.OriginalSource is Button)return;grab=e.GetPosition(stage);panning=stage.CaptureMouse();stage.Cursor=Cursors.SizeAll;};
   stage.MouseMove+=(s,e)=>{if(!panning)return;var p=e.GetPosition(stage);shift.X+=p.X-grab.X;shift.Y+=p.Y-grab.Y;grab=p;Clamp();};
   stage.MouseLeftButtonUp+=delegate{if(!panning)return;panning=false;stage.ReleaseMouseCapture();stage.Cursor=null;};
   stage.SizeChanged+=delegate{Clamp();};
   PreviewKeyDown+=(s,e)=>{switch(e.Key){case Key.Escape:Close();break;case Key.Left:Go(-1);break;case Key.Right:Go(1);break;case Key.Add:case Key.OemPlus:Zoom(1.25,null);break;case Key.Subtract:case Key.OemMinus:Zoom(1/1.25,null);break;case Key.D0:case Key.NumPad0:Fit();break;default:return;}e.Handled=true;};
   Show(index);
  }
  string Current {get{return locate(photos[index]);}}
  Button Tool(string glyph,string tip,Action act){var b=UI.Button("",act);b.Content=UI.Icon(glyph,14,Fx.Solid("#E6E6EA"));b.ToolTip=tip;b.Tag="flat";b.Background=Brushes.Transparent;b.BorderThickness=new Thickness(0);b.Padding=new Thickness(11,9,11,9);b.Margin=new Thickness(2,0,0,0);System.Windows.Automation.AutomationProperties.SetName(b,tip);return b;}
  Button Arrow(string glyph,HorizontalAlignment side,Action act){var b=Tool(glyph,side==HorizontalAlignment.Left?"Предыдущее фото (←)":"Следующее фото (→)",act);((TextBlock)b.Content).FontSize=22;b.HorizontalAlignment=side;b.VerticalAlignment=VerticalAlignment.Center;b.Margin=new Thickness(12);b.Background=Fx.Tint("#000000",0x66);b.Padding=new Thickness(14,18,14,18);return b;}
  void Go(int step){if(photos.Count<2)return;Show((index+step+photos.Count)%photos.Count);}
  void Show(int i){
   index=i;var a=photos[i];Title="Ритм — "+a.name;caption.Text=a.name;counter.Text=(photos.Count>1?(i+1)+" из "+photos.Count+"  ·  ":"")+Photo.Size(a.size)+(a.addedBy!=null?"  ·  добавил(а) "+a.addedBy:"");
   prev.Visibility=next.Visibility=photos.Count>1?Visibility.Visible:Visibility.Collapsed;
   image.Source=null;Fit();notice.Text="Загрузка…";notice.Visibility=Visibility.Visible;string path=Current;
   Photo.LoadAsync(path,4096,bmp=>{if(index!=i)return;image.Source=bmp;notice.Visibility=bmp==null?Visibility.Visible:Visibility.Collapsed;if(bmp==null)notice.Text=path==null?"Файл недоступен: нет связи с папкой проекта или файл удалён.":"Не удалось показать это изображение.\nОткройте его в программе Windows — кнопка внизу справа.";});
  }
  void Fit(){scale.ScaleX=scale.ScaleY=1;shift.X=shift.Y=0;}
  // Масштаб вокруг точки под курсором (или центра): точка снимка под ней остаётся на месте.
  void Zoom(double factor,Point? at){
   double was=scale.ScaleX,now=Math.Max(1,Math.Min(10,was*factor));if(Math.Abs(now-was)<.0001)return;
   var c=at??new Point(stage.ActualWidth/2,stage.ActualHeight/2);double lx=(c.X-shift.X)/was,ly=(c.Y-shift.Y)/was;
   scale.ScaleX=scale.ScaleY=now;shift.X=c.X-lx*now;shift.Y=c.Y-ly*now;Clamp();
  }
  void Clamp(){double s=scale.ScaleX;if(s<=1.0001){Fit();return;}double w=stage.ActualWidth,h=stage.ActualHeight;shift.X=Math.Min(0,Math.Max(w-w*s,shift.X));shift.Y=Math.Min(0,Math.Max(h-h*s,shift.Y));}
 }
}
