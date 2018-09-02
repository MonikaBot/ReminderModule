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
using System.Linq;
using SystemsModule;

public class ModuleEntryPoint : IModuleEntryPoint
{
    public IModule GetModule()
    {
        return new MonikaBot.SystemsModule.SystemsModule();
    }
}

namespace MonikaBot.SystemsModule
{
    public class SystemsModule : IModule
    {
        void Client_MessageCreated(DSharpPlus.EventArgs.MessageCreateEventArgs e)
        {
        }


        public static string SystemsDatabasePath = "systems.json";

        private bool WaitingOnInput = false;
        private DiscordUser WaitingOnInputFrom = null;
        private DiscordChannel WaitingChannel = null;
        private string PotentialSystemName = "";

        Dictionary<ulong, SystemsCollection<string, string>> SystemsDatabase = new Dictionary<ulong, SystemsCollection<string, string>>();

        public SystemsModule()
        {
            Name = "Systems Module";
            Description = "A complex module for allowing users to list their systems.";
            ModuleKind = ModuleType.External;
        }

        public override void ShutdownModule(CommandsManager managers)
        {
            FlushSystemsModule();
        }

        private void FlushSystemsModule()
        {
            using (var sw = new StreamWriter(SystemsDatabasePath))
            {
                sw.Write(JsonConvert.SerializeObject(SystemsDatabase, Formatting.Indented));
            }
        }

        public void LoadSystemsDictionary()
        {
            if (File.Exists(SystemsDatabasePath))
            {
                using (var sr = new StreamReader(SystemsDatabasePath))
                {
                    try
                    {
                        SystemsDatabase = JsonConvert.DeserializeObject<Dictionary<ulong, SystemsCollection<string, string>>>(sr.ReadToEnd());
                    }
                    catch(Exception)
                    {
                        Console.WriteLine("Old Systems database, deleting.");
                        File.Delete(SystemsDatabasePath);
                    }
                }
            }
        }

        private DiscordUser GetUserByID(DiscordGuild guild, string id)
        {
            List<DiscordUser> membersList = (List<DiscordUser>)guild.Members;
            return membersList.Find(x => x.Id.ToString() == id);
        }

        public override void Install(CommandsManager manager)
        {
            LoadSystemsDictionary();

            // Setup the one time listener for adding systems.
            manager.Client.MessageCreated += async (
                        e) =>
            {
                if (WaitingOnInput)
                {
                    if (WaitingOnInputFrom.Id == e.Author.Id && WaitingChannel.Id == e.Channel.Id)
                    {
                        if (!e.Message.Content.Contains("addsystem")) //skip because this has already been handled.
                        {
                            Console.WriteLine($"Got input from correct person in correct channel {WaitingOnInputFrom.Id} == {e.Author.Id}");
                            Console.WriteLine("Message: " + e.Message.Content);
                            try
                            {
                                if (SystemsDatabase.ContainsKey(e.Author.Id)) //we already have a systems collection for this user.
                                {
                                    SystemsDatabase[e.Author.Id].Add("System " + (SystemsDatabase[e.Author.Id].Count + 1), e.Message.Content);
                                    Console.WriteLine("Already had one, added");
                                }
                                else
                                {
                                    SystemsCollection<string, string> collection = new SystemsCollection<string, string>();
                                    collection.UserSnowflake = e.Author.Id;
                                    string systemName = "System " + (SystemsDatabase[e.Author.Id].Count + 1);
                                    if (!String.IsNullOrEmpty(PotentialSystemName))
                                    {
                                        systemName = PotentialSystemName;
                                        PotentialSystemName = null;
                                    }
                                    collection.Add(systemName, e.Message.Content);
                                    SystemsDatabase.Add(e.Author.Id, collection);

                                    Console.WriteLine("Created one");

                                }
                                //SystemsDatabase[e.Author.Id] = collection;
                                WaitingOnInput = false;
                                WaitingOnInputFrom = null;
                                Console.WriteLine("Flushing");
                                //Might as well save after write, I don't pay for the SSD on the VPS...
                                FlushSystemsModule();

                                await e.Channel.SendMessageAsync("👌");
                            }
                            catch (Exception ex)
                            {
                                await e.Channel.SendMessageAsync("bro something is royally fucked: " + ex.Message);
                                Console.WriteLine("Exception details\n\n" + ex.Message + "\n\n" + ex.StackTrace + "\n\n");
                                WaitingOnInput = false;
                                WaitingOnInputFrom = null;
                            }
                        }
                    }
                }
            };

            manager.AddCommand(new CommandStub("addsystem", "Adds your system to the database, simply follow the prompts. ", "addsystem", cmdArgs =>
            {
                if (cmdArgs.Args.Count > 1)
                {
                    PotentialSystemName = cmdArgs.Args[1];
                }
                else
                    PotentialSystemName = null;

                cmdArgs.Channel.SendMessageAsync("Yay!");
                Console.WriteLine("fucking work");
                var m = cmdArgs.Channel.SendMessageAsync($"Please enter system name/specs");
                Console.WriteLine($"[SystemsModule] Waiting on input from {cmdArgs.Message.Author.Username}");
                WaitingOnInput = true;
                WaitingOnInputFrom = cmdArgs.Message.Author;
                WaitingChannel = cmdArgs.Message.Channel;
            }, argCount: 1), this);

            manager.AddCommand(new CommandStub("system", "get someone's specs (mention is optional)", "system <user>", cmdArgs =>
            {
                if (cmdArgs.Args.Count == 1)
                {
                    // Check if it's a mention and handle nicely
                    if (cmdArgs.Args[0].StartsWith("<@") && (cmdArgs.Args[0].EndsWith(">")))
                    {
                        ulong ID = ulong.Parse(cmdArgs.Args[0].Trim(new char[] { '<', '>', '@', '!' }));
                        List<DiscordMember> membersListAsList = new List<DiscordMember>(cmdArgs.Channel.Guild.Members);
                        DiscordMember member = membersListAsList.Find(x => x.Id == ID);

                        if (SystemsDatabase.ContainsKey(ID))
                        {
                            SystemsCollection<string, string> collection = SystemsDatabase[ID];
                            var first = collection.First();
                            string system = first.Value;
                            // TODO come back in a sec
                            DiscordEmbedBuilder b = new DiscordEmbedBuilder();
                            b.WithAuthor(member.DisplayName, icon_url: member.AvatarUrl);
                            b.WithColor(DiscordColor.Purple);
                            b.AddField("System", system);

                            cmdArgs.Channel.SendMessageAsync(embed: b.Build());
                        }
                        else
                            cmdArgs.Channel.SendMessageAsync("Sorry! You're not in the database :c");
                    }
                    else
                    {
                        IReadOnlyList<DiscordMember> membersList = cmdArgs.Channel.Guild.Members;
                        if (membersList != null)
                        {
                            List<DiscordMember> membersListAsList = new List<DiscordMember>(membersList);
                            DiscordMember member = membersListAsList.Find(x => x.Username.Trim('@') == cmdArgs.Args[0].Trim());

                            if (member != null)
                            {
                                if (SystemsDatabase.ContainsKey(member.Id))
                                {
                                    SystemsCollection<string, string> collection = SystemsDatabase[member.Id];
                                    var first = collection.First();
                                    string system = first.Value;
                                    //cmdArgs.Channel.SendMessageAsync($"**System for {member.DisplayName}**:\n\n{system}");

                                    DiscordEmbedBuilder b = new DiscordEmbedBuilder();
                                    b.WithAuthor(member.DisplayName, icon_url: member.AvatarUrl);
                                    b.WithColor(DiscordColor.Purple);
                                    b.AddField(first.Key, system);

                                    cmdArgs.Channel.SendMessageAsync(embed: b.Build());
                                }
                                else
                                    cmdArgs.Channel.SendMessageAsync("Sorry! Not in the database :c");
                            }
                        }
                    }
                }
                else //do it for self
                {
                    ulong ID = cmdArgs.Author.Id;
                    List<DiscordMember> membersListAsList = new List<DiscordMember>(cmdArgs.Channel.Guild.Members);
                    DiscordMember member = membersListAsList.Find(x => x.Id == ID);

                    if (SystemsDatabase.ContainsKey(ID))
                    {
                        SystemsCollection<string, string> collection = SystemsDatabase[ID];
                        var first = collection.First();
                        string system = first.Value;
                        // TODO come back in a sec
                        DiscordEmbedBuilder b = new DiscordEmbedBuilder();
                        b.WithAuthor(member.DisplayName, icon_url: member.AvatarUrl);
                        b.WithColor(DiscordColor.Purple);
                        b.AddField(first.Key, system);

                        cmdArgs.Channel.SendMessageAsync(embed: b.Build());
                    }
                    else
                        cmdArgs.Channel.SendMessageAsync("Sorry! You're not in the database :c");
                }
            }, PermissionType.User, 1), this);     

            manager.AddCommand(new CommandStub("removesystem", "remove your system", "removesystem", cmdArgs =>
            {
                ulong userID = cmdArgs.Message.Author.Id;

                try
                {
                    SystemsDatabase.Remove(userID);
                    cmdArgs.Channel.SendMessageAsync("👌");
                }
                catch(Exception ex)
                {
                    cmdArgs.Channel.SendMessageAsync("Unable to remove your system. Please tell an admin immediately, something is royally borked.\n\n```\n" +
                                                     ex.Message + "\n```");
                }
            }, PermissionType.User, 0), this); 
        }
    }
}
