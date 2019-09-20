using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingEx : MonoBehaviour
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
        public int type;

        public Vector3 albedo;
        public float fuzz;
        public float ref_idx;

        public RTMaterial(int  _type, Vector3 _albedo, float _fuzz = 0, float _ref_idx = 0)
        {
            this.type = _type;
            this.albedo = _albedo;
            this.fuzz = _fuzz;
            this.ref_idx = _ref_idx;
        }

        public static int stride = sizeof(int) + sizeof(float) * 5;
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

    struct RTObjectBufferItem
    {
        public string name;
        public List<RTSphere> spheres;
        public ComputeBuffer objBuffer;

        public void InitBuffer()
        {
            if(this.spheres.Count <= 0)
            {
                return;
            }

            this.objBuffer = new ComputeBuffer(this.spheres.Count, RTSphere.stride);
        }

        public void UploadData(ComputeShader shader, int kernelIndex)
        {
            if(null == this.objBuffer)
            {
                return;
            }

            this.objBuffer.SetData(this.spheres);
            shader.SetBuffer(kernelIndex, name, this.objBuffer);
        }
    }

    struct RTObjectBuffer
    {
        public const int BufferLen = 3;
        public const int BufferItemLen = 50;
        public const int BufferObjectSize = BufferLen * BufferItemLen;

        public int maxObjCount;
        RTObjectBufferItem[] objBuffers;

        int curBufferIndex;
        int curObjCount;

        public void Init(int _maxObjCount)
        {
            this.maxObjCount = Math.Min(_maxObjCount, BufferObjectSize);
            this.objBuffers = new RTObjectBufferItem[10];
            for (int i = 0; i < this.objBuffers.Length; ++i)
            {
                this.objBuffers[i] = new RTObjectBufferItem();
                this.objBuffers[i].spheres = new List<RTSphere>();
                this.objBuffers[i].name = "spheres" + (i + 1);
            }

            this.curBufferIndex = 0;
        }

        public void InitBuffer()
        {
            for (int i = 0; i < this.objBuffers.Length; ++i)
            {
                this.objBuffers[i].InitBuffer();
            }
        }

        public void AddObject(ref RTSphere obj)
        {
            if(this.curObjCount >= this.maxObjCount)
            {
                return;
            }

            this.curObjCount++;

            var spheres = this.objBuffers[this.curBufferIndex].spheres;
            spheres.Add(obj);
            if(spheres.Count >= BufferItemLen)
            {
                this.curBufferIndex++;
            }
        }

        public void UploadData(ComputeShader shader, int kernelIndex)
        {
            for (int i = 0; i < this.objBuffers.Length; ++i)
            {
                this.objBuffers[i].UploadData(shader, kernelIndex);
            }
        }
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

        public int depth;
        public int sampleCount;

        public static int stride = RTCamera.stride + RTLight.stride + RTPlane.stride + sizeof(int) * 2;
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
    private RTObjectBuffer m_objBuffer;



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

        m_world.depth = this.m_depth;
        m_world.sampleCount = this.m_sampleCount;

        RTWorld[] data = {m_world};
        this.m_worldBuffer.SetData(data);
        this.m_computeShader.SetBuffer(this.m_kernelIndex, "world", this.m_worldBuffer);

        //this.m_objBuffer.UploadData(this.m_computeShader, this.m_kernelIndex);

        //this.m_computeShader.SetInt("MaxReflectCount", this.m_depth);

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
        this.m_objBuffer.Init(this.m_objCount);
    
        int objCount = 0;
        if (objCount < this.m_objCount)
        {
            RTSphere sphere = new RTSphere();
            sphere.center = new Vector3(0, 1, 0);
            sphere.radius = 1;
            sphere.material = new RTMaterial(3, new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)), 0, 1.5f);

            this.m_objBuffer.AddObject(ref sphere);
            ++objCount;
        }

        if (objCount < this.m_objCount)
        {
            RTSphere sphere = new RTSphere();
            sphere.center = new Vector3(-4, 1, 0);
            sphere.radius = 1;
            sphere.material = new RTMaterial(1, new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)));

            this.m_objBuffer.AddObject(ref sphere);
            ++objCount;
        }

        if (objCount < this.m_objCount)
        {
            RTSphere sphere = new RTSphere();
            sphere.center = new Vector3(4, 1, 0);
            sphere.radius = 1;
            sphere.material = new RTMaterial(2, new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)), 0.0f);

            this.m_objBuffer.AddObject(ref sphere);
            ++objCount;
        }

        int[] materialType = { 1, 2, 3 };
        for (int a = -4; a < 11 && objCount < this.m_objCount; ++a)
        {
            for (int b = -7; b < 7 && objCount < this.m_objCount; b++)
            {
                Vector3 center = new Vector3(a + 0.9f * Random(0, 1), 0.2f, b + 0.9f * Random(0, 1));
                if ((center - new Vector3(4, 0.2f, 0)).magnitude > 0.9f)
                {
                    RTSphere sphere = new RTSphere();
                    sphere.center = center;
                    sphere.radius = 0.2f;
                    sphere.material = new RTMaterial(materialType[UnityEngine.Random.Range(0, 3)],
                                                                        new Vector3(Random(0.5f, 1), Random(0.5f, 1), Random(0.5f, 1)),
                                                                        Random(0, 1), 1.5f);

                    this.m_objBuffer.AddObject(ref sphere);
                    ++objCount;
                }
            }
        }

        this.m_world.objList.plane = new RTPlane();
        this.m_world.objList.plane.Init(new Vector3(0, 1, 0), 0);
        this.m_world.objList.plane.material = new RTMaterial(1, new Vector3(0.5f, 0.5f, 0.5f));

        this.m_world.depth = this.m_depth;
        this.m_world.sampleCount = this.m_sampleCount;

        this.m_worldBuffer = new ComputeBuffer(1, RTWorld.stride);

        this.m_objBuffer.InitBuffer();
        this.m_objBuffer.UploadData(this.m_computeShader, this.m_kernelIndex);
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
