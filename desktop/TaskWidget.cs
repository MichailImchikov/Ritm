using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ritm {
 // Мини-виджет поверх всех окон: главные задачи и их описание. Живёт отдельно от главного окна, поэтому не сворачивается вместе с ним.
 public sealed class TaskWidget:Window {
  const int Limit=5,ExStyle=-20,ToolWindow=0x80,NoActivate=0x08000000;
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
  readonly Store store;readonly Func<string,string> projectName;readonly Action<string> open;readonly StackPanel list=new StackPanel();readonly TextBlock counter=UI.Text("",11,UI.Muted);string shown;
  public TaskWidget(Store data,Func<string,string> displayName,Action<string> onOpen){
   store=data;projectName=displayName;open=onOpen;Title="Главные задачи · Ритм";Width=320;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=false;WindowStartupLocation=WindowStartupLocation.Manual;
   var panel=new StackPanel();var head=new DockPanel{Margin=new Thickness(6,0,6,6)};DockPanel.SetDock(counter,Dock.Right);head.Children.Add(counter);head.Children.Add(UI.Text("Главные задачи",12,null,true));panel.Children.Add(head);panel.Children.Add(list);
   var card=UI.Card(panel,null,new Thickness(8,10,8,8));card.Margin=new Thickness(14);card.ToolTip="Перетащите виджет, чтобы переместить его";Content=card;
   // Без этих стилей виджет попадал бы в Alt+Tab и забирал фокус у окна, в котором вы работаете.
   SourceInitialized+=delegate{var h=new WindowInteropHelper(this).Handle;SetWindowLong(h,ExStyle,GetWindowLong(h,ExStyle)|ToolWindow|NoActivate);};
   SizeChanged+=delegate{Place();};
   MouseLeftButtonDown+=delegate{DragMove();store.Settings.WidgetLeft=Left;store.Settings.WidgetBottom=Top+ActualHeight;store.SaveLocalPreferences();Place();};
  }
  // Главные — мои открытые задачи: просроченные и на сегодня, срочные и высокие, а также те, что уже в работе.
  public static List<TaskItem> Pick(IEnumerable<TaskItem> tasks,Func<TaskItem,bool> mine,string today){
   Func<TaskItem,bool> due=t=>t.date!=null&&string.CompareOrdinal(t.date,today)<=0;
   return tasks.Where(t=>!t.completed&&!t.isMeeting&&mine(t)&&(due(t)||t.priority<=2||t.status=="doing")).OrderBy(t=>due(t)?0:1).ThenBy(t=>t.priority).ThenBy(t=>t.date??"9999").ThenBy(t=>t.createdAt).ToList();
  }
  public void Render(){
   string today=UI.Date(DateTime.Today);var picked=Pick(store.Tasks,store.IsMine,today);var rows=picked.Take(Limit).ToList();counter.Text=picked.Count>Limit?Limit+" из "+picked.Count:picked.Count.ToString();
   string key=today+Json.Write(rows.Select(t=>new[]{t.id,t.title,t.description,t.date,t.priority.ToString(),projectName(t.category)}).ToList());if(key==shown)return;shown=key;
   list.Children.Clear();foreach(var task in rows)list.Children.Add(Row(task,today));
   if(rows.Count==0){var empty=UI.Text("На сегодня ничего срочного.",11,UI.Muted);empty.Margin=new Thickness(6,2,6,6);list.Children.Add(empty);}
  }
  UIElement Row(TaskItem task,string today){
   string id=task.id,note=string.Join(" ",(task.description??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries)),project=task.category=="Без проекта"?null:projectName(task.category);bool late=task.date!=null&&string.CompareOrdinal(task.date,today)<0;
   var stack=new StackPanel();var top=new DockPanel();top.Children.Add(new Ellipse{Width=7,Height=7,Fill=UI.B(UI.PriorityColor(task.priority)),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center,ToolTip=UI.Priority(task.priority)});var name=UI.Text(task.title,12,null,true);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;top.Children.Add(name);stack.Children.Add(top);
   if(note!=""){var description=UI.Text(note,11,UI.Muted);description.LineHeight=15;description.MaxHeight=30;description.TextTrimming=TextTrimming.CharacterEllipsis;description.Margin=new Thickness(15,2,0,0);stack.Children.Add(description);}
   string details=string.Join(" · ",new[]{task.date==null?null:UI.FriendlyDate(task.date),project}.Where(x=>x!=null));if(details!=""){var meta=UI.Text(details,10,late?UI.B("#E5484D"):UI.Muted);meta.TextWrapping=TextWrapping.NoWrap;meta.TextTrimming=TextTrimming.CharacterEllipsis;meta.Margin=new Thickness(15,2,0,0);stack.Children.Add(meta);}
   var b=UI.Button("",()=>open(id));b.Content=stack;b.Tag="flat";b.Background=Brushes.Transparent;b.BorderThickness=new Thickness(0);b.Padding=new Thickness(6);b.Margin=new Thickness(0);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.ToolTip=note==""?task.title:task.title+"\n"+note;System.Windows.Automation.AutomationProperties.SetName(b,task.title);return b;
  }
  // По умолчанию — правый нижний угол над панелью задач. Держимся за нижний край: так список растёт вверх и не уходит за экран.
  void Place(){
   var area=SystemParameters.WorkArea;double left=store.Settings.WidgetLeft??area.Right-ActualWidth,bottom=store.Settings.WidgetBottom??area.Bottom;
   Left=Math.Max(SystemParameters.VirtualScreenLeft,Math.Min(left,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-ActualWidth));Top=Math.Max(SystemParameters.VirtualScreenTop,Math.Min(bottom-ActualHeight,SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-ActualHeight));
  }
 }
}
