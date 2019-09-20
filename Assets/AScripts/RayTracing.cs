using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracing : MonoBehaviour
{
    struct RTCamera
    {
        public Vector3 origin;
        public Vector3 u;
        public Vector3 v;
        public Vector3 w;

        public static int stride = sizeof(float) * 3 * 4;
    }

    struct RTLight
    {
        public int type;
        public Vector3 direction;
        public Vector3 color;
        public float intensity;

        public void SetColor(Color c)
        {
            color.x = c.r;
            color.y = c.g;
            color.z = c.b;
        }

        public static int stride = sizeof(int) + sizeof(float) * 7;
    }

    struct RTMaterial
    {
        public Vector3 color;
        public float reflectivness;

        public RTMaterial(Vector3 color, float reflectivness)
        {
            this.color = color;
            this.reflectivness = reflectivness;
        }

        public static int stride = sizeof(float) * 4;
    }

    struct RTSphere
    {
        public Vector3 center;
        public float radius;
        public RTMaterial material;

        public static int stride = sizeof(float) * 4 + RTMaterial.stride;
    }

    struct RTPlane
    {
        public Vector3 normal;
        public Vector3 position;
        public RTMaterial material;

        public void Init(Vector3 normal, float d)
        {
            this.normal = normal;
            this.position = d * normal;
        }

        public static int stride = sizeof(float) * 6 + RTMaterial.stride;
    }

    struct RTObjectList
    {
        //怎么向compute buffer传递数组
        //public RTSphere[] spheres;
        public RTPlane plane;
    }

    struct RTWorld
    {
        public RTCamera camera;
        public RTLight light;
        public RTObjectList objList;

        public static int stride = RTCamera.stride + RTLight.stride + RTPlane.stride;
    }

    // KernelName //
    private const string KernelName = "CSMain";

    protected bool m_bInited = false;
    public ComputeShader m_computeShader;
    private int m_kernelIndex = -1;
    private RenderTexture m_rt;
    private int m_width;
    private int m_height;

    //最大深度
    public int m_depth = 2;
    //重采样次数
    public int m_sampleCount = 4;
    //物件个数
    public int m_objCount = 2;
    //public float m_fov = 40;

    public Shader m_postShader;
    private Material m_postMat;

    private RTWorld m_world;
    private ComputeBuffer m_worldBuffer;

    private RTSphere[] m_spheres;
    private ComputeBuffer m_objBuffer;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Assert(this.m_postShader != null);
        m_postMat = new Material(m_postShader);
        m_postMat.hideFlags = HideFlags.DontSave;
    }

    // Update is called once per frame
    void Update()
    {
        if(!this.m_bInited)
        {
            return;
        }

        this.m_computeShader.SetTexture(this.m_kernelIndex, "ResultTex", m_rt);

        var lights = GameObject.FindGameObjectsWithTag("Light");
        var light = lights[0].GetComponent<Light>();
        this.m_world.light.type = 0;
        this.m_world.light.direction = -light.transform.forward;
        this.m_world.light.SetColor(light.color);
        this.m_world.light.intensity = light.intensity;

        Camera camera = Camera.main;
        float semiHeight = Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.PI / 180);
        m_world.camera.origin = Camera.main.transform.position;
        m_world.camera.w = camera.transform.forward;
        m_world.camera.v = camera.transform.up * semiHeight;
        m_world.camera.u = camera.transform.right * semiHeight * camera.aspect;
        RTWorld[] data = {m_world};
        this.m_worldBuffer.SetData(data);
        this.m_computeShader.SetBuffer(this.m_kernelIndex, "world", this.m_worldBuffer);

        this.m_objBuffer.SetData(this.m_spheres);
        this.m_computeShader.SetBuffer(this.m_kernelIndex, "spheres", this.m_objBuffer);

        this.m_computeShader.SetInt("MaxReflectCount", this.m_depth);

        this.m_computeShader.Dispatch(this.m_kernelIndex, this.m_width / 32, this.m_height / 32, 1);
    }

    void Init(int width, int height)
    {
        try
        {
            this.m_kernelIndex = this.m_computeShader.FindKernel(KernelName);
        }
        catch (Exception error)
        {
            Debug.LogFormat("Error: {0}", error.Message);
            return;
        }

        this.m_width = width;
        this.m_height = height;

        m_rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        m_rt.enableRandomWrite = true;
        m_rt.Create();

        this.InitWorldBuffer();

        this.m_bInited = true;
    }

    float Random(float tMin, float tMax)
    {
        return UnityEngine.Random.Range(tMin, tMax);
    }

    void InitWorldBuffer()
    {
        this.m_spheres = new RTSphere[this.m_objCount];
        int index = 0;
        {
            if(index < this.m_objCount)
            {
                RTSphere sphere = new RTSphere();
                sphere.center = new Vector3(0, 1, 0);
                sphere.radius = 1;
                sphere.material = new RTMaterial(new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)), Random(0, 0.5f));
                this.m_spheres[index++] = sphere;
            }

            if (index < this.m_objCount)
            {
                RTSphere sphere = new RTSphere();
                sphere.center = new Vector3(-4, 1, 0);
                sphere.radius = 1;
                sphere.material = new RTMaterial(new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)), Random(0, 0.5f));
                this.m_spheres[index++] = sphere;
            }

            if (index < this.m_objCount)
            {
                RTSphere sphere = new RTSphere();
                sphere.center = new Vector3(4, 1, 0);
                sphere.radius = 1;
                sphere.material = new RTMaterial(new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)), Random(0, 0.5f));
                this.m_spheres[index++] = sphere;
            }

            for (int a = -5; a < 10 && index < this.m_objCount; a++)
            {
                for (int b = -3; b < 3 && index < this.m_objCount; b++)
                {
                    Vector3 center = new Vector3(a +0.9f * Random(0, 1), 0.2f, b + 0.9f * Random(0, 1));
                    if ((center - new Vector3(4, 0.2f, 0)).magnitude > 0.9f)
                    {
                        RTSphere sphere = new RTSphere();
                        sphere.center = center;
                        sphere.radius = 0.2f;
                        sphere.material = new RTMaterial(new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)), Random(0, 0.5f));
                        this.m_spheres[index++] = sphere;
                    }
                }
            }
        }

        /*
        float radius = 2;
        for(; index < this.m_objCount;)
        {
            Vector3 pos;
            for(int y = 0; y < 10; ++y)
            {
                pos.y = y * 2 * (radius + 1) + radius + 1;
               
                for(int z = 0; z < 3; ++z)
                {
                    pos.z = z * 2 * (radius + 1);
                    for (int x = 0; x < 10; ++x)
                    {
                        RTSphere sphere = new RTSphere();
                        sphere.center = new Vector3((x - 1) * 2 * (radius + 1), pos.y, pos.z);
                        sphere.radius = radius;
                        sphere.material = new RTMaterial(new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)), Random(0, 0.5f));
                        this.m_spheres[index++] = sphere;

                        if(index >= this.m_objCount)
                        {
                            break;
                        }
                    }

                    if (index >= this.m_objCount)
                    {
                        break;
                    }
                }

                if (index >= this.m_objCount)
                {
                    break;
                }
            }
        }*/

        this.m_world.objList.plane = new RTPlane();
        this.m_world.objList.plane.Init(new Vector3(0, 1, 0), 0);
        this.m_world.objList.plane.material = new RTMaterial(new Vector3(1, 1, 1), 0.25f);

        this.m_worldBuffer = new ComputeBuffer(1, RTWorld.stride);

        this.m_objBuffer = new ComputeBuffer(this.m_objCount, RTSphere.stride);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if(!m_bInited)
        {
            this.Init(src.width, src.height);
        }

        m_postMat.SetTexture("_BaseTex", this.m_rt);
        Graphics.Blit(src, dest, m_postMat);
    }
}
