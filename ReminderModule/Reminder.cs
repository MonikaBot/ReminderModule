using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonikaBot.Commands;
using Newtonsoft.Json;
using System.Linq;
using DSharpPlus.Entities;
using System.Globalization;

public class ModuleEntryPoint : IModuleEntryPoint
{
    public IModule GetModule()
    {
        return new MonikaBot.ReminderModule.ReminderModule();
    }
}

namespace MonikaBot.ReminderModule
{
    internal class Reminder
    {
        public DateTime ReminderTime { get; set; }
        public string ReminderText { get; set; }
        public DiscordChannel Channel { get; set; }
        public string AuthorID { get; set; }
    }
    internal class TimerStateObjClass
    {
        public System.Threading.Timer TimerReference;  
        public bool TimerCanceled;  
    }

    public class ReminderModule : IModule
    {
        public static string ReminderDatabasePath = "reminders.json";

        // id, date to be reminded
        private List<Reminder> reminderDatabase;
        private TimerStateObjClass objClass;

        public ReminderModule()
        {
            Name = "Reminder Module";
            Description = "Reminds you of things.";
            ModuleKind = ModuleType.External;

            reminderDatabase = new List<Reminder>();
        }

        public override void ShutdownModule(CommandsManager manager)
        {
            FlushReminderDictionary();
        }

        private void BeginTimer()
        {
            objClass = new TimerStateObjClass();
            objClass.TimerCanceled = false;
            TimerCallback timerDelegate = new TimerCallback(TimerElapsed);

            System.Threading.Timer checkReminderTimer = new Timer(timerDelegate, objClass, 0, 2000);

            objClass.TimerReference = checkReminderTimer;
        }

        public override void Install(CommandsManager manager)
        {
            LoadReminderDictionary();
            BeginTimer();

            manager.AddCommand(new CommandStub("remindme", "Reminds you at a certain time.", "remindme <time without spaces> <what to be reminded of>", cmdArgs=>
            {
                //Args[0] would theoretically be the string you'd extract the time frame from
                if(cmdArgs.Args.Count > 0)
                {
                    TimeSpan reminderTime;
                    string reminderText = cmdArgs.Args.Count > 1 ? cmdArgs.Args[1] : "Reminder!";
                    reminderTime = DateTime.ParseExact(cmdArgs.Args[0], "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;
                    if(reminderTime != TimeSpan.Zero)
                    {
                        Reminder r = new Reminder()
                        {
                            AuthorID = cmdArgs.Author.Id.ToString(),
                            Channel = cmdArgs.Channel,
                            ReminderText = reminderText,
                            ReminderTime = (DateTime.Now + reminderTime)
                        };

                        DiscordEmbedBuilder b = new DiscordEmbedBuilder();
                        b.WithAuthor("Monika Bot", icon_url: "https://cdn.discordapp.com/app-icons/400465606454935562/07d979fd0d7f973cef55ecde5630105c.png");
                        b.WithColor(DiscordColor.Purple);
                        b.AddField("Reminder", reminderText);
                        b.AddField("Time", r.ReminderTime.ToString());

                        reminderDatabase.Add(r);

                        cmdArgs.Channel.SendMessageAsync($"Okay {cmdArgs.Author.Mention}! I've created your reminder~\n", embed: b.Build());
                    }
                    else
                    {
                        cmdArgs.Channel.SendMessageAsync("What kind of time is that? :/");
                    }
                }

            }, argCount: 2), this);
        }

        /// <summary>
        /// This is where we check to see if a reminder needs to go off.
        /// </summary>
        /// <param name="stateInfo">State info.</param>
        private void TimerElapsed(object stateInfo)
        {
            DateTime timeOfExecution = DateTime.Now;
            Reminder v;
            try
            {
                lock (reminderDatabase)
                {
                    v = reminderDatabase.First(x => TimesCloseToEachother(timeOfExecution, x.ReminderTime));
                }
                if (v != null)
                {
                    v.Channel.SendMessageAsync($"Oh <@{v.AuthorID}>~! You asked to be reminded of `{v.ReminderText}`!");
                    lock (reminderDatabase)
                    {
                        reminderDatabase.Remove(v);
                    }
                }
            }
            catch(System.InvalidOperationException)
            {}
            catch(Exception ex)
            {
                Console.WriteLine("Timer Elapsed Error: " + ex.Message);
            }

            if ((stateInfo as TimerStateObjClass).TimerCanceled)
            {
                (stateInfo as TimerStateObjClass).TimerReference.Dispose();
            }
        }

        private bool TimesCloseToEachother(DateTime timeOfExecution, DateTime reminderTime, int val = 5)
        {
            if(Math.Abs(timeOfExecution.Minute - reminderTime.Minute) <= 0)
            {
                if(Math.Abs(timeOfExecution.Hour - reminderTime.Hour) <= 0)
                {
                    return true;
                }
            }
            if (timeOfExecution > reminderTime)
                return true;
                

            /*
            if(Math.Abs(first.Second - second.Second) <= val)
            {
                return true;
            }*/
            return false;
        }

        public void FlushReminderDictionary()
        {
            using(var sw = new StreamWriter(ReminderDatabasePath))
            {
                sw.Write(JsonConvert.SerializeObject(reminderDatabase, Formatting.Indented));
            }
        }

        public void LoadReminderDictionary()
        {
            if (File.Exists(ReminderDatabasePath))
            {
                using (var sr = new StreamReader(ReminderDatabasePath))
                {
                    reminderDatabase = JsonConvert.DeserializeObject<List<Reminder>>(sr.ReadToEnd());
                }
            }
        }
    }
}
