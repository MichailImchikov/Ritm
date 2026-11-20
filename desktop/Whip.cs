using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Shapes=System.Windows.Shapes;
using System.Windows.Threading;

namespace Ritm {
 // «Хлыст»: участник общего проекта поторапливает другого. Удар — файл в папке pings\ общего проекта;
 // получатель видит его при очередной синхронизации (раз в 5 секунд) и получает уведомление со звуком.
 public class WhipPing {public string id {get;set;} public string project {get;set;} public string to {get;set;} public string from {get;set;} public string fromDevice {get;set;} public string createdUtc {get;set;} }
 public sealed partial class Store {
  const string PingsFolder="pings";
  // Уже учтённые файлы ударов. Удары, лежавшие в папке до первого прохода после запуска, не показываются.
  readonly HashSet<string> pingSeen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  readonly HashSet<string> pingPrimed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  readonly List<WhipPing> whips=new List<WhipPing>();
  public List<WhipPing> TakeWhips(){var list=whips.ToList();whips.Clear();return list;}

  // Фоновый шаг: читает только новые файлы ударов, а старше суток — удаляет, чтобы папка не росла.
  static void ScanPings(string root,HashSet<string> known,List<KeyValuePair<string,WhipPing>> found){
   var dir=Path.Combine(root,PingsFolder);if(!Directory.Exists(dir))return;
   foreach(var info in new DirectoryInfo(dir).EnumerateFiles("*.json")){
    if(DateTime.UtcNow-info.LastWriteTimeUtc>TimeSpan.FromDays(1)){try{info.Delete();}catch{}continue;}
    if(known.Contains(info.FullName))continue;
    WhipPing ping=null;try{if(info.Length<10000)ping=Json.Read<WhipPing>(File.ReadAllText(info.FullName,Encoding.UTF8));}catch{}
    found.Add(new KeyValuePair<string,WhipPing>(info.FullName,ping));
   }
  }
  void ApplyPings(SyncTarget target,List<KeyValuePair<string,WhipPing>> pings){
   bool primed=pingPrimed.Contains(target.Root);
   foreach(var p in pings){
    if(!pingSeen.Add(p.Key)||!primed)continue;var ping=p.Value;
    if(ping==null||!SamePerson(ping.to,target.Team.User)||ping.fromDevice==Settings.DeviceId)continue;
    whips.Add(new WhipPing{id=ping.id,project=target.Team.Name,to=ping.to,from=TaskItem.Person(ping.from)??"Участник",fromDevice=ping.fromDevice,createdUtc=ping.createdUtc});
   }
   pingPrimed.Add(target.Root);
  }
  // Готовит удар в потоке окна; отправка (SendWhip) — в фоне, потому что пишет на сервер.
  public WhipPing MakeWhip(string project,string to){
   var link=Team(project);if(link==null)throw new Exception("Хлыст работает только в общем проекте.");
   to=TaskItem.Person(to);if(to==null||SamePerson(to,link.User))throw new Exception("Выберите другого участника.");
   return new WhipPing{id=Guid.NewGuid().ToString("N"),project=project,to=to,from=link.User,fromDevice=Settings.DeviceId,createdUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)};
  }
  public string TeamPath(string project){var link=Team(project);return link==null?null:link.Path;}
  public static void SendWhip(string teamPath,WhipPing ping){
   if(!Directory.Exists(teamPath))throw new IOException("Папка проекта недоступна.");
   Atomic(Path.Combine(teamPath,PingsFolder,ping.id+".json"),Json.Write(ping));
  }
 }

 // Звук и анимация хлыста. Звук синтезируется в коде — внешние файлы не нужны.
 public static class WhipFx {
  static byte[] sound;
  // Короткий свист замаха и резкий щелчок через 0,2 с — совпадает с ударом в анимации курсора.
  static byte[] Sound(){
   if(sound!=null)return sound;
   const int rate=44100;int n=(int)(rate*0.6);var rnd=new Random(7);var pcm=new short[n];double lp=0,hp=0,prev=0;
   for(int i=0;i<n;i++){
    double t=i/(double)rate,s=0,noise=rnd.NextDouble()*2-1;
    if(t<0.2){double k=t/0.2;lp+=(0.03+0.5*k)*(noise-lp);s+=lp*k*k*0.55;}
    double c=t-0.2;
    if(c>=0){hp=noise-prev;s+=hp*Math.Exp(-c*70)*0.9+Math.Sin(2*Math.PI*2300*c)*Math.Exp(-c*180)*0.45+Math.Sin(2*Math.PI*160*c)*Math.Exp(-c*40)*0.35;}
    prev=noise;pcm[i]=(short)(Math.Max(-1,Math.Min(1,s))*32000);
   }
   using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms)){
    w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+n*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
    w.Write(Encoding.ASCII.GetBytes("data"));w.Write(n*2);foreach(var v in pcm)w.Write(v);w.Flush();sound=ms.ToArray();
   }
   return sound;
  }
  public static void Crack(){try{new System.Media.SoundPlayer(new MemoryStream(Sound())).Play();}catch{}}

  // Курсор на время удара становится хлыстом: замах и удар кончиком в точку щелчка.
  public static void Strike(Canvas layer,Point at){
   var whip=new Canvas{Width=72,Height=72,IsHitTestVisible=false};
   var handle=new Shapes.Line{X1=6,Y1=66,X2=22,Y2=48,Stroke=new SolidColorBrush(Color.FromRgb(0x6B,0x3F,0x22)),StrokeThickness=7,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
   var grip=new Shapes.Line{X1=9,Y1=63,X2=15,Y2=56,Stroke=new SolidColorBrush(Color.FromRgb(0x3E,0x24,0x14)),StrokeThickness=8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
   var lash=new Shapes.Path{Data=Geometry.Parse("M22,48 C40,40 34,14 70,2"),Stroke=new SolidColorBrush(Color.FromRgb(0xC8,0x8A,0x4E)),StrokeThickness=2.6,StrokeEndLineCap=PenLineCap.Round,StrokeStartLineCap=PenLineCap.Round};
   whip.Children.Add(lash);whip.Children.Add(handle);whip.Children.Add(grip);
   whip.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=6,ShadowDepth=2,Opacity=.45};
   // Кончик хлыста (70,2) приходится на точку щелчка; вращение — вокруг рукояти.
   Canvas.SetLeft(whip,at.X-70);Canvas.SetTop(whip,at.Y-2);
   var turn=new RotateTransform(0,6,66);whip.RenderTransform=turn;layer.Children.Add(whip);
   Mouse.OverrideCursor=Cursors.None;
   var swing=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(520)};
   swing.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
   swing.KeyFrames.Add(new EasingDoubleKeyFrame(-38,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)),new QuadraticEase{EasingMode=EasingMode.EaseOut}));
   swing.KeyFrames.Add(new EasingDoubleKeyFrame(14,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(210)),new QuadraticEase{EasingMode=EasingMode.EaseIn}));
   swing.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)),new ElasticEase{Oscillations=2,Springiness=4,EasingMode=EasingMode.EaseOut}));
   turn.BeginAnimation(RotateTransform.AngleProperty,swing);
   // В момент удара — короткая вспышка в точке щелчка.
   var flash=new Shapes.Ellipse{Width=26,Height=26,Stroke=new SolidColorBrush(Color.FromRgb(0xFF,0xD2,0x7A)),StrokeThickness=3,Opacity=0,IsHitTestVisible=false,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(.3,.3)};
   Canvas.SetLeft(flash,at.X-13);Canvas.SetTop(flash,at.Y-13);layer.Children.Add(flash);
   var begin=TimeSpan.FromMilliseconds(200);
   flash.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimationUsingKeyFrames{KeyFrames={new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)),new DiscreteDoubleKeyFrame(1,KeyTime.FromTimeSpan(begin)),new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)))}});
   var grow=new DoubleAnimationUsingKeyFrames{KeyFrames={new DiscreteDoubleKeyFrame(.3,KeyTime.FromTimeSpan(begin)),new EasingDoubleKeyFrame(1.6,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)),new QuadraticEase{EasingMode=EasingMode.EaseOut})}};
   ((ScaleTransform)flash.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty,grow);((ScaleTransform)flash.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty,grow);
   var done=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(600)};
   done.Tick+=delegate{done.Stop();layer.Children.Remove(whip);layer.Children.Remove(flash);Mouse.OverrideCursor=null;};done.Start();
  }
 }
}
