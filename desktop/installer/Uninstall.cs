using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

class Uninstall {
 static bool Running() { try { using (Mutex.OpenExisting("Local\\RitmPlannerDesktop-v1")) return true; } catch (WaitHandleCannotBeOpenedException) { return false; } }
 static void RemoveShortcut(string folder, string root) {
  string path = Path.Combine(folder, "Ритм.lnk"); if (!File.Exists(path)) return;
  var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
  dynamic link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
  string target = link.TargetPath;
  if (Path.GetFullPath(target).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) File.Delete(path);
 }
 [STAThread] static int Main() {
  try {
   if (Running()) { MessageBox.Show("Закройте Ритм перед удалением и повторите попытку.", "Удаление Ритма"); return 1; }
   if (MessageBox.Show("Удалить Ритм, ярлыки и все локальные задачи, вложения и настройки? Это действие нельзя отменить. Данные общих проектов на сервере останутся.", "Удаление Ритма", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return 0;
   string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RitmPlanner");
   RemoveShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), root);
   RemoveShortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs), root);
   // The project copy of this executable runs outside the directory being removed.
   if (Assembly.GetExecutingAssembly().Location.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Запустите файл «Удалить Ритм.exe» из папки проекта.", "Удаление Ритма"); return 1; }
   if (Directory.Exists(root)) Directory.Delete(root, true);
   MessageBox.Show("Ритм и его локальные данные удалены.", "Удаление Ритма"); return 0;
  } catch (Exception e) { MessageBox.Show("Не удалось полностью удалить Ритм: " + e.Message, "Удаление Ритма", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
 }
}
