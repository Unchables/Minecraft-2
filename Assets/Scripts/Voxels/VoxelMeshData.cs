using Unity.Mathematics;

namespace Voxels
{
    public static class VoxelMeshData
    {
        /// <summary>
        /// Provides the static data for a single voxel face.
        /// </summary>
        public static void GetFaceData(int faceIndex, int3 position,
            out (float3 v0, float3 v1, float3 v2, float3 v3) vertices,
            out (int t0, int t1, int t2, int t3, int t4, int t5) triangles,
            out (float2 uv0, float2 uv1, float2 uv2, float2 uv3) uvs)
        {
            // The relative triangle indices for a quad are always the same.
            triangles = (0, 1, 2, 0, 2, 3);

            // Define standard UV coordinates for a quad.
            // These will be scaled and offset in the job to map to the correct atlas tile.
            var uv00 = new float2(0, 0); // Bottom-left
            var uv10 = new float2(1, 0); // Bottom-right
            var uv01 = new float2(0, 1); // Top-left
            var uv11 = new float2(1, 1); // Top-right

            // The vertex winding order here is correct for Unity's counter-clockwise culling.
            switch (faceIndex)
            {
                case 0: // Top (Y+)
                    vertices = (position + new float3(0, 1, 0), position + new float3(0, 1, 1), position + new float3(1, 1, 1), position + new float3(1, 1, 0));
                    uvs = (uv00, uv01, uv11, uv10);
                    break;
                case 1: // Bottom (Y-)
                    vertices = (position + new float3(0, 0, 0), position + new float3(1, 0, 0), position + new float3(1, 0, 1), position + new float3(0, 0, 1));
                    uvs = (uv10, uv00, uv01, uv11);
                    break;
                case 2: // Left (X-)
                    vertices = (position + new float3(0, 0, 1), position + new float3(0, 0, 0), position + new float3(0, 1, 0), position + new float3(0, 1, 1));
                    uvs = (uv00, uv10, uv11, uv01);
                    break;
                case 3: // Right (X+)
                    vertices = (position + new float3(1, 0, 0), position + new float3(1, 0, 1), position + new float3(1, 1, 1), position + new float3(1, 1, 0));
                    uvs = (uv00, uv10, uv11, uv01);
                    break;
                case 4: // Front (Z+)
                    vertices = (position + new float3(0, 0, 1), position + new float3(1, 0, 1), position + new float3(1, 1, 1), position + new float3(0, 1, 1));
                    uvs = (uv00, uv10, uv11, uv01);
                    break;
                default: // Back (Z-)
                    vertices = (position + new float3(1, 0, 0), position + new float3(0, 0, 0), position + new float3(0, 1, 0), position + new float3(1, 1, 0));
                    uvs = (uv00, uv10, uv11, uv01);
                    break;
            }
        }
    }
}