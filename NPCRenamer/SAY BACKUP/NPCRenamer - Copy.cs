using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace NPCRenamer
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("StreetsOfRogue.exe")]
    public class Renamer : BaseUnityPlugin
    {
        public const string pluginGuid = "fishlips13.streetsofrogue.npcrenamer";
        public const string pluginName = "NPC Renamer";
        public const string pluginVersion = "1.0";

        public static Renamer instance;
        public static bool changingText = false;

        public static List<string> chatterNames = new List<string>();
        public static Dictionary<string, Chatter> chatters = new Dictionary<string, Chatter>();

        public void Awake()
        {
            if (instance == null)
                instance = this;

            Logger.LogInfo(string.Format("{0} v{1} ({2}) started.", pluginName, pluginVersion, pluginGuid));

            // Read chatter data
            string data = File.ReadAllText("names-lines.txt");
            string[] lines = data.Split('\n');
            
            foreach (string line in lines)
            {
                string[] values = line.Split(',');

                List<string> dialogues = new List<string>();
                for (int i = 1; i < values.Length; i++)
                    dialogues.Add(values[i]);

                string name = values[0];
                if (chatters.ContainsKey(name))
                    continue;

                chatterNames.Add(name);
                chatters[name] = new Chatter(name, dialogues.ToArray());
            }

            // Link in patch methods
            Harmony harmony = new Harmony(pluginGuid);

            //MethodInfo original = AccessTools.Method(typeof(Agent), "Say", new Type[] { typeof(string), typeof(bool) });
            //MethodInfo patch = AccessTools.Method(typeof(Renamer), "Say_PrefixPatch");
            //harmony.Patch(original, new HarmonyMethod(patch));

            MethodInfo original2 = AccessTools.Method(typeof(Fader), "FadedIn");
            MethodInfo patch2 = AccessTools.Method(typeof(Renamer), "FadedIn_PrefixPatch");
            harmony.Patch(original2, new HarmonyMethod(patch2));
        }

        // FadedIn patch - replace all agent names
        // --- Triggered finally after level setup
        public static void FadedIn_PrefixPatch()
        {
            if (GameController.gameController.sessionDataBig.curLevel == 0)
                return;

            foreach (Agent agent in GameController.gameController.agentList)
                agent.agentRealName = chatterNames[UnityEngine.Random.Range(0, chatterNames.Count)];
        }

        //Say patch - replace dialogue lines just-in-time
        // --- Calling Prefix with arguments doesn't work (why?) so we work around with single recursion
        public static bool Say_PrefixPatch(Agent __instance)
        {
            if (changingText)
            {
                changingText = false;
                return true;
            }

            // Chatter not available for reference? Use default
            if (!chatters.ContainsKey(__instance.agentRealName))
                return true;

            Chatter chatter = chatters[__instance.agentRealName];

            // No custom dialogue? Use default
            string dialogue = chatter.GetRandomDialogue();
            if (dialogue == "")
                return true;

            changingText = true;
            __instance.Say(dialogue);

            return false;
        }

        public class Chatter
        {
            public string name;
            public string[] dialogues;

            public Chatter(string name, string[] dialogues)
            {
                this.name = name;
                this.dialogues = dialogues;
            }

            public string GetRandomDialogue()
            {
                if (dialogues.Length == 0)
                    return "";

                return dialogues[UnityEngine.Random.Range(0, dialogues.Length)];
            }
        }
    }
}
