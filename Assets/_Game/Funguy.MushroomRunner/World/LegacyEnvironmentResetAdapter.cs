using System.Reflection;
using UnityEngine;

namespace Funguy.MushroomRunner
{
    [DisallowMultipleComponent]
    public sealed class LegacyEnvironmentResetAdapter : MonoBehaviour
    {
        private const string Main2EnvironmentSpawnerTypeName = "BlockSpawner";
        private const string ResetSpawnerMethodName = "ResetSpawner";

        public void ResetEnvironment()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.GetType().Name != Main2EnvironmentSpawnerTypeName)
                {
                    continue;
                }

                MethodInfo resetMethod = behaviour.GetType().GetMethod(
                    ResetSpawnerMethodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: System.Type.EmptyTypes,
                    modifiers: null);

                resetMethod?.Invoke(behaviour, null);
            }
        }
    }
}
