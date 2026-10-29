using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Ritm {
 // «Призрак» перетаскиваемой карточки: снимок, который плавно догоняет курсор, наклоняется по ходу движения и мягко «приземляется».
 public sealed class DragGhost {
  readonly Canvas layer;readonly Border shell;readonly ScaleTransform scale=new ScaleTransform(1,1);readonly RotateTransform tilt=new RotateTransform();readonly DropShadowEffect shadow;readonly Point grab;Point target,current;double angle;bool live;
  public double Height{get;private set;}
  // face — живая копия карточки (снимок через VisualBrush в WPF иногда получается пустым).
  public DragGhost(Canvas host,FrameworkElement source,FrameworkElement face,Point grabAt,Point start){
   layer=host;grab=grabAt;double w=Math.Max(1,source.ActualWidth),h=Math.Max(1,source.ActualHeight);Height=h;
   shadow=new DropShadowEffect{BlurRadius=14,ShadowDepth=3,Direction=270,Opacity=.2,Color=Colors.Black};
   face.Width=w;face.MinHeight=h;shell=new Border{Child=face,Width=w,IsHitTestVisible=false,Effect=shadow,RenderTransformOrigin=new Point(grab.X/w,grab.Y/h)};var g=new TransformGroup();g.Children.Add(scale);g.Children.Add(tilt);shell.RenderTransform=g;
   layer.Children.Add(shell);current=target=new Point(start.X-grab.X,start.Y-grab.Y);Place();live=true;CompositionTarget.Rendering+=Tick;
   if(Fx.Enabled){var back=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.6};scale.BeginAnimation(ScaleTransform.ScaleXProperty,Fx.A(1,1.045,220,0,back));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Fx.A(1,1.045,220,0,back));shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty,Fx.A(14,34,220));shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty,Fx.A(3,14,220));shadow.BeginAnimation(DropShadowEffect.OpacityProperty,Fx.A(.2,.38,220));}
  }
  public void Follow(Point cursor){target=new Point(cursor.X-grab.X,cursor.Y-grab.Y);}
  void Tick(object sender,EventArgs e){if(!live)return;double k=Fx.Enabled?.32:1,dx=(target.X-current.X)*k;current=new Point(current.X+dx,current.Y+(target.Y-current.Y)*k);double want=Math.Max(-10,Math.Min(10,dx*.9))+1.5;angle+=(want-angle)*.18;tilt.Angle=Fx.Enabled?angle:0;Place();}
  void Place(){Canvas.SetLeft(shell,current.X);Canvas.SetTop(shell,current.Y);}
  void Stop(){if(!live)return;live=false;CompositionTarget.Rendering-=Tick;}
  // Перелёт на место карточки: курс, наклон и масштаб возвращаются к нулю, тень опускается.
  public void Land(Point topLeft,Action done){Stop();if(!Fx.Enabled){layer.Children.Remove(shell);done();return;}
   var ease=new CubicEase{EasingMode=EasingMode.EaseInOut};var move=Fx.A(current.X,topLeft.X,280,0,ease);move.Completed+=delegate{done();var fade=Fx.A(1,0,120);fade.Completed+=delegate{layer.Children.Remove(shell);};shell.BeginAnimation(UIElement.OpacityProperty,fade);};
   shell.BeginAnimation(Canvas.LeftProperty,move);shell.BeginAnimation(Canvas.TopProperty,Fx.A(current.Y,topLeft.Y,280,0,ease));tilt.BeginAnimation(RotateTransform.AngleProperty,Fx.A(tilt.Angle,0,240,0,ease));scale.BeginAnimation(ScaleTransform.ScaleXProperty,Fx.A(null,1,260,0,ease));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Fx.A(null,1,260,0,ease));shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty,Fx.A(null,13,260));shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty,Fx.A(null,3,260));shadow.BeginAnimation(DropShadowEffect.OpacityProperty,Fx.A(null,.2,260));}
  public void Vanish(){Stop();if(!Fx.Enabled){layer.Children.Remove(shell);return;}var fade=Fx.A(null,0,180);fade.Completed+=delegate{layer.Children.Remove(shell);};shell.BeginAnimation(UIElement.OpacityProperty,fade);scale.BeginAnimation(ScaleTransform.ScaleXProperty,Fx.A(null,.9,180));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Fx.A(null,.9,180));}
 }
}
