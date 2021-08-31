using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestBtn : MonoBehaviour
{
    private Button testBtn;

    void Start()
    {
        testBtn = this.GetComponent<Button>();
        testBtn.onClick.AddListener(() => {
            CSharpHotfix.CSharpHotfixTestManager.RunTests();
        });
    }
}
