using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public partial struct MesherJob : IJob
    {
        // --- Input Data ---
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int AtlasSizeInTiles;

        [ReadOnly] public NativeArray<Voxel> Voxels;
        [ReadOnly] public NativeArray<Voxel> LeftVoxels, RightVoxels, DownVoxels, UpVoxels, BackVoxels, ForwardVoxels;
        [ReadOnly] public NativeArray<BlockTextureData> BlockTypeData;

        // --- Output Data ---
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
        public NativeList<float2> UVs0;
        public NativeList<float2> UVs1;

        private struct GreedyQuad
        {
            public ushort BlockId;
            public int Axis;      // 0 = Y (faces normal +/- Y), 1 = X, 2 = Z
            public int Direction; // 0 = positive normal (face points +axis), 1 = negative normal (face points -axis)
            public int Slice;     // slice index along the axis (0..ChunkSize)
            public int X, Y;      // origin (in 2D plane coordinates)
            public int Width, Height;
        }

        public void Execute()
        {
            // Temporary mask used per-plane
            var mask = new NativeArray<ushort>(ChunkSize * ChunkSize, Allocator.Temp);

            // For each axis and direction, perform greedy meshing
            for (int axis = 0; axis < 3; axis++)
            {
                for (int dir = 0; dir < 2; dir++)
                {
                    // For each slice along the axis from 0..ChunkSize (faces between voxels)
                    for (int slice = 0; slice <= ChunkSize; slice++)
                    {
                        // Build mask for this plane: mask[u + v * ChunkSize] = blockId (0 = no face)
                        for (int v = 0; v < ChunkSize; v++)
                        {
                            for (int u = 0; u < ChunkSize; u++)
                            {
                                // Map (axis, slice, u, v) -> two neighboring voxel positions A and B.
                                // Face exists where exactly one of A/B is solid.
                                // We will treat coordinates in local space 0..ChunkSize-1 for voxels.
                                bool aSolid, bSolid;
                                ushort aId = 0, bId = 0;

                                // Get voxel positions for A and B depending on axis:
                                // axis 0 (Y): A = (u, slice - 1, v), B = (u, slice, v)
                                // axis 1 (X): A = (slice - 1, u, v), B = (slice, u, v)
                                // axis 2 (Z): A = (u, v, slice - 1), B = (u, v, slice)
                                GetTwoVoxelsForFace(axis, slice, u, v, out aSolid, out aId, out bSolid, out bId);

                                ushort maskVal = 0;
                                if (aSolid != bSolid)
                                {
                                    // which side is solid?
                                    // We'll use convention: direction==0 means face normal points +axis (so solid is on negative side A)
                                    // If dir==0, we want faces where A is solid and B is air (A solid, B not)
                                    // If dir==1, we want faces where B is solid and A is air
                                    if (dir == 0 && aSolid && !bSolid)
                                        maskVal = aId;
                                    else if (dir == 1 && bSolid && !aSolid)
                                        maskVal = bId;
                                }

                                mask[u + v * ChunkSize] = maskVal;
                            }
                        }

                        // Run greedy rectangle merge on mask
                        for (int v = 0; v < ChunkSize; v++)
                        {
                            for (int u = 0; u < ChunkSize; )
                            {
                                ushort currentId = mask[u + v * ChunkSize];
                                if (currentId == 0) { u++; continue; }

                                // Determine width
                                int width = 1;
                                while (u + width < ChunkSize && mask[(u + width) + v * ChunkSize] == currentId) width++;

                                // Determine height
                                int height = 1;
                                bool stop = false;
                                while (v + height < ChunkSize && !stop)
                                {
                                    for (int k = 0; k < width; k++)
                                    {
                                        if (mask[(u + k) + (v + height) * ChunkSize] != currentId) { stop = true; break; }
                                    }
                                    if (!stop) height++;
                                }

                                // Zero-out consumed area
                                for (int hv = 0; hv < height; hv++)
                                    for (int wu = 0; wu < width; wu++)
                                        mask[(u + wu) + (v + hv) * ChunkSize] = 0;

                                // Emit quad: origin (u,v), size (width,height) on this slice, axis, dir, blockId = currentId
                                var q = new GreedyQuad
                                {
                                    BlockId = currentId,
                                    Axis = axis,
                                    Direction = dir,
                                    Slice = slice,
                                    X = u,
                                    Y = v,
                                    Width = width,
                                    Height = height
                                };
                                AppendGreedyQuad(q);

                                // advance u
                                u += width;
                            }
                        }
                    }
                }
            }

            mask.Dispose();
        }

        // Helper: fetch the two voxels on either side of the face plane
        // Uses local voxel coords — delegate to GetVoxel which accepts padded coords (x+1,y+1,z+1)
        [BurstCompile]
        private void GetTwoVoxelsForFace(int axis, int slice, int u, int v,
                                         out bool aSolid, out ushort aId,
                                         out bool bSolid, out ushort bId)
        {
            // A is the voxel with coordinate index (slice - 1) along axis
            // B is the voxel with coordinate index (slice) along axis
            int ax = 0, ay = 0, az = 0;
            int bx = 0, by = 0, bz = 0;

            switch (axis)
            {
                case 0: // Y axis: coords (u, *, v)
                    ax = u; ay = slice - 1; az = v;
                    bx = u; by = slice;     bz = v;
                    break;
                case 1: // X axis: coords (*, u, v)
                    ax = slice - 1; ay = u; az = v;
                    bx = slice;     by = u; bz = v;
                    break;
                default: // 2 Z axis: coords (u, v, *)
                    ax = u; ay = v; az = slice - 1;
                    bx = u; by = v; bz = slice;
                    break;
            }

            // Use GetVoxel with padded coords (local + 1)
            Voxel avox = GetVoxel(ax + 1, ay + 1, az + 1);
            Voxel bvox = GetVoxel(bx + 1, by + 1, bz + 1);

            aSolid = avox.IsSolid();
            bSolid = bvox.IsSolid();
            aId = aSolid ? avox.GetBlockID() : (ushort)0;
            bId = bSolid ? bvox.GetBlockID() : (ushort)0;
        }

        [BurstCompile]
        private void AppendGreedyQuad(GreedyQuad quad)
        {
            // --- 1. Texture/UV Calculation (MODIFIED) ---
            BlockTextureData blockTextures = BlockTypeData[quad.BlockId];
            BlockFaceTextures faceTexture;

            if (quad.Axis == 0) faceTexture = (quad.Direction == 0) ? blockTextures.Top : blockTextures.Bottom;
            else faceTexture = blockTextures.Side;

            float tileSize = 1.0f / AtlasSizeInTiles;

            // UV Channel 1: The base offset of the tile in the atlas.
            // This is the SAME for all 4 vertices of the quad.
            float2 baseUv = new float2(faceTexture.TileX * tileSize, faceTexture.TileY * tileSize);

            // UV Channel 0: The tiling coordinates.
            // These make the texture repeat across the quad's surface.
            var uv0_bl = new float2(0, 0);
            var uv0_br = new float2(quad.Width, 0);
            var uv0_tl = new float2(0, quad.Height);
            var uv0_tr = new float2(quad.Width, quad.Height);
            
            // --- 2. Vertex Position and Winding Order ---
            int vertIndex = Vertices.Length;
            float3 v0, v1, v2, v3;

            switch (quad.Axis)
            {
                case 0: // Y-Axis
                    v0 = new float3(quad.X,             quad.Slice, quad.Y);
                    v1 = new float3(quad.X + quad.Width, quad.Slice, quad.Y);
                    v2 = new float3(quad.X,             quad.Slice, quad.Y + quad.Height);
                    v3 = new float3(quad.X + quad.Width, quad.Slice, quad.Y + quad.Height);
                    
                    if (quad.Direction == 0) // +Y Normal (Top face)
                        AddQuad(vertIndex, v0, v2, v1, v3, uv0_bl, uv0_tl, uv0_br, uv0_tr, baseUv);
                    else // -Y Normal (Bottom face)
                        AddQuad(vertIndex, v0, v1, v2, v3, uv0_bl, uv0_br, uv0_tl, uv0_tr, baseUv);
                    break;

                case 1: // X-Axis
                {
                    // Define the four 3D vertex positions of the quad on the YZ-plane
                    float3 v_bottomLeft  = new float3(quad.Slice, quad.X,             quad.Y);
                    float3 v_bottomRight = new float3(quad.Slice, quad.X,             quad.Y + quad.Height);
                    float3 v_topLeft     = new float3(quad.Slice, quad.X + quad.Width, quad.Y);
                    float3 v_topRight    = new float3(quad.Slice, quad.X + quad.Width, quad.Y + quad.Height);
                    
                    // Define the corresponding four 2D UV coordinates
                    // The texture's U (width) maps to the quad's Z-extent (quad.Height)
                    // The texture's V (height) maps to the quad's Y-extent (quad.Width)
                    float2 uv_bottomLeft  = new float2(0, 0);
                    float2 uv_bottomRight = new float2(quad.Height, 0);
                    float2 uv_topLeft     = new float2(0, quad.Width);
                    float2 uv_topRight    = new float2(quad.Height, quad.Width);

                    if (quad.Direction == 0) // +X Normal (Right face)
                    {
                        // Winding order: BL, TL, BR, TR
                        AddQuad(vertIndex, 
                            v_bottomLeft, v_topLeft, v_bottomRight, v_topRight,
                            uv_bottomLeft, uv_topLeft, uv_bottomRight, uv_topRight, 
                            baseUv);
                    }
                    else // -X Normal (Left face)
                    {
                        // Winding order: BL, BR, TL, TR
                        AddQuad(vertIndex, 
                            v_bottomLeft, v_bottomRight, v_topLeft, v_topRight,
                            uv_bottomRight, uv_bottomLeft, uv_topRight, uv_topLeft, 
                            baseUv);
                    }
                    break;
                }

                case 2: // Z-Axis
                    v0 = new float3(quad.X,             quad.Y,             quad.Slice);
                    v1 = new float3(quad.X + quad.Width, quad.Y,             quad.Slice);
                    v2 = new float3(quad.X,             quad.Y + quad.Height, quad.Slice);
                    v3 = new float3(quad.X + quad.Width, quad.Y + quad.Height, quad.Slice);
                    
                    if (quad.Direction == 0) // +Z Normal (Forward face)
                        AddQuad(vertIndex, v0, v1, v2, v3, uv0_br, uv0_bl, uv0_tr, uv0_tl, baseUv);
                    else // -Z Normal (Back face)
                        AddQuad(vertIndex, v0, v2, v1, v3, uv0_bl, uv0_tl, uv0_br, uv0_tr, baseUv);
                    break;
            }
        }

        // MODIFIED: This function now accepts the tiling UVs and the base UV separately
        // and populates both UVs0 and UVs1 lists.
        [BurstCompile]
        private void AddQuad(int vertIndex, 
            float3 vA, float3 vB, float3 vC, float3 vD,
            float2 uv0_A, float2 uv0_B, float2 uv0_C, float2 uv0_D, float2 uv1_base)
        {
            Vertices.Add(vA);
            Vertices.Add(vB);
            Vertices.Add(vC);
            Vertices.Add(vD);

            // Add the corresponding tiling UVs to the first channel
            UVs0.Add(uv0_A);
            UVs0.Add(uv0_B);
            UVs0.Add(uv0_C);
            UVs0.Add(uv0_D);

            // Add the same base tile offset to all four vertices in the second channel
            UVs1.Add(uv1_base);
            UVs1.Add(uv1_base);
            UVs1.Add(uv1_base);
            UVs1.Add(uv1_base);

            // First triangle
            Triangles.Add(vertIndex + 0);
            Triangles.Add(vertIndex + 1); 
            Triangles.Add(vertIndex + 2); 

            // Second triangle
            Triangles.Add(vertIndex + 1);
            Triangles.Add(vertIndex + 3); 
            Triangles.Add(vertIndex + 2);
        }

        // --- Helper functions for indexing and coordinate transformation ---        
        private int GetIndexFromCoords(int x, int y, int z) => x + ChunkSize * (y + ChunkSize * z);

        [BurstCompile]
        private Voxel GetVoxel(int x, int y, int z)
        {
            // Convert padded -> local: padded coords are 0..ChunkSize+1, local coords are 0..ChunkSize-1
            int lx = x - 1;
            int ly = y - 1;
            int lz = z - 1;

            bool lxIn = lx >= 0 && lx < ChunkSize;
            bool lyIn = ly >= 0 && ly < ChunkSize;
            bool lzIn = lz >= 0 && lz < ChunkSize;

            // Fully inside main chunk
            if (lxIn && lyIn && lzIn)
                return Voxels[GetIndexFromCoords(lx, ly, lz)];

            // Only one-axis outside -> sample the corresponding neighbour if other coords are in-range
            // Left
            if (!lxIn && lyIn && lzIn)
            {
                if (lx == -1) // left neighbor's x = ChunkSize - 1
                    return LeftVoxels.Length == 0 ? default : LeftVoxels[GetIndexFromCoords(ChunkSize - 1, ly, lz)];
                if (lx == ChunkSize) // right neighbor (lx == ChunkSize means padded x was ChunkSize+1)
                    return RightVoxels.Length == 0 ? default : RightVoxels[GetIndexFromCoords(0, ly, lz)];
            }

            // Down / Up
            if (!lyIn && lxIn && lzIn)
            {
                if (ly == -1)
                    return DownVoxels.Length == 0 ? default : DownVoxels[GetIndexFromCoords(lx, ChunkSize - 1, lz)];
                if (ly == ChunkSize)
                    return UpVoxels.Length == 0 ? default : UpVoxels[GetIndexFromCoords(lx, 0, lz)];
            }

            // Back / Forward (Z)
            if (!lzIn && lxIn && lyIn)
            {
                if (lz == -1)
                    return BackVoxels.Length == 0 ? default : BackVoxels[GetIndexFromCoords(lx, ly, ChunkSize - 1)];
                if (lz == ChunkSize)
                    return ForwardVoxels.Length == 0 ? default : ForwardVoxels[GetIndexFromCoords(lx, ly, 0)];
            }

            // If more than one axis is out of range (corner or edge that requires diagonal neighbor),
            // we don't have diagonal neighbor chunks — treat as empty (air).
            return default;
        }
    }
}
