using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/*
 * 这个脚本之后需要做启动屏幕效果
 */

public class BootScenes : MonoBehaviour
{



    public TMP_Text t;
    
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("do some");


        
    }

    float time = 2f;

    // Update is called once per frame
    void Update()
    {
        time -= Time.deltaTime;

        t.text = time.ToString();

        if (time < 0)
        {
            AppManager.Instance.LoadStartScene();
        }
    }

    

}
