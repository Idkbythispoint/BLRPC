using System;
using System.Collections;
using System.Diagnostics;
using BoneLib;
using System.IO;
using BLRPC.Internal;
using BLRPC.Melon;
using MelonLoader;
using UnityEngine;
using BLRPC.Patching;
using Random = System.Random;

namespace BLRPC
{
    public class Main : MelonMod
    {
        internal const string Name = "BLRPC";
        internal const string Description = "Discord Rich Presence for BONELAB";
        internal const string Author = "SoulWithMae";
        internal const string Company = "Weather Electric";
        internal const string Version = "1.4.0";
        internal const string DownloadLink = "https://bonelab.thunderstore.io/package/SoulWithMae/BonelabRichPresence/";

        // Stuff for userdata folder
        private static readonly string UserDataDirectory = Path.Combine(MelonUtils.UserDataDirectory, "Weather Electric/BLRPC");
        private static readonly string LegacyDirectory = Path.Combine(MelonUtils.UserDataDirectory, "BLRPC");
        private static readonly string DLLPath = Path.Combine(UserDataDirectory, "discord_game_sdk.dll");
        private static readonly string UserEntriesPath = Path.Combine(UserDataDirectory, "UserEntries.txt");
        // Stuff for loading the discord game SDK assembly
        private static bool _hasLoadedLib;
        private static IntPtr _rpcLib;
        // Quest users.
        public static bool IsQuest;
        private static bool _checkedQuest;
        private static GameObject QuestNotif;
        // Prevents stuff from running if Discord isn't open
        public static bool DiscordClosed;


        public override void OnInitializeMelon()
        {
            ModConsole.Setup(LoggerInstance);
            if (IsQuest) return;
            Preferences.Setup();
            var discord = Process.GetProcessesByName("discord");
            var discordcanary = Process.GetProcessesByName("discordcanary");
            if (discordcanary.Length <= 0 && discord.Length <= 0)
            {
                ModConsole.Error("Neither Discord or Discord Canary are running!");
                DiscordClosed = true;
                return;
            }
            if (discordcanary.Length > 0 && discord.Length > 0)
            {
                ModConsole.Error("You have both Discord and Discord Canary running! Discord may struggle to pick one, and it may not work! Please close one and restart!");
            }
            if (!Directory.Exists(UserDataDirectory))
            {
                ModConsole.Msg($"User data directory not found, creating at {UserDataDirectory}", 1);
                Directory.CreateDirectory(UserDataDirectory);
            }
            if (!File.Exists(DLLPath))
            {
                ModConsole.Msg($"Discord SDK not unpacked, checking legacy path", 1);
                if (Directory.Exists(LegacyDirectory) && File.Exists(Path.Combine(LegacyDirectory, "discord_game_sdk.dll")))
                {
                    File.Move(Path.Combine(LegacyDirectory, "discord_game_sdk.dll"), DLLPath);
                }
                else
                {
                    ModConsole.Msg($"Legacy path not found, creating at {DLLPath}", 1);
                    File.WriteAllBytes(DLLPath, EmbeddedResource.GetResourceBytes("discord_game_sdk.dll"));
                }
            }
            if (!File.Exists(UserEntriesPath))
            {
                ModConsole.Msg($"User entries file not unpacked, checking legacy path", 1);
                if (Directory.Exists(LegacyDirectory) && File.Exists(Path.Combine(LegacyDirectory, "UserEntries.txt")))
                {
                    var entries = Path.Combine(LegacyDirectory, "UserEntries.txt");
                    File.Move(entries, UserEntriesPath);
                }
                else
                {
                    ModConsole.Msg($"Legacy path not found, creating at {UserEntriesPath}", 1);
                    File.WriteAllBytes(UserEntriesPath, EmbeddedResource.GetResourceBytes("UserEntries.txt"));
                }
            }
            if (!_hasLoadedLib)
            {
                ModConsole.Msg($"Loading Discord SDK from {DLLPath}", 1);
                _rpcLib = DllTools.LoadLibrary(DLLPath);
                _hasLoadedLib = true;
            }
            ModConsole.Msg("Initializing RPC", 1);
            Rpc.Initialize();
            MelonCoroutines.Start(AvatarUpdate());
            BoneMenu.Setup();
            Hooking.OnLevelInitialized += OnLevelLoad;
            Hooking.OnLevelUnloaded += OnLevelUnload;
        }

        public override void OnApplicationQuit()
        {
            if (IsQuest || DiscordClosed) return;
            Rpc.Dispose();
            if (_hasLoadedLib)
            {
                DllTools.FreeLibrary(_rpcLib);
            }
        }

        public override void OnUpdate()
        {
            if (!_checkedQuest)
            {
                if (QuestChecker9000.IsQest())
                {
                    ModConsole.Error("You are on Quest! This mod won't work! Please use the PC version of BONELAB!");
                    IsQuest = true;
                }
                _checkedQuest = true;
            }
            if (_checkedQuest && IsQuest && QuestNotif == null)
            {
                QuestNotif = DeQuestify.MakeQuestNotif();
            }
            if (IsQuest || DiscordClosed) return;
            Rpc.Discord.RunCallbacks();
        }

        private static IEnumerator AvatarUpdate()
        {
            while (_levelLoaded)
            {
                AvatarHandler.UpdateRpc();
                yield return new WaitForSeconds(10);
            }

            while (!_levelLoaded)
            {
                yield return null;
            }
            MelonCoroutines.Start(AvatarUpdate());
        }

        private static bool _levelLoaded;
        private static void OnLevelLoad(LevelInfo levelInfo)
        {
            if (IsQuest && QuestNotif == null)
            {
                QuestNotif = DeQuestify.MakeQuestNotif();
            }
            if (IsQuest || DiscordClosed) return;
            _levelLoaded = true;
            ModConsole.Msg($"Level loaded: {levelInfo.title}", 1);
            if (Preferences.ResetKillsOnLevelLoad.Value) NPCDeathCounter.Counter = 0;
            if (Preferences.ResetGunShotsOnLevelLoad.Value) ShotCounter.Counter = 0;
            if (Preferences.ResetDeathsOnLevelLoad.Value) PlayerDeathCounter.Counter = 0;
            SpawnCounter.Counter = 0;
            GlobalVariables.status = $"In {levelInfo.title}";
            ModConsole.Msg($"Status is {GlobalVariables.status}", 1);
            GlobalVariables.largeImageKey = CheckBarcode.CheckMap(levelInfo.barcode);
            ModConsole.Msg($"Large image key is {GlobalVariables.largeImageKey}", 1);
            GlobalVariables.largeImageText = levelInfo.title;
            ModConsole.Msg($"Large image text is {GlobalVariables.largeImageText}", 1);
            switch (Preferences.DetailsMode.Value)
            {
                case DetailsMode.GunShots:
                    GlobalVariables.details = "Gun Shots Fired: 0";
                    Rpc.SetRpc(GlobalVariables.details, GlobalVariables.status, GlobalVariables.largeImageKey, GlobalVariables.largeImageText, GlobalVariables.smallImageKey, GlobalVariables.smallImageText);
                    break;
                case DetailsMode.NPCDeaths:
                    GlobalVariables.details = "NPC Deaths: 0";
                    Rpc.SetRpc(GlobalVariables.details, GlobalVariables.status, GlobalVariables.largeImageKey, GlobalVariables.largeImageText, GlobalVariables.smallImageKey, GlobalVariables.smallImageText);
                    break;
                case DetailsMode.SpawnablesPlaced:
                    GlobalVariables.details = "Objects Spawned: 0";
                    Rpc.SetRpc(GlobalVariables.details, GlobalVariables.status, GlobalVariables.largeImageKey, GlobalVariables.largeImageText, GlobalVariables.smallImageKey, GlobalVariables.smallImageText);
                    break;
                case DetailsMode.SDKMods:
                    GlobalVariables.details = $"SDK Mods Loaded: {CheckPallets.GetPalletCount()}";
                    Rpc.SetRpc(GlobalVariables.details, GlobalVariables.status, GlobalVariables.largeImageKey, GlobalVariables.largeImageText, GlobalVariables.smallImageKey, GlobalVariables.smallImageText);
                    break;
                case DetailsMode.Extraes:
                    GlobalVariables.details = ExtraesMode.RandomScreamingAboutNonsense();
                    Rpc.SetRpc(GlobalVariables.details, GlobalVariables.status, GlobalVariables.largeImageKey, GlobalVariables.largeImageText, GlobalVariables.smallImageKey, GlobalVariables.smallImageText);
                    break;
                case DetailsMode.Entries:
                    GlobalVariables.details = GetEntry();
                    ModConsole.Msg($"Details are {GlobalVariables.details}", 1);
                    Rpc.SetRpc(GlobalVariables.details, GlobalVariables.status, GlobalVariables.largeImageKey, GlobalVariables.largeImageText, GlobalVariables.smallImageKey, GlobalVariables.smallImageText);
                    break;
                case DetailsMode.PlayerDeaths:
                    GlobalVariables.details = $"Player Deaths: {PlayerDeathCounter.Counter}";
                    Rpc.SetRpc(GlobalVariables.details, GlobalVariables.status, GlobalVariables.largeImageKey, GlobalVariables.largeImageText, GlobalVariables.smallImageKey, GlobalVariables.smallImageText);
                    break;
                default:
                    ModConsole.Error("You don't have a proper mode set!");
                    Rpc.SetRpc(null, GlobalVariables.status, GlobalVariables.largeImageKey, GlobalVariables.largeImageText, GlobalVariables.smallImageKey, GlobalVariables.smallImageText);
                    break;
            }
        }

        private static void OnLevelUnload()
        {
            _levelLoaded = false;
        }

        private static string GetEntry()
        {
            var rnd = new Random();
            var lines = File.ReadAllLines(UserEntriesPath);
            var r = rnd.Next(lines.Length);
            return lines[r];
        }
    }
}