using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// A shaped copy of a raw elevation function, evaluated lazily in world-aligned blocks. Thresholding the
    /// raw relief leaves wedge-shaped spikes and thin tapering tongues wherever pass corridors cut across
    /// ridges. Three filters remove them before anything is classified, in this order:
    /// <list type="number">
    /// <item>A Gaussian blur, which rounds the raw creases and every corner, convex and concave.</item>
    /// <item>A morphological opening (erode, then dilate, with a disk). It removes every peak narrower
    /// than the disk's diameter and rounds every convex corner to at least the disk's radius, while
    /// leaving valleys, such as the pass corridors, exactly as wide as they were. It must come after the
    /// main blur: blurring an opened tongue lowers it and cuts it back into a tapering spike.</item>
    /// <item>A light blur that only irons out the creases the opening's min and max leave in the relief,
    /// which relief shading would otherwise draw as arcs.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Each block samples the raw function on a regular lattice with a margin wide enough for both filters,
    /// opens and blurs it, and keeps only its own nodes. Queries between nodes use a
    /// cubic B-spline, which is continuous in slope: bilinear interpolation would crease the field along
    /// every lattice line, and relief shading would draw those creases as a grid. Every node depends only on
    /// its world position, so results never depend on which block was built first.
    /// </remarks>
    public sealed class SmoothedElevationGrid
    {
        private const int BlockNodes = 32;
        private const int MaximumCachedBlocks = 512;

        // The Gaussian is cut off this many standard deviations out; the dropped tail is under 1 %.
        private const float KernelReachInSigmas = 2.5f;

        // Just enough to smooth the opening's creases; far narrower than the narrowest surviving feature.
        private const float CreaseSmoothingRadius = 1f;

        private readonly Func<float, float, float> _rawElevation;
        private readonly float _step;
        private readonly float[] _kernel;
        private readonly int _kernelRadius;
        private readonly float[] _creaseKernel;
        private readonly int _creaseKernelRadius;
        private readonly int _openingRadius;
        private readonly Vector2Int[] _disk;
        private readonly Dictionary<Vector2Int, float[]> _blocks = new();
        private readonly Queue<Vector2Int> _blockOrder = new();
        private float[] _rawScratch = Array.Empty<float>();
        private float[] _blurredScratch = Array.Empty<float>();
        private float[] _erodedScratch = Array.Empty<float>();
        private float[] _openedScratch = Array.Empty<float>();
        private float[] _rowScratch = Array.Empty<float>();
        private Vector2Int _lastKey = new(int.MinValue, int.MinValue);
        private float[] _lastBlock;

        /// <param name="rawElevation">Pure function of world X and Z.</param>
        /// <param name="step">World metres between lattice nodes.</param>
        /// <param name="smoothingRadius">Gaussian standard deviation in metres; zero disables the blur.</param>
        /// <param name="openingRadius">Disk radius of the opening in metres; zero disables it.</param>
        public SmoothedElevationGrid(Func<float, float, float> rawElevation, float step, float smoothingRadius,
            float openingRadius = 0f)
        {
            _rawElevation = rawElevation ?? throw new ArgumentNullException(nameof(rawElevation));
            _step = Mathf.Max(.1f, step);
            float sigmaInNodes = Mathf.Max(0f, smoothingRadius) / _step;
            _kernelRadius = sigmaInNodes > 0f ? Mathf.CeilToInt(sigmaInNodes * KernelReachInSigmas) : 0;
            _kernel = CreateKernel(sigmaInNodes, _kernelRadius);
            float creaseSigma = openingRadius > 0f ? CreaseSmoothingRadius / _step : 0f;
            _creaseKernelRadius = creaseSigma > 0f ? Mathf.CeilToInt(creaseSigma * KernelReachInSigmas) : 0;
            _creaseKernel = CreateKernel(creaseSigma, _creaseKernelRadius);
            float openingInNodes = Mathf.Max(0f, openingRadius) / _step;
            _openingRadius = Mathf.FloorToInt(openingInNodes);
            _disk = CreateDisk(openingInNodes, _openingRadius);
        }

        public int CachedBlockCount => _blocks.Count;

        /// <summary>The smoothed elevation at any world point.</summary>
        public float Sample(float x, float z)
        {
            float gridX = x / _step;
            float gridZ = z / _step;
            int nodeX = Mathf.FloorToInt(gridX);
            int nodeZ = Mathf.FloorToInt(gridZ);
            float tx = gridX - nodeX;
            float tz = gridZ - nodeZ;
            BSplineWeights(tx, out float wx0, out float wx1, out float wx2, out float wx3);
            BSplineWeights(tz, out float wz0, out float wz1, out float wz2, out float wz3);

            return wz0 * Row(nodeX, nodeZ - 1, wx0, wx1, wx2, wx3)
                   + wz1 * Row(nodeX, nodeZ, wx0, wx1, wx2, wx3)
                   + wz2 * Row(nodeX, nodeZ + 1, wx0, wx1, wx2, wx3)
                   + wz3 * Row(nodeX, nodeZ + 2, wx0, wx1, wx2, wx3);
        }

        private float Row(int nodeX, int nodeZ, float w0, float w1, float w2, float w3)
        {
            return w0 * Node(nodeX - 1, nodeZ) + w1 * Node(nodeX, nodeZ)
                   + w2 * Node(nodeX + 1, nodeZ) + w3 * Node(nodeX + 2, nodeZ);
        }

        private float Node(int nodeX, int nodeZ)
        {
            var key = new Vector2Int(FloorDivide(nodeX, BlockNodes), FloorDivide(nodeZ, BlockNodes));
            if (key != _lastKey)
            {
                if (!_blocks.TryGetValue(key, out float[] block))
                {
                    block = BuildBlock(key);
                    CacheBlock(key, block);
                }

                _lastKey = key;
                _lastBlock = block;
            }

            int localX = nodeX - key.x * BlockNodes;
            int localZ = nodeZ - key.y * BlockNodes;
            return _lastBlock[localZ * BlockNodes + localX];
        }

        private float[] BuildBlock(Vector2Int key)
        {
            // Each stage shrinks the square it works on by its own reach, ending on the block's nodes.
            int crease = _creaseKernelRadius;
            int open = _openingRadius;
            int blur = _kernelRadius;
            int openedSize = BlockNodes + crease * 2;
            int erodedSize = openedSize + open * 2;
            int blurredSize = erodedSize + open * 2;
            int rawSize = blurredSize + blur * 2;
            EnsureScratch(ref _rawScratch, rawSize * rawSize);
            EnsureScratch(ref _blurredScratch, blurredSize * blurredSize);
            EnsureScratch(ref _erodedScratch, erodedSize * erodedSize);
            EnsureScratch(ref _openedScratch, openedSize * openedSize);
            EnsureScratch(ref _rowScratch, blurredSize * rawSize);

            int margin = (rawSize - BlockNodes) / 2;
            int firstX = key.x * BlockNodes - margin;
            int firstZ = key.y * BlockNodes - margin;
            for (int z = 0; z < rawSize; z++)
            {
                for (int x = 0; x < rawSize; x++)
                {
                    _rawScratch[z * rawSize + x] = _rawElevation((firstX + x) * _step, (firstZ + z) * _step);
                }
            }

            Blur(_rawScratch, rawSize, _blurredScratch, blurredSize, _kernel, blur);
            DiskFilter(_blurredScratch, blurredSize, _erodedScratch, erodedSize, open, takeMinimum: true);
            DiskFilter(_erodedScratch, erodedSize, _openedScratch, openedSize, open, takeMinimum: false);
            var block = new float[BlockNodes * BlockNodes];
            Blur(_openedScratch, openedSize, block, BlockNodes, _creaseKernel, crease);
            return block;
        }

        /// <summary>
        /// Separable Gaussian from a square input onto the square <paramref name="radius"/> nodes smaller on
        /// every side.
        /// </summary>
        private void Blur(float[] input, int inputSize, float[] output, int outputSize, float[] kernel, int radius)
        {
            // Horizontal pass over every input row, keeping only the output columns.
            for (int z = 0; z < inputSize; z++)
            {
                for (int x = 0; x < outputSize; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        sum += kernel[k + radius] * input[z * inputSize + x + radius + k];
                    }

                    _rowScratch[z * outputSize + x] = sum;
                }
            }

            // Vertical pass, keeping only the output rows.
            for (int z = 0; z < outputSize; z++)
            {
                for (int x = 0; x < outputSize; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        sum += kernel[k + radius] * _rowScratch[(z + radius + k) * outputSize + x];
                    }

                    output[z * outputSize + x] = sum;
                }
            }
        }

        /// <summary>
        /// Writes, for every node of the smaller output square, the minimum or maximum of the input under the
        /// disk centred on it. The input square is <paramref name="radius"/> nodes wider on every side.
        /// </summary>
        private void DiskFilter(float[] input, int inputSize, float[] output, int outputSize, int radius,
            bool takeMinimum)
        {
            for (int z = 0; z < outputSize; z++)
            {
                for (int x = 0; x < outputSize; x++)
                {
                    int centre = (z + radius) * inputSize + x + radius;
                    float value = input[centre];
                    for (int i = 0; i < _disk.Length; i++)
                    {
                        float candidate = input[centre + _disk[i].y * inputSize + _disk[i].x];
                        value = takeMinimum ? Mathf.Min(value, candidate) : Mathf.Max(value, candidate);
                    }

                    output[z * outputSize + x] = value;
                }
            }
        }

        private static void EnsureScratch(ref float[] scratch, int length)
        {
            if (scratch.Length < length)
            {
                scratch = new float[length];
            }
        }

        private static Vector2Int[] CreateDisk(float radius, int reach)
        {
            var offsets = new List<Vector2Int>();
            for (int z = -reach; z <= reach; z++)
            {
                for (int x = -reach; x <= reach; x++)
                {
                    if ((x != 0 || z != 0) && x * x + z * z <= radius * radius)
                    {
                        offsets.Add(new Vector2Int(x, z));
                    }
                }
            }

            return offsets.ToArray();
        }

        private void CacheBlock(Vector2Int key, float[] block)
        {
            if (_blocks.Count >= MaximumCachedBlocks)
            {
                Vector2Int oldest = _blockOrder.Dequeue();
                _blocks.Remove(oldest);
                if (oldest == _lastKey)
                {
                    _lastKey = new Vector2Int(int.MinValue, int.MinValue);
                    _lastBlock = null;
                }
            }

            _blocks.Add(key, block);
            _blockOrder.Enqueue(key);
        }

        private static float[] CreateKernel(float sigma, int radius)
        {
            var kernel = new float[radius * 2 + 1];
            if (radius == 0)
            {
                kernel[0] = 1f;
                return kernel;
            }

            float total = 0f;
            for (int i = -radius; i <= radius; i++)
            {
                float weight = Mathf.Exp(-(i * i) / (2f * sigma * sigma));
                kernel[i + radius] = weight;
                total += weight;
            }

            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] /= total;
            }

            return kernel;
        }

        /// <summary>Uniform cubic B-spline basis for the four nodes around a fractional position.</summary>
        private static void BSplineWeights(float t, out float w0, out float w1, out float w2, out float w3)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float oneMinus = 1f - t;
            w0 = oneMinus * oneMinus * oneMinus / 6f;
            w1 = (3f * t3 - 6f * t2 + 4f) / 6f;
            w2 = (-3f * t3 + 3f * t2 + 3f * t + 1f) / 6f;
            w3 = t3 / 6f;
        }

        private static int FloorDivide(int value, int divisor)
        {
            int quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }
    }
}
