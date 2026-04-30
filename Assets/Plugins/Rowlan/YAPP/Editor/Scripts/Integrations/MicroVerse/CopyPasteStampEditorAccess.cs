#if __MICROVERSE_VEGETATION__

using JBooth.MicroVerseCore;
using System;
using System.Reflection;
using UnityEngine;

namespace Rowlan.Yapp
{
    /// <summary>
    /// CopyPasteStampEditor class is protected, but we need to access 2 static methods of it. Hence reflection. For now.
    /// </summary>
    public class CopyPasteStampEditorAccess
    {
        private static Type copyPasteStampEditorType;
        private static MethodInfo captureMethod;
        private static MethodInfo captureTreesMethod;

        static CopyPasteStampEditorAccess()
        {
            // Find the assembly containing MicroVerse
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                copyPasteStampEditorType = assembly.GetType("JBooth.MicroVerseCore.CopyPasteStampEditor");
                if (copyPasteStampEditorType != null)
                    break;
            }

            if (copyPasteStampEditorType != null)
            {
                captureMethod = copyPasteStampEditorType.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static);
                captureTreesMethod = copyPasteStampEditorType.GetMethod("CaptureTrees", BindingFlags.Public | BindingFlags.Static);
            }
        }

        public static void Capture(object cpStamp, string path)
        {
            captureMethod?.Invoke(null, new object[] { cpStamp, path });
        }

        public static CopyStamp.TreeCopyData CaptureTrees(Terrain[] terrains, Bounds bounds, Transform trans)
        {
            return (CopyStamp.TreeCopyData)(captureTreesMethod?.Invoke(null, new object[] { terrains, bounds, trans }));
        }
    }
}
#endif