using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ritm {
 public sealed class TaskEditor:Window {
  readonly TaskItem task;readonly Func<TaskItem,bool> save;readonly Action<TaskItem> remove;
  TextBox title,description,newStep;ComboBox category,priority,statusChoice,assignee;readonly Func<string,List<string>> people;StackPanel assigneeBlock;string peopleFor;const string Nobody="Без исполнителя";DateField date;CheckBox completed;
  readonly StackPanel steps=new StackPanel();
  // Вложения: файлы, добавленные в этой форме, удаляются, если форму закрыли без сохранения.
  readonly Store files;readonly WrapPanel tiles=new WrapPanel();readonly List<Attachment> fresh=new List<Attachment>();bool saved;
  public TaskEditor(Window owner,TaskItem value,IEnumerable<string> categories,Func<TaskItem,bool> onSave,Action<TaskItem> onDelete,Func<string,List<string>> members=null,Store attachments=null){
   people=members;files=attachments;
   if(owner!=null)Owner=owner;Title=value.version==0?"Ритм — новая задача":"Ритм — задача";Width=570;Height=750;MinHeight=550;MinWidth=480;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=UI.B("#FAFBF7");task=value.Clone();save=onSave;remove=onDelete;if(owner!=null)Icon=owner.Icon;
   var root=new DockPanel{LastChildFill=true,Background=UI.Canvas};Content=root;
   var header=new StackPanel{Margin=new Thickness(27,20,27,16)};header.Children.Add(UI.Text(value.version==0?"Новая задача":"Задача",25,null,true));DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
   var footer=new DockPanel{Margin=new Thickness(26,15,26,18)};var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(UI.Button("Отмена",()=>Close()));var saveButton=UI.Button("Сохранить",Save,true);buttons.Children.Add(saveButton);DockPanel.SetDock(buttons,Dock.Right);footer.Children.Add(buttons);if(task.version>0){var deleteButton=UI.Button("Удалить",Delete);deleteButton.HorizontalAlignment=HorizontalAlignment.Left;deleteButton.Background=Brushes.Transparent;deleteButton.Tag="flat";deleteButton.BorderThickness=new Thickness(0);deleteButton.Foreground=UI.Muted;footer.Children.Add(deleteButton);}else footer.Children.Add(UI.Text("Сохраним на компьютере",10,UI.Muted));DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
   var panel=new StackPanel{Margin=new Thickness(27,0,27,12)};var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=panel};root.Children.Add(scroll);
   UI.Label(panel,"Название");title=UI.Input(task.title);System.Windows.Automation.AutomationProperties.SetName(title,"Название задачи");panel.Children.Add(title);
   UI.Label(panel,"Описание");description=UI.Input(task.description,10000,95);description.AcceptsReturn=true;description.TextWrapping=TextWrapping.Wrap;description.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;description.VerticalContentAlignment=VerticalAlignment.Top;System.Windows.Automation.AutomationProperties.SetName(description,"Описание задачи");panel.Children.Add(description);
   var dates=new Grid();dates.ColumnDefinitions.Add(new ColumnDefinition());dates.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});dates.ColumnDefinitions.Add(new ColumnDefinition());var left=new StackPanel();var right=new StackPanel();Grid.SetColumn(right,2);dates.Children.Add(left);dates.Children.Add(right);panel.Children.Add(dates);
   UI.Label(left,"Дата");date=new DateField(task.date);System.Windows.Automation.AutomationProperties.SetName(date,"Дата задачи");left.Children.Add(date);var clear=UI.Button("Оставить без даты",()=>date.SelectedDate=null);clear.FontSize=10;clear.Padding=new Thickness(2,4,2,4);clear.BorderThickness=new Thickness(0);clear.Background=Brushes.Transparent;clear.Tag="flat";clear.Foreground=UI.Accent;clear.HorizontalAlignment=HorizontalAlignment.Left;left.Children.Add(clear);
   UI.Label(right,"Приоритет");priority=new ComboBox{ItemsSource=new[]{"Срочный","Высокий","Обычный","Низкий"},SelectedIndex=task.priority-1,MinHeight=34,Padding=new Thickness(6)};System.Windows.Automation.AutomationProperties.SetName(priority,"Приоритет");right.Children.Add(priority);
   UI.Label(panel,"Проект");category=new ComboBox{IsEditable=true,ItemsSource=categories.Distinct().ToList(),Text=task.category,MinHeight=34,Padding=new Thickness(6)};System.Windows.Automation.AutomationProperties.SetName(category,"Проект");panel.Children.Add(category);var hint=UI.Text("Можно выбрать проект или вписать новый",10,UI.Muted);hint.Margin=new Thickness(0,6,0,0);panel.Children.Add(hint);
   UI.Label(panel,"Статус");statusChoice=new ComboBox{ItemsSource=new[]{"К выполнению","В работе","Готово"},SelectedIndex=task.completed?2:task.status=="doing"?1:0,MinHeight=34};System.Windows.Automation.AutomationProperties.SetName(statusChoice,"Статус задачи");panel.Children.Add(statusChoice);
   // Исполнитель есть только у задач общего проекта: поле появляется, когда выбран такой проект.
   assigneeBlock=new StackPanel();UI.Label(assigneeBlock,"Исполнитель");assignee=new ComboBox{IsEditable=true,MinHeight=34,Padding=new Thickness(6)};System.Windows.Automation.AutomationProperties.SetName(assignee,"Исполнитель");assigneeBlock.Children.Add(assignee);if(task.createdBy!=null){var author=UI.Text("Создал(а): "+task.createdBy,10,UI.Muted);author.Margin=new Thickness(0,6,0,0);assigneeBlock.Children.Add(author);}panel.Children.Add(assigneeBlock);UpdatePeople(task.assignee??"");category.SelectionChanged+=delegate{Dispatcher.BeginInvoke(new Action(()=>UpdatePeople(assignee.Text)));};category.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,new TextChangedEventHandler((s,e)=>UpdatePeople(assignee.Text)));UI.Label(panel,"Подзадачи");panel.Children.Add(steps);RenderSteps();var add=new DockPanel{Margin=new Thickness(0,7,0,0)};var addButton=UI.Button("+ Добавить",AddStep);DockPanel.SetDock(addButton,Dock.Right);add.Children.Add(addButton);newStep=UI.Input("");newStep.MaxLength=200;System.Windows.Automation.AutomationProperties.SetName(newStep,"Новая подзадача");newStep.KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter){AddStep();e.Handled=true;}};add.Children.Add(newStep);panel.Children.Add(add);
   if(files!=null){UI.Label(panel,"Вложения");panel.Children.Add(tiles);var attachRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,0)};attachRow.Children.Add(UI.Button("+ Фото или документ",Pick));var tip=UI.Text("или перетащите файлы в окно · Ctrl+V — вставить снимок экрана",10,UI.Muted);tip.Margin=new Thickness(6,0,0,0);attachRow.Children.Add(tip);panel.Children.Add(attachRow);RenderFiles();
    AllowDrop=true;DragEventHandler over=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effects=DragDropEffects.Copy;e.Handled=true;}};PreviewDragEnter+=over;PreviewDragOver+=over;PreviewDrop+=(s,e)=>{var dropped=e.Data.GetData(DataFormats.FileDrop) as string[];if(dropped!=null){e.Handled=true;AddFiles(dropped);}};
    Closed+=delegate{if(!saved)foreach(var a in fresh)files.Discard(a);};}
   completed=new CheckBox{Content="Задача выполнена",IsChecked=task.completed,Margin=new Thickness(0,22,0,8),Foreground=UI.Muted};completed.Visibility=Visibility.Collapsed;panel.Children.Add(completed);statusChoice.SelectionChanged+=delegate{completed.IsChecked=statusChoice.SelectedIndex==2;};completed.Checked+=delegate{statusChoice.SelectedIndex=2;};completed.Unchecked+=delegate{if(statusChoice.SelectedIndex==2)statusChoice.SelectedIndex=0;};
   Loaded+=delegate{title.Focus();title.CaretIndex=title.Text.Length;};PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();if(e.Key==System.Windows.Input.Key.S&&(System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0){Save();e.Handled=true;}if(files!=null&&e.Key==System.Windows.Input.Key.V&&System.Windows.Input.Keyboard.Modifiers==System.Windows.Input.ModifierKeys.Control&&Paste())e.Handled=true;};
  }
  void UpdatePeople(string typed){string name=(category.Text??"").Trim();if(name==peopleFor)return;peopleFor=name;var list=people==null?null:people(name);assigneeBlock.Visibility=list==null?Visibility.Collapsed:Visibility.Visible;if(list==null)return;assignee.ItemsSource=new[]{Nobody}.Concat(list).ToList();assignee.Text=typed;}
  void RenderSteps(){steps.Children.Clear();foreach(var step in task.subtasks){var current=step;var row=new DockPanel{Margin=new Thickness(0,4,0,4)};var check=new CheckBox{IsChecked=current.done,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};System.Windows.Automation.AutomationProperties.SetName(check,"Подзадача выполнена");check.Checked+=delegate{current.done=true;};check.Unchecked+=delegate{current.done=false;};DockPanel.SetDock(check,Dock.Left);row.Children.Add(check);var delete=UI.Button("×",()=>{task.subtasks.Remove(current);RenderSteps();});delete.Padding=new Thickness(8,4,8,4);DockPanel.SetDock(delete,Dock.Right);row.Children.Add(delete);var text=UI.Input(current.title);System.Windows.Automation.AutomationProperties.SetName(text,"Название подзадачи");text.TextChanged+=delegate{current.title=text.Text;};row.Children.Add(text);steps.Children.Add(row);}}
  void AddStep(){if(string.IsNullOrWhiteSpace(newStep.Text))return;if(task.subtasks.Count>=50){MessageBox.Show(this,"Максимум — 50 подзадач.","Ритм");return;}task.subtasks.Add(new Step{id=Guid.NewGuid().ToString(),title=newStep.Text.Trim()});newStep.Clear();RenderSteps();newStep.Focus();}
  void Pick(){var dialog=new Microsoft.Win32.OpenFileDialog{Multiselect=true,Title="Прикрепить к задаче",Filter="Фото и документы|*.*|Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.heic|Документы|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.odt;*.ods;*.rtf;*.txt;*.csv;*.zip"};if(dialog.ShowDialog(this)==true)AddFiles(dialog.FileNames);}
  void AddFiles(IEnumerable<string> paths){var problems=new List<string>();foreach(var p in paths){try{if(Directory.Exists(p))throw new Exception("«"+Path.GetFileName(p)+"» — это папка. Прикрепите файлы из неё.");fresh.Add(files.Attach(task,p));}catch(Exception e){problems.Add(e.Message);}}RenderFiles();if(problems.Count>0)MessageBox.Show(this,string.Join("\n",problems.Distinct()),"Не всё удалось прикрепить",MessageBoxButton.OK,MessageBoxImage.Information);}
  // Ctrl+V: скопированные в Проводнике файлы или снимок экрана. Текст в буфере по-прежнему вставляется в поле.
  bool Paste(){
   try{if(Clipboard.ContainsText())return false;if(Clipboard.ContainsFileDropList()){AddFiles(Clipboard.GetFileDropList().Cast<string>().ToList());return true;}if(!Clipboard.ContainsImage())return false;var shot=Clipboard.GetImage();if(shot==null)return false;
    // Изображение из буфера часто приходит с пустым альфа-каналом — без него снимок не станет прозрачным.
    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(shot,PixelFormats.Bgr32,null,0)));byte[] data;using(var m=new MemoryStream()){png.Save(m);data=m.ToArray();}
    fresh.Add(files.Attach(task,"Снимок "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")+".png",data));RenderFiles();return true;}
   catch(Exception e){MessageBox.Show(this,e.Message,"Не удалось вставить",MessageBoxButton.OK,MessageBoxImage.Information);return true;}
  }
  void RenderFiles(){tiles.Children.Clear();foreach(var a in task.attachments)tiles.Children.Add(Tile(a));}
  FrameworkElement Tile(Attachment a){
   string path=files.AttachmentPath(task,a);var face=new Grid{Width=112,Height=84,Clip=new RectangleGeometry(new Rect(0,0,112,84),9,9),Background=UI.Rail};
   var glyph=UI.Icon(a.IsImage?"":"",26,UI.Muted);glyph.HorizontalAlignment=HorizontalAlignment.Center;glyph.Margin=new Thickness(0,0,0,a.IsImage?0:16);face.Children.Add(glyph);
   if(!a.IsImage){var ext=UI.Text(a.Extension.TrimStart('.').ToUpperInvariant(),10,UI.Accent,true);ext.HorizontalAlignment=HorizontalAlignment.Center;ext.Margin=new Thickness(0,34,0,0);face.Children.Add(ext);}
   else if(path!=null){var img=new Image{Stretch=Stretch.UniformToFill};face.Children.Add(img);Photo.LoadAsync(path,240,bmp=>{if(bmp!=null){img.Source=bmp;glyph.Visibility=Visibility.Collapsed;}});}
   var remove=UI.Button("×",()=>Detach(a));remove.Padding=new Thickness(7,1,7,2);remove.Margin=new Thickness(0,5,5,0);remove.FontSize=12;remove.HorizontalAlignment=HorizontalAlignment.Right;remove.VerticalAlignment=VerticalAlignment.Top;remove.ToolTip="Убрать из задачи";remove.Opacity=0;System.Windows.Automation.AutomationProperties.SetName(remove,"Убрать вложение "+a.name);face.Children.Add(remove);
   var box=new StackPanel{Width=112,Margin=new Thickness(0,0,10,10),Cursor=System.Windows.Input.Cursors.Hand,Background=Brushes.Transparent};box.Children.Add(new Border{Child=face,BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10)});
   var name=UI.Text(a.name,10,UI.Ink);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(2,5,2,0);box.Children.Add(name);
   var meta=UI.Text(path==null?"Нет доступа к файлу":Photo.Size(a.size),9,UI.Muted);meta.Margin=new Thickness(2,1,2,0);box.Children.Add(meta);
   box.ToolTip=a.name+"\n"+Photo.Size(a.size)+(a.addedBy!=null?" · добавил(а) "+a.addedBy:"")+(path==null?"\nФайл недоступен: нет связи с папкой проекта или он удалён.":"");
   box.MouseEnter+=delegate{remove.Opacity=1;};box.MouseLeave+=delegate{remove.Opacity=0;};
   box.MouseLeftButtonUp+=(s,e)=>{if(e.OriginalSource is DependencyObject&&IsInside((DependencyObject)e.OriginalSource,remove))return;OpenFile(a);e.Handled=true;};
   var menu=new ContextMenu();Action<string,string,Action> item=(glyph2,text,act)=>{var i=new MenuItem{Header=text,Icon=UI.Icon(glyph2,12,UI.Muted)};i.Click+=delegate{act();};menu.Items.Add(i);};
   item(a.IsImage?"":"","Открыть",()=>OpenFile(a));if(a.IsImage)item("","Открыть в программе Windows",()=>Photo.Open(this,files.AttachmentPath(task,a)));item("","Показать в папке",()=>Photo.Reveal(this,files.AttachmentPath(task,a)));item("","Сохранить как…",()=>Photo.SaveCopy(this,a,files.AttachmentPath(task,a)));menu.Items.Add(new Separator());item("","Убрать из задачи",()=>Detach(a));box.ContextMenu=menu;
   return box;
  }
  static bool IsInside(DependencyObject d,DependencyObject target){while(d!=null){if(d==target)return true;d=d is Visual?VisualTreeHelper.GetParent(d):LogicalTreeHelper.GetParent(d);}return false;}
  void OpenFile(Attachment a){if(!a.IsImage){Photo.Open(this,files.AttachmentPath(task,a));return;}var photos=task.attachments.Where(x=>x.IsImage).ToList();new PhotoViewer(this,photos,photos.IndexOf(a),x=>files.AttachmentPath(task,x)).ShowDialog();}
  // Убранный файл остаётся в папке: на него может ссылаться прежняя версия задачи или её копия.
  void Detach(Attachment a){task.attachments.Remove(a);if(fresh.Remove(a))files.Discard(a);RenderFiles();}
  void Save(){
   try{if(!string.IsNullOrWhiteSpace(newStep.Text)){if(task.subtasks.Count>=50)throw new Exception("Максимум — 50 подзадач.");AddStep();}task.title=title.Text;task.description=description.Text;task.category=category.Text;if(people!=null&&people((task.category??"").Trim())!=null){var a=(assignee.Text??"").Trim();task.assignee=a==Nobody?null:a;}task.priority=priority.SelectedIndex+1;task.date=date.SelectedDate.HasValue?UI.Date(date.SelectedDate.Value):null;task.completed=completed.IsChecked==true;task.status=new[]{"todo","doing","done"}[statusChoice.SelectedIndex];task.Validate();if(save(task)){saved=true;DialogResult=true;}}
   catch(Exception e){MessageBox.Show(this,e.Message,"Не удалось сохранить",MessageBoxButton.OK,MessageBoxImage.Information);}
  }
  void Delete(){if(MessageBox.Show(this,"Удалить задачу? Предыдущая версия сохранится в локальной истории.","Ритм",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;try{remove(task);DialogResult=true;}catch(Exception e){MessageBox.Show(this,e.Message,"Не удалось удалить");}}
 }
}




