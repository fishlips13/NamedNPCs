using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NamedNPCs
{
    public class NPCName
    {
        public string Name { get; }
        private List<string> dialogues;

        private int levelSpawns = 0;
        private int runSpawns = 0;
        private string prevDialogue = "";

        public NPCName(string name, List<string> dialogues)
        {
            Name = name;
            this.dialogues = dialogues;
        }

        public void Use()
        {
            levelSpawns++;
            runSpawns++;
        }

        public void ResetLevel()
        {
            levelSpawns = 0;
        }

        public void ResetRun()
        {
            runSpawns = 0;
        }

        public bool CanUse(int levelMax, int runMax)
        {
            return levelSpawns < levelMax && runSpawns < runMax;
        }

        public string GetRandomDialogue()
        {
            if (dialogues.Count == 0)
                return "";


            int index = UnityEngine.Random.Range(0, dialogues.Count);
            if (prevDialogue != "")
                dialogues.Add(prevDialogue);
            prevDialogue = dialogues[index];
            dialogues.RemoveAt(index);
            
            return prevDialogue;
        }
    }
}
