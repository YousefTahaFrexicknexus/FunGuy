using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Extracts CPU hot-frame attribution from a previously captured Unity Profiler .data trace.
/// Runs only on explicit request. Does not enter Play Mode or modify scenes/settings.
/// </summary>
[InitializeOnLoad]
internal static class FunGuyCpuTraceInspect
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string Dir = Path.Combine(Root, "work", "windows-profiling-2026-11-09", "cpu-profile");
    private static readonly string Request = Path.Combine(Dir, "inspect-last-trace.request");
    private static double nextPoll;
    private static bool busy;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly struct Frame
    {
        public readonly int Index;
        public readonly float Ms;
        public Frame(int index, float ms) { Index = index; Ms = ms; }
    }

    static FunGuyCpuTraceInspect()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (busy || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.timeSinceStartup < nextPoll)
            return;
        nextPoll = EditorApplication.timeSinceStartup + 2;
        if (!File.Exists(Request))
            return;
        busy = true;
        File.Delete(Request);
        try { Analyze(); }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Dir, "trace-inspection-error.txt"), ex.ToString());
            Debug.LogError("[FunGuy CPU Audit] Failed to inspect saved trace: " + ex);
        }
        finally { busy = false; }
    }

    private static void Analyze()
    {
        Directory.CreateDirectory(Dir);
        string path = Directory.GetFiles(Dir, "funguy-cpu-*.data")
            .Where(p => new FileInfo(p).Length > 1000)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        var events = new List<string>
        {
            "trace=" + path,
            "beforeLoadFirst=" + ProfilerDriver.firstFrameIndex +
            " beforeLoadLast=" + ProfilerDriver.lastFrameIndex
        };

        bool hasFrames = ProfilerDriver.lastFrameIndex >= 0 &&
                         ProfilerDriver.firstFrameIndex >= 0 &&
                         ProfilerDriver.lastFrameIndex >= ProfilerDriver.firstFrameIndex;

        if (!hasFrames && path != null)
        {
            MethodInfo load = typeof(ProfilerDriver).GetMethod("LoadProfile",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(bool) }, null);
            if (load != null)
            {
                bool loaded = (bool)load.Invoke(null, new object[] { path, false });
                events.Add("LoadProfile=" + loaded);
            }
        }

        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        events.Add("first=" + first + " last=" + last);
        if (first < 0 || last < first)
        {
            File.WriteAllLines(Path.Combine(Dir, "trace-inspection-events.txt"), events);
            return;
        }

        var frames = new List<Frame>();
        for (int i = first; i <= last; i++)
        {
            try
            {
                using (var view = ProfilerDriver.GetRawFrameDataView(i, 0))
                    if (view != null && view.valid)
                        frames.Add(new Frame(i, view.frameTimeMs));
            }
            catch (Exception ex) { events.Add("frame " + i + " " + ex.Message); }
        }
        events.Add("validFrames=" + frames.Count);

        var slowFrames = frames.OrderByDescending(x => x.Ms).Take(32).ToList();
        foreach (int estimate in new[] { 949, 1006, 1193, 1501, 1504, 1734, 1737, 1916, 1996, 2054 })
        {
            Frame closest = frames.OrderBy(x => Math.Abs(x.Index - estimate)).FirstOrDefault();
            if (closest.Index > 0 && slowFrames.All(x => x.Index != closest.Index))
                slowFrames.Add(closest);
        }

        var details = new List<string> { "frame,frame_ms,thread,kind,marker,ms" };
        var samplesByFrame = new List<string> { "frame,frame_ms,sample_count" };
        foreach (Frame frame in slowFrames)
        {
            try
            {
                using (var view = ProfilerDriver.GetRawFrameDataView(frame.Index, 0))
                {
                    if (view == null || !view.valid) continue;
                    int n = Math.Min(view.sampleCount, 12000);
                    samplesByFrame.Add(frame.Index + "," + frame.Ms.ToString("F3", Invariant) + "," + n);
                    var inclusive = new List<(string name, float ms, int children)>(n);
                    for (int i = 0; i < n; i++)
                        inclusive.Add((view.GetSampleName(i), view.GetSampleTimeMs(i), view.GetSampleChildrenCount(i)));

                    foreach (var sample in inclusive.OrderByDescending(x => x.ms).Take(35))
                    {
                        if (sample.ms < 0.05f) continue;
                        details.Add(frame.Index + "," + frame.Ms.ToString("F4", Invariant) +
                                    ",Main,inclusive," + Csv(sample.name) + "," +
                                    sample.ms.ToString("F4", Invariant));
                    }

                    // Stack-derived exclusive durations for ordinary nested profiler sample trees.
                    var exclusive = new float[n];
                    var stack = new Stack<(int index, int remaining)>();
                    for (int i = 0; i < n; i++)
                    {
                        while (stack.Count > 0 && stack.Peek().remaining <= 0)
                            stack.Pop();
                        exclusive[i] = inclusive[i].ms;
                        if (stack.Count > 0)
                        {
                            var parent = stack.Pop();
                            exclusive[parent.index] -= inclusive[i].ms;
                            parent.remaining--;
                            stack.Push(parent);
                        }
                        if (inclusive[i].children > 0)
                            stack.Push((i, inclusive[i].children));
                    }
                    foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => exclusive[i]).Take(20))
                    {
                        if (exclusive[i] < 0.05f) continue;
                        details.Add(frame.Index + "," + frame.Ms.ToString("F4", Invariant) +
                                    ",Main,derived_self," + Csv(inclusive[i].name) + "," +
                                    exclusive[i].ToString("F4", Invariant));
                    }
                }
            }
            catch (Exception ex) { events.Add("details frame " + frame.Index + " " + ex.Message); }
        }

        File.WriteAllLines(Path.Combine(Dir, "trace-inspection-events.txt"), events);
        File.WriteAllLines(Path.Combine(Dir, "trace-slowest-frame-summary.csv"), samplesByFrame);
        File.WriteAllLines(Path.Combine(Dir, "trace-slowest-frame-markers.csv"), details);
        Debug.Log("[FunGuy CPU Audit] Inspected " + frames.Count + " frames; " +
                  slowFrames.Count + " targeted frames. Files in " + Dir);
    }

    private static string Csv(string value) =>
        "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
