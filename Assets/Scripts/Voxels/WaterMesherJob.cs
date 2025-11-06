using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public partial struct WaterMesherJob : IJob
    {
        // --- Input Data ---
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public NativeArray<Voxel> Voxels;
        // --- MODIFICATION: We now need all 6 neighbors to check all faces ---
        [ReadOnly] public NativeArray<Voxel> LeftVoxels, RightVoxels, DownVoxels, UpVoxels, BackVoxels, ForwardVoxels;

        // --- Output Data ---
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
        public NativeList<float2> UVs;

        private const byte MAX_WATER_LEVEL = 7;

        public void Execute()
        {
            var mask = new NativeArray<bool>(ChunkSize * ChunkSize, Allocator.Temp);

            // Iterate through the 3 axes (Y, X, Z)
            for (int axis = 0; axis < 3; axis++)
            {
                // Iterate through the 2 directions along the axis (e.g., +Y and -Y)
                for (int dir = 0; dir < 2; dir++)
                {
                    // For each slice along the axis
                    for (int slice = 0; slice < ChunkSize; slice++)
                    {
                        // 1. Build the 2D mask for this slice
                        for (int v = 0; v < ChunkSize; v++)
                        {
                            for (int u = 0; u < ChunkSize; u++)
                            {
                                // Get the two voxels on either side of this potential face
                                GetVoxelsForFace(axis, slice, u, v, out var voxelA, out var voxelB);

                                // The face points from A to B. We want to draw a face if A has water and B is empty.
                                // The 'dir' variable flips which voxel we consider "A" and "B"
                                Voxel interiorVoxel = (dir == 0) ? voxelA : voxelB;
                                Voxel exteriorVoxel = (dir == 0) ? voxelB : voxelA;

                                bool shouldDrawFace = interiorVoxel.GetWaterLevel() > 0 && 
                                                      exteriorVoxel.GetWaterLevel() == 0 && 
                                                      !exteriorVoxel.IsSolid();
                                
                                mask[u + v * ChunkSize] = shouldDrawFace;
                            }
                        }
                        
                        // 2. Run greedy meshing on the completed mask
                        for (int v = 0; v < ChunkSize; v++)
                        {
                            for (int u = 0; u < ChunkSize; )
                            {
                                if (!mask[u + v * ChunkSize])
                                {
                                    u++;
                                    continue;
                                }

                                // Find width of the quad
                                int width = 1;
                                while (u + width < ChunkSize && mask[(u + width) + v * ChunkSize])
                                {
                                    width++;
                                }

                                // Find height of the quad
                                int height = 1;
                                bool done = false;
                                while (v + height < ChunkSize && !done)
                                {
                                    for (int k = 0; k < width; k++)
                                    {
                                        if (!mask[(u + k) + (v + height) * ChunkSize])
                                        {
                                            done = true;
                                            break;
                                        }
                                    }
                                    if (!done) height++;
                                }

                                // 3. A quad is found, emit its vertices
                                AddQuadVertices(axis, dir, slice, u, v, width, height);

                                // Zero out the mask area that we've processed
                                for (int h = 0; h < height; h++)
                                for (int w = 0; w < width; w++)
                                    mask[(u + w) + (v + h) * ChunkSize] = false;
                                
                                u += width;
                            }
                        }
                    }
                }
            }
            mask.Dispose();
        }
        
        /// <summary>
        /// Corrected quad generator: fixes plane offsets (+1 where needed), Y-face surface positions,
        /// and consistent vertex ordering / sizing for each axis.
        /// </summary>
        private void AddQuadVertices(int axis, int dir, int slice, int u, int v, int width, int height)
        {
            // Get the voxel at the quad origin to determine water level.
            GetVoxelsForFace(axis, slice, u, v, out var va, out var vb);
            Voxel originVoxel = (dir == 0) ? va : vb;
            byte waterLevel = originVoxel.GetWaterLevel();

            // Surface height between 0..1 (relative to voxel)
            float surfaceHeight = (float)waterLevel / MAX_WATER_LEVEL;
            if (waterLevel == MAX_WATER_LEVEL) surfaceHeight = 0.9f; // small inset to avoid z-fight

            float3 v0, v1, v2, v3;

            switch (axis)
            {
                case 0: // Y-axis plane (horizontal faces)
                {
                    // interior at slice (y = slice) and exterior at slice+1 (y = slice+1)
                    // If dir==0 => interior at slice (lower voxel), we want top surface at slice + surfaceHeight
                    // If dir==1 => interior at slice+1 (upper voxel), bottom surface should be at slice+1
                    float y = (dir == 0) ? (slice + surfaceHeight) : (slice + 1f);

                    // u -> x, v -> z ; quad spans x:[u,u+width], z:[v,v+height]
                    v0 = new float3(u, y, v);
                    v1 = new float3(u + width, y, v);
                    v2 = new float3(u, y, v + height);
                    v3 = new float3(u + width, y, v + height);

                    // For the top face (dir==0) use same ordering as original top; for bottom flip to keep normals outward
                    if (dir == 0) AddQuad(v0, v2, v1, v3, width, height); // top (+Y)
                    else AddQuad(v0, v1, v2, v3, width, height); // bottom (-Y)
                    break;
                }

                case 1: // X-axis plane (vertical faces on X)
                {
                    // plane x = slice+1 for both directions
                    float x = slice + 1f;

                    // For axis 1: u -> y, v -> z; quad spans y:[u,u+width], z:[v,v+height]
                    v0 = new float3(x, u, v);
                    v1 = new float3(x, u + width, v);
                    v2 = new float3(x, u, v + height);
                    v3 = new float3(x, u + width, v + height);

                    // dir==0 means interior voxel is at x = slice so face is +X outward; adjusted for correct winding
                    if (dir == 0) AddQuad(v0, v1, v2, v3, width, height);
                    else AddQuad(v0, v2, v1, v3, width, height);
                    break;
                }

                case 2: // Z-axis plane (vertical faces on Z)
                {
                    // plane z = slice+1 for both directions
                    float z = slice + 1f;

                    // For axis 2: u -> x, v -> y; quad spans x:[u,u+width], y:[v,v+height]
                    v0 = new float3(u, v, z);
                    v1 = new float3(u + width, v, z);
                    v2 = new float3(u, v + height, z);
                    v3 = new float3(u + width, v + height, z);

                    if (dir == 0) AddQuad(v0, v1, v2, v3, width, height); // +Z
                    else AddQuad(v0, v2, v1, v3, width, height); // -Z
                    break;
                }
            }
        }

        
        /// <summary>
        /// Adds a quad's data to the output lists.
        /// </summary>
        private void AddQuad(float3 vA, float3 vB, float3 vC, float3 vD, int uvWidth, int uvHeight)
        {
            int vertIndex = Vertices.Length;
            Vertices.Add(vA); Vertices.Add(vB); Vertices.Add(vC); Vertices.Add(vD);
            
            UVs.Add(new float2(0, 0));
            UVs.Add(new float2(0, uvHeight));
            UVs.Add(new float2(uvWidth, 0));
            UVs.Add(new float2(uvWidth, uvHeight));

            Triangles.Add(vertIndex); Triangles.Add(vertIndex + 1); Triangles.Add(vertIndex + 2);
            Triangles.Add(vertIndex + 2); Triangles.Add(vertIndex + 1); Triangles.Add(vertIndex + 3);
        }

        /// <summary>
        /// Helper to get the two voxels on either side of a face.
        /// </summary>
        private void GetVoxelsForFace(int axis, int slice, int u, int v, out Voxel a, out Voxel b)
        {
            int ax=0, ay=0, az=0;
            int bx=0, by=0, bz=0;

            switch (axis)
            {
                case 0: // Y-axis plane
                    ax=u; ay=slice; az=v;
                    bx=u; by=slice+1; bz=v;
                    break;
                case 1: // X-axis plane
                    ax=slice; ay=u; az=v;
                    bx=slice+1; by=u; bz=v;
                    break;
                case 2: // Z-axis plane
                    ax=u; ay=v; az=slice;
                    bx=u; by=v; bz=slice+1;
                    break;
            }
            a = GetVoxel(ax, ay, az);
            b = GetVoxel(bx, by, bz);
        }

        /// <summary>
        /// A robust helper to get a voxel at any local coordinate, checking neighbor chunks if necessary.
        /// </summary>
        private Voxel GetVoxel(int x, int y, int z)
        {
            if (x < 0) return LeftVoxels.Length > 0 ? LeftVoxels[GetIndexFromCoords(x + ChunkSize, y, z)] : default;
            if (x >= ChunkSize) return RightVoxels.Length > 0 ? RightVoxels[GetIndexFromCoords(x - ChunkSize, y, z)] : default;
            if (y < 0) return DownVoxels.Length > 0 ? DownVoxels[GetIndexFromCoords(x, y + ChunkSize, z)] : default;
            if (y >= ChunkSize) return UpVoxels.Length > 0 ? UpVoxels[GetIndexFromCoords(x, y - ChunkSize, z)] : default;
            if (z < 0) return BackVoxels.Length > 0 ? BackVoxels[GetIndexFromCoords(x, y, z + ChunkSize)] : default;
            if (z >= ChunkSize) return ForwardVoxels.Length > 0 ? ForwardVoxels[GetIndexFromCoords(x, y, z - ChunkSize)] : default;
            
            return Voxels[GetIndexFromCoords(x, y, z)];
        }

        private int GetIndexFromCoords(int x, int y, int z) => x + z * ChunkSize + y * ChunkSize * ChunkSize;
    }
}