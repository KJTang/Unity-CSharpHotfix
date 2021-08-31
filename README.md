# CSharpHotfix

A tool support modify c# code when Unity is playing.


## How-to-Use
### Enable Tool
Unity Toolbar -> CSharpHotfix -> Enable: 
![enable tool](Document/enable_tool.png)

### Hello World
create a new game object with new script named "Hello World":   
![create hello world](Document/hello_world.png)

### Edit Hello World
``` csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HelloWorld : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Hello World");
    }
}

```

### First Run
run game, you will see "Hello World" in the console:  
![output hotfix](Document/output.png)  


### Create Hotfix 
no need stop game, copy "HelloWorld.cs" to path: 
```YOU_PROJECT/CSharpHotfix/HelloWorld.cs```

and modify it: 
``` csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HelloWorld : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Hello World (hotfixed)");
    }
}
```

### Use Hotfix
Unity Toolbar -> CSharpHotfix -> Hotfix:
![hotfix](Document/hotfix.png)

### See Result
duplicate "HelloWorld" gameobject in the hierarchy (or copy & paste it), it will create a new gameobject:   
![create hello world hotfix](Document/hello_world_hotfix.png)  

and you will see "Hello World (hotfixed)" in the console:  
![output hotfix](Document/output_hotfix.png)  


## Limited
There're many limitations when use this tool: 
* **Cannot Debug**: as we will modify the .dll files when enable tool, you can't debug after then. For example, break points in visual studio will never be triggered. 
* **Only Support Method**: you can only hotfix method which is already declared in old c# codes. new class/methd/member will be ignored.
* **Bugs**: as a simple tool just created for testing, it hasn't been test enough.


## Run Tests
we have a few simple test cases included, just click the "Run Tests" button in SampleScene, you can see the different result before and after hotfix.  

### Before Hotfix
![test origin](Document/test_origin.png)  

### After Hotfix
![test hotfix](Document/test_hotfix.png)  
