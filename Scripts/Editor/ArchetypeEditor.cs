using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArchetypeEditor : MonoBehaviour
{


    public void Construct_居民房()
    {

    }










    private class ArchetypeBuilder
    {
        BuildingArchetype archetype;
        public ArchetypeBuilder()
        {
            archetype = new BuildingArchetype();
        }



        public void SetName(string name)
        {
            archetype.DisplayName = name;
        }
        //...设置其他参数


        public void ConstructData(string path=null)
        {
            //保存到目标路径
        }
    }

}
