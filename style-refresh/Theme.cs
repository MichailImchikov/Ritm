using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Data;
namespace Ritm {
 public sealed class ThemeColor:INotifyPropertyChanged{Color color;public Color Color{get{return color;}set{color=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Color"));}}public event PropertyChangedEventHandler PropertyChanged;}
 public static class UI { [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size); public static void TitleBar(Window window){try{IntPtr h=new System.Windows.Interop.WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;int value=Dark?1:0;DwmSetWindowAttribute(h,20,ref value,4);}catch{}}
  static readonly Dictionary<string,SolidColorBrush> brushes=new Dictionary<string,SolidColorBrush>(StringComparer.OrdinalIgnoreCase);
  static readonly Dictionary<string,ThemeColor> sources=new Dictionary<string,ThemeColor>(StringComparer.OrdinalIgnoreCase);
  public static bool Dark;
  static readonly Dictionary<string,string> colors=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {"#2D3933","#E2E8E3"},{"#849079","#98A59D"},{"#CB704A","#D58866"},{"#E5E8DF","#35413A"},
   {"#FFFFFF","#232C27"},{"#F8F8F5","#19211D"},{"#F0F1EB","#151C18"},{"#F7F8F1","#1D2721"},
   {"#FAFBF7","#1B241F"},{"#F0F3EA","#29382E"},{"#F0F3E9","#29382E"},{"#EADFD2","#3B3028"},
   {"#A65C3D","#E8A57F"},{"#E9EDE1","#2D3C30"},{"#EAF0E1","#304633"},{"#FCFCF8","#202B24"},
   {"#ACB69E","#66766A"},{"#F1F3EC","#28332C"},{"#EDF1E8","#2B3A2F"},{"#FBF0E6","#3B3028"},
   {"#F8FAF3","#1D2721"},{"#6683A4","#91B2D3"},{"#BD8B60","#D9AD80"},{"#729781","#93BAA1"},{"#9B80AD","#B7A2C9"}
  };
  public static Brush B(string hex){SolidColorBrush brush;if(!brushes.TryGetValue(hex,out brush)){string target=Dark&&colors.ContainsKey(hex)?colors[hex]:hex;var source=new ThemeColor{Color=(Color)ColorConverter.ConvertFromString(target)};brush=new SolidColorBrush();BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding("Color"){Source=source,Mode=BindingMode.OneWay});sources[hex]=source;brushes[hex]=brush;}return brush;}
  public static readonly Brush Ink=B("#2D3933"),Muted=B("#849079"),Accent=B("#CB704A"),Line=B("#E5E8DF"),Paper=B("#FFFFFF"),Canvas=B("#F8F8F5"),Rail=B("#F0F1EB");
  public static void SetTheme(string theme){Dark=theme=="dark";foreach(var pair in sources){string color=Dark&&colors.ContainsKey(pair.Key)?colors[pair.Key]:pair.Key;pair.Value.Color=(Color)ColorConverter.ConvertFromString(color);}if(Application.Current!=null)foreach(Window window in Application.Current.Windows)TitleBar(window);}
  public static TextBlock Text(string value,double size=13,Brush brush=null,bool bold=false){return new TextBlock{Text=value,FontSize=size,Foreground=brush??Ink,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
  public static Button Button(string label,Action action,bool primary=false){var b=new Button{Content=label,Padding=new Thickness(13,9,13,9),FontSize=12,Margin=new Thickness(0,0,6,0),Background=primary?Accent:Paper,Foreground=primary?Brushes.White:Ink,BorderBrush=primary?Accent:Line,BorderThickness=new Thickness(1),Cursor=System.Windows.Input.Cursors.Hand};b.Click+=delegate{action();};return b;}
  public static Border Card(UIElement content,Brush background=null,Thickness? padding=null){return new Border{Background=background??Paper,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=padding??new Thickness(17),Child=content};}
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
   r.Add(typeof(Button),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='surface' CornerRadius='6' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Opacity' Value='0.8'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='surface' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
   r.Add(typeof(ComboBox),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Paper}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='MinHeight' Value='34'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton Focusable='False' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' IsChecked='{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='5'><TextBlock Text='⌄' Margin='0,0,9,0' HorizontalAlignment='Right' VerticalAlignment='Center' Foreground='{DynamicResource Ink}'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter x:Name='Display' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='10,5,27,5' VerticalAlignment='Center'/><TextBox x:Name='PART_EditableTextBox' Visibility='Collapsed' Background='{DynamicResource Paper}' Foreground='{DynamicResource Ink}' BorderThickness='0' Margin='8,4,28,4' VerticalContentAlignment='Center'/><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'><Border Background='{DynamicResource Paper}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='5' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='300'><ScrollViewer><StackPanel IsItemsHost='True'/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsEditable' Value='True'><Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/><Setter TargetName='Display' Property='Visibility' Value='Collapsed'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
   r.Add(typeof(ComboBoxItem),new Style(typeof(ComboBoxItem)){Setters={new Setter(Control.ForegroundProperty,Ink),new Setter(Control.BackgroundProperty,Paper),new Setter(Control.PaddingProperty,new Thickness(10,8,10,8))}});
  }
 }
}

