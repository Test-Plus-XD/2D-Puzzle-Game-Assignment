using UnityEditor;
using UnityEngine;

public class BundleIdentifierSetter
{
    [MenuItem("Tools/Set Bundle Identifier")]
    public static void SetBundleIdentifier()
    {
        // Set Android package name
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.Limbus Company.Puzzle Game Assignment");

        // Set iOS bundle identifier
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.Limbus Company.Puzzle Game Assignment");

        Debug.Log("Bundle Identifier set successfully.");
    }
}