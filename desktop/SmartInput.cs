using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ritm {
 // Быстрый ввод: «Позвонить маме завтра !1 #Личное» → название, дата, приоритет и проект.
 public sealed class SmartTask {public string Title,Date,Project;public int Priority=3;public bool HasExtras{get{return Date!=null||Project!=null||Priority!=3;}}}
 public static class SmartInput {
  static readonly string[][] weekdays={new[]{"пн","понедельник"},new[]{"вт","вторник"},new[]{"ср","среда","среду"},new[]{"чт","четверг"},new[]{"пт","пятница","пятницу"},new[]{"сб","суббота","субботу"},new[]{"вс","воскресенье"}};
  public static SmartTask Parse(string text,DateTime today,IEnumerable<string> projects){
   var r=new SmartTask();var words=new List<string>();var known=(projects??new string[0]).ToList();
   foreach(var raw in (text??"").Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries)){
    string w=raw.ToLowerInvariant().TrimEnd(',','.');
    if(w.Length==2&&w[0]=='!'&&w[1]>='1'&&w[1]<='4'){r.Priority=w[1]-'0';continue;}
    if(raw.Length>1&&raw[0]=='#'&&r.Project==null){string tag=raw.Substring(1);r.Project=known.FirstOrDefault(p=>p.Equals(tag,StringComparison.OrdinalIgnoreCase)||p.Replace(' ','_').Equals(tag,StringComparison.OrdinalIgnoreCase))??tag.Replace('_',' ');continue;}
    if(r.Date==null){var day=Day(w,today);if(day.HasValue){r.Date=UI.Date(day.Value);if(words.Count>0&&(words[words.Count-1].ToLowerInvariant()=="в"||words[words.Count-1].ToLowerInvariant()=="во"))words.RemoveAt(words.Count-1);continue;}}
    words.Add(raw);
   }
   r.Title=string.Join(" ",words).Trim();
   if(r.Title.Length==0)return new SmartTask{Title=(text??"").Trim()};
   if(r.Project!=null&&(r.Project.Length>50||r.Project=="Без проекта"))r.Project=null;
   return r;
  }
  static DateTime? Day(string w,DateTime today){
   if(w=="сегодня")return today;if(w=="завтра")return today.AddDays(1);if(w=="послезавтра")return today.AddDays(2);
   for(int i=0;i<7;i++)if(weekdays[i].Contains(w)){int current=((int)today.DayOfWeek+6)%7;int ahead=(i-current+7)%7;return today.AddDays(ahead==0?7:ahead);}
   DateTime d;
   if(DateTime.TryParseExact(w,new[]{"d.M.yyyy","dd.MM.yyyy"},CultureInfo.InvariantCulture,DateTimeStyles.None,out d))return d;
   if(DateTime.TryParseExact(w+"."+today.Year,new[]{"d.M.yyyy","dd.MM.yyyy"},CultureInfo.InvariantCulture,DateTimeStyles.None,out d))return d<today?d.AddYears(1):d;
   return null;
  }
  public static string Describe(SmartTask t){var parts=new List<string>();if(t.Date!=null)parts.Add(UI.FriendlyDate(t.Date));if(t.Priority!=3)parts.Add(UI.Priority(t.Priority));if(t.Project!=null)parts.Add("#"+t.Project);return parts.Count==0?"":"→ "+string.Join(" · ",parts);}
 }
}
