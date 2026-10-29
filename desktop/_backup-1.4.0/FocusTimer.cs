using System;
using System.Globalization;
namespace Ritm {
 public sealed class FocusState {public string Mode {get;set;} public bool Running {get;set;} public int RemainingSeconds {get;set;} public string DeadlineUtc {get;set;} public int Sessions {get;set;} }
 public sealed class FocusTimer {
  public readonly FocusState State;readonly Func<DateTime> clock;
  public int Duration {get{return State.Mode=="break"?300:1500;}}
  public int Remaining {get{if(!State.Running)return Math.Max(0,State.RemainingSeconds);DateTime due;if(!DateTime.TryParse(State.DeadlineUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out due))return State.RemainingSeconds;return Math.Max(0,(int)Math.Ceiling((due-clock()).TotalSeconds));}}
  public FocusTimer(FocusState state,Func<DateTime> utcNow=null){State=state??new FocusState();clock=utcNow??(()=>DateTime.UtcNow);if(State.Mode!="focus"&&State.Mode!="break")State.Mode="focus";if(State.RemainingSeconds<=0||State.RemainingSeconds>Duration){State.RemainingSeconds=Duration;State.Running=false;}DateTime due;if(State.Running&&!DateTime.TryParse(State.DeadlineUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out due))State.Running=false;}
  public void Start(){if(State.Running)return;if(Remaining<=0)State.RemainingSeconds=Duration;State.DeadlineUtc=clock().AddSeconds(State.RemainingSeconds).ToString("o",CultureInfo.InvariantCulture);State.Running=true;}
  public void Pause(){State.RemainingSeconds=Remaining;State.Running=false;State.DeadlineUtc=null;}
  public void Reset(){State.Running=false;State.DeadlineUtc=null;State.RemainingSeconds=Duration;}
  public void SetMode(string mode){if(mode!="focus"&&mode!="break")throw new ArgumentException("mode");State.Mode=mode;Reset();}
  public bool Tick(){if(!State.Running||Remaining>0)return false;if(State.Mode=="focus")State.Sessions++;State.Mode=State.Mode=="focus"?"break":"focus";Reset();return true;}
 }
}
