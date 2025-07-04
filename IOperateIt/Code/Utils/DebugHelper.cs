using AlgernonCommons;
using ColossalFramework;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IOperateIt.Utils
{
    internal class DebugHelper
    {
        private static bool m_bInit = false;
        private static Camera m_mainCamera = null;
        private static Material m_debugMaterialWire = null;
        private static Material m_debugMaterial = null;
        private static MaterialPropertyBlock m_debugMaterialBlock = null;
        private static Mesh m_debugMeshMarker = null;
        private static Mesh m_debugMeshCube = null;

        private static void Initialize()
        {
            if (!m_bInit)
            {
                m_bInit = true;

                m_mainCamera = Singleton<RenderManager>.instance.CurrentCameraInfo.m_camera;

                /* Loading Audio Clips:
                string path = Path.Combine(AssemblyUtils.AssemblyPath, "Resources/Sounds/soundfile.ogg");
                WWW www = new WWW(new Uri(path).AbsoluteUri);
                www.GetAudioClip(true, false);
                 */

                /* Loading Textures:
                CreateSpriteAtlas("IOperateIt Sprites", 1, "Textures");
                 */

                string path = Path.Combine(AssemblyUtils.AssemblyPath, "Resources/Shaders/wireframeshader.asset");
                WWW www = new WWW(new Uri(path).AbsoluteUri);
                AssetBundle shaderBundle = www.assetBundle;
                Shader s = shaderBundle.LoadAsset<Shader>("Wireframe");
                if (s != null)
                {
                    m_debugMaterialWire = new Material(s);
                }

                m_debugMaterialBlock = new MaterialPropertyBlock();

                m_debugMaterial = new Material(Shader.Find("Custom/Citizens/Citizen/Underground"));

                InitMarkerMesh();
                InitCubeMesh();
            }

        }

        private static void InitMarkerMesh()
        {
            m_debugMeshMarker = new Mesh();

            //Create vertices
            List<Vector3> generatedVertices = new List<Vector3>();

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    generatedVertices.Add(new Vector3(x * 0.1f - 0.05f, y, 0));
                }
            }

            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    generatedVertices.Add(new Vector3(0, y, z * 0.1f - 0.05f));
                }
            }

            //Create triangles
            List<int> generatedTriangles = new List<int>();

            for (int i = 0; i < 2; i++)
            {
                //first triangle
                generatedTriangles.Add(4 * i + 0);
                generatedTriangles.Add(4 * i + 2);
                generatedTriangles.Add(4 * i + 3);

                //second triangle
                generatedTriangles.Add(4 * i + 0);
                generatedTriangles.Add(4 * i + 3);
                generatedTriangles.Add(4 * i + 1);

                //reverse triangles for visible backside
                generatedTriangles.Add(4 * i + 0);
                generatedTriangles.Add(4 * i + 3);
                generatedTriangles.Add(4 * i + 2);

                generatedTriangles.Add(4 * i + 0);
                generatedTriangles.Add(4 * i + 1);
                generatedTriangles.Add(4 * i + 3);
            }

            m_debugMeshMarker.vertices = generatedVertices.ToArray();
            m_debugMeshMarker.triangles = generatedTriangles.ToArray();
        }

        private static void InitCubeMesh()
        {
            m_debugMeshCube = new Mesh();

            Vector3[] vertices = {
                new Vector3 (-0.5f, -0.5f,- 0.5f),
                new Vector3 ( 0.5f, -0.5f, -0.5f),
                new Vector3 ( 0.5f,  0.5f, -0.5f),
                new Vector3 (-0.5f,  0.5f, -0.5f),
                new Vector3 (-0.5f,  0.5f,  0.5f),
                new Vector3 ( 0.5f,  0.5f,  0.5f),
                new Vector3 ( 0.5f, -0.5f,  0.5f),
                new Vector3 (-0.5f, -0.5f,  0.5f),
            };

            int[] triangles = {
                0, 2, 1, //face front
			    0, 3, 2,
                2, 3, 4, //face top
			    2, 4, 5,
                1, 2, 5, //face right
			    1, 5, 6,
                0, 7, 4, //face left
			    0, 4, 3,
                5, 4, 7, //face back
			    5, 7, 6,
                0, 6, 7, //face bottom
			    0, 1, 6
            };

            m_debugMeshCube.vertices = vertices;
            m_debugMeshCube.triangles = triangles;
        }
        
        public static void DrawDebugMarker(float size, Vector3 position, Color color = default)
        {
            if (!Logging.DetailLogging) return;

            Initialize();

            Material debugMat = m_debugMaterial;

            debugMat.renderQueue = 4000;

            m_debugMaterialBlock.Clear();
            m_debugMaterialBlock.SetColor("_Color", color);

            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(1.0f, size, 1.0f));

            Graphics.DrawMesh(m_debugMeshMarker, matrix, debugMat, 24, m_mainCamera, 0, m_debugMaterialBlock);
        }

        public static void DrawDebugBox(Vector3 size, Vector3 center, Quaternion rotation, Color color = default)
        {
            if (!Logging.DetailLogging) return;

            Initialize();

            Material debugMat;
            if (m_debugMaterialWire != null)
            {
                debugMat = m_debugMaterialWire;
            }
            else
            {
                debugMat = m_debugMaterial;
            }
            debugMat.renderQueue = 4000;

            m_debugMaterialBlock.Clear();
            m_debugMaterialBlock.SetFloat("_WireThickness", 400f);
            m_debugMaterialBlock.SetFloat("_WireSmoothness", 3f);
            m_debugMaterialBlock.SetColor("_WireColor", color);
            m_debugMaterialBlock.SetColor("_Color", color);
            m_debugMaterialBlock.SetColor("_BaseColor", Color.clear);
            m_debugMaterialBlock.SetFloat("_MaxTriSize", 200f);

            Matrix4x4 matrix = Matrix4x4.TRS(center, rotation, size);

            Graphics.DrawMesh(m_debugMeshCube, matrix, debugMat, 24, m_mainCamera, 0, m_debugMaterialBlock);
        }
    }
}
