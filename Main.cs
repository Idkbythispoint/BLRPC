﻿using System;
using BoneLib;
using System.IO;
using BLRPC.Internal;
using BLRPC.Melon;
using MelonLoader;
using UnityEngine;
using BLRPC.Patching;
using HarmonyLib;
using SLZ.VRMK;
using Random = System.Random;

namespace BLRPC
{
    public class Main : MelonMod
    {
        internal const string Name = "BLRPC";
        internal const string Description = "Discord Rich Presence for BONELAB";
        internal const string Author = "SoulWithMae";
        internal const string Company = "Weather Electric";
        internal const string Version = "1.3.1";
        internal const string DownloadLink = "null";
        
        // Stuff for userdata folder
        private static readonly string UserDataDirectory = Path.Combine(MelonUtils.UserDataDirectory, "BLRPC");
        private static readonly string DLLPath = Path.Combine(MelonUtils.UserDataDirectory, "BLRPC", "discord_game_sdk.dll");
        private static readonly string UserEntriesPath = Path.Combine(MelonUtils.UserDataDirectory, "BLRPC", "UserEntries.txt");
        // Stuff for loading the discord game SDK assembly
        private static bool _hasLoadedLib;
        private static IntPtr _rpcLib;
        // Quest users.
        private bool _isQuest;
        public override void OnInitializeMelon()
        {
            ModConsole.Setup(LoggerInstance);
            if (Application.platform == RuntimePlatform.Android)
            {
                // copilot came up with the "please use the PC version" line
                // just to make it clear, this mod will not work on Quest.
                // wait they wont see these comments they're kinda stupid
                // eh fuck it ill just add more logging to make it obvious
                ModConsole.Error("You are on Quest! This mod won't work! Please use the PC version of BONELAB!");
                ModConsole.Error("Seriously, this won't work at all. Don't come whining to me.");
                ModConsole.Error("You can't install Discord on Quest, so it won't work.");
                ModConsole.Error("All of the code is prevented from running if you're on Quest. It'll just cause issues.");
                ModConsole.Error("Just to get it through: DO NOT COMPLAIN TO ME ABOUT IT NOT WORKING. ITS IMPOSSIBLE FOR IT TO WORK.");
                _isQuest = true;
            }
            if (_isQuest) return;
            Preferences.Setup();
            if (!Directory.Exists(UserDataDirectory))
            {
                ModConsole.Msg($"User data directory not found, creating at {UserDataDirectory}", LoggingMode.DEBUG);
                Directory.CreateDirectory(UserDataDirectory);
            }
            if (!File.Exists(DLLPath))
            {
                ModConsole.Msg($"Discord SDK not unpacked, unpacking at {DLLPath}", LoggingMode.DEBUG);
                File.WriteAllBytes(DLLPath, EmbeddedResource.GetResourceBytes("discord_game_sdk.dll"));
            }
            if (!File.Exists(UserEntriesPath))
            {
                ModConsole.Msg($"User entries file not unpacked, unpacking at {UserEntriesPath}", LoggingMode.DEBUG);
                File.WriteAllBytes(UserEntriesPath, EmbeddedResource.GetResourceBytes("UserEntries.txt"));
            }
            if (!_hasLoadedLib)
            {
                ModConsole.Msg($"Loading Discord SDK from {DLLPath}", LoggingMode.DEBUG);
                _rpcLib = DllTools.LoadLibrary(DLLPath);
                _hasLoadedLib = true;
            }
            ModConsole.Msg("Initializing RPC", LoggingMode.DEBUG);
            Rpc.Initialize();
            Hooking.OnLevelInitialized += OnLevelLoad;
            Hooking.OnSwitchAvatarPostfix += AvatarUpdate;
        }

        public override void OnApplicationQuit()
        {
            Rpc.Dispose();
            if (_hasLoadedLib)
            {
                DllTools.FreeLibrary(_rpcLib);
            }
        }
        public override void OnUpdate()
        {
            if (_isQuest) return;
            Rpc.Discord.RunCallbacks();
        }

        private static void AvatarUpdate(Avatar avatar)
        {
            AvatarHandler.UpdateRpc();
        }

        [HarmonyPatch(typeof(Player_Health), "MakeVignette")]
        public static class VignettePatch
        {
            // ReSharper disable once InconsistentNaming, __instance CANNOT be renamed.
            public static void Postfix(Player_Health __instance)
            {
                OnRigmanagerReady();
            }
        }

        private static void OnRigmanagerReady()
        {
            AvatarHandler.UpdateRpc();
        }
        
        private static void OnLevelLoad(LevelInfo levelInfo)
        {
            MelonLogger.Msg($"Level loaded: {levelInfo.title}", LoggingMode.DEBUG);
            NPCDeathCounter.Counter = 0;
            ShotCounter.Counter = 0;
            SpawnCounter.Counter = 0;
            DoomlabPatch.Counter = 0;
            GlobalVariables.status = $"In {levelInfo.title}";
            ModConsole.Msg($"Status is {GlobalVariables.status}", LoggingMode.DEBUG);
            GlobalVariables.largeImageKey = CheckBarcode.CheckMap(levelInfo.barcode);
            ModConsole.Msg($"Large image key is {GlobalVariables.largeImageKey}", LoggingMode.DEBUG);
            GlobalVariables.largeImageText = levelInfo.title;
            ModConsole.Msg($"Large image text is {GlobalVariables.largeImageText}", LoggingMode.DEBUG);
            AvatarHandler.UpdateRpc();
            switch (Preferences.detailsMode.entry.Value)
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
                    ModConsole.Msg($"Details are {GlobalVariables.details}", LoggingMode.DEBUG);
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

        private static string GetEntry()
        {
            var rnd = new Random();
            var lines = File.ReadAllLines(UserEntriesPath);
            var r = rnd.Next(lines.Length);
            return lines[r];
        }
    }
}