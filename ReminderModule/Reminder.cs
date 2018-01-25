using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonikaBot.Commands;
using Newtonsoft.Json;
using System.Linq;
using DSharpPlus.Entities;

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

    public class ReminderModule : IModule
    {
        public static string ReminderDatabasePath = "reminders.json";

        // id, date to be reminded
        private Dictionary<string, Reminder> reminderDatabase;

        public ReminderModule()
        {
            Name = "Reminder Module";
            Description = "Reminds you of things.";
            ModuleKind = ModuleType.External;
        }

        public override void Install(CommandsManager manager)
        {
            Timer checkReminderTimer = new Timer(TimerElapsed, new AutoResetEvent(false), 0, 2000);

            manager.AddCommand(new CommandStub("remindme", "Reminds you at a certain time.", "remindme <time without spaces> <what to be reminded of>", 
                                               PermissionType.User, 2, cmdArgs=>
            {
                //Args[0] would theoretically be the string you'd extract the time frame from
                if(cmdArgs.Args.Count > 0)
                {
                    TimeSpan reminderTime;
                    string reminderText = cmdArgs.Args.Count > 1 ? cmdArgs.Args[0] : "Reminder!";
                    if(TimeSpan.TryParse(cmdArgs.Args[0], out reminderTime))
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

                        reminderDatabase.Add(cmdArgs.Author.Id.ToString(), r);

                        cmdArgs.Channel.SendMessageAsync($"Okay {cmdArgs.Author.Mention}! I've created your reminder~\n", embed: b.Build());
                    }
                    else
                    {
                        cmdArgs.Channel.SendMessageAsync("What kind of time is that? :/");
                    }
                }

            }), this);
        }

        /// <summary>
        /// This is where we check to see if a reminder needs to go off.
        /// </summary>
        /// <param name="stateInfo">State info.</param>
        private void TimerElapsed(object stateInfo)
        {
            DateTime timeOfExecution = DateTime.Now;
            Task.Run(() =>
            {
                Console.WriteLine("timer ticks");
                Reminder v = reminderDatabase.First(x => TicksCloseToEachother(timeOfExecution, x.Value.ReminderTime)).Value;
                if(v != null)
                {
                    v.Channel.SendMessageAsync($"Oh <@{v.AuthorID}>~! You asked to be reminded of `{v.ReminderText}`!");
                }
            });
        }

        private bool TicksCloseToEachother(DateTime first, DateTime second, int val = 10)
        {
            if(Math.Abs(first.Ticks - second.Ticks) <= val)
            {
                return true;
            }
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
            using(var sr = new StreamReader(ReminderDatabasePath))
            {
                reminderDatabase = JsonConvert.DeserializeObject<Dictionary<string, Reminder>>(sr.ReadToEnd());
            }
        }
    }
}
