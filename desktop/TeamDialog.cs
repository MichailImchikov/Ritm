using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ritm {
 // Подключение общего проекта из сетевой папки. Без existing — новое подключение; с existing — только смена имени.
 public sealed class TeamDialog:Window {
  readonly Store store;readonly TeamLink existing;readonly TextBox path=UI.Input("",400),projectName=UI.Input("",50);readonly ComboBox user=new ComboBox{IsEditable=true,MinHeight=34,Padding=new Thickness(6)};readonly TextBlock found=UI.Text("",11,UI.Muted);readonly StackPanel nameBlock=new StackPanel();TeamProbe probe;int probing;
  public TeamLink Result;
  public TeamDialog(Window owner,Store data,TeamLink link=null){
   if(owner!=null){Owner=owner;Icon=owner.Icon;}store=data;existing=link;Title=link==null?"Общий проект · Ритм":"Ваше имя · Ритм";Width=500;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=UI.Canvas;
   var p=new StackPanel{Margin=new Thickness(26,22,26,22)};Content=p;p.Children.Add(UI.Text(link==null?"Подключить общий проект":"Ваше имя в проекте «"+link.Name+"»",21,null,true));UI.Gap(p,8);
   p.Children.Add(UI.Text(link==null?"Укажите папку на сервере в локальной сети. Все, кто подключит ту же папку, увидят общие задачи и смогут назначать их друг другу.":"Под этим именем вам назначают задачи. Задачи, уже назначенные на прежнее имя, останутся на нём.",12,UI.Muted));
   if(link==null){
    UI.Label(p,"Папка на сервере");var row=new DockPanel();var browse=UI.Button("Обзор…",Browse);browse.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(browse,Dock.Right);row.Children.Add(browse);path.ToolTip="Например: \\\\server\\Общие\\Ритм\\Отдел";System.Windows.Automation.AutomationProperties.SetName(path,"Папка на сервере");row.Children.Add(path);p.Children.Add(row);
    found.Margin=new Thickness(0,7,0,0);found.TextWrapping=TextWrapping.Wrap;p.Children.Add(found);path.LostFocus+=delegate{Probe();};path.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){Probe();user.Focus();e.Handled=true;}};
   }
   UI.Label(p,"Ваше имя");user.Text=link!=null?link.User:store.Settings.UserName??Store.WindowsName()??"";if(link!=null)user.ItemsSource=store.Members(link.Name);System.Windows.Automation.AutomationProperties.SetName(user,"Ваше имя");p.Children.Add(user);var hint=UI.Text("По умолчанию — имя пользователя Windows. Можно выбрать себя из списка участников или вписать другое.",10,UI.Muted);hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(0,6,0,0);p.Children.Add(hint);
   if(link==null){UI.Label(nameBlock,"Название проекта");System.Windows.Automation.AutomationProperties.SetName(projectName,"Название проекта");nameBlock.Children.Add(projectName);nameBlock.Visibility=Visibility.Collapsed;p.Children.Add(nameBlock);}
   UI.Gap(p,22);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(UI.Button("Отмена",()=>Close()));buttons.Children.Add(UI.Button(link==null?"Подключить":"Сохранить",Save,true));p.Children.Add(buttons);
   PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape)Close();};Loaded+=delegate{if(link==null)path.Focus();else user.Focus();};
  }
  void Browse(){using(var d=new System.Windows.Forms.FolderBrowserDialog{Description="Папка общего проекта на сервере",SelectedPath=path.Text}){if(d.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;path.Text=d.SelectedPath;Probe();user.Focus();}}
  // Сетевая папка может отвечать долго, поэтому проверка идёт в фоне.
  async void Probe(){
   string target=path.Text.Trim();if(target.Length==0){found.Text="";return;}if(probe!=null&&string.Equals(probe.Path,target,StringComparison.OrdinalIgnoreCase))return;
   int ticket=++probing;found.Foreground=UI.Muted;found.Text="Проверяем папку…";TeamProbe result=null;string error=null;
   try{result=await Task.Run(()=>Store.ProbeTeam(target));}catch(Exception e){error=e.Message;}
   if(ticket!=probing)return;probe=result;
   if(result==null){found.Foreground=Fx.Solid("#E5484D");found.Text=error;nameBlock.Visibility=Visibility.Collapsed;return;}
   string typed=user.Text;user.ItemsSource=result.Members;user.Text=typed;
   if(result.Exists){found.Text="Найден проект «"+result.Name+"»"+(result.Members.Count>0?" · участники: "+string.Join(", ",result.Members.Take(6))+(result.Members.Count>6?"…":""):"");nameBlock.Visibility=Visibility.Collapsed;}
   else{found.Text="Папка пока пустая — будет создан новый общий проект.";projectName.Text=result.Name;nameBlock.Visibility=Visibility.Visible;}
  }
  void Save(){
   try{
    if(existing!=null){store.SetTeamUser(existing.Name,user.Text);Result=existing;DialogResult=true;return;}
    if(TaskItem.Person(user.Text)==null){user.Focus();throw new Exception("Введите своё имя — так вас увидят остальные участники.");}
    Cursor=Cursors.Wait;Result=store.ConnectTeam(path.Text,projectName.Text,user.Text);DialogResult=true;
   }catch(Exception e){Cursor=null;MessageBox.Show(this,e.Message,"Ритм",MessageBoxButton.OK,MessageBoxImage.Information);}
  }
 }
}
