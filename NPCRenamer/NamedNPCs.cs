using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

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
        public static int levelSpawnsMin = 0;
        public static int levelSpawnsMax = 0;
        public static int nameOccurancePerLevelMax = 0;
        public static int nameOccurancePerRunMax = 0;
        public static string playerName = "";
        public static string priorityName = "";

        public static List<string> names = new List<string>();
        public static Dictionary<string, int> namesCountLevel = new Dictionary<string, int>();
        public static Dictionary<string, int> namesCountRun = new Dictionary<string, int>();

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

                    int equalsIndex = line.IndexOf('=');
                    string paramName = line.Substring(0, equalsIndex).Trim();
                    string paramValue = line.Substring(equalsIndex + 1, line.Length - equalsIndex - 1).Trim();

                    if (bool.TryParse(paramValue, out bool paramBool)) // Booleans
                    {
                        switch (paramName)
                        {
                            case "EnableRenaming":
                                enableRenaming = paramBool;
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
                                
                        }
                    }
                    else // Strings
                    {
                        paramValue = paramValue.Replace("\"", "");

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

                    string name = line.Trim();

                    if (name.ToLower() == priorityName.ToLower() || namesCountLevel.ContainsKey(name))
                        continue;

                    names.Add(name);
                    namesCountLevel.Add(name, 0);
                    namesCountRun.Add(name, 0);
                }
            }
            catch
            {
                Logger.LogError("NamedNPCs: Error opening/parsing names file");
                return;
            }
 
            // Link in patch methods
            Harmony harmony = new Harmony(pluginGuid);

            MethodInfo original = AccessTools.Method(typeof(Fader), "FadedIn");
            MethodInfo patch = AccessTools.Method(typeof(Renamer), "FadedIn_PrefixPatch");
            harmony.Patch(original, new HarmonyMethod(patch));
        }

        // FadedIn patch - replace all agent names
        // --- Triggered finally after level setup
        public static void FadedIn_PrefixPatch()
        {
            Debug.Log("Agent Count: " + GameController.gameController.agentList.Count);
            if (!enableRenaming || GameController.gameController.levelEditing)
                return;

            // Flush run name use on Floor 1-1
            if (GameController.gameController.sessionDataBig.curLevel == 1)
            {
                foreach (string name in namesCountRun.Keys.ToList())
                    namesCountRun[name] = 0;
            }

            // Flush level name use each level
            foreach (string name in namesCountLevel.Keys.ToList())
                namesCountLevel[name] = 0;

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
            List<string> namePool = new List<string>();
            foreach (string name in names)
            {
                if (namesCountRun[name] < nameOccurancePerRunMax)
                    namePool.Add(name);
            }

            // Mass name indiscriminately in homebase
            if (GameController.gameController.sessionDataBig.curLevel == 0)
            {
                List<string> closedNamePool = new List<string>();
                List<Agent> closedAgentPool = new List<Agent>();

                while (agentPool.Count > 0)
                {
                    int nameIndex = UnityEngine.Random.Range(0, namePool.Count);

                    agentPool[0].agentRealName = namePool[nameIndex];

                    closedNamePool.Add(namePool[nameIndex]);
                    closedAgentPool.Add(agentPool[0]);
                    namePool.RemoveAt(nameIndex);
                    agentPool.RemoveAt(0);

                    if (namePool.Count == 0)
                        namePool = closedNamePool;
                }

                return;
            }

            // Narrow name selection by occurrence / availability
            int replaceCount = Math.Min(namePool.Count, UnityEngine.Random.Range(levelSpawnsMin, levelSpawnsMax + 1));

            List<string> nameCandidates = new List<string>();
            for (int i = 0; i < replaceCount; i++)
            {
                int index = UnityEngine.Random.Range(0, namePool.Count);
                nameCandidates.Add(namePool[index]);
                namePool.RemoveAt(index);
            }

            // Name agents
            while (nameCandidates.Count > 0 && agentPool.Count > 0)
            {
                string name = nameCandidates[0];
                if (namesCountLevel[name] == nameOccurancePerLevelMax || namesCountRun[name] == nameOccurancePerRunMax)
                {
                    nameCandidates.RemoveAt(0);
                    continue;
                }

                int agentIndex = UnityEngine.Random.Range(0, agentPool.Count);

                agentPool[agentIndex].agentRealName = name;
                agentPool.RemoveAt(agentIndex);

                namesCountLevel[name]++;
                namesCountRun[name]++;
            }
        }
    }
}
