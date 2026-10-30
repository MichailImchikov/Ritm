using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ritm {
 public static class MeetingSchedule {
  public static bool OccursOn(TaskItem meeting,DateTime day){
   DateTime start;
   if(meeting==null||!meeting.isMeeting||!DateTime.TryParseExact(meeting.date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out start)||day.Date<start.Date)return false;
   return meeting.meetingDays==null||meeting.meetingDays.Count==0?day.Date==start.Date:meeting.meetingDays.Contains((int)day.DayOfWeek);
  }
  public static bool UpcomingToday(TaskItem meeting,DateTime now){
   DateTime time;return OccursOn(meeting,now.Date)&&DateTime.TryParseExact(meeting.meetingTime,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out time)&&now<now.Date.Add(time.TimeOfDay);
  }
  public static bool Due(TaskItem meeting,DateTime occurrenceDay,DateTime now,int minutesBefore){
   DateTime time;
   if(!OccursOn(meeting,occurrenceDay)||!DateTime.TryParseExact(meeting.meetingTime,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out time))return false;
   DateTime due=occurrenceDay.Date.Add(time.TimeOfDay).AddMinutes(-minutesBefore);
   return now>=due&&now<due.AddMinutes(1);
  }
 }
 public sealed partial class MainWindow {
  string todayMeetingState;
  List<TaskItem> TodayMeetings(DateTime now){return store.Tasks.Where(t=>t.isMeeting&&store.IsTeamName(t.category)&&(project==""||t.category==project)&&MeetingSchedule.OccursOn(t,now.Date)).OrderBy(t=>t.meetingTime).ToList();}
  void RefreshTodayAtMeetingBoundary(){DateTime now=DateTime.Now;string state=UI.Date(now)+"|"+string.Join(",",store.Tasks.Where(t=>t.isMeeting&&store.IsTeamName(t.category)&&MeetingSchedule.UpcomingToday(t,now)).OrderBy(t=>t.id).Select(t=>t.id));if(todayMeetingState!=state){todayMeetingState=state;Refresh();}}
  void CheckMeetings(){
   if(!Notifications.IsOn(store.Settings,Notifications.Meeting))return;
   var sent=store.Settings.MeetingNotified;
   if(sent==null)sent=store.Settings.MeetingNotified=new Dictionary<string,string>();
   DateTime now=DateTime.Now;
   bool changed=false;
   foreach(string key in sent.Where(x=>string.CompareOrdinal(x.Value,UI.Date(now.AddDays(-1)))<0).Select(x=>x.Key).ToList()){sent.Remove(key);changed=true;}
   foreach(int offset in new[]{10,0}){
    var due=new List<Tuple<TaskItem,DateTime,string>>();
    foreach(var meeting in store.Tasks.Where(t=>t.isMeeting&&store.IsTeamName(t.category))){
     foreach(var day in new[]{now.Date,now.Date.AddDays(1)}){
      if(!MeetingSchedule.Due(meeting,day,now,offset))continue;
      string key=meeting.id+"|"+UI.Date(day)+"|"+meeting.meetingTime+"|"+offset;
      if(!sent.ContainsKey(key))due.Add(Tuple.Create(meeting,day,key));
     }
    }
    if(due.Count==0)continue;
    var first=due[0];string title=offset==10?"Встреча через 10 минут":"Встреча начинается";
    string message=due.Count==1?"«"+first.Item1.title+"» · "+first.Item1.meetingTime+" · "+ProjectDisplay(first.Item1.category):due.Count+" встреч: "+string.Join(", ",due.Select(x=>x.Item1.title+" ("+x.Item1.meetingTime+")"));
    if(notifier.Show(Notifications.Meeting,title,message,()=>{anchor=selected=first.Item2;Go("calendar");})){
     foreach(var item in due)sent[item.Item3]=UI.Date(item.Item2);
     changed=true;
    }
   }
   if(changed)store.SaveLocalPreferences();
  }
 }
}
