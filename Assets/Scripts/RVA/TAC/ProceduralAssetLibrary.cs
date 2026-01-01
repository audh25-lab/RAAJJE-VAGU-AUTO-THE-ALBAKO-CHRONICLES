using UnityEngine;

namespace RVA.TAC {
    public static class ProceduralAssetLibrary {
        // Generate HD pixel art style texture at runtime
        public static Texture2D GeneratePixelArtTexture(string id, int width = 64, int height = 64) {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point; // Pixel art look
            
            // Fill with procedural pattern based on ID hash
            int seed = id.GetHashCode();
            var rng = new System.Random(seed);
            
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    Color color = new Color(
                        (float)rng.NextDouble(),
                        (float)rng.NextDouble(),
                        (float)rng.NextDouble()
                    );
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            return texture;
        }
        
        // Generate simple mesh at runtime
        public static Mesh GenerateProceduralMesh(string type) {
            var mesh = new Mesh();
            
            switch (type) {
                case "DhoniBoat":
                    // Simple boat shape
                    Vector3[] vertices = {
                        new Vector3(-1, 0, 2), new Vector3(1, 0, 2),
                        new Vector3(-2, 0, -2), new Vector3(2, 0, -2),
                        new Vector3(0, 1, 0)
                    };
                    int[] triangles = {0,1,4, 1,3,4, 3,2,4, 2,0,4};
                    mesh.vertices = vertices;
                    mesh.triangles = triangles;
                    break;
                    
                case "PalmTree":
                    // Simple palm cone
                    mesh = GenerateCone(0.2f, 3f, 8);
                    break;
            }
            
            mesh.RecalculateNormals();
            return mesh;
        }
        
        static Mesh GenerateCone(float radius, float height, int segments) {
            var mesh = new Mesh();
            var vertices = new Vector3[segments + 2];
            vertices[0] = Vector3.zero;
            vertices[1] = Vector3.up * height;
            
            for (int i = 0; i < segments; i++) {
                float angle = i * Mathf.PI * 2 / segments;
                vertices[i + 2] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            }
            
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++) {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = (i + 1) % segments + 2;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            return mesh;
        }
        
        // Generate audio clip from wave data
        public static AudioClip GenerateProceduralAudio(string type) {
            int samples = 44100;
            var clip = AudioClip.Create(type, samples, 1, 44100, false);
            float[] data = new float[samples];
            
            switch (type) {
                case "BoduberuDrum":
                    // Simple drum beat pattern
                    for (int i = 0; i < samples; i++) {
                        data[i] = Mathf.Sin(i * 0.1f) * Mathf.Exp(-i * 0.0001f);
                    }
                    break;
                    
                case "OceanAmbient":
                    // White noise for ocean
                    var rng = new System.Random();
                    for (int i = 0; i < samples; i++) {
                        data[i] = (float)(rng.NextDouble() * 0.1 - 0.05);
                    }
                    break;
            }
            
            clip.SetData(data, 0);
            return clip;
        }
    }
}
