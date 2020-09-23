using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPCRenamer
{
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
