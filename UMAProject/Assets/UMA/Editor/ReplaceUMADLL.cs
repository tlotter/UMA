using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// If you want to replace the UMA.dll with the source code from https://github.com/huika/UMA, Unity3d will loose references to essential scripts such as SlotData.cs, OverlayData.cs ...
/// 1. remove UMA.dll and UMAEditor.dll from your project
/// 2. add source code of the dll to your project (https://github.com/huika/UMA)
/// 3. press the menu "UMA/Replace UMA DLL"
///
/// Good read how fileID and guid work with DLLs:
/// http://forum.unity3d.com/threads/problems-compiling-dlls-from-monodevelop.148617/#post-1024523
/// 
/// Hint: 
/// - This is script is not optimized for performance, because you only run it onces ;-)
/// - Assets must be serialzed as Text, you can change this here: Edit -> Project Settings -> Editor -> Asset Serialziation = Force Text
/// </summary>
public class UMAReplaceDLL
{
    [MenuItem("UMA/Replace UMA DLL")]
    static void Replace()
    {
        List<UnityReference> references = new List<UnityReference>();

        references.Add(new UnityReference("e20699a64490c4e4284b27a8aeb05666", "1772484567", FindAssetGuid("OverlayData.cs"), "11500000")); // OverlayData.cs
        references.Add(new UnityReference("e20699a64490c4e4284b27a8aeb05666", "-1278852528", FindAssetGuid("SlotData.cs"), "11500000")); // SlotData.cs
        references.Add(new UnityReference("e20699a64490c4e4284b27a8aeb05666", "-335686737", FindAssetGuid("RaceData.cs"), "11500000")); // RaceData.cs
        references.Add(new UnityReference("e20699a64490c4e4284b27a8aeb05666", "-1571472132", FindAssetGuid("UMADefaultMeshCombiner.cs"), "11500000")); // UMADefaultMeshCombiner.cs
        references.Add(new UnityReference("e20699a64490c4e4284b27a8aeb05666", "-946187639", FindAssetGuid("UMALegacyMeshCombiner.cs"), "11500000")); // UMALegacyMeshCombiner.cs
        references.Add(new UnityReference("e20699a64490c4e4284b27a8aeb05666", "-1550055707", FindAssetGuid("UMAData.cs"), "11500000")); // UMAData.cs
        references.Add(new UnityReference("e20699a64490c4e4284b27a8aeb05666", "-1708169498", FindAssetGuid("UmaTPose.cs"), "11500000")); // UmaTPose.cs
        references.Add(new UnityReference("e20699a64490c4e4284b27a8aeb05666", "-1175167296", FindAssetGuid("TextureMerge.cs"), "11500000")); // TextureMerge.cs

        ReplaceReferences(Application.dataPath, references);
    }

    static string FindAssetGuid(string assetName)
    {
        string[] files = Directory.GetFiles(Application.dataPath, "*" + assetName, SearchOption.AllDirectories);

        if (files.Length == 0)
        {
            // make sure that we don't continue and break anything!
            throw new System.Exception("Unable to find guid for " + assetName);
        }
        else if (files.Length > 1)
        {
             // make sure that we don't continue and break anything!
            throw new System.Exception("File exists multiple times " + assetName);
        }

        string assetPath = Path.Combine("Assets", files[0].Substring(Application.dataPath.Length));
        return AssetDatabase.AssetPathToGUID(assetPath);
    }

    static void ReplaceReferences(string assetFolder, List<UnityReference> r)
    {
        if (EditorSettings.serializationMode != SerializationMode.ForceText)
        {
            Debug.LogError("Failed to replace references, you must set serialzation mode to text. Edit -> Project Settings -> Editor -> Asset Serialziation = Force Text");
            return;
        }

        string[] files = Directory.GetFiles(assetFolder, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];

            if (EditorUtility.DisplayCancelableProgressBar("Replace UMA DLL", file, i / (float)files.Length))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            if (file.EndsWith(".asset") || file.EndsWith(".prefab") || file.EndsWith(".unity"))
            {
                ReplaceReferencesInFile(file, r);
                FindNotReplacedReferences(file, "e20699a64490c4e4284b27a8aeb05666");
            }
        }

        EditorUtility.ClearProgressBar();
    }

    static void ReplaceReferencesInFile(string filePath, List<UnityReference> references)
    {
        var fileContents = System.IO.File.ReadAllText(filePath);

        bool match = false;

        foreach (UnityReference r in references)
        {
            Regex regex = new Regex(@"fileID: " + r.srcFileId + ", guid: " + r.srcGuid);
            if (regex.IsMatch(fileContents))
            {
                fileContents = regex.Replace(fileContents, "fileID: " + r.dstFileId + ", guid: " + r.dstGuid);
                match = true;
                Debug.Log("Replaced: " + filePath);
            }
        }

        if (match)
        {
            System.IO.File.WriteAllText(filePath, fileContents);
        }
    }

    /// <summary>
    /// Just to make sure that all references are replaced.
    /// </summary>
    static void FindNotReplacedReferences(string filePath, string guid)
    {
        var fileContents = System.IO.File.ReadAllText(filePath);

        // -?        number can be negative
        // [0-9]+    1-n numbers
        Regex.Replace(fileContents, @"fileID: -?[0-9]+, guid: " + guid,
                      (match) =>
                      {
                          if (match.Value != "fileID: 11500000, guid: " + guid)
                          {
                              Debug.LogWarning("NotReplaced: " + match.Value + "  " + filePath);
                          }
                          return match.Value;
                      });
    }


    class UnityReference
    {
        public UnityReference(string srcGuid, string srcFileId, string dstGuid, string dstFileId)
        {
            this.srcGuid = srcGuid;
            this.srcFileId = srcFileId;
            this.dstGuid = dstGuid;
            this.dstFileId = dstFileId;
        }

        public string srcGuid;
        public string srcFileId;
        public string dstGuid;
        public string dstFileId;
    }
}