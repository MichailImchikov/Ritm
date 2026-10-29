using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Ritm {
 // Анимации интерфейса. Отключаются в настройках («Плавные анимации») и при рендере превью.
 public static class Fx {
  static bool on=true;public static bool Suppressed;
  public static bool Enabled{get{return on&&!Suppressed;}set{on=value;}}
  static readonly IEasingFunction Ease=new CubicEase{EasingMode=EasingMode.EaseOut},Back=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.45};
  internal static DoubleAnimation A(double? from,double to,double ms,double delay=0,IEasingFunction ease=null){var a=new DoubleAnimation{To=to,Duration=TimeSpan.FromMilliseconds(ms),BeginTime=TimeSpan.FromMilliseconds(delay),EasingFunction=ease??Ease};if(from.HasValue)a.From=from;return a;}
  static TransformGroup Rig(UIElement e){var g=e.RenderTransform as TransformGroup;if(g==null||g.Children.Count!=2||!(g.Children[0] is ScaleTransform)){g=new TransformGroup();g.Children.Add(new ScaleTransform());g.Children.Add(new TranslateTransform());e.RenderTransform=g;e.RenderTransformOrigin=new Point(.5,.5);}return g;}
  static ScaleTransform Scale(UIElement e){return (ScaleTransform)Rig(e).Children[0];}
  static TranslateTransform Move(UIElement e){return (TranslateTransform)Rig(e).Children[1];}
  // Появление: прозрачность и сдвиг снизу (или сбоку).
  public static void Enter(UIElement e,double delay=0,double dy=12,double dx=0){if(e==null||!Enabled)return;double target=e.Opacity;e.Opacity=0;var fade=A(0,target,300,delay);fade.Completed+=delegate{e.Opacity=target;e.BeginAnimation(UIElement.OpacityProperty,null);};e.BeginAnimation(UIElement.OpacityProperty,fade);var t=Move(e);t.Y=dy;t.X=dx;t.BeginAnimation(TranslateTransform.YProperty,A(dy,0,380,delay));if(dx!=0)t.BeginAnimation(TranslateTransform.XProperty,A(dx,0,380,delay));}
  public static void Pop(UIElement e,double from=.7){if(e==null||!Enabled)return;var s=Scale(e);s.BeginAnimation(ScaleTransform.ScaleXProperty,A(from,1,320,0,Back));s.BeginAnimation(ScaleTransform.ScaleYProperty,A(from,1,320,0,Back));}
  public static void FadeOut(UIElement e,Action done,double to=0,double ms=240){if(e==null||!Enabled){done();return;}var a=A(null,to,ms);a.Completed+=delegate{done();};e.BeginAnimation(UIElement.OpacityProperty,a);}
  // Карточка приподнимается при наведении, тень становится мягче и шире.
  public static void Lift(FrameworkElement card){card.MouseEnter+=delegate{if(!Enabled)return;Move(card).BeginAnimation(TranslateTransform.YProperty,A(null,-3,180));var s=card.Effect as DropShadowEffect;if(s!=null&&!s.IsFrozen){s.BeginAnimation(DropShadowEffect.BlurRadiusProperty,A(null,22,180));s.BeginAnimation(DropShadowEffect.ShadowDepthProperty,A(null,6,180));}};card.MouseLeave+=delegate{if(!Enabled)return;Move(card).BeginAnimation(TranslateTransform.YProperty,A(null,0,220));var s=card.Effect as DropShadowEffect;if(s!=null&&!s.IsFrozen){s.BeginAnimation(DropShadowEffect.BlurRadiusProperty,A(null,13,220));s.BeginAnimation(DropShadowEffect.ShadowDepthProperty,A(null,3,220));}};}
  public static void Progress(RangeBase bar,double value,double delay=0){if(!Enabled){bar.Value=value;return;}bar.Value=value;bar.BeginAnimation(RangeBase.ValueProperty,A(0,value,750,delay));}
  public static void Grow(FrameworkElement e,double height,double delay=0,double ms=600){e.Height=height;if(!Enabled||height<=0)return;e.BeginAnimation(FrameworkElement.HeightProperty,A(0,height,ms,delay));}
  public static void Shrink(FrameworkElement e,Action done,double ms=180){if(!Enabled){done();return;}var a=A(null,0,ms);a.Completed+=delegate{done();};e.BeginAnimation(FrameworkElement.HeightProperty,a);}
  // Плавная смена темы: снимок окна поверх нового оформления медленно растворяется.
  public static void Crossfade(Window w,Action change){var root=w.Content as Grid;if(!Enabled||root==null||root.ActualWidth<1||root.ActualHeight<1){change();return;}
   try{var dpi=VisualTreeHelper.GetDpi(root);var bmp=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(root.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);bmp.Render(root);
    var shot=new Image{Source=bmp,Stretch=Stretch.Fill,IsHitTestVisible=false};Grid.SetColumnSpan(shot,Math.Max(1,root.ColumnDefinitions.Count));Grid.SetRowSpan(shot,Math.Max(1,root.RowDefinitions.Count));Panel.SetZIndex(shot,1000);root.Children.Add(shot);change();var a=A(1,0,450,40);a.Completed+=delegate{root.Children.Remove(shot);};shot.BeginAnimation(UIElement.OpacityProperty,a);
   }catch{change();}}
  // Всплывающее уведомление внизу справа; необязательная кнопка действия (например, «Отменить»).
  public static void Toast(Panel host,string text,string action=null,Action onAction=null,double seconds=5){if(host==null)return;while(host.Children.Count>=3)host.Children.RemoveAt(0);
   var row=new DockPanel{LastChildFill=true};Border toast=null;Action dismiss=null;
   if(action!=null){var b=UI.Button(action,()=>{dismiss();if(onAction!=null)onAction();});b.Tag="flat";b.Background=Brushes.Transparent;b.BorderThickness=new Thickness(0);b.Foreground=UI.B("#565284");b.FontWeight=FontWeights.SemiBold;b.Padding=new Thickness(10,4,4,4);b.Margin=new Thickness(14,0,0,0);DockPanel.SetDock(b,Dock.Right);row.Children.Add(b);}
   var label=UI.Text(text,12,UI.Canvas);label.MaxWidth=330;row.Children.Add(label);
   toast=new Border{Child=row,Background=UI.Ink,CornerRadius=new CornerRadius(11),Padding=new Thickness(16,11,action==null?16:8,11),Margin=new Thickness(0,8,0,0),HorizontalAlignment=HorizontalAlignment.Right,Effect=new DropShadowEffect{BlurRadius=20,ShadowDepth=5,Opacity=.28,Color=Colors.Black}};
   bool gone=false;dismiss=()=>{if(gone)return;gone=true;FadeOut(toast,()=>host.Children.Remove(toast),0,200);};toast.MouseLeftButtonUp+=delegate{dismiss();};
   host.Children.Add(toast);Enter(toast,0,0,40);var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();dismiss();};timer.Start();}
  // Кольцо прогресса (таймер фокуса): дуга от верхней точки по часовой стрелке.
  public static Geometry Arc(double size,double thickness,double fraction){double r=(size-thickness)/2,c=size/2;fraction=Math.Max(0,Math.Min(1,fraction));if(fraction<=0)return Geometry.Empty;if(fraction>=.9999)return new EllipseGeometry(new Point(c,c),r,r);double a=fraction*2*Math.PI;var end=new Point(c+r*Math.Sin(a),c-r*Math.Cos(a));var fig=new PathFigure{StartPoint=new Point(c,c-r),IsClosed=false};fig.Segments.Add(new ArcSegment(end,new Size(r,r),0,fraction>.5,SweepDirection.Clockwise,true));var g=new PathGeometry();g.Figures.Add(fig);return g;}
  public static Border Chip(string text,Brush fg,Brush bg,string glyph=null){var p=new StackPanel{Orientation=Orientation.Horizontal};if(glyph!=null){var i=UI.Icon(glyph,9,fg);i.Margin=new Thickness(0,0,5,0);p.Children.Add(i);}p.Children.Add(UI.Text(text,10,fg,true));return new Border{Child=p,Background=bg,CornerRadius=new CornerRadius(6),Padding=new Thickness(7,3,7,3),Margin=new Thickness(0,0,6,6)};}
  public static Brush Tint(string hex,byte alpha){var c=(Color)ColorConverter.ConvertFromString(hex);var b=new SolidColorBrush(Color.FromArgb(alpha,c.R,c.G,c.B));b.Freeze();return b;}
  public static Brush Solid(string hex){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();return b;}
  public static Rectangle Dashed(){return new Rectangle{Stroke=UI.Line,StrokeThickness=1.4,StrokeDashArray=new DoubleCollection{4,3},RadiusX=12,RadiusY=12};}
 }
}
