using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class Axis
{
    public int screen_size;
    public double world_range;
    public double range_start;
    private int inverted = 1;

    public Axis(int _screen_size, float _world_range, float _range_start, bool _inverted = false)
    {
        screen_size = _screen_size;
        world_range = _world_range;
        range_start = _range_start;
        if (_inverted)
        {
            inverted = -1;
        }
    }

    public double pixel2World(float pixel)
    {
        return pixel * world_range / screen_size + range_start;
    }

    public void zoom(float fac, float pixel_pos)
    {
        double pos = pixel2World(pixel_pos);
        double w = pixel_pos / screen_size * world_range;

        range_start = pos - inverted * w * fac;
        world_range *= fac;
    }
}


public class Shader_execute : MonoBehaviour
{
    public ComputeShader MandelbrotShader;
    public ComputeShader JuliaShader;
    public RenderTexture Texture;
    private int numthreadsx = 8;
    private int numthreadsy = 8;
    private Axis X_Mandelbrot;
    private Axis Y_Mandelbrot;
    private Axis X_Julia;
    private Axis Y_Julia;
    private Vector2 res;
    private d_buffer[] Mandelbrot_data;
    private d_buffer_julia[] Julia_data;
    private Texture2D rgbTex;

    private int totalsize;
    public static int iterations;
    public static bool Mandelbrot = true;
    public static double[] Julia_constant;
    private ComputeBuffer databuffer_mandelbrot;
    private ComputeBuffer databuffer_julia;
    private ComputeBuffer databuffer_colormap;
    public int colormap_size = 100;

    public int[] colormap;
    private Color[] pixels;

    private float avg_escape_value;
    private int[] colormap_prep;

    private int count;
    private int get_colormap_at_count = 100;
    


    public struct Colormap
    {
        public float[] R;
        public float[] G;
        public float[] B;
    }

    struct d_buffer
    {
        public double x_start;
        public double x_range;
        public double y_start;
        public double y_range;
    }

    struct d_buffer_julia
    {
        public double x_start;
        public double x_range;
        public double y_start;
        public double y_range;
        public double const_0;
        public double const_1;
    }


    // Start is called before the first frame update
    void Awake()
    {
        iterations = 20;
        res = new Vector2(1000, 1000);
        Texture = new RenderTexture((int) res.x, (int) res.y, 24);
        Texture.enableRandomWrite = true;

        X_Mandelbrot = new Axis((int) res.x, 4, -2);
        Y_Mandelbrot = new Axis((int) res.y, 4, -2);
        X_Julia = new Axis((int) res.x, 4, -2);
        Y_Julia = new Axis((int) res.y, 4, -2);
        totalsize = sizeof(double);
        Julia_constant = new double[2];
        databuffer_mandelbrot = new ComputeBuffer(4, totalsize);

        databuffer_julia = new ComputeBuffer(6, totalsize);

        Mandelbrot_data = new d_buffer[1];
        Julia_data = new d_buffer_julia[1];

        colormap = new int[colormap_size];
        for (int i = 0; i < colormap_size; i++)
        {
            colormap[i] = i;
        }
        rgbTex = new Texture2D((int)res.x,(int)res.y);


    }


    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        count++;
        if (Texture == null)
        {
            Texture = new RenderTexture((int) res.x, (int) res.y, 24);
            Texture.enableRandomWrite = true;
            Texture.Create();
        }



        Vector3 mouse_pos = Input.mousePosition;
        float fac = Input.GetAxis("Mouse ScrollWheel");

        if (Mandelbrot)
        {
            X_Mandelbrot.range_start += Input.GetAxis("Horizontal") * Time.deltaTime *
                                        X_Mandelbrot.world_range;


            Y_Mandelbrot.range_start += Input.GetAxis("Vertical") * Time.deltaTime *
                                        Y_Mandelbrot.world_range;
            X_Mandelbrot.zoom(1 + fac, Mathf.Abs(mouse_pos.x));
            Y_Mandelbrot.zoom(1 + fac, Mathf.Abs(mouse_pos.y));


            Mandelbrot_data[0] = new d_buffer
            {
                x_range = X_Mandelbrot.world_range, x_start = X_Mandelbrot.range_start,
                y_range = Y_Mandelbrot.world_range, y_start = Y_Mandelbrot.range_start,
            };


            MandelbrotShader.SetTexture(0, "Result", Texture);
            MandelbrotShader.SetFloat("Resolution", Texture.width);
            MandelbrotShader.SetInt("colormap_ids_size", colormap_size);
            MandelbrotShader.SetInt("iterations", iterations);


            //Debug.Log((X_Mandelbrot.range_start, X_Mandelbrot.world_range));
            databuffer_mandelbrot = new ComputeBuffer(1, sizeof(double) * 4);
            databuffer_mandelbrot.SetData(Mandelbrot_data);

            MandelbrotShader.SetBuffer(0, "data", databuffer_mandelbrot);

            databuffer_colormap = new ComputeBuffer(colormap_size, sizeof(int));
            databuffer_colormap.SetData(colormap);

            MandelbrotShader.SetBuffer(0, "colormap", databuffer_colormap);


            MandelbrotShader.Dispatch(0, Texture.width / numthreadsx, Texture.height / numthreadsy, 1);
            Graphics.Blit(Texture, dest);
            databuffer_mandelbrot.Dispose();
            databuffer_colormap.Dispose();
            if (count >= get_colormap_at_count)
            {
                RenderTexture.active = Texture;

                rgbTex.ReadPixels(new Rect(0, 0, Texture.width, Texture.height), 0, 0);

                pixels = rgbTex.GetPixels();    

                colormap_prep = new int[iterations];

                calculate_Colormap();

                RenderTexture.active = null;
                count = 0;
            }


        }
        else
        {
            X_Julia.zoom(1 + fac, Mathf.Abs(mouse_pos.x));
            Y_Julia.zoom(1 + fac, Mathf.Abs(mouse_pos.y));
        
            Julia_data[0] = new d_buffer_julia()
            {
                x_range = X_Julia.world_range, x_start = X_Julia.range_start,
                y_range = Y_Julia.world_range, y_start = Y_Julia.range_start,
                const_0 = Julia_constant[0], const_1 = Julia_constant[1]
            };


            JuliaShader.SetTexture(0, "Result", Texture);
            JuliaShader.SetFloat("Resolution", Texture.width);
            //Debug.Log((X_Mandelbrot.range_start, X_Mandelbrot.world_range));
            databuffer_julia = new ComputeBuffer(1, sizeof(double) * 6);
            databuffer_julia.SetData(Julia_data);
            JuliaShader.SetBuffer(0, "data", databuffer_julia);
            JuliaShader.SetInt("iterations", iterations);
            JuliaShader.Dispatch(0, Texture.width / numthreadsx, Texture.height / numthreadsy, 1);
            Graphics.Blit(Texture, dest);
            databuffer_julia.Dispose();
        }
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 mouse_pos = Input.mousePosition;
        if (Input.GetButtonDown("Fire2") && Mandelbrot)
        {
            X_Julia = new Axis((int) res.x, 4, -2);
            Y_Julia = new Axis((int) res.y, 4, -2);
            Mandelbrot = false;
            Julia_constant[0] = X_Mandelbrot.pixel2World(mouse_pos.x);
            Julia_constant[1] = X_Mandelbrot.pixel2World(mouse_pos.y);
        }
    }

    void calculate_Colormap()
    {

        foreach (var p in pixels)
        {
            avg_escape_value += p.grayscale;
            colormap_prep[Mathf.RoundToInt(p.grayscale * iterations)] += 1;
        }

        float colormap_prep_real_fac = colormap_size / iterations;
        avg_escape_value /= pixels.Length;
        int temp_sum = 0;
        int last_escape = 0;
        int c = 0;
        for (int i = 0; i < iterations; i++)
        {
            temp_sum += colormap_prep[i];

            if (temp_sum > avg_escape_value)
            {
                temp_sum = 0;
                for (int j = last_escape; j < i; j++)
                {
                    colormap[Mathf.FloorToInt(j * colormap_prep_real_fac)] = c;
                }
                last_escape = i;
                c++;
                
            }
        }

        colormap[colormap_size -1] = 4242;




    }
}