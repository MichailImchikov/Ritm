using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

class Setup {
 static string Root { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RitmPlanner"); } }
 static bool Running() { try { using (Mutex.OpenExisting("Local\\RitmPlannerDesktop-v1")) return true; } catch (WaitHandleCannotBeOpenedException) { return false; } }
 static void Extract(string resource, string target) { using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) using (var output = File.Create(target)) input.CopyTo(output); }
 static void Shortcut(string folder, string exe) {
  var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
  dynamic link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Path.Combine(folder, "Ритм.lnk") });
  link.TargetPath = exe; link.WorkingDirectory = Path.GetDirectoryName(exe); link.IconLocation = exe + ",0"; link.Save();
 }
 [STAThread] static int Main() {
  try {
   if (Running()) { MessageBox.Show("Закройте Ритм перед установкой и повторите попытку.", "Установка Ритма"); return 1; }
   if (MessageBox.Show("Установить или обновить Ритм для текущего пользователя?", "Установка Ритма", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
   string app = Path.Combine(Root, "App", "current"); Directory.CreateDirectory(app);
   string exe = Path.Combine(app, "Ritm.exe"); Extract("Ritm.exe", exe); Extract("Ritm.exe.config", exe + ".config");
   Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), exe);
   Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs), exe);
   MessageBox.Show("Ритм установлен. Ярлык находится на рабочем столе.", "Установка Ритма"); return 0;
  } catch (Exception e) { MessageBox.Show("Не удалось установить Ритм: " + e.Message, "Установка Ритма", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
 }
}
