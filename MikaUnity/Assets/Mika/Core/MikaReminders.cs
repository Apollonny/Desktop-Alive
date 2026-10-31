using System;
using System.Collections.Generic;
using System.Linq;

namespace Mika.Core
{
    [Serializable] public sealed class MikaReminder
    {
        public string id;
        public string title="Yeni hatırlatıcı";
        public int intervalMinutes=60;
        public bool enabled=true;
        public long nextDueUtcTicks;
        public long snoozeUntilUtcTicks;
        public bool pending;
    }

    /// <summary>Small persistent interval reminder queue. Times are stored as UTC ticks.</summary>
    public sealed class MikaReminderService
    {
        const int MinIntervalMinutes=5,MaxIntervalMinutes=24*60;
        readonly MikaSettings settings;

        public MikaReminderService(MikaSettings settings,DateTime utcNow)
        {
            this.settings=settings??throw new ArgumentNullException(nameof(settings));
            if(this.settings.reminders==null)this.settings.reminders=new List<MikaReminder>();
            foreach(var reminder in this.settings.reminders)
            {
                if(reminder==null)continue;
                if(string.IsNullOrWhiteSpace(reminder.id))reminder.id=Guid.NewGuid().ToString("N");
                reminder.title=CleanTitle(reminder.title);
                reminder.intervalMinutes=ClampInterval(reminder.intervalMinutes);
                if(reminder.nextDueUtcTicks<=0||reminder.nextDueUtcTicks>DateTime.MaxValue.Ticks)
                    reminder.nextDueUtcTicks=utcNow.AddMinutes(reminder.intervalMinutes).Ticks;
                if(reminder.snoozeUntilUtcTicks<0||reminder.snoozeUntilUtcTicks>DateTime.MaxValue.Ticks)reminder.snoozeUntilUtcTicks=0;
            }
        }

        public MikaReminder Add(string title,int intervalMinutes,DateTime utcNow)
        {
            var reminder=new MikaReminder
            {
                id=Guid.NewGuid().ToString("N"),title=CleanTitle(title),
                intervalMinutes=ClampInterval(intervalMinutes),enabled=true,
                nextDueUtcTicks=utcNow.AddMinutes(ClampInterval(intervalMinutes)).Ticks
            };
            settings.reminders.Add(reminder);
            return reminder;
        }

        public MikaReminder Poll(DateTime utcNow)
        {
            if(!settings.remindersEnabled)return null;
            foreach(var reminder in settings.reminders.Where(r=>r!=null&&r.enabled).OrderBy(r=>r.pending?0:1).ThenBy(r=>r.nextDueUtcTicks))
            {
                if(!reminder.pending)
                {
                    if(reminder.snoozeUntilUtcTicks>utcNow.Ticks)continue;
                    if(reminder.snoozeUntilUtcTicks>0)reminder.snoozeUntilUtcTicks=0;
                    if(reminder.nextDueUtcTicks>utcNow.Ticks)continue;
                    reminder.pending=true;
                }
                return reminder;
            }
            return null;
        }

        public bool Snooze(string id,int minutes,DateTime utcNow)
        {
            var reminder=Find(id);if(reminder==null)return false;
            reminder.pending=false;reminder.snoozeUntilUtcTicks=utcNow.AddMinutes(Math.Max(1,minutes)).Ticks;
            return true;
        }

        public bool Complete(string id,DateTime utcNow)
        {
            var reminder=Find(id);if(reminder==null)return false;
            reminder.pending=false;reminder.snoozeUntilUtcTicks=0;
            reminder.nextDueUtcTicks=utcNow.AddMinutes(reminder.intervalMinutes).Ticks;
            return true;
        }

        public bool Delete(string id)
        {
            var reminder=Find(id);if(reminder==null)return false;
            settings.reminders.Remove(reminder);return true;
        }

        static MikaReminder FindIn(IEnumerable<MikaReminder> reminders,string id)
            =>reminders.FirstOrDefault(r=>r!=null&&r.id==id);
        MikaReminder Find(string id)=>FindIn(settings.reminders,id);
        static int ClampInterval(int minutes)=>Math.Max(MinIntervalMinutes,Math.Min(MaxIntervalMinutes,minutes));
        static string CleanTitle(string title)
        {
            title=(title??"").Trim();
            if(title.Length==0)title="Hatırlatma";
            return title.Length>48?title.Substring(0,48):title;
        }
    }
}
