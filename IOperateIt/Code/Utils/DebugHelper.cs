#if DEBUG
using AlgernonCommons;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/*
 * Object Layers:
 * Default
 * Buildings
 * Citizens
 * MetroTunnels
 * ScenarioMarkers
 * Props
 * Trees
 * Vehicles
 * Lights
 * LightsFloating
 * PowerLines
 * NetBuildings
 * Road
 * DirectionArrows
 * Notifications
 * Offscreen
 * Markers
 * Terrain
 * Decoration
 * Water
 * WaterPipes
 */

namespace IOperateIt.Utils
{
    internal class DebugHelper
    {
        private static Camera mainCamera = null;
        private static Material debugMaterialWire = null;
        private static Material debugMaterial = null;
        private static MaterialPropertyBlock debugMaterialBlock = null;
        private static Mesh debugMeshMarker = null;
        private static Mesh debugMeshCube = null;

        static DebugHelper()
        {

            mainCamera = RenderManager.instance.CurrentCameraInfo.m_camera;

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
                debugMaterialWire = new Material(s);
            }

            debugMaterialBlock = new MaterialPropertyBlock();

            debugMaterial = new Material(Shader.Find("Custom/Citizens/Citizen/Underground"));

            InitMarkerMesh();
            InitCubeMesh();
        }

        private static void InitMarkerMesh()
        {
            debugMeshMarker = new Mesh();

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

            debugMeshMarker.vertices = generatedVertices.ToArray();
            debugMeshMarker.triangles = generatedTriangles.ToArray();
        }

        private static void InitCubeMesh()
        {
            debugMeshCube = new Mesh();

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

            debugMeshCube.vertices = vertices;
            debugMeshCube.triangles = triangles;
        }

        public static void DrawDebugMarker(float size, Vector3 position, Color color = default)
        {
            Material debugMat = debugMaterial;

            debugMat.renderQueue = 4000;

            debugMaterialBlock.Clear();
            debugMaterialBlock.SetColor("_Color", color);

            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(1.0f, size, 1.0f));

            Graphics.DrawMesh(debugMeshMarker, matrix, debugMat, 24, mainCamera, 0, debugMaterialBlock);
        }

        public static void DrawDebugBox(Vector3 size, Vector3 center, Quaternion rotation, Color color = default)
        {
            Material debugMat;
            if (debugMaterialWire != null)
            {
                debugMat = debugMaterialWire;
            }
            else
            {
                debugMat = debugMaterial;
            }
            debugMat.renderQueue = 4000;

            debugMaterialBlock.Clear();
            debugMaterialBlock.SetFloat("_WireThickness", 400f);
            debugMaterialBlock.SetFloat("_WireSmoothness", 3f);
            debugMaterialBlock.SetColor("_WireColor", color);
            debugMaterialBlock.SetColor("_Color", color);
            debugMaterialBlock.SetColor("_BaseColor", Color.clear);
            debugMaterialBlock.SetFloat("_MaxTriSize", 200f);

            Matrix4x4 matrix = Matrix4x4.TRS(center, rotation, size);

            Graphics.DrawMesh(debugMeshCube, matrix, debugMat, 24, mainCamera, 0, debugMaterialBlock);
        }
    }
}
#endif