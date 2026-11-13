using UnityEngine;
using System.Diagnostics;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Debug = UnityEngine.Debug;
using Unity.Jobs.LowLevel.Unsafe;


[BurstCompile]
struct SumRedJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Color32> Pixels;
    public NativeArray<long> PartialSums;
    public int ChunkSize;
 
    public void Execute(int chunk)
    {
        int start = chunk * ChunkSize;
        int end = start + ChunkSize;
        if (end > Pixels.Length) end = Pixels.Length;
 
        long sum = 0;
        for (int i = start; i < end; i++)
            sum += Pixels[i].r;
 
        PartialSums[chunk] = sum;
    }
}

public class RedChannelSum : MonoBehaviour
{
    public string imagePath = @"image path here";
    public int chunksPerWorker = 4;
    public int minChunkSize = 4096;
 
    void Start()
    {
        // finding the image on disk from the image path above start
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(imagePath));
        Color32[] managed = tex.GetPixels32();
 

        int workers = JobsUtility.JobWorkerCount + 1;          
        int targetChunks = workers * chunksPerWorker;
        int chunkSize = (managed.Length + targetChunks - 1) / targetChunks;
        if (chunkSize < minChunkSize) chunkSize = minChunkSize;
        int chunkCount = (managed.Length + chunkSize - 1) / chunkSize;
 
        Debug.Log($"Image {tex.width}x{tex.height} ({managed.Length} px), workers: {workers}, chunk size: {chunkSize}, chunks: {chunkCount}");
 

        var pixels = new NativeArray<Color32>(managed, Allocator.TempJob);
        var partials = new NativeArray<long>(chunkCount, Allocator.TempJob);
 
        var job = new SumRedJob
        {
            Pixels = pixels,
            PartialSums = partials,
            ChunkSize = chunkSize
        };
 
        //  excluding burst compile time from the measurement
        job.Schedule(chunkCount, 1).Complete();
 
        // start of the parallel processing 
        var sw = Stopwatch.StartNew();
        job.Schedule(chunkCount, 1).Complete();
        long total = 0;
        for (int i = 0; i < chunkCount; i++) total += partials[i];
        sw.Stop();
        Debug.Log($"[Burst Job] Red sum = {total} in {sw.Elapsed.TotalMilliseconds:F3} ms");
 
        // single threaded processing for comparison
        sw.Restart();
        long check = 0;
        for (int i = 0; i < managed.Length; i++) check += managed[i].r;
        sw.Stop();
        Debug.Log($"[Main thread] Red sum = {check} in {sw.Elapsed.TotalMilliseconds:F3} ms");
        Debug.Log(total == check ? "Results match." : "MISMATCH between job and baseline!");
 
        pixels.Dispose();
        partials.Dispose();
    }
}
