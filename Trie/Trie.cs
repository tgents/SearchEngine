using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
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
            foreach (char letter in str.ToLower())
            {
                int index = GetNum(letter);
                if (current.children == null)
                {
                    current.children = new Node[27];
                }
                if (current.children[index] == null)
                {
                    current.children[index] = new Node(letter);
                }
                current = current.children[index];
            }
            current.isWord = true;
        }

        public List<string> Search(string str)
        {
            Node current = GetLastNode(rootNode, str, 0);
            List<string> matches = new List<string>();
            if (current == null)
            {
                matches.Add("nothing found :(");
                return matches;
            }
            matches = GetStrings(current, matches, str.ToLower());
            return matches;
        }

        private Node GetLastNode(Node current, string str, int index)
        {
            if (current == null || current.children == null || index == str.Length)
            {
                return current;
            }
            int num = GetNum(str.ElementAt(index));
            return GetLastNode(current.children[num], str, index + 1);
        }

        private List<string> GetStrings(Node current, List<string> gimmeWords, string previous)
        {
            if (current != null && current.isWord)
            {
                gimmeWords.Add(previous);
            }

            if (gimmeWords.Count() < 30 && current.children != null)
            {
                
                foreach (Node child in current.children)
                {
                    if (child != null)
                    {
                        gimmeWords = GetStrings(child, gimmeWords, previous + child.id);
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
    }
}