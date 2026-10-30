using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Ritm {
 public sealed partial class MainWindow {
  static readonly string[] MeetingColorIds={"blue","teal","green","violet","orange","red"};
  static readonly string[] MeetingColorHex={"#285EA8","#116F76","#287044","#6545A0","#A45316","#A23742"};
  static Color HueColor(double hue){double h=(hue%360)/60.0;int sector=(int)h;double f=h-sector,v=.70,p=.17,q=v-(v-p)*f,t=p+(v-p)*f;double r=0,g=0,b=0;switch(sector){case 0:r=v;g=t;b=p;break;case 1:r=q;g=v;b=p;break;case 2:r=p;g=v;b=t;break;case 3:r=p;g=q;b=v;break;case 4:r=t;g=p;b=v;break;default:r=v;g=p;b=q;break;}return Color.FromRgb((byte)(r*255),(byte)(g*255),(byte)(b*255));}
  static Color MeetingColor(TaskItem meeting){if(meeting.meetingHue.HasValue)return HueColor(meeting.meetingHue.Value);int i=Array.IndexOf(MeetingColorIds,meeting.meetingColor);return (Color)ColorConverter.ConvertFromString(MeetingColorHex[i<0?0:i]);}
  static int MeetingHue(TaskItem meeting){if(meeting.meetingHue.HasValue)return meeting.meetingHue.Value;var c=MeetingColor(meeting);double r=c.R/255.0,g=c.G/255.0,b=c.B/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;if(d==0)return 0;double h=max==r?((g-b)/d)%6:max==g?(b-r)/d+2:(r-g)/d+4;return (int)Math.Round((h*60+360)%360);}
  static Brush MeetingBrush(TaskItem meeting){var c=MeetingColor(meeting);c.A=(byte)Math.Round(255*(100-(meeting.meetingTransparency??0))/100.0);return new SolidColorBrush(c);}
  static Brush MeetingTextBrush(TaskItem meeting){var c=MeetingColor(meeting);var bg=((SolidColorBrush)UI.Paper).Color;double a=(100-(meeting.meetingTransparency??0))/100.0;double luminance=.2126*(c.R*a+bg.R*(1-a))+.7152*(c.G*a+bg.G*(1-a))+.0722*(c.B*a+bg.B*(1-a));return luminance>150?Brushes.Black:Brushes.White;}
  void AddChoice(string date){
   var w=new Window{Owner=this,Title="Добавить",Width=360,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UI.Canvas};
   var p=new StackPanel{Margin=new Thickness(24)};w.Content=p;p.Children.Add(UI.Text("Что добавить?",21,null,true));UI.Gap(p,16);
   int choice=0;
   p.Children.Add(UI.Button("Задача",()=>{choice=1;w.Close();},true));UI.Gap(p,8);
   p.Children.Add(UI.Button("Встреча",()=>{choice=2;w.Close();}));w.ShowDialog();
   if(choice==1)Edit(null,date);
   if(choice==2)EditMeeting(new TaskItem{isMeeting=true,category=store.IsTeamName(project)?project:null,date=date??UI.Date(DateTime.Today),meetingTime="10:00"});
  }
  void EditMeeting(TaskItem source){
   var value=source.Clone();var teams=store.Teams.Select(t=>t.Name).ToList();
   if(teams.Count==0){MessageBox.Show(this,"Сначала подключите общий проект.","Встреча");return;}
   var w=new Window{Owner=this,Title=value.version==0?"Новая встреча":"Встреча",Width=470,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UI.Canvas};
   var p=new StackPanel{Margin=new Thickness(24)};w.MaxHeight=SystemParameters.WorkArea.Height-40;w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};p.Children.Add(UI.Text(w.Title,22,null,true));UI.Gap(p,12);
   p.Children.Add(UI.Text("Общий проект",12));var projects=new ComboBox{ItemsSource=teams,SelectedItem=teams.Contains(value.category)?value.category:teams[0],Margin=new Thickness(0,4,0,10),IsEnabled=value.version==0};p.Children.Add(projects);
   p.Children.Add(UI.Text("Название",12));var name=UI.Input(value.title,200);name.Margin=new Thickness(0,4,0,10);p.Children.Add(name);
   p.Children.Add(UI.Text("Описание",12));var description=UI.Input(value.description,10000);description.AcceptsReturn=true;description.Height=70;description.TextWrapping=TextWrapping.Wrap;description.Margin=new Thickness(0,4,0,10);p.Children.Add(description);
   p.Children.Add(UI.Text("Дата начала",12));var date=new DateField(value.date);date.Margin=new Thickness(0,4,0,10);p.Children.Add(date);
   p.Children.Add(UI.Text("Время (чч:мм)",12));var time=UI.Input(value.meetingTime??"10:00",5);time.Margin=new Thickness(0,4,0,10);p.Children.Add(time);
   p.Children.Add(UI.Text("Цвет в календаре",12));var preview=new Border{Height=32,CornerRadius=new CornerRadius(7),Margin=new Thickness(0,5,0,5),BorderBrush=UI.Line,BorderThickness=new Thickness(1),Child=UI.Text("Предпросмотр встречи",12,null,true)};((TextBlock)preview.Child).Margin=new Thickness(10,6,0,0);p.Children.Add(preview);
   var hue=new Slider{Minimum=0,Maximum=360,Value=MeetingHue(value),IsSnapToTickEnabled=true,TickFrequency=1,Margin=new Thickness(0,0,0,2)};System.Windows.Automation.AutomationProperties.SetName(hue,"Цвет встречи");p.Children.Add(hue);
   var spectrum=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,0)};for(int i=0;i<=6;i++)spectrum.GradientStops.Add(new GradientStop(HueColor(i*60),i/6.0));p.Children.Add(new Border{Height=6,CornerRadius=new CornerRadius(3),Background=spectrum,Margin=new Thickness(0,0,0,9)});
   var transparencyLabel=UI.Text("",12);p.Children.Add(transparencyLabel);var transparency=new Slider{Minimum=0,Maximum=100,Value=value.meetingTransparency??0,IsSnapToTickEnabled=true,TickFrequency=1,Margin=new Thickness(0,4,0,10)};System.Windows.Automation.AutomationProperties.SetName(transparency,"Прозрачность встречи, проценты");p.Children.Add(transparency);
   Action updatePreview=()=>{var sample=value.Clone();sample.meetingTransparency=(int)transparency.Value;preview.Background=MeetingBrush(sample);((TextBlock)preview.Child).Foreground=MeetingTextBrush(sample);transparencyLabel.Text="Прозрачность · "+(int)transparency.Value+"%";};hue.ValueChanged+=delegate{value.meetingHue=(int)hue.Value;updatePreview();};transparency.ValueChanged+=delegate{updatePreview();};updatePreview();
   p.Children.Add(UI.Text("Повторять каждую неделю",12));p.Children.Add(UI.Text("Выберите дни недели или оставьте пустым для одной встречи",10,UI.Muted));
   var days=new WrapPanel{Margin=new Thickness(0,8,0,14)};var checks=new List<CheckBox>();var labels=new[]{"Пн","Вт","Ср","Чт","Пт","Сб","Вс"};
   for(int i=0;i<7;i++){int day=(i+1)%7;var check=new CheckBox{Content=labels[i],Margin=new Thickness(0,0,12,0),IsChecked=value.meetingDays!=null&&value.meetingDays.Contains(day),Tag=day};checks.Add(check);days.Children.Add(check);}p.Children.Add(days);
   p.Children.Add(UI.Button("Сохранить",()=>{try{value.category=(string)projects.SelectedItem;value.title=name.Text;value.description=description.Text;value.date=date.SelectedDate.HasValue?UI.Date(date.SelectedDate.Value):null;value.meetingTime=time.Text.Trim();value.meetingTransparency=(int)transparency.Value;value.meetingDays=checks.Where(c=>c.IsChecked==true).Select(c=>(int)c.Tag).ToList();store.Save(value,value.version);w.DialogResult=true;}catch(Exception e){MessageBox.Show(w,e.Message,"Не удалось сохранить");}},true));
   if(value.version>0){UI.Gap(p,8);p.Children.Add(UI.Button("Удалить встречу целиком",()=>{if(MessageBox.Show(w,"Удалить встречу и все её повторения?","Удаление",MessageBoxButton.YesNo)==MessageBoxResult.Yes){try{store.Delete(value);w.DialogResult=true;}catch(Exception e){MessageBox.Show(w,e.Message,"Не удалось удалить");}}}));}
   w.ShowDialog();Refresh();RunCloudSync();
  }
 }
}
