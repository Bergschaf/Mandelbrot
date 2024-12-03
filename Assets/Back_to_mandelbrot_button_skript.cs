using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Back_to_mandelbrot_button_skript : MonoBehaviour
{
    public Button b;

    public void Click()
    {
        Shader_execute.Mandelbrot = true;
    }

    // Update is called once per frame
    void Update()
    {
        b.interactable = !Shader_execute.Mandelbrot;
    }
}
