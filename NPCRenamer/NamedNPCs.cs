using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace NamedNPCs
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("StreetsOfRogue.exe")]
    public class Renamer : BaseUnityPlugin
    {
        public const string pluginGuid = "fishlips13.streetsofrogue.namednpcs";
        public const string pluginName = "Named NPCs";
        public const string pluginVersion = "1.0";

        public static bool enableRenaming = false;
        public static bool enableDialogueReplacement = false;
        public static int levelSpawnsMin = 0;
        public static int levelSpawnsMax = 0;
        public static int nameOccurancePerLevelMax = 0;
        public static int nameOccurancePerRunMax = 0;
        public static float dialogueReplacementChance = 0.0f;
        public static string playerName = "";
        public static string priorityName = "";

        public static Dictionary<string, NPCName> npcNamesDict = new Dictionary<string, NPCName>();

        public void Awake()
        {
            Logger.LogInfo(string.Format("{0} v{1} ({2}) started.", pluginName, pluginVersion, pluginGuid));

            try
            {
                // Read config data
                foreach (string line in File.ReadLines(@"BepInEx\plugins\NamedNPCs\config.cfg"))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    string[] operands = line.Split('=');
                    string paramName = operands[0].Trim();
                    string paramValue = operands[1].Trim();

                    paramValue = paramValue.Replace("\"", "");

                    if (bool.TryParse(paramValue, out bool paramBool)) // Booleans
                    {
                        switch (paramName)
                        {
                            case "EnableRenaming":
                                enableRenaming = paramBool;
                                break;
                            case "EnableDialogueReplacement":
                                enableDialogueReplacement = paramBool;
                                break;
                        }
                    }
                    else if (int.TryParse(paramValue, out int paramInt)) // Integers
                    {
                        switch (paramName)
                        {
                            case "LevelSpawnsMin":
                                levelSpawnsMin = Math.Max(0, paramInt);
                                break;
                            case "LevelSpawnsMax":
                                levelSpawnsMax = Math.Max(0, paramInt);
                                break;
                            case "NameOccurancePerLevelMax":
                                nameOccurancePerLevelMax = Math.Max(0, paramInt);
                                break;
                            case "NameOccurancePerRunMax":
                                nameOccurancePerRunMax = Math.Max(0, paramInt);
                                break;
                            case "DialogueReplacementChance":
                                dialogueReplacementChance = Mathf.Clamp(paramInt / 100.0f, 0.0f, 1.0f);
                                break;
                        }
                    }
                    else // Strings
                    {
                        switch (paramName)
                        {
                            case "PlayerName":
                                if (paramValue != "")
                                    playerName = paramValue;
                                break;
                            case "PriorityName":
                                if (paramValue != "")
                                    priorityName = paramValue;
                                break;
                        }
                    }
                }
            }
            catch
            {
                Logger.LogError("NamedNPCs: Error opening/parsing configuration file");
                return;
            }

            try
            {
                // Read name data
                foreach (string line in File.ReadLines(@"BepInEx\plugins\NamedNPCs\names.txt"))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    string[] entries = line.Trim().Split('|');
                    string npcName = entries[0];
                    List<string> dialogues = entries.Skip(1).ToList();

                    if (npcName.ToLower() == priorityName.ToLower() || npcNamesDict.ContainsKey(npcName))
                        continue;

                    npcNamesDict.Add(npcName, new NPCName(npcName, dialogues));
                }
            }
            catch
            {
                Logger.LogError("NamedNPCs: Error opening/parsing names file");
                return;
            }
 
            // Link in patch methods
            Harmony harmony = new Harmony(pluginGuid);
            MethodInfo original;
            MethodInfo patch;

            if (!enableRenaming)
                return;

            original = AccessTools.Method(typeof(Fader), "FadedIn");
            patch = AccessTools.Method(typeof(Renamer), "FadedIn_PrefixPatch");
            harmony.Patch(original, new HarmonyMethod(patch));

            if (!enableDialogueReplacement)
                return;

            original = AccessTools.Method(typeof(Agent), "Say", new Type[] { typeof(string), typeof(bool) });
            patch = AccessTools.Method(typeof(Renamer), "Say_PrefixPatch");
            harmony.Patch(original, new HarmonyMethod(patch));
        }

        // FadedIn patch - replace all agent names
        // --- Triggered finally after level setup
        public static void FadedIn_PrefixPatch()
        {
            if (GameController.gameController.levelEditing)
                return;

            // Flush run name use on Floor 1-1
            if (GameController.gameController.sessionDataBig.curLevel == 1)
            {
                foreach (NPCName npcName in npcNamesDict.Values)
                    npcName.ResetRun();
            }

            // Flush level name use each level
            foreach (NPCName npcName in npcNamesDict.Values)
                npcName.ResetLevel();

            // Find potential agents to name (heandle special cases)
            List<Agent> agentPool = new List<Agent>();
            foreach (Agent agent in GameController.gameController.agentList)
            {
                if (priorityName != "" && (agent.agentRealName == "Killer Robot" || agent.agentRealName == "Mayor"))
                    agent.agentRealName = priorityName;
                else if (agent.isPlayer == 0) // All other non-players (0 seems to mean false here)
                    agentPool.Add(agent);
                else if (playerName != "")
                    agent.agentRealName = playerName;
            }

            // Find legal names
            List<NPCName> npcNamePool = new List<NPCName>();
            foreach (NPCName npcName in npcNamesDict.Values)
            {
                if (npcName.CanUse(nameOccurancePerLevelMax, nameOccurancePerRunMax))
                    npcNamePool.Add(npcName);
            }
            // Mass name indiscriminately in homebase
            if (GameController.gameController.sessionDataBig.curLevel == 0)
            {
                List<NPCName> closedNamePool = new List<NPCName>();
                List<Agent> closedAgentPool = new List<Agent>();

                while (agentPool.Count > 0)
                {
                    int nameIndex = UnityEngine.Random.Range(0, npcNamePool.Count);

                    agentPool[0].agentRealName = npcNamePool[nameIndex].Name;

                    closedNamePool.Add(npcNamePool[nameIndex]);
                    closedAgentPool.Add(agentPool[0]);
                    npcNamePool.RemoveAt(nameIndex);
                    agentPool.RemoveAt(0);

                    if (npcNamePool.Count == 0)
                        npcNamePool = closedNamePool;
                }

                return;
            }

            // Narrow name selection by occurrence / availability
            int replaceCount = Math.Min(npcNamePool.Count, UnityEngine.Random.Range(levelSpawnsMin, levelSpawnsMax + 1));
            
            List<NPCName> nameCandidates = new List<NPCName>();
            for (int i = 0; i < replaceCount; i++)
            {
                int index = UnityEngine.Random.Range(0, npcNamePool.Count);
                nameCandidates.Add(npcNamePool[index]);
                npcNamePool.RemoveAt(index);
            }
            
            // Name agents
            while (nameCandidates.Count > 0 && agentPool.Count > 0)
            {
                int nameIndex = UnityEngine.Random.Range(0, nameCandidates.Count);

                if (!nameCandidates[nameIndex].CanUse(nameOccurancePerLevelMax, nameOccurancePerRunMax))
                {
                    nameCandidates.RemoveAt(nameIndex);
                    continue;
                }

                int agentIndex = UnityEngine.Random.Range(0, agentPool.Count);
                
                agentPool[agentIndex].agentRealName = nameCandidates[nameIndex].Name;
                agentPool.RemoveAt(agentIndex);

                nameCandidates[nameIndex].Use();
            }
        }

        //Say patch - replace dialogue lines just-in-time
        public static void Say_PrefixPatch(Agent __instance, ref string myMessage)
        {
            if (npcNamesDict.ContainsKey(__instance.agentRealName) && UnityEngine.Random.Range(0.0f, 1.0f) > dialogueReplacementChance)
            {
                string message = npcNamesDict[__instance.agentRealName].GetRandomDialogue();
                if (message != "")
                    myMessage = message;
            }
        }
    }
}
