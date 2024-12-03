using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dropdown : MonoBehaviour
{
    private int[] valueSelection;
    // Start is called before the first frame update
    void Start()
    {
        valueSelection = new[] {20, 100, 500, 1000};
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ValueChange(int value)
    {
        Shader_execute.iterations = valueSelection[value];
    }
}
