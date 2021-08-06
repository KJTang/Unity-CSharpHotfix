#define UNITY_EDITOR    // debug

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfix
{
    public class CSharpHotfixManager 
    {
        public static bool IsHotfixEnabled
        {
            get { return isHotfixEnabled; }
            set
            {
                isHotfixEnabled = value;
            }
        }
        private static bool isHotfixEnabled = false;


    }

}
