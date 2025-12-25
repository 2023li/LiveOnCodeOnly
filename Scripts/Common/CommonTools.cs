using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CommonTools 
{

    public static Vector3 Vector3NoZ(Vector3 v3)
    {
        return new Vector3(v3.x,v3.y,0);
    }
}
