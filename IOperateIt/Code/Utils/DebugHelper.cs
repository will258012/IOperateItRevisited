using System.Collections.Generic;
using UnityEngine;

namespace IOperateIt.Code.Utils
{
    internal class DebugHelper
    {
        public static void drawDebugMarker(float size, Vector3 position, Color color = default)
        {
            //Create vertices
            List<Vector3> generatedVertices = new List<Vector3>();
            float sizePerStep = size;

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    generatedVertices.Add(new Vector3(x * 0.1f - 0.05f, y * sizePerStep, 0));
                }
            }

            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    generatedVertices.Add(new Vector3(0, y * sizePerStep, z * 0.1f - 0.05f));
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

            Mesh planeMesh = new Mesh();
            planeMesh.vertices = generatedVertices.ToArray();
            planeMesh.triangles = generatedTriangles.ToArray();

            Material debugMat = new Material(Shader.Find("Custom/Citizens/Citizen/Underground"));
            debugMat.color = color;
            debugMat.renderQueue = 4000;

            Graphics.DrawMesh(planeMesh, position, Quaternion.identity, debugMat, 24);
        }
    }
}
