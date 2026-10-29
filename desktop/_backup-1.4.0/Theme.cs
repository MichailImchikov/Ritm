using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Linq;
namespace Ritm {
 public sealed class PaletteDefinition {public string Id,Name;public string[] Light,Dark;public PaletteDefinition(string id,string name,string[] light,string[] dark){Id=id;Name=name;Light=light;Dark=dark;}}
 public sealed class SurfaceTint:IValueConverter{public object Convert(object value,Type target,object parameter,System.Globalization.CultureInfo culture){var c=(Color)value;double t=UI.Dark?.05:0;return Color.FromRgb((byte)(c.R+(255-c.R)*t),(byte)(c.G+(255-c.G)*t),(byte)(c.B+(255-c.B)*t));}public object ConvertBack(object v,Type t,object p,System.Globalization.CultureInfo c){throw new NotSupportedException();}}
 public sealed class ThemeColor:INotifyPropertyChanged{Color color;public Color Color{get{return color;}set{color=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Color"));}}public event PropertyChangedEventHandler PropertyChanged;}
 public static class UI { [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size); public static void TitleBar(Window window){try{IntPtr h=new System.Windows.Interop.WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;int value=Dark?1:0;DwmSetWindowAttribute(h,20,ref value,4);}catch{}}
  static readonly Dictionary<string,SolidColorBrush> brushes=new Dictionary<string,SolidColorBrush>(StringComparer.OrdinalIgnoreCase);
  static readonly Dictionary<string,ThemeColor> sources=new Dictionary<string,ThemeColor>(StringComparer.OrdinalIgnoreCase);
  public static bool Dark;
  public static string Palette="violet";
  public static readonly PaletteDefinition[] Palettes={
   new PaletteDefinition("violet","Ирис",new[]{"#6554D9","#E4DFFC","#F4F6FB","#FFFFFF","#ECEFF7","#DEE2ED","#657087"},new[]{"#6E5ACF","#292541","#13151D","#1D202B","#0E1017","#303442","#9CA3B7"}),
   new PaletteDefinition("ocean","Океан",new[]{"#2768C8","#DFEBFE","#F3F7FC","#FFFFFF","#EAF1FA","#DDE4EF","#5F6F89"},new[]{"#2D6ABB","#1D2F49","#111821","#1B2633","#0C121B","#2E3C4D","#A3AFC1"}),
   new PaletteDefinition("sage","Шалфей",new[]{"#197961","#DDF1E9","#F3F8F5","#FFFFFF","#E7F1EC","#D8E6DE","#60776A"},new[]{"#227C65","#1D352F","#111B18","#1B2C25","#0C1511","#2C4237","#A0B6AA"}),
   new PaletteDefinition("amber","Янтарь",new[]{"#965F17","#F9EACD","#FBF8F1","#FFFFFF","#F4EEDD","#E8DFCA","#7D6F58"},new[]{"#96631D","#362D21","#1B1712","#2A241C","#14110D","#44392B","#B9AB95"}),
   new PaletteDefinition("rose","Пудра",new[]{"#AE426D","#F9DFE9","#FCF5F8","#FFFFFF","#F7E9F0","#EADAE3","#866579"},new[]{"#B04572","#3C2534","#1D1419","#2E1F28","#170E13","#48313E","#C2A5B5"})
  };
  static readonly Dictionary<string,string> colors=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {"#2D3933","#EEF0F7"},{"#849079","#9CA3B7"},{"#CB704A","#7665E8"},{"#E5E8DF","#303442"},
   {"#FFFFFF","#1D202B"},{"#F8F8F5","#13151D"},{"#F0F1EB","#0E1017"},{"#F7F8F1","#181B25"},
   {"#FAFBF7","#13151D"},{"#F0F3EA","#25263D"},{"#F0F3E9","#25263D"},{"#EADFD2","#292541"},
   {"#A65C3D","#B1A3FF"},{"#E9EDE1","#292541"},{"#EAF0E1","#292541"},{"#FCFCF8","#181B25"},
   {"#ACB69E","#646C82"},{"#F1F3EC","#1A1D27"},{"#EDF1E8","#252B40"},{"#FBF0E6","#292541"},
   {"#F8FAF3","#161923"},{"#6683A4","#79A9FF"},{"#BD8B60","#B99AFF"},{"#729781","#6DC7C1"},{"#9B80AD","#EBBB75"},
   {"#5978DB","#79A9FF"},{"#7861D5","#B99AFF"},{"#29938B","#6DC7C1"},{"#D0A45E","#EBBB75"},{"#565284","#565284"}
  };
  static readonly Dictionary<string,string> light=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {"#2D3933","#24283B"},{"#849079","#737D95"},{"#CB704A","#6554D9"},{"#E5E8DF","#DEE2ED"},
   {"#F8F8F5","#F4F6FB"},{"#F0F1EB","#ECEFF7"},{"#F7F8F1","#EEF1F8"},{"#FAFBF7","#F4F6FB"},
   {"#F0F3EA","#ECEAFF"},{"#F0F3E9","#ECEAFF"},{"#EADFD2","#E5E0FC"},{"#A65C3D","#6554D9"},
   {"#E9EDE1","#E4DFFC"},{"#EAF0E1","#E4DFFC"},{"#FCFCF8","#F8F9FD"},{"#ACB69E","#ABB2C5"},
   {"#F1F3EC","#F0F2F8"},{"#EDF1E8","#E8EFFC"},{"#FBF0E6","#EAE5FF"},{"#F8FAF3","#EFF2F8"},
   {"#6683A4","#517ACA"},{"#BD8B60","#9163CC"},{"#729781","#248D87"},{"#9B80AD","#B58238"},
   {"#5978DB","#5978DB"},{"#7861D5","#7861D5"},{"#29938B","#29938B"},{"#D0A45E","#B58238"},{"#565284","#A496E5"}
  };
  static string Resolve(string hex){var p=Palettes.FirstOrDefault(x=>x.Id==Palette)??Palettes[0];var values=Dark?p.Dark:p.Light;int index=hex=="#CB704A"||hex=="#A65C3D"||hex=="#565284"?0:hex=="#E9EDE1"||hex=="#EAF0E1"||hex=="#EADFD2"||hex=="#FBF0E6"?1:hex=="#F8F8F5"||hex=="#FAFBF7"?2:hex=="#FFFFFF"?3:hex=="#F0F1EB"?4:hex=="#E5E8DF"?5:hex=="#849079"?6:-1;if(index>=0)return values[index];return Dark?(colors.ContainsKey(hex)?colors[hex]:hex):(light.ContainsKey(hex)?light[hex]:hex);}
  public static Brush B(string hex){SolidColorBrush brush;if(!brushes.TryGetValue(hex,out brush)){var source=new ThemeColor{Color=(Color)ColorConverter.ConvertFromString(Resolve(hex))};brush=new SolidColorBrush();BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding("Color"){Source=source,Mode=BindingMode.OneWay});sources[hex]=source;brushes[hex]=brush;}return brush;}
  public static readonly Brush Ink=B("#2D3933"),Muted=B("#849079"),Accent=B("#CB704A"),Line=B("#E5E8DF"),Paper=B("#FFFFFF"),Canvas=B("#F8F8F5"),Rail=B("#F0F1EB");
  public static void SetTheme(string theme){Dark=theme=="dark";foreach(var pair in sources)pair.Value.Color=(Color)ColorConverter.ConvertFromString(Resolve(pair.Key));if(Application.Current!=null)foreach(Window window in Application.Current.Windows)TitleBar(window);}
  public static void SetPalette(string palette){Palette=Palettes.Any(p=>p.Id==palette)?palette:"violet";SetTheme(Dark?"dark":"light");}
  public static TextBlock Text(string value,double size=13,Brush brush=null,bool bold=false){return new TextBlock{Text=value,FontSize=size,Foreground=brush??Ink,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
  public static Button Button(string label,Action action,bool primary=false){var b=new Button{Content=label,Tag=primary?"primary":"surface",Padding=new Thickness(15,10,15,10),FontSize=12,Margin=new Thickness(0,0,6,0),Background=primary?Accent:Paper,Foreground=primary?Brushes.White:Ink,BorderBrush=primary?Accent:Line,BorderThickness=new Thickness(1),Cursor=System.Windows.Input.Cursors.Hand};b.Click+=delegate{action();};return b;}
  public static Brush CardSurface(){var top=new GradientStop{Offset=0};BindingOperations.SetBinding(top,GradientStop.ColorProperty,new Binding("Color"){Source=Paper,Converter=new SurfaceTint()});var bottom=new GradientStop{Offset=1};BindingOperations.SetBinding(bottom,GradientStop.ColorProperty,new Binding("Color"){Source=Paper});return new LinearGradientBrush(new GradientStopCollection{top,bottom},new Point(0,0),new Point(0,1));}
  public static Border Card(UIElement content,Brush background=null,Thickness? padding=null){return new Border{Background=background??CardSurface(),BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=padding??new Thickness(18),Child=content,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=13,ShadowDepth=3,Opacity=Dark?.22:.08,Color=Colors.Black}};}
  public static void Gap(Panel p,double height=12){p.Children.Add(new Border{Height=height});}
  public static void Label(Panel p,string title){var t=Text(title,11,Muted,true);t.Margin=new Thickness(0,14,0,6);p.Children.Add(t);}
  public static TextBox Input(string value,int max=200,int height=36){return new TextBox{Text=value??"",MaxLength=max,MinHeight=height,Padding=new Thickness(10,7,10,7),BorderBrush=Line,BorderThickness=new Thickness(1),FontSize=13,Background=Paper,Foreground=Ink,CaretBrush=Ink,VerticalContentAlignment=VerticalAlignment.Center};}
  public static string Date(DateTime d){return d.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);}
  public static string DisplayDate(string d){return string.IsNullOrEmpty(d)?"Без даты":DateTime.ParseExact(d,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture).ToString("d MMMM",new System.Globalization.CultureInfo("ru-RU"));}
  public static string Priority(int p){return new[]{"","Срочный","Высокий","Обычный","Низкий"}[p];}
  public static string ColorFor(string name){int hash=0;foreach(char c in name??"")hash=(hash*31+c)&0x7fffffff;return new[]{"#6683A4","#BD8B60","#729781","#9B80AD"}[hash%4];}
  public static void InstallStyles(){
   EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler((s,e)=>TitleBar((Window)s)));var r=Application.Current.Resources;r["Ink"]=Ink;r["Paper"]=Paper;r["Line"]=Line;r["Accent"]=Accent;
   r.Add(typeof(Window),new Style(typeof(Window)){Setters={new Setter(Control.FontFamilyProperty,new FontFamily("Segoe UI")),new Setter(Control.FontSizeProperty,13.0),new Setter(Control.ForegroundProperty,Ink)}});
   r.Add(typeof(Button),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='surface' CornerRadius='9' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><Border.Effect><DropShadowEffect BlurRadius='8' ShadowDepth='2' Opacity='0.18'/></Border.Effect><Grid><Border x:Name='gloss' IsHitTestVisible='False' CornerRadius='8' BorderBrush='#18FFFFFF' BorderThickness='1,1,1,0'><Border.Background><LinearGradientBrush StartPoint='0,0' EndPoint='0,1'><GradientStop Color='#12FFFFFF' Offset='0'/><GradientStop Color='#00FFFFFF' Offset='1'/></LinearGradientBrush></Border.Background></Border><ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Effect'><Setter.Value><DropShadowEffect BlurRadius='11' ShadowDepth='3' Opacity='0.22'/></Setter.Value></Setter><Setter TargetName='gloss' Property='Opacity' Value='0.8'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='surface' Property='RenderTransform'><Setter.Value><TranslateTransform Y='1.5'/></Setter.Value></Setter><Setter TargetName='surface' Property='Effect'><Setter.Value><DropShadowEffect BlurRadius='4' ShadowDepth='0' Opacity='0.13'/></Setter.Value></Setter><Setter TargetName='gloss' Property='Opacity' Value='0.3'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='surface' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='Tag' Value='flat'><Setter TargetName='surface' Property='Effect' Value='{x:Null}'/><Setter TargetName='gloss' Property='Opacity' Value='0'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='surface' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
   r.Add(typeof(ComboBox),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Paper}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='MinHeight' Value='34'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton Focusable='False' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' IsChecked='{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8'><TextBlock Text='⌄' Margin='0,0,9,0' HorizontalAlignment='Right' VerticalAlignment='Center' Foreground='{DynamicResource Ink}'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter x:Name='Display' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='10,5,27,5' VerticalAlignment='Center'/><TextBox x:Name='PART_EditableTextBox' Visibility='Collapsed' Background='{DynamicResource Paper}' Foreground='{DynamicResource Ink}' BorderThickness='0' Margin='8,4,28,4' VerticalContentAlignment='Center'/><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'><Border Background='{DynamicResource Paper}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='8' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='300'><ScrollViewer><StackPanel IsItemsHost='True'/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsEditable' Value='True'><Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/><Setter TargetName='Display' Property='Visibility' Value='Collapsed'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
   r.Add(typeof(ComboBoxItem),new Style(typeof(ComboBoxItem)){Setters={new Setter(Control.ForegroundProperty,Ink),new Setter(Control.BackgroundProperty,Paper),new Setter(Control.PaddingProperty,new Thickness(10,8,10,8))}});
   r.Add(typeof(TextBox),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border x:Name='frame' CornerRadius='8' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='frame' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
   r.Add(typeof(ContextMenu),new Style(typeof(ContextMenu)){Setters={new Setter(Control.BackgroundProperty,Paper),new Setter(Control.ForegroundProperty,Ink),new Setter(Control.BorderBrushProperty,Line),new Setter(Control.PaddingProperty,new Thickness(5))}});
  }
 }
}






