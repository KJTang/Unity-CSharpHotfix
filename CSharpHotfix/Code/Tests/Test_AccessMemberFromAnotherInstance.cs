using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest
{
    
    public class CompareItem 
    {   
        public int CompareTo(object obj)
        {
            if (ReferenceEquals(obj, this) || !(obj is CompareItem))
            {
                return 0;
            }
        
            var srcValue = this.Value;
            var dstValue = (obj as CompareItem).Value;
            if (srcValue != dstValue)
            {
                return srcValue > dstValue ? -1 : 1;
            }
            return 0;
        }
    }


    public class Test_AccessMemberFromAnotherInstance
    {

        public string Func()
        {
            var item1 = new CompareItem(1);
            var item2 = new CompareItem(2);
            if (item1.CompareTo(item2) >= 0)
                return "invalid";
            return "hotfixed";
        }
    }
}