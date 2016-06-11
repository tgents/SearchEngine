using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class Node
        {
            public Node[] children { get; set; }
            //public LinkedList<string> words;
            public char id { get; set; }
            public bool isWord { get; set; }

            public Node()
            {
                id = ' ';
            }

            public Node(char letter)
            {
                id = letter;
                isWord = false;
            }
        }
}