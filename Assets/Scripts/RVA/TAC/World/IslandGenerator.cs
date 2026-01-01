using UnityEngine;
using System.Collections.Generic;

public class IslandGenerator : MonoBehaviour
{
    [Header("Island Configuration")]
    public int numberOfIslands = 41;
    public int mapWidth = 256;
    public int mapHeight = 256;
    public float terrainHeightMultiplier = 20f;
    public float waterLevel = 0.4f;

    [Header("Noise Settings")]
    public float noiseScale = 20f;
    public int octaves = 4;
    [Range(0,1)]
    public float persistance = 0.5f;
    public float lacunarity = 2f;
    public int seed;

    [Header("Falloff Map")]
    public bool useFalloff = true;
    [Range(1,10)]
    public float falloffPowerA = 3f;
    [Range(1,10)]
    public float falloffPowerB = 2.2f;


    public void GenerateIslands()
    {
        for (int i = 0; i < numberOfIslands; i++)
        {
            GenerateIsland(i);
        }
    }

    private void GenerateIsland(int islandIndex)
    {
        // Create a container for the island
        GameObject islandGO = new GameObject($"Island_{islandIndex}");
        islandGO.transform.position = new Vector3(islandIndex * (mapWidth + 50), 0, 0); // Position islands next to each other for now
        Terrain terrain = islandGO.AddComponent<Terrain>();
        TerrainData terrainData = new TerrainData();

        // Set terrain data properties
        terrainData.heightmapResolution = mapWidth + 1;
        terrainData.size = new Vector3(mapWidth, terrainHeightMultiplier, mapHeight);

        // Generate Heightmap
        float[,] heightMap = GenerateHeightMap(mapWidth, mapHeight);
        terrainData.SetHeights(0, 0, heightMap);

        // Assign terrain data
        terrain.terrainData = terrainData;
        islandGO.AddComponent<TerrainCollider>().terrainData = terrainData;

        Debug.Log($"Generated Island {islandIndex}");
    }

    private float[,] GenerateHeightMap(int width, int height)
    {
        float[,] noiseMap = GenerateNoiseMap(width, height, seed, noiseScale, octaves, persistance, lacunarity, Vector2.zero);
        float[,] falloffMap = null;

        if (useFalloff)
        {
            falloffMap = GenerateFalloffMap(width, height);
        }

        float[,] heightMap = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (useFalloff)
                {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);
                }
                heightMap[x, y] = noiseMap[x, y];
            }
        }
        return heightMap;
    }
    
    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, int octaves, float persistance, float lacunarity, Vector2 offset) {
		float[,] noiseMap = new float[mapWidth,mapHeight];

		System.Random prng = new System.Random (seed);
		Vector2[] octaveOffsets = new Vector2[octaves];
		for (int i = 0; i < octaves; i++) {
			float offsetX = prng.Next (-100000, 100000) + offset.x;
			float offsetY = prng.Next (-100000, 100000) + offset.y;
			octaveOffsets [i] = new Vector2 (offsetX, offsetY);
		}

		if (scale <= 0) {
			scale = 0.0001f;
		}

		float maxNoiseHeight = float.MinValue;
		float minNoiseHeight = float.MaxValue;

		float halfWidth = mapWidth / 2f;
		float halfHeight = mapHeight / 2f;


		for (int y = 0; y < mapHeight; y++) {
			for (int x = 0; x < mapWidth; x++) {

				float amplitude = 1;
				float frequency = 1;
				float noiseHeight = 0;

				for (int i = 0; i < octaves; i++) {
					float sampleX = (x-halfWidth) / scale * frequency + octaveOffsets[i].x;
					float sampleY = (y-halfHeight) / scale * frequency + octaveOffsets[i].y;

					float perlinValue = Mathf.PerlinNoise (sampleX, sampleY) * 2 - 1;
					noiseHeight += perlinValue * amplitude;

					amplitude *= persistance;
					frequency *= lacunarity;
				}

				if (noiseHeight > maxNoiseHeight) {
					maxNoiseHeight = noiseHeight;
				} else if (noiseHeight < minNoiseHeight) {
					minNoiseHeight = noiseHeight;
				}
				noiseMap [x, y] = noiseHeight;
			}
		}

		for (int y = 0; y < mapHeight; y++) {
			for (int x = 0; x < mapWidth; x++) {
				noiseMap [x, y] = Mathf.InverseLerp (minNoiseHeight, maxNoiseHeight, noiseMap [x, y]);
			}
		}

		return noiseMap;
	}

    private float[,] GenerateFalloffMap(int width, int height)
    {
        float[,] map = new float[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                float x = i / (float)width * 2 - 1;
                float y = j / (float)height * 2 - 1;

                float value = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                map[i, j] = EvaluateFalloff(value);
            }
        }

        return map;
    }

    private float EvaluateFalloff(float value)
    {
        float a = falloffPowerA;
        float b = falloffPowerB;
        return Mathf.Pow(value, a) / (Mathf.Pow(value, a) + Mathf.Pow(b - b * value, a));
    }
}
