using UnityEditor;

namespace UnityStandardAssets.CinematicEffects
{
    public interface IAntiAliasingEditor
    {
        void OnEnable(SerializedObject serializedObject, string path);
        void OnInspectorGUI(IAntiAliasing target);
    }
}
