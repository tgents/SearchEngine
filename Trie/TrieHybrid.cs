using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication3
{
    public class Trie
    {
        public Node rootNode;
        public Trie()
        {
            rootNode = new Node();
        }

        public void Add(string str)
        {
            Node current = rootNode;
            int index = 0;
            while (current.children != null && index < str.Length)
            {
                int childIndex = GetNum(str.ElementAt(index));
                if (current.children[childIndex] == null)
                {
                    current.children[childIndex] = new Node(str.ElementAt(index));
                }
                current = current.children[childIndex];
                index++;
            }

            if(index == str.Length)
            {
                current.isWord = true;
            } else
            {
                string word = str.Substring(index);
                if (!word.Equals(""))
                {
                    current.words.Add(word);
                }
                if (current.words.Count > 50)
                {
                    current.changeToNodes();
                }
            }
        }

        public List<string> Search(string str)
        {
            Node current = rootNode;
            int pos = 0;
            while(current.children != null && pos < str.Length)
            {
                if(current == null)
                {
                    return null;
                }
                char currentChar = str.ElementAt(pos);
                int index = GetNum(currentChar);
                current = current.children[index];
                pos++;
            }
            List<string> matches = new List<string>();
            if (pos < str.Length)
            {
                foreach(string word in current.words)
                {
                    string wordup = str.Substring(0, pos) + word;
                    if (wordup.Contains(str))
                    {
                        matches.Add(wordup);
                    }
                }
            }
            else
            {
                matches = GetMoreWords(current, matches, str);
            }
            return matches;
        }

        private List<string> GetMoreWords(Node current, List<string> gimmeWords, string prefix)
        {
            if(gimmeWords.Count() > 100)
            {
                return gimmeWords;
            }
            if (current.isWord)
            {
                gimmeWords.Add(prefix + current.id);
            }
            if(current.children == null)
            {
                foreach(string word in current.words)
                {
                    gimmeWords.Add(prefix + word);
                }
            } else
            {
                foreach(Node child in current.children)
                {
                    if(child != null)
                    {
                        gimmeWords = GetMoreWords(child, gimmeWords, prefix + current.id);
                    }
                }
            }
            return gimmeWords;
        }

        private int GetNum(char letter)
        {
            int num = letter - 'a';
            return num < 0 ? 26 : num;
        }

        public class Node
        {
            public Node[] children { get; set; }
            public HashSet<string> words;
            public char id { get; set; }
            public bool isWord { get; set; }

            public Node()
            {
                id = ' ';
                isWord = false;
                words = new HashSet<string>();
                children = null;
            }

            public Node(char letter)
            {
                id = letter;
                isWord = false;
                words = new HashSet<string>();
                children = null;
            }

            public void changeToNodes()
            {
                children = new Node[27];
                foreach (string word in words)
                {
                    char childId = word.ElementAt(0);
                    int index = GetNum(childId);
                    if(children[index] == null)
                    {
                        children[index] = new Node(childId);
                    }
                    if(word.Length > 1)
                    {
                        children[index].words.Add(word.Substring(1));
                    } else
                    {
                        children[index].isWord = true;
                    }
                }
                words = null;
            }

            private int GetNum(char letter)
            {
                int num = letter - 'a';
                return num < 0 ? 26 : num;
            }
        }
    }
}
